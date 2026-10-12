using System;
using System.Collections.Generic;

namespace AutoEDM.Reverse
{
    /// <summary>Quanto o sólido que o plano vai construir difere da malha, estimado por pontos.</summary>
    public sealed class PlanEvaluation
    {
        public int Samples;

        /// <summary>Volume da malha estimado pelos mesmos pontos, mm³.</summary>
        public double MeshVolumeMm3;

        /// <summary>Volume em que a malha tem material e o sólido previsto não (falta), e o
        /// contrário (sobra), mm³ — TUDO, inclusive a casca fina da tesselação.</summary>
        public double MissingMm3, ExtraMm3;

        /// <summary>A parte da discordância LONGE da superfície da malha (mais de
        /// <see cref="PlanEvaluator.FarMm"/>): é o erro de forma, o que o desvio de 0,05 mm do
        /// construído vai acusar. Perto da superfície é flecha de tesselação e raio.</summary>
        public double FarErrorMm3;

        /// <summary>Onde está o erro longe: centro (frame do plano) e volume de cada aglomerado,
        /// do maior para o menor.</summary>
        public readonly List<Tuple<double[], double>> FarClusters = new List<Tuple<double[], double>>();
    }

    /// <summary>
    /// PREVISÃO do sólido sem a Solid Edge. Pontos aleatórios na caixa da malha; para cada um,
    /// "dentro da malha" (paridade de um raio em +Z, triângulos numa grade XY) contra "dentro do
    /// sólido previsto" (a faixa que contém o ponto e o enrolamento dos laços dela; depois cada
    /// correção NA ORDEM, que põe ou tira o material da região dela). A discordância longe da
    /// superfície da malha é erro de forma.
    ///
    /// Nasceu para escolher a direção de extrusão da vector_v2-6 (2026-10-11): o volume do plano
    /// sozinho engana (falta e sobra se cancelam), e construir na SE para descobrir custa minutos
    /// por tentativa. Conferido contra o construído: nos corpos 2, 4 e 6 do Rack_Anghinoni
    /// (desvio ≤ 0,04 mm na SE) a previsão dá 0 mm³ de erro longe; no corpo 3, só as
    /// concordâncias de canto que a SE também acusou.
    /// </summary>
    public static class PlanEvaluator
    {
        /// <summary>Discordância a mais disto da superfície da malha conta como erro de forma.</summary>
        public const double FarMm = 0.3;

        public static PlanEvaluation Evaluate(double[] pointsMm, PrismaticPlan plan, CorrectionPlan corrections,
            int samples, int seed)
        {
            return Evaluate(pointsMm, plan, corrections, samples, seed, FarMm);
        }

        public static PlanEvaluation Evaluate(double[] pointsMm, PrismaticPlan plan, CorrectionPlan corrections,
            int samples, int seed, double farMm)
        {
            if (pointsMm == null) throw new ArgumentNullException(nameof(pointsMm));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var ev = new PlanEvaluation { Samples = samples };
            if (pointsMm.Length < 9 || samples <= 0) return ev;

            var grid = new ColumnGrid(pointsMm, 0.5);
            var solid = new PredictedSolid(plan, corrections);

            double[] mn = { double.MaxValue, double.MaxValue, double.MaxValue };
            double[] mx = { double.MinValue, double.MinValue, double.MinValue };
            for (int i = 0; i + 2 < pointsMm.Length; i += 3)
                for (int k = 0; k < 3; k++)
                {
                    mn[k] = Math.Min(mn[k], pointsMm[i + k]);
                    mx[k] = Math.Max(mx[k], pointsMm[i + k]);
                }
            double unit = (mx[0] - mn[0]) * (mx[1] - mn[1]) * (mx[2] - mn[2]) / samples;

            var rnd = new Random(seed);
            var far = new List<double[]>();
            int inMesh = 0, missing = 0, extra = 0;
            for (int i = 0; i < samples; i++)
            {
                double[] q =
                {
                    mn[0] + rnd.NextDouble() * (mx[0] - mn[0]),
                    mn[1] + rnd.NextDouble() * (mx[1] - mn[1]),
                    mn[2] + rnd.NextDouble() * (mx[2] - mn[2])
                };
                bool m = grid.Inside(q[0], q[1], q[2]);
                bool s = solid.Contains(q);
                if (m) inMesh++;
                if (m == s) continue;
                if (m) missing++; else extra++;
                if (grid.Distance(q, farMm) > farMm) far.Add(q);
            }

            ev.MeshVolumeMm3 = inMesh * unit;
            ev.MissingMm3 = missing * unit;
            ev.ExtraMm3 = extra * unit;
            ev.FarErrorMm3 = far.Count * unit;

            // Aglomerados de 2 mm: é o "onde" do relatório.
            var centers = new List<double[]>();
            var counts = new List<int>();
            foreach (double[] q in far)
            {
                int hit = -1;
                for (int k = 0; k < centers.Count && hit < 0; k++)
                    if (Math.Abs(centers[k][0] - q[0]) < 2 && Math.Abs(centers[k][1] - q[1]) < 2 && Math.Abs(centers[k][2] - q[2]) < 2) hit = k;
                if (hit < 0) { centers.Add(q); counts.Add(1); } else counts[hit]++;
            }
            var order = new List<int>();
            for (int k = 0; k < centers.Count; k++) order.Add(k);
            order.Sort((a, b) => counts[b].CompareTo(counts[a]));
            foreach (int k in order) ev.FarClusters.Add(Tuple.Create(centers[k], counts[k] * unit));
            return ev;
        }

