using System;
using System.Collections.Generic;

namespace AutoEDM.Reverse
{
    public static partial class SurfaceRecognizer
    {
        /// <summary>Superfície menor que isto, de qualquer tipo, entra no "resto" que o raio de
        /// concordância pode reclamar (retalhos de 0,01–0,7 mm² que o ajuste solto produziu).</summary>
        private const double FilletPoolAreaMm2 = 1.0;

        /// <summary>
        /// RAIO DE CONCORDÂNCIA por hipótese e verificação. Um raio liga duas superfícies JÁ
        /// reconhecidas, e a geometria dele sai delas:
        ///   - entre dois planos: CILINDRO de eixo n₁ × n₂;
        ///   - entre um plano e um cilindro de eixo perpendicular a ele: TORO no eixo do cilindro
        ///     (o raio da perna do corpo 3 contornando a ponta R1).
        /// Com o eixo conhecido falta só o círculo do perfil (Kåsa no plano perpendicular ao eixo,
        /// ou no meridiano), ajustado com os triângulos cuja normal está no plano do perfil, e
        /// aparado dos que não cabem. Só vale se o círculo for TANGENTE às duas superfícies.
        ///
        /// Existe porque o crescimento de cilindro não pega esse raio: a tesselação dele, no STL
        /// real, não é regrada (o R5 da perna saía em ~290 triângulos "livres" por lado, mais uma
        /// dúzia de "cilindros" de 0,1 mm² com eixos tortos), e o ajuste solto mistura o pedaço
        /// cilíndrico com o tórico.
        /// </summary>
        private static void RecognizeFillets(RecognitionResult result, int[] region, List<int>[] neighbours,
            double[] nx, double[] ny, double[] nz, double[] area, double[] p, RecognizerOptions o)
        {
            int ns = result.Surfaces.Count;
            var tris = new List<int>[ns];
            for (int i = 0; i < ns; i++) tris[i] = new List<int>();
            for (int t = 0; t < region.Length; t++) if (region[t] >= 0) tris[region[t]].Add(t);

            var pool = new bool[ns];
            for (int i = 0; i < ns; i++)
            {
                RecognizedSurface s = result.Surfaces[i];
                pool[i] = s.Kind == "livre" || s.AreaMm2 < FilletPoolAreaMm2;
            }

            var adj = new HashSet<int>[ns];
            for (int i = 0; i < ns; i++) adj[i] = new HashSet<int>();
            for (int t = 0; t < region.Length; t++)
            {
                if (region[t] < 0) continue;
                foreach (int nb in neighbours[t])
                    if (region[nb] >= 0 && region[nb] != region[t]) adj[region[t]].Add(region[nb]);
            }

            var assigned = new bool[region.Length];
            var seen = new bool[ns];
            bool any = false;

            for (int start = 0; start < ns; start++)
            {
                if (!pool[start] || seen[start]) continue;

                // Grupo conexo de superfícies do resto, e as superfícies "de verdade" em volta.
                var group = new List<int>();
                var queue = new Queue<int>();
                queue.Enqueue(start);
                seen[start] = true;
                var bases = new HashSet<int>();
                while (queue.Count > 0)
                {
                    int g = queue.Dequeue();
                    group.Add(g);
                    foreach (int q in adj[g])
                    {
                        if (pool[q]) { if (!seen[q]) { seen[q] = true; queue.Enqueue(q); } }
                        else bases.Add(q);
                    }
                }
                bool hasFree = false;
                foreach (int g in group) if (result.Surfaces[g].Kind == "livre") hasFree = true;
                if (!hasFree) continue;

                var groupTris = new List<int>();
                foreach (int g in group) groupTris.AddRange(tris[g]);

                foreach (int pi in bases)
                {
                    RecognizedSurface P = result.Surfaces[pi];
                    if (P.Kind != "plano" || P.Normal == null) continue;
                    foreach (int si in bases)
                    {
                        if (si == pi) continue;
                        RecognizedSurface S = result.Surfaces[si];
                        RecognizedSurface fillet = TryFillet(P, S, tris[si], groupTris, assigned, neighbours, nx, ny, nz, area, p, o, out List<int> inliers);
                        if (fillet == null) continue;

                        int id = result.Surfaces.Count;
                        result.Surfaces.Add(fillet);
                        foreach (int t in inliers) { region[t] = id; assigned[t] = true; }
                        any = true;
                    }
                }
            }
            // Retalho minúsculo de eixo/normal fora de X/Y/Z encostado em região livre é pedaço
            // dela (concordância de raio variável em volta da ponta R1 da perna do corpo 3 saía
            // em dezenas de "cilindros R2,76 de eixo (0,24; 0,94; −0,26)" de 0,1 mm²): volta
            // para a região livre, em vez de aparecer como raio na identificação.
            for (int i = 0; i < ns; i++)
            {
                RecognizedSurface s = result.Surfaces[i];
                if (!pool[i] || s.Kind == "livre" || IsAligned(s)) continue;
                int free = -1;
                foreach (int q in adj[i]) if (result.Surfaces[q].Kind == "livre") { free = q; break; }
                if (free < 0) continue;
                var left = tris[i].FindAll(t => !assigned[t]);
                if (left.Count == 0) continue;
                foreach (int t in left) { region[t] = free; assigned[t] = true; }
                tris[free].AddRange(left);
                any = true;
            }

            if (!any) return;

            // O que sobrou de cada superfície do resto continua dela; a que ficou vazia sai.
            for (int i = 0; i < ns; i++)
            {
                if (!pool[i]) continue;
                var left = tris[i].FindAll(t => !assigned[t] || region[t] == i);
                if (left.Count == tris[i].Count && result.Surfaces[i].Kind != "livre") continue;
                if (left.Count == 0) { result.Surfaces[i] = null; continue; }
                result.Surfaces[i] = result.Surfaces[i].Kind == "livre"
                    ? MakeFreeForm(left, area, p)
                    : (result.Surfaces[i].Kind == "plano" ? MakePlane(left, nx, ny, nz, area, p) : result.Surfaces[i]);
            }
            result.FreeFormAreaMm2 = 0;
            foreach (RecognizedSurface s in result.Surfaces)
                if (s != null && s.Kind == "livre") result.FreeFormAreaMm2 += s.AreaMm2;
        }

