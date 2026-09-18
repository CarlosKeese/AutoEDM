using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using AutoEDM.Com;
using AutoEDM.Diagnostics;
using AutoEDM.Model;

namespace AutoEDM.Reverse
{
    /// <summary>O que a leitura da malha trouxe do corpo, já em milímetros.</summary>
    public sealed class MeshData
    {
        /// <summary>Sopa de triângulos: 9 doubles por triângulo, em mm.</summary>
        public double[] PointsMm;

        /// <summary>Normais por triângulo (3 doubles cada), quando a Solid Edge as devolve.
        /// null quando não vieram — o reconhecedor calcula as dele de qualquer forma.</summary>
        public double[] Normals;

        /// <summary>Identificador da face de origem de cada faceta, quando vem. Num corpo de
        /// facetas costuma não vir; num B-rep tesselado é segmentação de graça.</summary>
        public int[] FaceIds;

        public int TriangleCount;

        /// <summary>Como a leitura foi feita — vai para o relatório, porque a rota usada muda o
        /// que está disponível.</summary>
        public string Route;

        public string Error;

        public bool Ok { get { return PointsMm != null && PointsMm.Length >= 9; } }
    }

    /// <summary>
    /// Lê os triângulos de um corpo da Solid Edge por <c>Body.GetFacetData</c>.
    ///
    /// A assinatura NÃO foi adivinhada — veio do dump da typelib (docs/api/SolidEdgeGeometry.md):
    /// <c>GetFacetData(Tolerance: Double, [out] FacetCount: Int32, [out] Points: Array,
    /// [opt][out] Normals, [opt][out] TextureCoords, [opt][out] StyleIDs, [opt][out] FaceIDs,
    /// [opt] bHonourPrefs)</c>. A sonda de 2026-09-18 usou só os três primeiros e funcionou
    /// (4.168 facetas na malha real); aqui pedimos também Normals e FaceIDs, que são a diferença
    /// entre segmentar no escuro e segmentar com a face de origem na mão.
    ///
    /// Como os opcionais podem não estar implementados nesta build, a leitura é em DUAS rotas: a
    /// completa primeiro, e a mínima (a já provada) se ela falhar. Qual rota valeu vai no
    /// relatório — silenciar isso esconderia que os FaceIDs não vieram.
    ///
    /// Toda a API de geometria da SE é em METROS; a conversão para mm acontece aqui, uma vez, e
    /// daqui para dentro do reconhecedor tudo é mm.
    /// </summary>
    public static class MeshReader
    {
        public static MeshData Read(object body, double toleranceMm)
        {
            var data = new MeshData();
            if (body == null) { data.Error = "Corpo nulo."; return data; }

            List<string> members = ComDiagnostics.GetMemberNames(body);
            if (!Has(members, "GetFacetData"))
            {
                data.Error = "Este corpo não expõe GetFacetData.";
                return data;
            }

            double toleranceM = Units.MmToM(toleranceMm);

            if (TryFull(body, toleranceM, data)) return data;
            TryMinimal(body, toleranceM, data);
            return data;
        }

        /// <summary>Rota completa: pede Points, Normals e FaceIDs de uma vez.</summary>
        private static bool TryFull(object body, double toleranceM, MeshData data)
        {
            try
            {
                object[] args =
                {
                    toleranceM,        // Tolerance
                    0,                 // [out] FacetCount
                    new double[0],     // [out] Points
                    new double[0],     // [opt][out] Normals
                    new double[0],     // [opt][out] TextureCoords
                    new int[0],        // [opt][out] StyleIDs
                    new int[0]         // [opt][out] FaceIDs
                };
                var mod = new ParameterModifier(args.Length);
                for (int i = 1; i < args.Length; i++) mod[i] = true;

                body.GetType().InvokeMember("GetFacetData", BindingFlags.InvokeMethod, null, body, args,
                    new[] { mod }, CultureInfo.InvariantCulture, null);

                var pts = args[2] as double[];
                if (pts == null || pts.Length < 9) return false;

                data.TriangleCount = args[1] is int ? (int)args[1] : pts.Length / 9;
                data.PointsMm = ToMm(pts);
                data.Normals = args[3] as double[];
                data.FaceIds = args[6] as int[];
                data.Route = "GetFacetData completo (Points + Normals + FaceIDs)";

                Log.Info($"[MALHA] GetFacetData completo: FacetCount={data.TriangleCount}, " +
                         $"Points={pts.Length} doubles, " +
                         $"Normals={(data.Normals == null ? "não vieram" : data.Normals.Length + " doubles")}, " +
                         $"FaceIDs={(data.FaceIds == null ? "não vieram" : data.FaceIds.Length + " ids")}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("[MALHA] GetFacetData completo falhou (cai para a rota mínima): " +
                         ex.GetBaseException().Message);
                return false;
            }
        }

        /// <summary>Rota mínima: só Points. É exatamente a chamada que a sonda já provou.</summary>
        private static void TryMinimal(object body, double toleranceM, MeshData data)
        {
            try
            {
                object[] args = { toleranceM, 0, new double[0] };
                var mod = new ParameterModifier(3);
                mod[1] = true;
                mod[2] = true;

                body.GetType().InvokeMember("GetFacetData", BindingFlags.InvokeMethod, null, body, args,
                    new[] { mod }, CultureInfo.InvariantCulture, null);

                var pts = args[2] as double[];
                if (pts == null || pts.Length < 9)
                {
                    data.Error = "GetFacetData devolveu pontos vazios nas duas rotas.";
                    return;
                }

                data.TriangleCount = args[1] is int ? (int)args[1] : pts.Length / 9;
                data.PointsMm = ToMm(pts);
                data.Route = "GetFacetData mínimo (só Points)";
                Log.Info($"[MALHA] GetFacetData mínimo: FacetCount={data.TriangleCount}, Points={pts.Length} doubles.");
            }
            catch (Exception ex)
            {
                data.Error = "GetFacetData falhou: " + ex.GetBaseException().Message;
                Log.Warn("[MALHA] " + data.Error);
            }
        }

        private static double[] ToMm(double[] metres)
        {
            var mm = new double[metres.Length];
            for (int i = 0; i < metres.Length; i++) mm[i] = Units.MToMm(metres[i]);
            return mm;
        }

        private static bool Has(List<string> members, string name)
        {
            if (members == null) return false;
            foreach (string m in members)
                if (string.Equals(m, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