        // ------------------------------------------------------------------ malha

        /// <summary>Triângulos numa grade XY, para o raio vertical e para a distância.</summary>
        internal sealed class ColumnGrid
        {
            private readonly double[] p;
            private readonly double x0, y0, cell;
            private readonly int nx, ny;
            private readonly List<int>[] bins;

            public ColumnGrid(double[] pts, double cellMm)
            {
                p = pts;
                cell = cellMm;
                double mnx = double.MaxValue, mny = double.MaxValue, mxx = double.MinValue, mxy = double.MinValue;
                for (int i = 0; i + 2 < p.Length; i += 3)
                {
                    mnx = Math.Min(mnx, p[i]); mxx = Math.Max(mxx, p[i]);
                    mny = Math.Min(mny, p[i + 1]); mxy = Math.Max(mxy, p[i + 1]);
                }
                x0 = mnx - 1e-6; y0 = mny - 1e-6;
                nx = (int)((mxx - x0) / cell) + 1;
                ny = (int)((mxy - y0) / cell) + 1;
                bins = new List<int>[nx * ny];
                for (int i = 0; i < bins.Length; i++) bins[i] = new List<int>();
                for (int t = 0; t < p.Length / 9; t++)
                {
                    double a = double.MaxValue, b = double.MinValue, c = double.MaxValue, d = double.MinValue;
                    for (int k = 0; k < 3; k++)
                    {
                        a = Math.Min(a, p[t * 9 + k * 3]); b = Math.Max(b, p[t * 9 + k * 3]);
                        c = Math.Min(c, p[t * 9 + k * 3 + 1]); d = Math.Max(d, p[t * 9 + k * 3 + 1]);
                    }
                    for (int i = (int)((a - x0) / cell); i <= (int)((b - x0) / cell); i++)
                        for (int j = (int)((c - y0) / cell); j <= (int)((d - y0) / cell); j++)
                            bins[j * nx + i].Add(t);
                }
            }

            /// <summary>
            /// Dentro pelo ENROLAMENTO do raio de (x, y, z) para +Z: cada triângulo cruzado conta +1
            /// ou −1 pelo lado para onde a normal dele aponta, e dentro é soma diferente de zero.
            /// Paridade (par/ímpar) não serve: malha com cascas SOBREPOSTAS (o stack-container,
            /// 2026-10-11 — volume com sinal 2.367 mm³, sólido de verdade 2.700) tem o pedaço
            /// sobreposto contado duas vezes, e a paridade o via vazio; a SE, como o enrolamento,
            /// o vê cheio.
            /// </summary>
            public bool Inside(double x, double y, double z)
            {
                int i = (int)((x - x0) / cell), j = (int)((y - y0) / cell);
                if (i < 0 || j < 0 || i >= nx || j >= ny) return false;
                int count = 0;
                foreach (int t in bins[j * nx + i])
                {
                    int b = t * 9;
                    double ax = p[b], ay = p[b + 1], bx = p[b + 3], by = p[b + 4], cx = p[b + 6], cy = p[b + 7];
                    double d = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
                    if (Math.Abs(d) < 1e-15) continue;
                    double l1 = ((bx - x) * (cy - y) - (by - y) * (cx - x)) / d;
                    double l2 = ((cx - x) * (ay - y) - (cy - y) * (ax - x)) / d;
                    double l3 = 1 - l1 - l2;
                    if (l1 < 0 || l2 < 0 || l3 < 0) continue;
                    if (l1 * p[b + 2] + l2 * p[b + 5] + l3 * p[b + 8] > z) count += d > 0 ? 1 : -1;
                }
                return count != 0;
            }