        /// <summary>
        /// Pedaço de cilindro COAXIAL a um cilindro maior vizinho (mesmo eixo, raio quase igual) é
        /// o mesmo cilindro com a tesselação mudando de jeito: o R1 da ponta da perna do corpo 3
        /// saía em "R0,905 / R0,94 / R0,95 / R0,955 / R0,965" no alto (Z 30–36), onde a
        /// tesselação gira e se mistura com o começo da concordância. Os triângulos que cabem no
        /// maior vão para ele; os que não cabem viram região livre (são da concordância).
        /// </summary>
        private static void MergeCoaxialFragments(RecognitionResult result, int[] region, List<int>[] neighbours,
            double[] area, double[] p, RecognizerOptions o)
        {
            int ns = result.Surfaces.Count;
            var tris = new List<int>[ns];
            for (int i = 0; i < ns; i++) tris[i] = new List<int>();
            for (int t = 0; t < region.Length; t++) if (region[t] >= 0) tris[region[t]].Add(t);
            var adj = new HashSet<int>[ns];
            for (int i = 0; i < ns; i++) adj[i] = new HashSet<int>();
            for (int t = 0; t < region.Length; t++)
            {
                if (region[t] < 0) continue;
                foreach (int nb in neighbours[t])
                    if (region[nb] >= 0 && region[nb] != region[t]) adj[region[t]].Add(region[nb]);
            }

            var pieces = new List<List<int>>();
            for (int i = 0; i < ns; i++)
            {
                RecognizedSurface a = result.Surfaces[i];
                if (a == null || a.Kind != "cilindro" || a.SweepDeg >= 360) continue;
                int host = -1;
                foreach (int q in adj[i])
                {
                    RecognizedSurface b = result.Surfaces[q];
                    if (b == null || b.Kind != "cilindro" || b.AreaMm2 < 3 * a.AreaMm2) continue;
                    if (Math.Abs(Dot3(a.AxisMm, b.AxisMm)) < Math.Cos(1.0 * Math.PI / 180)) continue;
                    if (Math.Abs(a.RadiusMm - b.RadiusMm) > 0.15 * Math.Max(1, b.RadiusMm)) continue;
                    if (RadialDistance(a.PointMm, b.PointMm, b.AxisMm) > 0.15) continue;
                    host = q;
                    break;
                }
                if (host < 0) continue;

                RecognizedSurface h = result.Surfaces[host];
                double tol = 2 * CylinderTolerance(h.RadiusMm, o);
                var leftovers = new List<int>();
                foreach (int t in tris[i])
                {
                    bool fits = true;
                    for (int k = 0; k < 3 && fits; k++)
                    {
                        double[] v = { p[t * 9 + k * 3], p[t * 9 + k * 3 + 1], p[t * 9 + k * 3 + 2] };
                        if (Math.Abs(RadialDistance(v, h.PointMm, h.AxisMm) - h.RadiusMm) > tol) fits = false;
                    }
                    if (fits)
                    {
                        region[t] = host;
                        h.AreaMm2 += area[t];
                        h.TriangleCount++;
                        for (int k = 0; k < 3; k++) Span(h.MinMm, h.MaxMm, p[t * 9 + k * 3], p[t * 9 + k * 3 + 1], p[t * 9 + k * 3 + 2]);
                    }
                    else leftovers.Add(t);
                }
                result.Surfaces[i] = null;
                if (leftovers.Count > 0) pieces.Add(leftovers);
            }

            // O que não coube vira região livre — UMA POR PEDAÇO. Uma só para o corpo inteiro
            // ligava os grupos das quatro pernas, e o raio R5 de uma parede virava um cilindro de
            // 120 mm atravessando de uma perna à outra.
            foreach (List<int> leftovers in pieces)
            {
                int id = result.Surfaces.Count;
                foreach (int t in leftovers) region[t] = id;
                result.Surfaces.Add(MakeFreeForm(leftovers, area, p));
            }
        }

