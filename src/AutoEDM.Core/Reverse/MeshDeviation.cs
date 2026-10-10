using System;
using System.Collections.Generic;

namespace AutoEDM.Reverse
{
    public sealed class DeviationResult
    {
        public int Points;
        public double MaxMm;
        public double RmsMm;

        /// <summary>Onde está o pior ponto (da malha de origem), em mm.</summary>
        public double[] WorstAtMm;

        /// <summary>Pontos acima do limite pedido.</summary>
        public int Above;
    }

    /// <summary>
    /// Quanto uma superfície triangulada se afasta da outra: para cada vértice de A, a distância
    /// até o triângulo mais próximo de B. É a conferência que o volume NÃO faz — na malha 6 do
    /// Rack_Anghinoni os 24 chanfros tiram exatamente o que os 24 colarinhos põem, e o volume do
    /// sólido bateu com o do plano sem as correções terem provado nada.
    ///
    /// Busca por grade uniforme: cada triângulo de B entra nas células que a caixa dele toca, e a
    /// consulta abre anéis de células até o raio já achado ficar menor que a distância ao anel.
    /// </summary>
    public static class MeshDeviation
    {
        public static DeviationResult Compute(double[] fromPointsMm, double[] toPointsMm, double limitMm)
        {
            return Compute(fromPointsMm, toPointsMm, limitMm, null);
        }

        /// <param name="far">Se não for null, recebe (x, y, z, distância) de cada ponto acima do limite.</param>
        public static DeviationResult Compute(double[] fromPointsMm, double[] toPointsMm, double limitMm, List<double[]> far)
        {
            var result = new DeviationResult { WorstAtMm = new double[3] };
            int triCount = toPointsMm.Length / 9;
            if (triCount == 0 || fromPointsMm.Length < 3) return result;

            double[] min = { double.MaxValue, double.MaxValue, double.MaxValue };
            double[] max = { double.MinValue, double.MinValue, double.MinValue };
            for (int i = 0; i + 2 < toPointsMm.Length; i += 3)
                for (int k = 0; k < 3; k++)
                {
                    min[k] = Math.Min(min[k], toPointsMm[i + k]);
                    max[k] = Math.Max(max[k], toPointsMm[i + k]);
                }

            double diag = Math.Sqrt(Sq(max[0] - min[0]) + Sq(max[1] - min[1]) + Sq(max[2] - min[2]));
            double cell = Math.Max(diag / 100.0, 0.5);
            var grid = new Dictionary<long, List<int>>();
            for (int t = 0; t < triCount; t++)
            {
                int b = t * 9;
                double[] lo = { double.MaxValue, double.MaxValue, double.MaxValue };
                double[] hi = { double.MinValue, double.MinValue, double.MinValue };
                for (int v = 0; v < 3; v++)
                    for (int k = 0; k < 3; k++)
                    {
                        lo[k] = Math.Min(lo[k], toPointsMm[b + v * 3 + k]);
                        hi[k] = Math.Max(hi[k], toPointsMm[b + v * 3 + k]);
                    }
                for (long ix = Cell(lo[0], min[0], cell); ix <= Cell(hi[0], min[0], cell); ix++)
                    for (long iy = Cell(lo[1], min[1], cell); iy <= Cell(hi[1], min[1], cell); iy++)
                        for (long iz = Cell(lo[2], min[2], cell); iz <= Cell(hi[2], min[2], cell); iz++)
                        {
                            long key = Key(ix, iy, iz);
                            List<int> list;
                            if (!grid.TryGetValue(key, out list)) grid[key] = list = new List<int>();
                            list.Add(t);
                        }
            }

            // Vértices repetidos (sopa sem índice) contam uma vez.
            var seen = new HashSet<long>();
            double sumSq = 0;
            for (int i = 0; i + 2 < fromPointsMm.Length; i += 3)
            {
                double x = fromPointsMm[i], y = fromPointsMm[i + 1], z = fromPointsMm[i + 2];
                long id = Key((long)Math.Round(x * 1e4), (long)Math.Round(y * 1e4), (long)Math.Round(z * 1e4));
                if (!seen.Add(id)) continue;

                double d = Nearest(x, y, z, toPointsMm, grid, min, cell);
                result.Points++;
                sumSq += d * d;
                if (d > limitMm) { result.Above++; if (far != null) far.Add(new[] { x, y, z, d }); }
                if (d > result.MaxMm) { result.MaxMm = d; result.WorstAtMm = new[] { x, y, z }; }
            }
            result.RmsMm = result.Points > 0 ? Math.Sqrt(sumSq / result.Points) : 0;
            return result;
        }