            /// <summary>Distância à malha, procurada até 'reach' (além disso devolve algo maior).</summary>
            public double Distance(double[] q, double reach)
            {
                int ci = (int)((q[0] - x0) / cell), cj = (int)((q[1] - y0) / cell);
                int r = (int)Math.Ceiling(reach / cell) + 1;
                double best = double.MaxValue;
                var seen = new HashSet<int>();
                for (int i = ci - r; i <= ci + r; i++)
                    for (int j = cj - r; j <= cj + r; j++)
                    {
                        if (i < 0 || j < 0 || i >= nx || j >= ny) continue;
                        foreach (int t in bins[j * nx + i])
                            if (seen.Add(t)) best = Math.Min(best, MeshDeviation.PointTriangle(q[0], q[1], q[2], p, t * 9));
                    }
                return best;
            }
        }

        // ------------------------------------------------------------------ sólido previsto

        /// <summary>
        /// O sólido que o plano vai construir, para testar ponto a ponto. Cada polígono leva a
        /// caixa (u, v) junto e o enrolamento é o inteiro de Sunday (cruzamentos com sinal), não a
        /// soma de ângulos: com os degraus de faixa a vector_v2-6 passou de 138 para ~230
        /// correções por direção avaliada, e a previsão (60 mil pontos × todas) dobrava o tempo do
        /// plano. Mesmo resultado, fora de pontos exatamente na borda.
        /// </summary>
        private sealed class PredictedSolid
        {
            private readonly List<double> z0 = new List<double>(), z1 = new List<double>();
            private readonly List<List<Poly>> bandPolys = new List<List<Poly>>();
            private readonly List<CorrectionRegion> regions = new List<CorrectionRegion>();
            private readonly List<double[]> regionW = new List<double[]>();
            private readonly List<Poly> regionPolys = new List<Poly>();

            public PredictedSolid(PrismaticPlan plan, CorrectionPlan corrections)
            {
                foreach (SectionBand b in plan.Bands)
                {
                    z0.Add(b.Z0);
                    z1.Add(b.Z1);
                    var polys = new List<Poly>();
                    foreach (SectionLoop l in b.Loops) polys.Add(new Poly(LoopPolygon(l)));
                    bandPolys.Add(polys);
                }
                if (corrections != null)
                    foreach (CorrectionRegion c in corrections.Regions)
                    {
                        regions.Add(c);
                        regionW.Add(c.W);
                        var poly = new List<double[]>();
                        foreach (ProfileEdge e in c.Edges)
                            if (e.Kind == "arco") poly.AddRange(ArcPoints(e.Start, e.Mid, e.End, 24));
                            else poly.Add(e.Start);
                        regionPolys.Add(new Poly(poly));
                    }
            }

            public bool Contains(double[] q)
            {
                bool state = false;
                for (int b = 0; b < z0.Count; b++)
                {
                    if (q[2] <= z0[b] || q[2] >= z1[b]) continue;
                    int w = 0;
                    foreach (Poly poly in bandPolys[b]) w += poly.Winding(q[0], q[1]);
                    state = w != 0;
                    break;
                }
                for (int k = 0; k < regions.Count; k++)
                {
                    CorrectionRegion c = regions[k];
                    double dx = q[0] - c.Origin[0], dy = q[1] - c.Origin[1], dz = q[2] - c.Origin[2];
                    double[] w = regionW[k];
                    double u, v;
                    if (c.Kind == "revolucao")
                    {
                        v = dx * c.V[0] + dy * c.V[1] + dz * c.V[2];
                        double ru = dx * c.U[0] + dy * c.U[1] + dz * c.U[2], rw = dx * w[0] + dy * w[1] + dz * w[2];
                        u = Math.Sqrt(ru * ru + rw * rw);
                        if (c.SweepRad < 2 * Math.PI - 1e-9 && Math.Abs(Math.Atan2(rw, ru)) > c.SweepRad / 2) continue;
                    }
                    else
                    {
                        double ww = dx * w[0] + dy * w[1] + dz * w[2];
                        if (ww < 0 || ww > c.DepthMm) continue;
                        u = dx * c.U[0] + dy * c.U[1] + dz * c.U[2];
                        v = dx * c.V[0] + dy * c.V[1] + dz * c.V[2];
                    }
                    if (regionPolys[k].Winding(u, v) != 0) state = c.Add;
                }
                return state;
            }
        }