        /// <summary>Uma hipótese: raio entre o plano P e a superfície S, com os triângulos ainda
        /// livres do grupo. Devolve a superfície (cilindro ou toro) e os triângulos dela, ou null.</summary>
        /// <summary>+1 se as facetas do cilindro apontam para fora do eixo (pino, ponta
        /// arredondada), −1 se para dentro (furo).</summary>
        private static double ConvexSign(RecognizedSurface S, List<int> sTris, double[] nx, double[] ny, double[] nz, double[] p)
        {
            double sum = 0;
            foreach (int t in sTris)
            {
                double[] rad = Radial(Centroid(p, t), S.PointMm, S.AxisMm);
                if (rad != null) sum += nx[t] * rad[0] + ny[t] * rad[1] + nz[t] * rad[2];
            }
            return sum >= 0 ? 1 : -1;
        }

        /// <summary>O maior pedaço conexo (por aresta) dos triângulos dados: um raio de
        /// concordância é UMA faixa contínua de malha, mesmo quando duas pernas têm paredes no
        /// mesmo plano.</summary>
        private static List<int> LargestConnected(List<int> tris, List<int>[] neighbours)
        {
            var set = new HashSet<int>(tris);
            var seen = new HashSet<int>();
            List<int> best = new List<int>();
            foreach (int start in tris)
            {
                if (seen.Contains(start)) continue;
                var comp = new List<int>();
                var q = new Queue<int>();
                q.Enqueue(start);
                seen.Add(start);
                while (q.Count > 0)
                {
                    int t = q.Dequeue();
                    comp.Add(t);
                    foreach (int nb in neighbours[t])
                        if (set.Contains(nb) && seen.Add(nb)) q.Enqueue(nb);
                }
                if (comp.Count > best.Count) best = comp;
            }
            return best;
        }