        private static double Nearest(double x, double y, double z, double[] tri, Dictionary<long, List<int>> grid,
            double[] min, double cell)
        {
            long cx = Cell(x, min[0], cell), cy = Cell(y, min[1], cell), cz = Cell(z, min[2], cell);
            double best = double.MaxValue;
            for (int ring = 0; ring < 200; ring++)
            {
                // Tudo que está fora do anel `ring` está a pelo menos (ring) células de distância.
                if (best < (ring - 1) * cell) break;
                for (long ix = cx - ring; ix <= cx + ring; ix++)
                    for (long iy = cy - ring; iy <= cy + ring; iy++)
                        for (long iz = cz - ring; iz <= cz + ring; iz++)
                        {
                            if (Math.Max(Math.Abs(ix - cx), Math.Max(Math.Abs(iy - cy), Math.Abs(iz - cz))) != ring) continue;
                            List<int> list;
                            if (!grid.TryGetValue(Key(ix, iy, iz), out list)) continue;
                            foreach (int t in list)
                            {
                                double d = PointTriangle(x, y, z, tri, t * 9);
                                if (d < best) best = d;
                            }
                        }
            }
            return best;
        }

        /// <summary>Distância de ponto a triângulo (Ericson, "Real-Time Collision Detection", 5.1.5).</summary>
        public static double PointTriangle(double px, double py, double pz, double[] t, int b)
        {
            double ax = t[b], ay = t[b + 1], az = t[b + 2];
            double bx = t[b + 3], by = t[b + 4], bz = t[b + 5];
            double cx = t[b + 6], cy = t[b + 7], cz = t[b + 8];

            double abx = bx - ax, aby = by - ay, abz = bz - az;
            double acx = cx - ax, acy = cy - ay, acz = cz - az;
            double apx = px - ax, apy = py - ay, apz = pz - az;
            double d1 = abx * apx + aby * apy + abz * apz, d2 = acx * apx + acy * apy + acz * apz;
            if (d1 <= 0 && d2 <= 0) return Dist(px, py, pz, ax, ay, az);

            double bpx = px - bx, bpy = py - by, bpz = pz - bz;
            double d3 = abx * bpx + aby * bpy + abz * bpz, d4 = acx * bpx + acy * bpy + acz * bpz;
            if (d3 >= 0 && d4 <= d3) return Dist(px, py, pz, bx, by, bz);

            double vc = d1 * d4 - d3 * d2;
            if (vc <= 0 && d1 >= 0 && d3 <= 0)
            {
                double v = d1 / (d1 - d3);
                return Dist(px, py, pz, ax + v * abx, ay + v * aby, az + v * abz);
            }

            double cpx = px - cx, cpy = py - cy, cpz = pz - cz;
            double d5 = abx * cpx + aby * cpy + abz * cpz, d6 = acx * cpx + acy * cpy + acz * cpz;
            if (d6 >= 0 && d5 <= d6) return Dist(px, py, pz, cx, cy, cz);

            double vb = d5 * d2 - d1 * d6;
            if (vb <= 0 && d2 >= 0 && d6 <= 0)
            {
                double w = d2 / (d2 - d6);
                return Dist(px, py, pz, ax + w * acx, ay + w * acy, az + w * acz);
            }

            double va = d3 * d6 - d5 * d4;
            if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0)
            {
                double w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                return Dist(px, py, pz, bx + w * (cx - bx), by + w * (cy - by), bz + w * (cz - bz));
            }

            double denom = 1.0 / (va + vb + vc);
            double vv = vb * denom, ww = vc * denom;
            return Dist(px, py, pz, ax + abx * vv + acx * ww, ay + aby * vv + acy * ww, az + abz * vv + acz * ww);
        }

        private static long Cell(double v, double origin, double cell) { return (long)Math.Floor((v - origin) / cell); }

        private static long Key(long x, long y, long z)
        {
            unchecked { return (x * 73856093L) ^ (y * 19349663L) ^ (z * 83492791L); }
        }

        private static double Sq(double v) { return v * v; }

        private static double Dist(double ax, double ay, double az, double bx, double by, double bz)
        {
            return Math.Sqrt(Sq(ax - bx) + Sq(ay - by) + Sq(az - bz));
        }
    }
}
