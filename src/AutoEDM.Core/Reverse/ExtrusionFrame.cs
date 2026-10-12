using System;
using System.Collections.Generic;
using System.Globalization;

namespace AutoEDM.Reverse
{
    /// <summary>
    /// O frame em que a rota 2,5D trabalha: o Z LOCAL é a direção de extrusão escolhida.
    ///
    /// Por que existe: toda a secção e as correções foram escritas e provadas com Z como direção
    /// de extrusão, e há peça que não é 2,5D em Z — a vector_v2-6 (canal em U dobrado,
    /// 2026-10-11) é em Y; as peças do frame-fidget-mini-claw-machine estão GIRADAS 30° em volta
    /// de Y, e a direção delas não é eixo nenhum. Em vez de reescrever a secção para direção
    /// qualquer, a malha e as superfícies reconhecidas são levadas a um frame em que a direção
    /// escolhida É o Z, e só a construção volta ao mundo.
    ///
    /// Eixo do mundo: PERMUTAÇÃO CÍCLICA (local[i] = mundo[(eixo + 1 + i) % 3]) — rotação própria
    /// e aritmética exata (multiplicar por 0 e 1), então a normal (0, 1, 0) que o reconhecedor
    /// arredondou sai (0, 0, 1) EXATA, e a secção, que compara normal com 0 e 1 exatos, vale sem
    /// tolerância nova. Direção qualquer: base ortonormal (U, V, d), e no frame local os vetores
    /// a menos de <see cref="SnapLocal"/> de um eixo são levados a ele — o ajuste da face girada
    /// traz erro de 1e-7, e sem isso nenhuma parede seria "vertical".
    /// </summary>
    public sealed class ExtrusionFrame
    {
        /// <summary>Eixo do mundo que é a direção de extrusão (0 = X, 1 = Y, 2 = Z), ou −1 se a
        /// direção não for eixo do mundo.</summary>
        public readonly int Axis;

        /// <summary>Os eixos locais x, y, z escritos no mundo (unitários, ortogonais, z = direção).</summary>
        private readonly double[] ex, ey, ez;

        /// <summary>Componente de vetor local abaixo disto vira 0 (direção qualquer).</summary>
        public const double SnapLocal = 1e-5;

        public static readonly ExtrusionFrame Z = new ExtrusionFrame(2);

        public ExtrusionFrame(int axis)
        {
            if (axis < 0 || axis > 2) throw new ArgumentOutOfRangeException(nameof(axis));
            Axis = axis;
            ex = Unit((axis + 1) % 3);
            ey = Unit((axis + 2) % 3);
            ez = Unit(axis);
        }

        /// <summary>Frame de direção qualquer. Se a direção for eixo do mundo (a menos de
        /// 1e-9), devolve o frame de permutação dele — o caminho exato.</summary>
        public static ExtrusionFrame Along(double[] direction)
        {
            double l = Math.Sqrt(direction[0] * direction[0] + direction[1] * direction[1] + direction[2] * direction[2]);
            double[] d = { direction[0] / l, direction[1] / l, direction[2] / l };
            for (int k = 0; k < 3; k++) if (Math.Abs(Math.Abs(d[k]) - 1) < 1e-9) return new ExtrusionFrame(k);
            return new ExtrusionFrame(d);
        }

        private ExtrusionFrame(double[] d)
        {
            Axis = -1;
            ez = d;
            // x local: o eixo do mundo mais perpendicular a d, tirado o componente ao longo de d
            // (num giro em volta de Y, x local fica no plano XZ e y local é ±Y — o esboço sai
            // "de pé" para quem olha).
            int pick = 0;
            for (int k = 1; k < 3; k++) if (Math.Abs(d[k]) < Math.Abs(d[pick])) pick = k;
            double[] w = Unit(pick);
            double along = w[0] * d[0] + w[1] * d[1] + w[2] * d[2];
            double[] x = { w[0] - along * d[0], w[1] - along * d[1], w[2] - along * d[2] };
            double lx = Math.Sqrt(x[0] * x[0] + x[1] * x[1] + x[2] * x[2]);
            ex = new[] { x[0] / lx, x[1] / lx, x[2] / lx };
            ey = new[] { ez[1] * ex[2] - ez[2] * ex[1], ez[2] * ex[0] - ez[0] * ex[2], ez[0] * ex[1] - ez[1] * ex[0] };
        }

        public bool IsIdentity => Axis == 2;

        public string AxisName => Axis >= 0 ? "XYZ"[Axis].ToString()
            : string.Format(CultureInfo.InvariantCulture, "({0:0.###}; {1:0.###}; {2:0.###})", ez[0], ez[1], ez[2]);

        /// <summary>A direção de extrusão no mundo.</summary>
        public double[] Direction => (double[])ez.Clone();

        /// <summary>Vetor ou ponto do mundo no frame local.</summary>
        public double[] ToLocal(double[] w)
        {
            if (w == null) return null;
            if (Axis >= 0) return new[] { w[(Axis + 1) % 3], w[(Axis + 2) % 3], w[Axis] };
            return new[] { Dot(w, ex), Dot(w, ey), Dot(w, ez) };
        }

        /// <summary>Vetor ou ponto do frame local no mundo.</summary>
        public double[] ToWorld(double[] l)
        {
            if (l == null) return null;
            var w = new double[3];
            if (Axis >= 0)
            {
                w[(Axis + 1) % 3] = l[0];
                w[(Axis + 2) % 3] = l[1];
                w[Axis] = l[2];
                return w;
            }
            for (int k = 0; k < 3; k++) w[k] = l[0] * ex[k] + l[1] * ey[k] + l[2] * ez[k];
            return w;
        }