        private static RecognizedSurface TryFillet(RecognizedSurface P, RecognizedSurface S, List<int> sTris, List<int> groupTris,
            bool[] assigned, List<int>[] neighbours, double[] nx, double[] ny, double[] nz, double[] area, double[] p,
            RecognizerOptions o, out List<int> inliers)
        {
            inliers = null;
            double[] nP = P.Normal;
            double sinTol = Math.Sin(5.0 * Math.PI / 180.0);
            const double reach = 25.0; // raio de concordância maior que isto não é procurado

            bool toroidal;
            double[] axis, axisPoint = null;
            if (S.Kind == "plano" && S.Normal != null)
            {
                axis = Cross(nP, S.Normal);
                double l = Math.Sqrt(axis[0] * axis[0] + axis[1] * axis[1] + axis[2] * axis[2]);
                if (l < 0.1) return null; // paralelos: não há raio entre eles
                axis = new[] { axis[0] / l, axis[1] / l, axis[2] / l };
                toroidal = false;
            }
            else if (S.Kind == "cilindro" && S.AxisMm != null && S.PointMm != null &&
                     Math.Abs(Dot3(S.AxisMm, nP)) > Math.Cos(1.0 * Math.PI / 180.0))
            {
                axis = S.AxisMm;
                axisPoint = S.PointMm;
                toroidal = true;
            }
            else return null;

            // ---------------------------------------------- candidatos
            var cand = new List<int>();
            foreach (int t in groupTris)
            {
                if (assigned[t]) continue;
                double[] n = { nx[t], ny[t], nz[t] };
                double[] c = Centroid(p, t);
                if (!toroidal)
                {
                    if (Math.Abs(Dot3(n, axis)) > sinTol) continue;
                    if (Math.Abs(Dot3(Sub(c, S.PointMm), S.Normal)) > reach) continue;
                }
                else
                {
                    double[] rad = Radial(c, axisPoint, axis);
                    if (rad == null) continue;
                    double[] tangent = Cross(axis, rad);
                    if (Math.Abs(Dot3(n, tangent)) > sinTol) continue;
                    if (S.ArcFrom != null && !InArc(rad, S, 8.0)) continue;
                    double rho = RadialDistance(c, axisPoint, axis);
                    if (Math.Abs(rho - S.RadiusMm) > reach) continue;
                }
                if (Math.Abs(Dot3(Sub(c, P.PointMm), nP)) > reach) continue;
                cand.Add(t);
            }
            if (cand.Count < 6) return null;

            // ---------------------------------------------- perfil: só o raio é livre
            // O centro do raio fica a r de cada superfície, do lado para onde a normal (para
            // fora) aponta — raio côncavo, material no canto — ou do lado oposto — aresta viva
            // arredondada. Com o canto conhecido, o perfil inteiro depende só de r e desse
            // sinal: busca de CONSENSO em r (quantos vértices caem no círculo), robusta aos
            // triângulos estranhos que vêm junto no grupo.
            double[] e1 = Perpendicular(axis), e2 = Cross(axis, e1);
            var pts = new List<double[]>();
            foreach (int t in cand)
                for (int k = 0; k < 3; k++)
                    pts.Add(new[] { p[t * 9 + k * 3], p[t * 9 + k * 3 + 1], p[t * 9 + k * 3 + 2] });

            Func<double[], double[]> to2D;
            double[] corner2D, dir2D;
            if (!toroidal)
            {
                to2D = v => new[] { Dot3(v, e1), Dot3(v, e2) };
                // Canto = interseção das duas retas (traços dos planos no plano do perfil).
                double[] a1 = { Dot3(nP, e1), Dot3(nP, e2) }, a2 = { Dot3(S.Normal, e1), Dot3(S.Normal, e2) };
                double b1 = Dot3(nP, P.PointMm), b2 = Dot3(S.Normal, S.PointMm);
                double det = a1[0] * a2[1] - a1[1] * a2[0];
                if (Math.Abs(det) < 1e-9) return null;
                corner2D = new[] { (b1 * a2[1] - b2 * a1[1]) / det, (a1[0] * b2 - a2[0] * b1) / det };
                dir2D = new[] { a1[0] + a2[0], a1[1] + a2[1] };
            }
            else
            {
                to2D = v =>
                {
                    double[] d = Sub(v, axisPoint);
                    double h = Dot3(d, axis);
                    double[] r = { d[0] - h * axis[0], d[1] - h * axis[1], d[2] - h * axis[2] };
                    return new[] { Math.Sqrt(Dot3(r, r)), h };
                };
                // Meridiano (ρ, h): o "canto" é a geratriz do cilindro S na altura do plano P;
                // a normal de S é ±ρ (convexo se as facetas dele apontam para fora do eixo).
                double hP = Dot3(Sub(P.PointMm, axisPoint), axis);
                corner2D = new[] { S.RadiusMm, hP };
                dir2D = new[] { ConvexSign(S, sTris, nx, ny, nz, p), Dot3(nP, axis) };
            }

            var q2 = pts.ConvertAll(v => to2D(v));
            double tol = Math.Max(CylinderTolerance(5.0, o), 0.01);
            double bestR = 0, bestSign = 0;
            int bestCount = 0;
            foreach (double sign in new[] { 1.0, -1.0 })
                for (double r = 0.1; r <= reach; r += 0.01)
                {
                    int count = 0;
                    double cx0 = corner2D[0] + sign * r * dir2D[0], cy0 = corner2D[1] + sign * r * dir2D[1];
                    foreach (double[] q in q2)
                        if (Math.Abs(Math.Sqrt((q[0] - cx0) * (q[0] - cx0) + (q[1] - cy0) * (q[1] - cy0)) - r) <= tol) count++;
                    if (count > bestCount) { bestCount = count; bestR = r; bestSign = sign; }
                }
            if (bestCount < 9) return null;
            // Refino fino de r só com os pontos que concordam com o melhor (os vizinhos de fora
            // da tolerância puxavam o R5 para 5,01).
            var agree = new List<double[]>();
            {
                double cx0 = corner2D[0] + bestSign * bestR * dir2D[0], cy0 = corner2D[1] + bestSign * bestR * dir2D[1];
                foreach (double[] q in q2)
                    if (Math.Abs(Math.Sqrt((q[0] - cx0) * (q[0] - cx0) + (q[1] - cy0) * (q[1] - cy0)) - bestR) <= tol) agree.Add(q);
            }
            double rad2 = bestR, bestErr = double.MaxValue;
            for (double r = bestR - 0.02; r <= bestR + 0.02; r += 0.0001)
            {
                double cx0 = corner2D[0] + bestSign * r * dir2D[0], cy0 = corner2D[1] + bestSign * r * dir2D[1];
                double err = 0;
                foreach (double[] q in agree)
                {
                    double d = Math.Sqrt((q[0] - cx0) * (q[0] - cx0) + (q[1] - cy0) * (q[1] - cy0)) - r;
                    err += d * d;
                }
                if (err < bestErr) { bestErr = err; rad2 = r; }
            }
            double cu = corner2D[0] + bestSign * rad2 * dir2D[0], cv = corner2D[1] + bestSign * rad2 * dir2D[1];

            var keep = new List<int>();
            foreach (int t in cand)
            {
                bool ok = true;
                for (int k = 0; k < 3 && ok; k++)
                {
                    double[] q = to2D(new[] { p[t * 9 + k * 3], p[t * 9 + k * 3 + 1], p[t * 9 + k * 3 + 2] });
                    if (Math.Abs(Math.Sqrt((q[0] - cu) * (q[0] - cu) + (q[1] - cv) * (q[1] - cv)) - rad2) > tol) ok = false;
                }
                if (ok) keep.Add(t);
            }
            keep = LargestConnected(keep, neighbours);
            if (keep.Count < 4) return null;

            // O perfil aceito tem de COBRIR o arco do raio (90° entre o fundo e a parede): um
            // círculo errado só encosta perto de uma das tangências — em volta da ponta R1 da
            // perna do corpo 3, onde a concordância é de raio variável, um "toro r 3,79" casava
            // só com a base.
            var prof = new List<double>();
            foreach (int t in keep)
                for (int k = 0; k < 3; k++)
                {
                    double[] q = to2D(new[] { p[t * 9 + k * 3], p[t * 9 + k * 3 + 1], p[t * 9 + k * 3 + 2] });
                    prof.Add(Math.Atan2(q[1] - cv, q[0] - cu));
                }
            double expected = toroidal ? 90.0
                : Math.Acos(Math.Max(-1, Math.Min(1, Dot3(nP, S.Normal)))) * 180 / Math.PI;
            double covered = SweepDegrees(prof);
            if (covered < 0.6 * expected) return null;

            // Raio de concordância é cotado redondo (R5, R2,5, R0,5): a menos de 0,015 mm de um
            // múltiplo de 0,05, vira ele. Com a tangência imposta às duas faces a malha puxava o
            // R5 da perna para 5,005–5,01.
            double nice = Math.Round(rad2 / 0.05) * 0.05;
            if (Math.Abs(nice - rad2) <= 0.015 && nice > 0)
            {
                rad2 = nice;
                cu = corner2D[0] + bestSign * rad2 * dir2D[0];
                cv = corner2D[1] + bestSign * rad2 * dir2D[1];
            }

            inliers = keep;
            return BuildFillet(keep, toroidal, axis, axisPoint, e1, e2, cu, cv, rad2, to2D, area, p);
        }

