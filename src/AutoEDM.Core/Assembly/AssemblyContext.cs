using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using AutoEDM.Diagnostics;

namespace AutoEDM.Assembly
{
    /// <summary>A part placed in the assembly, with the info we care about.</summary>
    public sealed class OccurrenceInfo
    {
        public dynamic ComOccurrence { get; }
        public string Name { get; }
        public dynamic OccurrenceDocument { get; }

        public OccurrenceInfo(dynamic comOccurrence, string name, dynamic occurrenceDocument)
        {
            ComOccurrence = comOccurrence;
            Name = name;
            OccurrenceDocument = occurrenceDocument;
        }
    }

    /// <summary>
    /// Works with an open Solid Edge assembly (.asm): enumerates occurrences,
    /// resolves the underlying part document, and reads placement transforms.
    ///
    /// This matches the real workflow: the assembly origin is the machine zero,
    /// the cavity to erode is an occurrence in it, and each electrode is a new
    /// in-context part. Occurrence placement (origin) is also the raw material for
    /// the future burn-coordinate report.
    ///
    /// COM (SolidEdgeAssembly):
    ///   AssemblyDocument.Occurrences        -> Occurrences
    ///     Occurrence.Name                   -> string
    ///     Occurrence.OccurrenceDocument     -> referenced PartDocument/SubAssembly
    ///     Occurrence.GetTransform(out x,y,z, out ax,ay,az)  (angles in radians)
    /// </summary>
    public sealed class AssemblyContext
    {
        public dynamic AssemblyDocument { get; }

        public AssemblyContext(dynamic assemblyDocument)
        {
            AssemblyDocument = assemblyDocument ?? throw new ArgumentNullException(nameof(assemblyDocument));
        }

        /// <summary>Top-level occurrences of the assembly.</summary>
        public IReadOnlyList<OccurrenceInfo> GetOccurrences()
        {
            var result = new List<OccurrenceInfo>();
            dynamic occurrences;
            try { occurrences = AssemblyDocument.Occurrences; }
            catch (Exception ex)
            {
                Log.Warn($"AssemblyDocument.Occurrences unavailable: {ex.Message}");
                return result;
            }

            int count;
            try { count = (int)occurrences.Count; }
            catch { count = 0; }

            for (int i = 1; i <= count; i++) // 1-based
            {
                try
                {
                    dynamic occ = occurrences.Item(i);
                    string name = SafeName(occ);
                    dynamic doc = SafeDoc(occ);
                    result.Add(new OccurrenceInfo(occ, name, doc));
                }
                catch (Exception ex)
                {
                    Log.Warn($"Occurrence[{i}] skipped: {ex.Message}");
                }
            }
            return result;
        }

        /// <summary>
        /// Best-effort read of an occurrence origin (METROS) relative to the assembly.
        /// GetTransform devolve metros na API COM do Solid Edge — NÃO dividir por 1000
        /// no chamador (PutOrigin também espera metros); use <see cref="Model.Units"/> se
        /// precisar converter p/ mm. Returns false if the method shape isn't as expected
        /// on this SE version.
        ///
        /// Out-params sufixados "M" (revisão 2026-07-23, docs/REVISAO-AutoEDM.md P1.2): este
        /// era o ponto de maior risco do projeto p/ o erro silencioso de fator 1000 — a
        /// assinatura antiga devolvia METROS sem nenhum sinal no nome (só um comentário),
        /// então quem chamava tinha que lembrar de cabeça. O sufixo obriga a decisão a
        /// aparecer no próprio ponto de captura (<c>out double xM</c>).
        /// </summary>
        public bool TryGetOrigin(OccurrenceInfo occ, out double xM, out double yM, out double zM)
        {
            xM = yM = zM = 0;
            if (!TryReadTransform(occ, out double[] v)) return false;
            xM = v[0]; yM = v[1]; zM = v[2];
            return true;
        }

        /// <summary>
        /// Best-effort read of an occurrence placement: origin (metros) + rotation
        /// angles (radianos), via GetTransform. Usado pelo relatório de coordenadas
        /// para (a) transladar para o zero-máquina e (b) sinalizar se há rotação.
        /// Ver nota de unidades em <see cref="TryGetOrigin"/> — mesmo sufixo "M"/"Rad".
        /// </summary>
        public bool TryGetPlacement(OccurrenceInfo occ,
            out double xM, out double yM, out double zM,
            out double axRad, out double ayRad, out double azRad)
        {
            xM = yM = zM = axRad = ayRad = azRad = 0;
            if (!TryReadTransform(occ, out double[] v)) return false;
            xM = v[0]; yM = v[1]; zM = v[2];
            axRad = v[3]; ayRad = v[4]; azRad = v[5];
            return true;
        }

