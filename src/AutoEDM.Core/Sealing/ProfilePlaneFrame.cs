using System;
using System.Reflection;
using AutoEDM.Diagnostics;
using AutoEDM.Model;

namespace AutoEDM.Sealing
{
    /// <summary>
    /// Onde um plano de esboço está, DE VERDADE, no espaço 3D — descoberto perguntando ao
    /// próprio Solid Edge em vez de deduzido de qual RefPlane é qual.
    ///
    /// POR QUE ISSO EXISTE. Para desenhar um perfil a partir de coordenadas 3D reais (o
    /// diâmetro de um canal, a altura de uma face) é preciso saber que direção 3D é o "x" e
    /// que direção é o "y" DAQUELE plano — e isso muda por plano, por versão e por como o
    /// plano foi criado. Adivinhar é como se erra sinal e troca eixo.
    ///
    /// <c>Profile.Convert2DCoordinate(x2d, y2d, [out] x3d, [out] y3d, [out] z3d)</c> responde
    /// direto: (0,0) dá a ORIGEM do plano, (1,0) e (0,1) dão os dois vetores unitários dele em
    /// 3D, e o produto vetorial dá a NORMAL. Com isso em mãos, dá para converter qualquer ponto
    /// 3D com <c>Convert3DCoordinate</c> e nunca mais pensar em orientação de plano.
    /// Os dois métodos têm parâmetros [out], então precisam do <c>ParameterModifier</c> by-ref
    /// — a mesma armadilha do <c>Face.GetRange</c>.
    ///
    /// Unidades: a API é em METROS; esta classe expõe tudo em MILÍMETROS.
    /// </summary>
    public sealed class ProfilePlaneFrame
    {
        /// <summary>Origem do plano em 3D (mm).</summary>
        public double[] Origin { get; private set; }

        /// <summary>Direção 3D do "x" do esboço (unitária).</summary>
        public double[] U { get; private set; }

        /// <summary>Direção 3D do "y" do esboço (unitária).</summary>
        public double[] V { get; private set; }

        /// <summary>Normal do plano (unitária) = U × V.</summary>
        public double[] Normal { get; private set; }

        /// <summary>
        /// Descobre o referencial do plano do perfil. Devolve null (com aviso no log) se o
        /// Solid Edge não responder — nunca lança.
        /// </summary>
        public static ProfilePlaneFrame Discover(object profile)
        {
            double[] o, px, py;
            if (!TryTo3d(profile, 0, 0, out o)) return null;
            if (!TryTo3d(profile, 1, 0, out px)) return null;   // 1 METRO no x do esboço
            if (!TryTo3d(profile, 0, 1, out py)) return null;

            var u = Normalize(Sub(px, o));
            var v = Normalize(Sub(py, o));
            if (u == null || v == null) { Log.Warn("[plano] eixos do esboço degenerados."); return null; }

            var frame = new ProfilePlaneFrame { Origin = o, U = u, V = v, Normal = Normalize(Cross(u, v)) };
            Log.Info($"  [plano] origem ({o[0]:0.###}, {o[1]:0.###}, {o[2]:0.###}) mm  " +
                     $"normal ({frame.Normal[0]:0.###}, {frame.Normal[1]:0.###}, {frame.Normal[2]:0.###})");
            return frame;
        }

        /// <summary>Distância COM SINAL de um ponto 3D (mm) ao plano, ao longo da normal.</summary>
        public double SignedDistance(double[] pointMm) => Dot(Sub(pointMm, Origin), Normal);

        /// <summary>true se a reta (ponto + direção) está inteiramente contida no plano.</summary>
        public bool ContainsLine(double[] pointMm, double[] direction, double tolMm = 0.01) =>
            Math.Abs(Dot(direction, Normal)) < 1e-6 && Math.Abs(SignedDistance(pointMm)) < tolMm;

        /// <summary>true se a reta é paralela ao plano (mesmo estando afastada dele).</summary>
        public bool IsParallelToLine(double[] direction) => Math.Abs(Dot(direction, Normal)) < 1e-6;

        /// <summary>
        /// Converte um ponto 3D (mm) para as coordenadas 2D do esboço (mm). É por aqui que
        /// todo ponto do perfil passa: assim o desenho é feito em coordenadas REAIS da peça e
        /// o Solid Edge é quem resolve a orientação do plano.
        /// </summary>
        public bool TryTo2dMm(object profile, double[] point3dMm, out double x2dMm, out double y2dMm)
        {
            x2dMm = y2dMm = 0;
            try
            {
                object[] args =
                {
                    Units.MmToM(point3dMm[0]), Units.MmToM(point3dMm[1]), Units.MmToM(point3dMm[2]),
                    0.0, 0.0
                };
                var mod = new ParameterModifier(5);
                mod[3] = true; mod[4] = true; // [out] x2d, y2d
                profile.GetType().InvokeMember("Convert3DCoordinate", BindingFlags.InvokeMethod, null, profile,
                    args, new[] { mod }, null, null);
                x2dMm = Units.MToMm(Convert.ToDouble(args[3]));
                y2dMm = Units.MToMm(Convert.ToDouble(args[4]));
                return true;
            }
            catch (Exception e) { Log.Warn("  [plano] Convert3DCoordinate falhou: " + e.GetBaseException().Message); return false; }
        }

        private static bool TryTo3d(object profile, double x2dM, double y2dM, out double[] pointMm)
        {
            pointMm = null;
            try
            {
                object[] args = { x2dM, y2dM, 0.0, 0.0, 0.0 };
                var mod = new ParameterModifier(5);
                mod[2] = true; mod[3] = true; mod[4] = true; // [out] x3d, y3d, z3d
                profile.GetType().InvokeMember("Convert2DCoordinate", BindingFlags.InvokeMethod, null, profile,
                    args, new[] { mod }, null, null);
                pointMm = new[]
                {
                    Units.MToMm(Convert.ToDouble(args[2])),
                    Units.MToMm(Convert.ToDouble(args[3])),
                    Units.MToMm(Convert.ToDouble(args[4]))
                };
                return true;
            }
            catch (Exception e) { Log.Warn("  [plano] Convert2DCoordinate falhou: " + e.GetBaseException().Message); return false; }
        }

        // ------------------------------------------------------------------ vetores

        public static double[] Sub(double[] a, double[] b) => new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };
        public static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

        public static double[] Cross(double[] a, double[] b) => new[]
        {
            a[1] * b[2] - a[2] * b[1],
            a[2] * b[0] - a[0] * b[2],
            a[0] * b[1] - a[1] * b[0]
        };

        public static double[] Normalize(double[] v)
        {
            double n = Math.Sqrt(Dot(v, v));
            return n < 1e-12 ? null : new[] { v[0] / n, v[1] / n, v[2] / n };
        }

        public static double[] Add(double[] a, double[] b, double scale) =>
            new[] { a[0] + b[0] * scale, a[1] + b[1] * scale, a[2] + b[2] * scale };

        /// <summary>Vetor unitário do eixo global (0=X, 1=Y, 2=Z).</summary>
        public static double[] AxisVector(int axisIndex)
        {
            var v = new double[3];
            v[axisIndex] = 1.0;
            return v;
        }
    }
}