        public double[] ToWorld(double x, double y, double z) => ToWorld(new[] { x, y, z });

        /// <summary>Eixo local i (0 = x, 1 = y, 2 = z) escrito no mundo.</summary>
        public double[] LocalAxisInWorld(int i)
        {
            return (double[])(i == 0 ? ex : i == 1 ? ey : ez).Clone();
        }

        /// <summary>Sopa de triângulos (9 doubles por triângulo) no frame local.</summary>
        public double[] ToLocalPoints(double[] pointsMm)
        {
            if (IsIdentity) return pointsMm;
            var p = new double[pointsMm.Length];
            for (int i = 0; i + 2 < pointsMm.Length; i += 3)
            {
                if (Axis >= 0)
                {
                    p[i] = pointsMm[i + (Axis + 1) % 3];
                    p[i + 1] = pointsMm[i + (Axis + 2) % 3];
                    p[i + 2] = pointsMm[i + Axis];
                    continue;
                }
                double x = pointsMm[i], y = pointsMm[i + 1], z = pointsMm[i + 2];
                p[i] = x * ex[0] + y * ex[1] + z * ex[2];
                p[i + 1] = x * ey[0] + y * ey[1] + z * ey[2];
                p[i + 2] = x * ez[0] + y * ez[1] + z * ez[2];
            }
            return p;
        }

        /// <summary>
        /// O reconhecimento no frame local: as mesmas superfícies, com todo vetor e ponto levado
        /// ao frame. O mapa triângulo → superfície é o mesmo (a ordem dos triângulos não muda).
        /// Reconhecer de novo a malha girada daria o mesmo resultado, mas custa o reconhecimento
        /// inteiro outra vez (68 s na vector_v2-6).
        /// </summary>
        public RecognitionResult ToLocal(RecognitionResult r)
        {
            if (r == null || IsIdentity) return r;
            var l = new RecognitionResult
            {
                TriangleCount = r.TriangleCount,
                DegenerateTriangles = r.DegenerateTriangles,
                TotalAreaMm2 = r.TotalAreaMm2,
                FreeFormAreaMm2 = r.FreeFormAreaMm2,
                RegionOfTriangle = r.RegionOfTriangle
            };
            BoxToLocal(r.MinMm, r.MaxMm, out l.MinMm, out l.MaxMm);
            foreach (RecognizedSurface s in r.Surfaces) l.Surfaces.Add(ToLocal(s));
            return l;
        }

        private RecognizedSurface ToLocal(RecognizedSurface s)
        {
            var o = new RecognizedSurface
            {
                Kind = s.Kind,
                TriangleCount = s.TriangleCount,
                AreaMm2 = s.AreaMm2,
                Normal = LocalDirection(s.Normal),
                PointMm = ToLocal(s.PointMm),
                AxisMm = LocalDirection(s.AxisMm),
                RadiusMm = s.RadiusMm,
                LengthMm = s.LengthMm,
                SweepDeg = s.SweepDeg,
                ArcFrom = LocalDirection(s.ArcFrom),
                ArcTo = LocalDirection(s.ArcTo),
                MajorRadiusMm = s.MajorRadiusMm,
                TubeFromDeg = s.TubeFromDeg,
                TubeToDeg = s.TubeToDeg,
                HalfAngleDeg = s.HalfAngleDeg,
                ApexMm = ToLocal(s.ApexMm),
                RadiusMinMm = s.RadiusMinMm,
                RadiusMaxMm = s.RadiusMaxMm,
                FitRmsMm = s.FitRmsMm
            };
            BoxToLocal(s.MinMm, s.MaxMm, out o.MinMm, out o.MaxMm);
            return o;
        }

        /// <summary>Direção no frame local; na direção qualquer, componente minúsculo vira 0.</summary>
        private double[] LocalDirection(double[] w)
        {
            double[] v = ToLocal(w);
            if (v == null || Axis >= 0) return v;
            int zeros = 0;
            for (int k = 0; k < 3; k++) if (Math.Abs(v[k]) < SnapLocal) { v[k] = 0; zeros++; }
            if (zeros == 2)
            {
                // Sobrou um componente: é o eixo, EXATO (a secção compara com 1).
                for (int k = 0; k < 3; k++) v[k] = Math.Sign(v[k]);
                return v;
            }
            double l = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
            for (int k = 0; k < 3; k++) v[k] /= l;
            return v;
        }

        /// <summary>Caixa alinhada no frame local: a dos 8 cantos (na permutação, exata).</summary>
        private void BoxToLocal(double[] min, double[] max, out double[] lmin, out double[] lmax)
        {
            if (min == null || max == null) { lmin = ToLocal(min); lmax = ToLocal(max); return; }
            if (Axis >= 0) { lmin = ToLocal(min); lmax = ToLocal(max); return; }
            lmin = new[] { double.MaxValue, double.MaxValue, double.MaxValue };
            lmax = new[] { double.MinValue, double.MinValue, double.MinValue };
            for (int c = 0; c < 8; c++)
            {
                double[] q = ToLocal(new[] { (c & 1) == 0 ? min[0] : max[0], (c & 2) == 0 ? min[1] : max[1], (c & 4) == 0 ? min[2] : max[2] });
                for (int k = 0; k < 3; k++) { lmin[k] = Math.Min(lmin[k], q[k]); lmax[k] = Math.Max(lmax[k], q[k]); }
            }
        }

        private static double[] Unit(int k)
        {
            var u = new double[3];
            u[k] = 1;
            return u;
        }

        private static double Dot(double[] a, double[] b) { return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]; }
    }
}