        /// <summary>
        /// Lê os 6 valores de <c>Occurrence.GetTransform(out x,y,z, out ax,ay,az)</c>
        /// (metros/radianos). Os 6 são parâmetros [out]; em late binding é OBRIGATÓRIO
        /// marcá-los by-ref com um <see cref="ParameterModifier"/> — sem isso o
        /// InvokeMember NÃO popula os slots e a leitura volta (0,0,0,0,0,0) (mesmo bug do
        /// Face.GetRange, Logs 8-11). Era a causa de a cavidade aparecer sempre em (0,0,0)
        /// e do eletrodo sair deslocado no eixo Z na montagem.
        /// </summary>
        private static bool TryReadTransform(OccurrenceInfo occ, out double[] vals)
        {
            vals = new double[6];
            try
            {
                object[] args = { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 };
                var mod = new ParameterModifier(6);
                for (int i = 0; i < 6; i++) mod[i] = true; // [out] by-ref

                object target = occ.ComOccurrence;
                target.GetType().InvokeMember(
                    "GetTransform", BindingFlags.InvokeMethod, null, target, args,
                    new[] { mod }, CultureInfo.InvariantCulture, null);

                for (int i = 0; i < 6; i++) vals[i] = Convert.ToDouble(args[i]);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn($"GetTransform failed for '{occ.Name}': {ex.GetBaseException().Message}");
                return false;
            }
        }

        /// <summary>
        /// A pose COMPLETA da ocorrência (rotação 3D de verdade), via
        /// <c>Occurrence.GetMatrix([in,out] Matrix: SAFEARRAY(double))</c> — assinatura
        /// confirmada no dump da typelib SE 2023 (interface <c>Occurrence</c>, ao lado de
        /// <c>PutMatrix(Matrix, Replace)</c>).
        ///
        /// Por que a matriz e não os 3 ângulos do <see cref="TryGetPlacement"/>: os ângulos não
        /// vêm com a ordem de composição, e errar a ordem só aparece quando dois eixos giram
        /// juntos — exatamente o caso da cavidade inclinada. Ver <see cref="OccurrenceTransform"/>.
        ///
        /// O array vai PRÉ-ALOCADO com 16 posições e marcado by-ref: é [in,out], mesmo cuidado
        /// do <c>SurfaceByBoundaries.Add</c>. Devolve null (com log) se a leitura falhar — quem
        /// chama continua tendo o caminho antigo.
        /// </summary>
        public static OccurrenceTransform TryGetPose(OccurrenceInfo occ)
        {
            try
            {
                var matrix = new double[16];
                object[] args = { matrix };
                var mod = new ParameterModifier(1);
                mod[0] = true; // [in,out]

                object target = occ.ComOccurrence;
                target.GetType().InvokeMember(
                    "GetMatrix", BindingFlags.InvokeMethod, null, target, args,
                    new[] { mod }, CultureInfo.InvariantCulture, null);

                // O InvokeMember pode devolver o SAFEARRAY num objeto NOVO em vez de preencher
                // o nosso — aceitar os dois evita ler 16 zeros achando que leu a matriz.
                double[] read = args[0] as double[];
                if (read == null || read.Length < 16) read = matrix;
                if (read.Length < 16) { Log.Warn($"GetMatrix de '{occ.Name}': vieram {read.Length} valores, esperados 16."); return null; }

                bool allZero = true;
                foreach (double v in read) if (Math.Abs(v) > 1e-12) { allZero = false; break; }
                if (allZero) { Log.Warn($"GetMatrix de '{occ.Name}': matriz toda zero — leitura descartada."); return null; }

                // Gabarito para decidir a arrumação do array (ver OccurrenceTransform.FromMatrix).
                double[] origin = null;
                if (TryReadTransform(occ, out double[] t6)) origin = new[] { t6[0], t6[1], t6[2] };

                var pose = OccurrenceTransform.FromMatrix(read, origin);
                Log.Info($"Pose de '{occ.Name}': {pose.Describe()} — {pose.LayoutEvidence}.");
                return pose;
            }
            catch (Exception ex)
            {
                Log.Warn($"GetMatrix falhou para '{occ.Name}': {ex.GetBaseException().Message}");
                return null;
            }
        }

        private static string SafeName(dynamic occ)
        {
            try { return occ.Name; } catch { return "<unnamed>"; }
        }

        private static dynamic SafeDoc(dynamic occ)
        {
            try { return occ.OccurrenceDocument; } catch { return null; }
        }
    }
}
