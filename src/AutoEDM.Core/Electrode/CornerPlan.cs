using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AutoEDM.Electrode
{
    /// <summary>Uma divisão do plano (T7): o plano da parede (normal = a normal para FORA da parede; "frente" = lado dela).</summary>
    public sealed class CornerNode
    {
        public int Id;
        public CutPlane Wall;
        /// <summary>Parede com saída: o plano tem inclinação (a frente recua num plano vertical).</summary>
        public bool Inclined;
        public string Choice;
    }

    /// <summary>Uma parte planejada (T7): o caminho na árvore, os planos de recorte, os enchimentos e o que o juiz ainda acusa.</summary>
    public sealed class CornerCell
    {
        public string Label;
        /// <summary>(nó, lado): true = frente da parede do nó.</summary>
        public List<(int node, bool front)> Path = new List<(int, bool)>();
        /// <summary>Semiespaços a tirar (mm, referencial do eletrodo inteiro; o lado da normal sai).</summary>
        public List<CutPlane> Planes = new List<CutPlane>();
        /// <summary>Enchimentos a tirar (caixa da face do raio, rente e com folga), mm, referencial do inteiro.</summary>
        public List<TipCut> Fillers = new List<TipCut>();
        /// <summary>Achados reprovados previstos (do juiz do inteiro) que ficam nesta parte.</summary>
        public List<MillHit> Failed = new List<MillHit>();
        public BoundingBox Footprint;
        public int Depth;
        public string MergeNote;
        public bool Millable => Failed.Count == 0;
    }

    public sealed class CornerPlanOptions
    {
        public int MaxDepth = 6;
        public int MaxCells = 16;
        /// <summary>Parte com a pegada menor que isto em alguma direção é reunida à vizinha (mm).</summary>
        public double MinWidthMm = 2.0;
        public double FillerClearanceMm = 0.001;
        /// <summary>T8: tirar da parte da frente a faixa sob a face da parede (evita o degrau da saída na quina). Medido no
        /// 15428.100 (T8, rodada 1): o recorte da faixa falhou várias vezes (E_INVALIDARG) e criou faces em contra-saída —
        /// DESLIGADO até a rota ser corrigida.</summary>
        public bool GrowthStrip;
    }

    /// <summary>
    /// T7 — o PLANO da divisão na quina, antes de construir (lógica pura sobre o juiz do eletrodo inteiro).
    /// <list type="number">
    /// <item>Divide recursivamente: canto vivo pelo plano da parede (como a T6); raio côncavo &lt; fresa pelo plano da parede
    /// PLANA TANGENTE a ele, e a parte da frente perde a caixa da face do raio (enchimento, rota C1 da T2c) — o aço fica com
    /// canto vivo ali (Carlos, 2026-11-12). Raio sem parede tangente plana fica para fio.</item>
    /// <item>A parte de TRÁS (a parede é face externa dela) leva o plano inclinado da parede; a da FRENTE leva um plano
    /// VERTICAL posto do lado dela, só encolhendo (sem contra-saída, nunca sobreposição).</item>
    /// <item>Reúne vizinhas cuja união passa no juiz e as partes estreitas (&lt; 2 mm) à vizinha de maior divisa.</item>
    /// </list>
    /// </summary>
    public static class CornerPlanner
    {
        private sealed class Ctx
        {
            public IList<FaceRec> Faces;
            public List<MillHit> Failed;
            public Dictionary<MillHit, List<CutPlane>> Tangent;
            /// <summary>Todas as faces planas vizinhas de cada raio (parede e piso): a outra tangente do enchimento.</summary>
            public Dictionary<MillHit, List<CutPlane>> Planar = new Dictionary<MillHit, List<CutPlane>>();
            public List<CornerNode> Nodes = new List<CornerNode>();
            public List<CornerCell> Leaves = new List<CornerCell>();
            public BoundingBox Rect;
            public double ZLo, ZHi;
            public CornerPlanOptions O;
            public List<string> Log;
            /// <summary>Partes que, construídas, saíram com ponta pequena demais: reunidas como as estreitas.</summary>
            public ISet<string> Small;
        }

        /// <summary>O plano da divisão. <paramref name="rect"/>: a pegada do prisma (mm, referencial do inteiro);
        /// <paramref name="zLo"/>…<paramref name="zHi"/>: fundo do prisma e fechamento.</summary>
        public static List<CornerCell> Plan(IList<FaceRec> faces, IList<EdgeSample> edges, MillJudgeResult whole, BoundingBox rect,
            double zLo, double zHi, CornerPlanOptions o, List<string> log, ISet<string> forceSmall = null)
        {
            var c = new Ctx { Faces = faces, Failed = whole.Failed.ToList(), Rect = rect, ZLo = zLo, ZHi = zHi, O = o ?? new CornerPlanOptions(), Log = log ?? new List<string>(), Small = forceSmall };
            c.Tangent = TangentWalls(faces, edges, c.Failed, c.Planar);
            foreach (MillHit h in c.Failed.Where(h => h.Kind == MillHitKind.SmallRadius && !c.Tangent.ContainsKey(h)))
                c.Log.Add("raio sem parede tangente plana (fica para fio, sem plano inventado): " + h.Describe());
            var root = new CornerCell { Label = "", Failed = c.Failed.ToList(), Footprint = rect };
            var queue = new Queue<CornerCell>();
            queue.Enqueue(root);
            // em largura: o limite de partes corta por igual
            while (queue.Count > 0)
            {
                CornerCell cell = queue.Dequeue();
                int pending = queue.Count + c.Leaves.Count;
                CornerNode node = cell.Depth < c.O.MaxDepth && pending + 2 <= c.O.MaxCells ? Choose(c, cell) : null;
                if (node == null)
                {
                    if (cell.Depth >= c.O.MaxDepth && cell.Failed.Any(h => Splittable(c, h))) c.Log.Add($"parte {Name(cell)}: parou no limite de profundidade ({c.O.MaxDepth}).");
                    c.Leaves.Add(cell);
                    continue;
                }
                foreach (bool front in new[] { false, true })
                {
                    CornerCell kid = Child(c, cell, node, front);
                    if (kid == null) { c.Log.Add($"parte {Name(cell)}{(front ? ".2" : ".1")}: vazia, descartada."); continue; }
                    queue.Enqueue(kid);
                }
            }
            int before = c.Leaves.Count;
            Merge(c);
            c.Log.Add($"partes: {before} antes de reunir, {c.Leaves.Count} depois.");
            return c.Leaves;
        }

        private static string Name(CornerCell cell) => cell.Label.Length == 0 ? "inteiro" : cell.Label.TrimStart('.');

        private static bool Splittable(Ctx c, MillHit h) =>
            (h.IsEdge && CornerSplitPlanner.Candidates(c.Faces, h).Count > 0) || c.Tangent.ContainsKey(h);

        /// <summary>Raio côncavo (cilindro) → os planos das paredes PLANAS vizinhas dele (pela aresta comum), normal para fora da parede.</summary>
        private static Dictionary<MillHit, List<CutPlane>> TangentWalls(IList<FaceRec> faces, IList<EdgeSample> edges, List<MillHit> failed,
            Dictionary<MillHit, List<CutPlane>> planar)
        {
            var map = new Dictionary<MillHit, List<CutPlane>>();
            var es = edges ?? new List<EdgeSample>();
            IEnumerable<(int face, double[] mid)> Nbrs(int f) => es.Where(e => e.Mid != null && (e.FaceA == f || e.FaceB == f))
                                                                    .Select(e => (e.FaceA == f ? e.FaceB : e.FaceA, e.Mid));
            foreach (MillHit h in failed.Where(x => x.Kind == MillHitKind.SmallRadius && x.FaceA >= 0 &&
                                                    (faces[x.FaceA].Type == MillJudge.igCyl || faces[x.FaceA].Type == MillJudge.igTorus)))
            {
                bool torus = faces[h.FaceA].Type == MillJudge.igTorus;
                var all = new List<CutPlane>();
                void Take(int other, double[] mid, string what)
                {
                    if (other < 0 || other >= faces.Count) return;
                    FaceRec w = faces[other];
                    if (!w.IsPlane || w.Normal == null) return;
                    if (all.Any(p => Same(p, mid, w.Normal))) return;
                    all.Add(new CutPlane { Point = (double[])mid.Clone(), Normal = (double[])w.Normal.Clone(), What = what + MillJudge.FaceText(w), Face = other });
                }
                foreach (var nb in Nbrs(h.FaceA))
                {
                    Take(nb.face, nb.mid, torus ? "parede vizinha do toro " : "parede tangente ao raio ");
                    // T8 (Carlos: "prefiro separar em mais eletrodos"): o toro de canto encosta em raios; as paredes deles também dividem
                    if (torus && nb.face >= 0 && nb.face < faces.Count && !faces[nb.face].IsPlane)
                        foreach (var nb2 in Nbrs(nb.face)) if (nb2.face != h.FaceA) Take(nb2.face, nb2.mid, "parede vizinha do toro (pelo raio) ");
                }
                // o toro não tem a "outra tangente" do prisma: o enchimento dele é a caixa da face
                planar[h] = torus ? new List<CutPlane>() : all;
                var walls = all.Where(p => Math.Abs(p.Normal[2]) < 0.2).ToList();   // só parede (com saída) divide; o piso não
                if (walls.Count > 0) map[h] = walls;
            }
            return map;
        }

        private static bool Same(CutPlane p, double[] point, double[] normal) =>
            Math.Abs(p.Normal[0] * normal[0] + p.Normal[1] * normal[1] + p.Normal[2] * normal[2]) > 0.9995 &&
            Math.Abs(p.Distance(point[0], point[1], point[2])) < CornerSplitPlanner.OnPlaneMm;

        /// <summary>O achado fica resolvido pelo plano: aresta no plano, ou raio cuja parede tangente é o plano.</summary>
        private static bool Resolves(Ctx c, CutPlane p, MillHit h)
        {
            if (h.IsEdge) return CornerSplitPlanner.OnPlane(p, h);
            return c.Tangent.TryGetValue(h, out List<CutPlane> t) && t.Any(q => Same(p, q.Point, q.Normal));
        }

        private static CornerNode Choose(Ctx c, CornerCell cell)
        {
            var cands = new List<CutPlane>();
            foreach (MillHit h in cell.Failed)
            {
                if (h.IsEdge) cands.AddRange(CornerSplitPlanner.Candidates(c.Faces, h));
                else if (c.Tangent.TryGetValue(h, out List<CutPlane> t)) cands.AddRange(t);
            }
            CutPlane best = null;
            int bestRes = 0, bestCross = int.MaxValue;
            foreach (CutPlane p in cands)
            {
                if (cell.Path.Any(x => Same(c.Nodes[x.node].Wall, p.Point, p.Normal))) continue;   // já é divisa desta parte
                int res = cell.Failed.Count(h => Resolves(c, p, h));
                if (res == 0) continue;
                int cross = cell.Failed.Count(h => !Resolves(c, p, h) && h.FaceA >= 0 && CornerSplitPlanner.Crosses(p, c.Faces[h.FaceA].Min, c.Faces[h.FaceA].Max));
                if (best == null || res > bestRes || (res == bestRes && cross < bestCross)) { best = p; bestRes = res; bestCross = cross; }
            }
            if (best == null) return null;
            // o MESMO plano (mesma orientação) usado em outro ramo é o mesmo nó: a reunião enxerga a divisa comum
            CornerNode same = c.Nodes.FirstOrDefault(n => n.Wall.Normal[0] * best.Normal[0] + n.Wall.Normal[1] * best.Normal[1] + n.Wall.Normal[2] * best.Normal[2] > 0.9995 &&
                                                          Math.Abs(n.Wall.Distance(best.Point[0], best.Point[1], best.Point[2])) < CornerSplitPlanner.OnPlaneMm);
            if (same != null)
            {
                c.Log.Add($"divisão {Name(cell)} — o mesmo plano do nó {same.Id} ({same.Wall.Describe()}): resolve {bestRes}");
                return same;
            }
            var node = new CornerNode
            {
                Id = c.Nodes.Count, Wall = best, Inclined = Math.Abs(best.Normal[2]) > 1e-4,
                Choice = string.Format(CultureInfo.InvariantCulture, "{0} — {1}: resolve {2}, reparte {3}", Name(cell), best.Describe(), bestRes, bestCross)
            };
            c.Nodes.Add(node);
            c.Log.Add("divisão " + node.Choice + " (" + best.What + ")");
            return node;
        }

        /// <summary>
        /// O plano a tirar para ficar de um lado. Atrás: o plano da parede (inclinado), tirando a frente. Frente (T8, Carlos
        /// 2026-11-17, "1 - a"): tirando o de trás, um plano VERTICAL pelo ponto da parede mais afastado para trás entre
        /// <c>ZLo</c> e <c>ZHi</c> — a parte CRESCE sobre a cunha (sobreposição ≤ tan(saída) × altura) e não deixa lâmina.
        /// A sobreposição fica dentro da ponta (prisma − aço): só cavidade.
        /// </summary>
        public static CutPlane SidePlane(CutPlane wall, bool front, double zLo, double zHi)
        {
            double[] n = wall.Normal;
            if (!front) return new CutPlane { Point = (double[])wall.Point.Clone(), Normal = (double[])n.Clone(), What = "atrás da parede (plano dela)", Face = wall.Face };
            double hl = Math.Sqrt(n[0] * n[0] + n[1] * n[1]);
            if (hl < 1e-9 || Math.Abs(n[2]) < 1e-9) return wall.Flipped("frente da parede (plano vertical)");
            double[] h = { n[0] / hl, n[1] / hl, 0 };
            double s = Math.Min(S(wall, zLo), S(wall, zHi));
            return new CutPlane
            {
                Point = new[] { wall.Point[0] + s * h[0], wall.Point[1] + s * h[1], wall.Point[2] },
                Normal = new[] { -h[0], -h[1], 0.0 }, Face = wall.Face,
                What = string.Format(CultureInfo.InvariantCulture, "frente da parede, crescida até a vertical (sobreposição até {0:0.###} mm)", Math.Abs(S(wall, zHi) - S(wall, zLo)))
            };
        }

        /// <summary>Posição (ao longo da normal horizontal da parede) do plano dela na altura <paramref name="z"/>.</summary>
        private static double S(CutPlane wall, double z)
        {
            double[] n = wall.Normal;
            double hl = Math.Sqrt(n[0] * n[0] + n[1] * n[1]);
            return -n[2] * (z - wall.Point[2]) / hl;
        }

        /// <summary>
        /// T8: a parte da frente cresce até a vertical, mas NÃO por baixo da face da parede (a lâmina atrás dela recriaria o
        /// canto vivo da quina, com o degrau da saída). Esta faixa — entre o plano inclinado e a vertical, na extensão da face
        /// (caixa projetada no comprimento da parede e da base dela até acima do fechamento) — sai da parte da frente.
        /// Null se a parede não tem saída ou a face não é conhecida.
        /// </summary>
        public static TipCut GrowthStrip(CutPlane wall, CutPlane vertical, FaceRec wf, double zLo, double zHi)
        {
            double[] n = wall.Normal;
            double hl = Math.Sqrt(n[0] * n[0] + n[1] * n[1]);
            if (wf == null || hl < 1e-9 || Math.Abs(n[2]) < 1e-9) return null;
            double[] h = { n[0] / hl, n[1] / hl, 0 }, t = { -h[1], h[0], 0 };
            double[] p0 = wall.Point;
            double t0 = double.MaxValue, t1 = double.MinValue;
            for (int i = 0; i < 8; i++)
            {
                double[] p = { (i & 1) == 0 ? wf.Min[0] : wf.Max[0], (i & 2) == 0 ? wf.Min[1] : wf.Max[1] };
                double s = (p[0] - p0[0]) * t[0] + (p[1] - p0[1]) * t[1];
                t0 = Math.Min(t0, s); t1 = Math.Max(t1, s);
            }
            double sv = (vertical.Point[0] - p0[0]) * h[0] + (vertical.Point[1] - p0[1]) * h[1];
            double za = Math.Max(wf.Min[2], zLo), zb = zHi + 1.0, eps = 0.001, tm = (t0 + t1) / 2;
            double[] P(double s, double z) => new[] { p0[0] + s * h[0] + tm * t[0], p0[1] + s * h[1] + tm * t[1], z };
            return new TipCut
            {
                What = string.Format(CultureInfo.InvariantCulture, "faixa sob a parede (sem crescer atrás dela), {0:0.##} mm ao longo dela", t1 - t0),
                IsStrip = true,
                Prism = new[] { P(S(wall, za) + eps, za), P(S(wall, zb) + eps, zb), P(sv - eps, zb), P(sv - eps, za) },
                Axis = t, LengthMm = t1 - t0 + 0.02
            };
        }

        /// <summary>Planos e recortes de um lado do nó: o plano do lado e, na frente de parede com saída, a faixa sob a face.</summary>
        private static void AddSide(Ctx c, CornerNode node, bool front, List<CutPlane> planes, List<TipCut> cuts)
        {
            CutPlane sp = SidePlane(node.Wall, front, c.ZLo, c.ZHi);
            planes.Add(sp);
            if (!front || !node.Inclined || !c.O.GrowthStrip) return;
            FaceRec wf = node.Wall.Face >= 0 && node.Wall.Face < c.Faces.Count ? c.Faces[node.Wall.Face] : null;
            TipCut strip = GrowthStrip(node.Wall, sp, wf, c.ZLo, c.ZHi);
            if (strip != null) cuts.Add(strip);
        }

        private static CornerCell Child(Ctx c, CornerCell parent, CornerNode node, bool front)
        {
            var kid = new CornerCell { Label = parent.Label + (front ? ".2" : ".1"), Depth = parent.Depth + 1 };
            kid.Path.AddRange(parent.Path);
            kid.Path.Add((node.Id, front));
            kid.Planes.AddRange(parent.Planes);
            kid.Fillers.AddRange(parent.Fillers);
            AddSide(c, node, front, kid.Planes, kid.Fillers);
            BoundingBox? fp = CornerSplitPlanner.ClipFootprint(c.Rect, kid.Planes, c.ZLo, c.ZHi);
            if (!fp.HasValue) return null;
            kid.Footprint = fp.Value;
            foreach (MillHit h in parent.Failed)
            {
                if (Resolves(c, node.Wall, h))
                {
                    // raio resolvido: a caixa da face sai da parte em que ele fica (a da frente da parede tangente)
                    if (!h.IsEdge && front == (Side(node.Wall, h) >= 0))
                        kid.Fillers.Add(FillerOf(c, h, node.Wall));
                    continue;
                }
                if ((Side(node.Wall, h) > 0) == front) kid.Failed.Add(h);
            }
            return kid;
        }

        private static double Side(CutPlane p, MillHit h) => h.Mid == null ? 0 : p.Distance(h.Mid[0], h.Mid[1], h.Mid[2]);

        /// <summary>
        /// O enchimento do raio <paramref name="h"/> resolvido pela parede <paramref name="wall"/>: a caixa da face (rota C1 da
        /// T2c) e, quando o raio tem a OUTRA tangente plana, o prisma triangular canto–tangentes ao longo do eixo (a caixa de um
        /// raio inclinado em planta tira material de fora do raio e cria cantos novos — medido na T7, 1ª rodada).
        /// </summary>
        private static TipCut FillerOf(Ctx c, MillHit h, CutPlane wall)
        {
            FaceRec f = c.Faces[h.FaceA];
            double k = c.O.FillerClearanceMm;
            var flush = new BoundingBox { MinX = f.Min[0], MaxX = f.Max[0], MinY = f.Min[1], MaxY = f.Max[1], MinZ = f.Min[2], MaxZ = f.Max[2] };
            var clear = new BoundingBox { MinX = f.Min[0] - k, MaxX = f.Max[0] + k, MinY = f.Min[1] - k, MaxY = f.Max[1] + k, MinZ = f.Min[2] - k, MaxZ = f.Max[2] + k };
            var cut = new TipCut { What = "enchimento do " + h.Describe(), Flush = flush, Clear = clear };
            CutPlane q = c.Planar.TryGetValue(h, out List<CutPlane> all) ? all.FirstOrDefault(p => !Same(p, wall.Point, wall.Normal)) : null;
            if (q != null && !double.IsNaN(f.R)) FillerPrism(cut, wall, q, f, h.Mid);
            return cut;
        }

        /// <summary>Prisma triangular: canto (interseção das duas tangentes) e as duas tangentes a 1,5 × o comprimento de tangência.</summary>
        public static void FillerPrism(TipCut cut, CutPlane w, CutPlane q, FaceRec f, double[] mid)
        {
            double[] nw = w.Normal, nq = q.Normal;
            double kk = Dot(nw, nq);
            if (Math.Abs(kk) > 0.98) return;   // tangentes paralelas: fica a caixa
            double[] t = Unit(Cross(nw, nq));
            double[] m = mid ?? new[] { (f.Min[0] + f.Max[0]) / 2, (f.Min[1] + f.Max[1]) / 2, (f.Min[2] + f.Max[2]) / 2 };
            double dW = w.Distance(m[0], m[1], m[2]), dQ = q.Distance(m[0], m[1], m[2]);
            double a = (-dW + kk * dQ) / (1 - kk * kk), b = (-dQ + kk * dW) / (1 - kk * kk);
            double[] corner = { m[0] + a * nw[0] + b * nq[0], m[1] + a * nw[1] + b * nq[1], m[2] + a * nw[2] + b * nq[2] };
            // na parede W, perpendicular ao eixo, para a frente de Q (onde o raio fica); idem em Q
            double[] uw = Unit(Cross(t, nw)); if (Dot(uw, nq) < 0) uw = Neg(uw);
            double[] uq = Unit(Cross(t, nq)); if (Dot(uq, nw) < 0) uq = Neg(uq);
            double empty = Math.PI - Math.Acos(Math.Max(-1, Math.Min(1, kk)));   // vão entre as paredes = π − ângulo entre as normais
            double tan = f.R / Math.Tan(empty / 2);
            double L = 1.5 * tan + 0.01;
            // comprimento ao longo do eixo: a caixa da face projetada no eixo
            double lo = double.MaxValue, hi = double.MinValue;
            for (int i = 0; i < 8; i++)
            {
                double[] p = { (i & 1) == 0 ? f.Min[0] : f.Max[0], (i & 2) == 0 ? f.Min[1] : f.Max[1], (i & 4) == 0 ? f.Min[2] : f.Max[2] };
                double s = Dot(new[] { p[0] - corner[0], p[1] - corner[1], p[2] - corner[2] }, t);
                lo = Math.Min(lo, s); hi = Math.Max(hi, s);
            }
            double sc = (lo + hi) / 2;
            double[] c0 = { corner[0] + sc * t[0], corner[1] + sc * t[1], corner[2] + sc * t[2] };
            cut.Prism = new[]
            {
                c0,
                new[] { c0[0] + L * uw[0], c0[1] + L * uw[1], c0[2] + L * uw[2] },
                new[] { c0[0] + L * uq[0], c0[1] + L * uq[1], c0[2] + L * uq[2] }
            };
            cut.Axis = t;
            cut.LengthMm = hi - lo + 0.02;
        }

        private static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        private static double[] Cross(double[] a, double[] b) => new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
        private static double[] Neg(double[] a) => new[] { -a[0], -a[1], -a[2] };
        private static double[] Unit(double[] a) { double l = Math.Sqrt(Dot(a, a)); return new[] { a[0] / l, a[1] / l, a[2] / l }; }

        // ---------------- reunião ----------------

        /// <summary>A região comum de duas partes (os lados que as duas têm iguais) — vale se nenhuma outra parte cai nela.</summary>
        private static CornerCell Union(Ctx c, CornerCell a, CornerCell b)
        {
            var common = a.Path.Where(x => b.Path.Contains(x)).ToList();
            if (common.Count == a.Path.Count || common.Count == b.Path.Count) return null;
            // vizinhas: divididas por um nó (lados opostos)
            if (!a.Path.Any(x => b.Path.Contains((x.node, !x.front)))) return null;
            foreach (CornerCell z in c.Leaves)
            {
                if (z == a || z == b) continue;
                bool excluded = common.Any(x => z.Path.Contains((x.node, !x.front)));
                if (!excluded) return null;
            }
            var u = new CornerCell { Label = a.Label + "+" + b.Label, Depth = Math.Min(a.Depth, b.Depth) };
            u.Path.AddRange(common);
            foreach (var x in common) AddSide(c, c.Nodes[x.node], x.front, u.Planes, u.Fillers);
            BoundingBox? fp = CornerSplitPlanner.ClipFootprint(c.Rect, u.Planes, c.ZLo, c.ZHi);
            if (!fp.HasValue) return null;
            u.Footprint = fp.Value;
            // achados da união: os do inteiro que caem nela e nenhum nó do caminho resolve; enchimentos dos nós do caminho
            foreach (MillHit h in c.Failed)
            {
                bool inside = true, resolved = false;
                CutPlane by = null;
                foreach (var x in common)
                {
                    CutPlane w = c.Nodes[x.node].Wall;
                    if (Resolves(c, w, h))
                    {
                        resolved = true;
                        by = w;
                        if (!h.IsEdge && x.front != (Side(w, h) >= 0)) { inside = false; break; }
                        continue;
                    }
                    if ((Side(w, h) > 0) != x.front) { inside = false; break; }
                }
                if (!inside) continue;
                if (!resolved) u.Failed.Add(h);
                else if (!h.IsEdge) u.Fillers.Add(FillerOf(c, h, by));
            }
            return u;
        }

        private static double SharedLength(CornerCell a, CornerCell b)
        {
            double ox = Math.Min(a.Footprint.MaxX, b.Footprint.MaxX) - Math.Max(a.Footprint.MinX, b.Footprint.MinX);
            double oy = Math.Min(a.Footprint.MaxY, b.Footprint.MaxY) - Math.Max(a.Footprint.MinY, b.Footprint.MinY);
            if (ox < -0.1 || oy < -0.1) return -1;
            return Math.Max(ox, oy);
        }

        private static bool Narrow(CornerCell a, double min) => a.Footprint.MaxX - a.Footprint.MinX < min || a.Footprint.MaxY - a.Footprint.MinY < min;

        private static void Merge(Ctx c)
        {
            bool changed = true;
            var stuck = new HashSet<CornerCell>();
            while (changed)
            {
                changed = false;
                // 1) parte estreita → vizinha de maior divisa (mesmo que a união não passe)
                CornerCell narrow = c.Leaves.FirstOrDefault(x => !stuck.Contains(x) && (Narrow(x, c.O.MinWidthMm) || (c.Small != null && c.Small.Contains(x.Label))));
                if (narrow != null)
                {
                    var opts = c.Leaves.Where(y => y != narrow).Select(y => (y, u: Union(c, narrow, y))).Where(t => t.u != null)
                                       .OrderByDescending(t => SharedLength(narrow, t.y)).ToList();
                    if (opts.Count > 0)
                    {
                        var t = opts[0];
                        t.u.MergeNote = $"parte estreita {Name(narrow)} reunida a {Name(t.y)}" + (t.u.Millable ? "" : " (a união NÃO passa no juiz)");
                        c.Log.Add(t.u.MergeNote);
                        Replace(c, narrow, t.y, t.u);
                        changed = true;
                        continue;
                    }
                    c.Log.Add($"parte estreita {Name(narrow)}: nenhuma vizinha reunível — fica.");
                    stuck.Add(narrow);
                    changed = true;
                    continue;
                }
                // 2) vizinhas cuja união passa no juiz
                for (int i = 0; i < c.Leaves.Count && !changed; i++)
                    for (int j = i + 1; j < c.Leaves.Count && !changed; j++)
                    {
                        CornerCell u = Union(c, c.Leaves[i], c.Leaves[j]);
                        if (u == null || !u.Millable) continue;
                        u.MergeNote = $"{Name(c.Leaves[i])} + {Name(c.Leaves[j])} reunidas (a união passa no juiz)";
                        c.Log.Add(u.MergeNote);
                        Replace(c, c.Leaves[i], c.Leaves[j], u);
                        changed = true;
                    }
            }
        }

        private static void Replace(Ctx c, CornerCell a, CornerCell b, CornerCell u)
        {
            int at = Math.Min(c.Leaves.IndexOf(a), c.Leaves.IndexOf(b));
            c.Leaves.Remove(a); c.Leaves.Remove(b);
            c.Leaves.Insert(at, u);
        }
    }
}