        /// <summary>Polígono fechado com a caixa, e o número de enrolamento inteiro.</summary>
        private sealed class Poly
        {
            private readonly double[] x, y;
            private readonly double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;

            public Poly(List<double[]> pts)
            {
                x = new double[pts.Count];
                y = new double[pts.Count];
                for (int k = 0; k < pts.Count; k++)
                {
                    x[k] = pts[k][0]; y[k] = pts[k][1];
                    minX = Math.Min(minX, x[k]); maxX = Math.Max(maxX, x[k]);
                    minY = Math.Min(minY, y[k]); maxY = Math.Max(maxY, y[k]);
                }
            }

            /// <summary>Sunday: cruzamentos com sinal da semirreta para +x.</summary>
            public int Winding(double px, double py)
            {
                if (x.Length < 3 || px < minX || px > maxX || py < minY || py > maxY) return 0;
                int wn = 0;
                for (int i = 0, j = x.Length - 1; i < x.Length; j = i++)
                {
                    double ax = x[j], ay = y[j], bx = x[i], by = y[i];
                    double side = (bx - ax) * (py - ay) - (px - ax) * (by - ay);
                    if (ay <= py) { if (by > py && side > 0) wn++; }
                    else if (by <= py && side < 0) wn--;
                }
                return wn;
            }
        }

        private static List<double[]> LoopPolygon(SectionLoop l)
        {
            var poly = new List<double[]>();
            foreach (SectionElement e in l.Elements)
            {
                if (e.Kind == "circulo")
                {
                    bool cw = l.SignedAreaMm2 < 0;
                    for (int k = 0; k < 72; k++)
                    {
                        double t = 2 * Math.PI * k / 72 * (cw ? -1 : 1);
                        poly.Add(new[] { e.Center[0] + e.RadiusMm * Math.Cos(t), e.Center[1] + e.RadiusMm * Math.Sin(t) });
                    }
                    continue;
                }
                if (e.Kind == "arco") poly.AddRange(ArcPoints(e.Start, e.Mid, e.End, 16));
                else poly.Add(e.Start);
            }
            return poly;
        }

        /// <summary>Arco por três pontos em n passos (o último ponto fica para o elemento seguinte).</summary>
        private static List<double[]> ArcPoints(double[] s, double[] m, double[] e, int n)
        {
            var res = new List<double[]>();
            double ax = s[0], ay = s[1], bx = m[0], by = m[1], cx = e[0], cy = e[1];
            double d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
            if (Math.Abs(d) < 1e-15) { res.Add(s); return res; }
            double a2 = ax * ax + ay * ay, b2 = bx * bx + by * by, c2 = cx * cx + cy * cy;
            double ux = (a2 * (by - cy) + b2 * (cy - ay) + c2 * (ay - by)) / d;
            double uy = (a2 * (cx - bx) + b2 * (ax - cx) + c2 * (bx - ax)) / d;
            double r = Math.Sqrt((ax - ux) * (ax - ux) + (ay - uy) * (ay - uy));
            double t0 = Math.Atan2(ay - uy, ax - ux), tm = Math.Atan2(by - uy, bx - ux), t1 = Math.Atan2(cy - uy, cx - ux);
            double ccw = Wrap(t1 - t0);
            double sweep = Wrap(tm - t0) < ccw ? ccw : ccw - 2 * Math.PI;
            for (int k = 0; k < n; k++)
            {
                double t = t0 + sweep * k / n;
                res.Add(new[] { ux + r * Math.Cos(t), uy + r * Math.Sin(t) });
            }
            return res;
        }

        private static double Wrap(double a)
        {
            while (a < 0) a += 2 * Math.PI;
            while (a >= 2 * Math.PI) a -= 2 * Math.PI;
            return a;
        }

        private static double Dot(double[] a, double[] b) { return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]; }
    }
}