        /// <summary>A superfície do raio montada direto dos parâmetros já determinados (eixo,
        /// centro e r): nenhum reajuste livre para deslocar o eixo que as duas superfícies
        /// vizinhas fixaram.</summary>
        private static RecognizedSurface BuildFillet(List<int> tris, bool toroidal, double[] axis, double[] axisPoint,
            double[] e1, double[] e2, double cu, double cv, double r, Func<double[], double[]> to2D, double[] area, double[] p)
        {
            double[] min = { double.MaxValue, double.MaxValue, double.MaxValue };
            double[] max = { double.MinValue, double.MinValue, double.MinValue };
            double areaSum = 0, sq = 0, wMin = double.MaxValue, wMax = double.MinValue;
            int n = 0;
            var around = new List<double>();
            var tube = new List<double>();
            double[] g1 = Perpendicular(axis), g2 = Cross(axis, g1);
            foreach (int t in tris)
            {
                areaSum += area[t];
                for (int k = 0; k < 3; k++)
                {
                    double[] v = { p[t * 9 + k * 3], p[t * 9 + k * 3 + 1], p[t * 9 + k * 3 + 2] };
                    Span(min, max, v[0], v[1], v[2]);
                    double[] q = to2D(v);
                    double d = Math.Sqrt((q[0] - cu) * (q[0] - cu) + (q[1] - cv) * (q[1] - cv)) - r;
                    sq += d * d; n++;
                    if (!toroidal)
                    {
                        around.Add(Math.Atan2(q[1] - cv, q[0] - cu));
                        double w = Dot3(v, axis);
                        if (w < wMin) wMin = w;
                        if (w > wMax) wMax = w;
                    }
                    else
                    {
                        double[] rad = Radial(v, axisPoint, axis);
                        if (rad != null) around.Add(Math.Atan2(Dot3(rad, g2), Dot3(rad, g1)));
                        tube.Add(Math.Atan2(q[1] - cv, q[0] - cu));
                    }
                }
            }

            var s = new RecognizedSurface
            {
                TriangleCount = tris.Count, AreaMm2 = areaSum, AxisMm = axis, RadiusMm = r,
                FitRmsMm = Math.Sqrt(sq / Math.Max(1, n)), MinMm = min, MaxMm = max
            };
            double sweep = SweepDegrees(around);
            s.SweepDeg = sweep;
            double from, to;
            ArcEnds(around, out from, out to);
            if (!toroidal)
            {
                s.Kind = "cilindro";
                double wMid = (wMin + wMax) / 2;
                s.PointMm = Add3(Add3(Scale3(e1, cu), Scale3(e2, cv)), Scale3(axis, wMid));
                s.LengthMm = wMax - wMin;
                if (sweep < 360.0)
                {
                    s.ArcFrom = Combine(e1, e2, Math.Cos(from), Math.Sin(from));
                    s.ArcTo = Combine(e1, e2, Math.Cos(to), Math.Sin(to));
                }
            }
            else
            {
                s.Kind = "toro";
                s.PointMm = Add3(axisPoint, Scale3(axis, cv));
                s.MajorRadiusMm = cu;
                if (sweep < 360.0)
                {
                    s.ArcFrom = Combine(g1, g2, Math.Cos(from), Math.Sin(from));
                    s.ArcTo = Combine(g1, g2, Math.Cos(to), Math.Sin(to));
                }
                double tf, tt;
                ArcEnds(tube, out tf, out tt);
                s.TubeFromDeg = tf * 180 / Math.PI;
                s.TubeToDeg = tt * 180 / Math.PI;
            }
            return s;
        }

