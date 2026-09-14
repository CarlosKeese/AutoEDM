using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoEDM.Wedm
{
    public enum WireCurveKind
    {
        /// <summary>Segmento de reta (IGES 110).</summary>
        Line,
        /// <summary>Arco ou círculo completo num plano horizontal (IGES 100).</summary>
        Arc,
        /// <summary>B-spline (racional ou não) com os polos/nós originais (IGES 126).</summary>
        BSpline,
        /// <summary>Polilinha fina — quando a curva não tem leitura exata (elipse, spline recusada).</summary>
        Polyline,
    }

    /// <summary>
    /// Uma curva de perfil de corte a fio, já em mm e nas coordenadas da PEÇA (Carlos, 2026-09-14:
    /// os .igs vão para o Pitágoras e têm de cair exatamente no lugar). Criada pelas fábricas,
    /// que já deixam a geometria no formato que o IGES pede — o arco, por exemplo, sai sempre
    /// anti-horário visto de +Z. Lógica pura, sem COM.
    /// </summary>
    public sealed class WireCurve
    {
        private WireCurve() { }

        public WireCurveKind Kind { get; private set; }

        /// <summary>De onde veio (nome da feature de curva) — só para log e mensagens.</summary>
        public string Source { get; set; }

        /// <summary>Reta: pontas. Arco: início e fim no sentido ANTI-HORÁRIO visto de +Z (iguais no círculo completo).</summary>
        public double[] Start { get; private set; }
        public double[] End { get; private set; }

        /// <summary>Arco: centro e raio (mm).</summary>
        public double[] Center { get; private set; }
        public double Radius { get; private set; }
        public bool FullCircle { get; private set; }

        /// <summary>B-spline: grau, vetor de nós COMPLETO (polos + grau + 1), pesos (null = polinomial), polos (mm) e o trecho usado [ParamStart, ParamEnd].</summary>
        public int Degree { get; private set; }
        public double[] Knots { get; private set; }
        public double[] Weights { get; private set; }
        public double[][] Poles { get; private set; }
        public double ParamStart { get; private set; }
        public double ParamEnd { get; private set; }

        /// <summary>Polilinha: vértices (mm).</summary>
        public IReadOnlyList<double[]> Points { get; private set; }

        public static WireCurve Line(double[] start, double[] end) =>
            new WireCurve { Kind = WireCurveKind.Line, Start = Copy3(start), End = Copy3(end) };

        /// <summary>
        /// Arco num plano horizontal. O sentido em que o SE percorre o arco (eixo +Z ou −Z, aresta
        /// invertida ou não) não importa: o arco é o trecho do círculo entre as pontas que passa por
        /// <paramref name="mid"/>, e sai normalizado para anti-horário visto de +Z — a convenção da
        /// entidade 100 do IGES. Pontas coincidentes = círculo completo.
        /// </summary>
        public static WireCurve HorizontalArc(double[] center, double radius, double[] start, double[] end, double[] mid,
            double closedToleranceMm = 1e-4)
        {
            if (Distance(start, end) <= closedToleranceMm)
                return new WireCurve
                {
                    Kind = WireCurveKind.Arc, Center = Copy3(center), Radius = radius,
                    Start = Copy3(start), End = Copy3(start), FullCircle = true,
                };

            double s = Angle(center, start), e = Angle(center, end), m = Angle(center, mid);
            bool ccw = Positive(m - s) < Positive(e - s);
            return new WireCurve
            {
                Kind = WireCurveKind.Arc, Center = Copy3(center), Radius = radius,
                Start = Copy3(ccw ? start : end), End = Copy3(ccw ? end : start),
            };
        }

        /// <summary>B-spline. Lança <see cref="ArgumentException"/> se nós/pesos não fecharem com os polos.</summary>
        public static WireCurve BSpline(int degree, double[] knots, double[] weights, double[][] poles, double paramStart, double paramEnd)
        {
            if (degree < 1) throw new ArgumentException($"grau {degree} inválido");
            if (poles == null || poles.Length < degree + 1) throw new ArgumentException($"{poles?.Length ?? 0} polo(s) para grau {degree}");
            if (knots == null || knots.Length != poles.Length + degree + 1)
                throw new ArgumentException($"{knots?.Length ?? 0} nó(s), esperado {poles.Length + degree + 1} (polos + grau + 1)");
            if (weights != null && weights.Length != poles.Length)
                throw new ArgumentException($"{weights.Length} peso(s) para {poles.Length} polo(s)");
            if (weights != null && weights.Any(w => w <= 0)) throw new ArgumentException("peso não positivo");

            return new WireCurve
            {
                Kind = WireCurveKind.BSpline, Degree = degree,
                Knots = (double[])knots.Clone(),
                Weights = weights == null ? null : (double[])weights.Clone(),
                Poles = poles.Select(Copy3).ToArray(),
                ParamStart = paramStart, ParamEnd = paramEnd,
            };
        }

        public static WireCurve Polyline(IEnumerable<double[]> points)
        {
            var pts = (points ?? Enumerable.Empty<double[]>()).Select(Copy3).ToList();
            if (pts.Count < 2) throw new ArgumentException("polilinha com menos de 2 pontos");
            return new WireCurve { Kind = WireCurveKind.Polyline, Points = pts };
        }

        /// <summary>Faixa de Z da curva. Na B-spline vale a dos polos: a curva fica no fecho convexo deles.</summary>
        public void GetZRange(out double minZ, out double maxZ)
        {
            IEnumerable<double> zs;
            switch (Kind)
            {
                case WireCurveKind.Line: zs = new[] { Start[2], End[2] }; break;
                case WireCurveKind.Arc: zs = new[] { Center[2], Start[2], End[2] }; break;
                case WireCurveKind.BSpline: zs = Poles.Select(p => p[2]); break;
                default: zs = Points.Select(p => p[2]); break;
            }
            minZ = zs.Min();
            maxZ = zs.Max();
        }

        /// <summary>Ponto da B-spline no parâmetro <paramref name="t"/> (de Boor, em coordenadas homogêneas).</summary>
        public double[] Evaluate(double t)
        {
            if (Kind != WireCurveKind.BSpline) throw new InvalidOperationException("Evaluate só vale para B-spline");
            int p = Degree, n = Poles.Length - 1;
            t = Math.Max(Knots[p], Math.Min(Knots[n + 1], t));

            int k = n;
            for (int i = p; i < n; i++)
                if (t < Knots[i + 1]) { k = i; break; }

            var d = new double[p + 1][];
            for (int j = 0; j <= p; j++)
            {
                double w = Weights?[j + k - p] ?? 1.0;
                double[] pole = Poles[j + k - p];
                d[j] = new[] { pole[0] * w, pole[1] * w, pole[2] * w, w };
            }
            for (int r = 1; r <= p; r++)
                for (int j = p; j >= r; j--)
                {
                    int i = j + k - p;
                    double span = Knots[i + p + 1 - r] - Knots[i];
                    double a = span == 0 ? 0 : (t - Knots[i]) / span;
                    for (int c = 0; c < 4; c++) d[j][c] = (1 - a) * d[j - 1][c] + a * d[j][c];
                }
            return new[] { d[p][0] / d[p][3], d[p][1] / d[p][3], d[p][2] / d[p][3] };
        }

        public static double Distance(double[] a, double[] b)
        {
            double dx = a[0] - b[0], dy = a[1] - b[1], dz = a[2] - b[2];
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static double Angle(double[] center, double[] p) => Math.Atan2(p[1] - center[1], p[0] - center[0]);

        private static double Positive(double angle)
        {
            double twoPi = 2 * Math.PI;
            angle %= twoPi;
            return angle < 0 ? angle + twoPi : angle;
        }

        private static double[] Copy3(double[] p)
        {
            if (p == null || p.Length < 3) throw new ArgumentException("ponto precisa de X, Y e Z");
            return new[] { p[0], p[1], p[2] };
        }
    }
}