        private static bool FitCircle(List<int> tris, double[] p, Func<double[], double[]> to2D,
            out double cu, out double cv, out double r)
        {
            cu = cv = r = 0;
            double su = 0, sv = 0, suu = 0, svv = 0, suv = 0, sr = 0, sru = 0, srv = 0;
            int n = 0;
            foreach (int t in tris)
                for (int k = 0; k < 3; k++)
                {
                    double[] q = to2D(new[] { p[t * 9 + k * 3], p[t * 9 + k * 3 + 1], p[t * 9 + k * 3 + 2] });
                    double u = q[0], v = q[1], rr = u * u + v * v;
                    su += u; sv += v; suu += u * u; svv += v * v; suv += u * v;
                    sr += rr; sru += rr * u; srv += rr * v;
                    n++;
                }
            double[] sol = Solve3(new[,] { { suu, suv, su }, { suv, svv, sv }, { su, sv, n } }, new[] { -sru, -srv, -sr });
            if (sol == null) return false;
            cu = -sol[0] / 2; cv = -sol[1] / 2;
            double rsq = cu * cu + cv * cv - sol[2];
            if (rsq <= 0) return false;
            r = Math.Sqrt(rsq);
            return true;
        }

        /// <summary>A direção radial do ponto está dentro do arco do cilindro S (com folga)?</summary>
        private static bool InArc(double[] radial, RecognizedSurface S, double marginDeg)
        {
            double[] g = Cross(S.AxisMm, S.ArcFrom);
            double a = Math.Atan2(Dot3(radial, g), Dot3(radial, S.ArcFrom)) * 180 / Math.PI;
            if (a < -marginDeg) a += 360;
            return a <= S.SweepDeg + marginDeg;
        }

        /// <summary>Plano de normal em X/Y/Z, ou cilindro/cone/toro de eixo em X/Y/Z.</summary>
        private static bool IsAligned(RecognizedSurface s)
        {
            double[] v = s.Kind == "plano" ? s.Normal : s.AxisMm;
            if (v == null) return false;
            int zeros = 0;
            foreach (double c in v) if (Math.Abs(c) < 1e-6) zeros++;
            return zeros >= 2;
        }

        private static double[] Radial(double[] c, double[] axisPoint, double[] axis)
        {
            double[] d = Sub(c, axisPoint);
            double h = Dot3(d, axis);
            double[] r = { d[0] - h * axis[0], d[1] - h * axis[1], d[2] - h * axis[2] };
            double l = Math.Sqrt(Dot3(r, r));
            return l < 1e-9 ? null : new[] { r[0] / l, r[1] / l, r[2] / l };
        }

        private static double RadialDistance(double[] c, double[] axisPoint, double[] axis)
        {
            double[] d = Sub(c, axisPoint);
            double h = Dot3(d, axis);
            double[] r = { d[0] - h * axis[0], d[1] - h * axis[1], d[2] - h * axis[2] };
            return Math.Sqrt(Dot3(r, r));
        }

        private static double[] Centroid(double[] p, int t)
        {
            int b = t * 9;
            return new[] { (p[b] + p[b + 3] + p[b + 6]) / 3, (p[b + 1] + p[b + 4] + p[b + 7]) / 3, (p[b + 2] + p[b + 5] + p[b + 8]) / 3 };
        }

        private static double Dot3(double[] a, double[] b) { return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]; }
        private static double[] Sub(double[] a, double[] b) { return new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] }; }
        private static double[] Add3(double[] a, double[] b) { return new[] { a[0] + b[0], a[1] + b[1], a[2] + b[2] }; }
        private static double[] Scale3(double[] a, double k) { return new[] { a[0] * k, a[1] * k, a[2] * k }; }
    }
}
