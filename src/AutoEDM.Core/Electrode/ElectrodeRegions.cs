using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AutoEDM.Electrode
{
    /// <summary>Uma face do eletrodo lida para o agrupamento por região (mm, referencial da peça do eletrodo).</summary>
    public sealed class FaceRec
    {
        public int Id;
        /// <summary>GNTTypePropertyConstants (plano −1909484335, cilindro −114972029, toro −114972025…).</summary>
        public int Type;
        /// <summary>Raio (cilindro) ou raio menor (toro), mm; NaN nos outros.</summary>
        public double R = double.NaN;
        /// <summary>Convexa no ELETRODO (só cilindro/toro).</summary>
        public bool? Convex;
        /// <summary>Achado de raio: côncavo abaixo da menor fresa (R &lt; 0,5) ou fundo demais para ela.</summary>
        public bool Finding;
        public double[] Min, Max;
        public double AreaMm2;
        public int[] Edges = new int[0];
        /// <summary>Normal para FORA (só plano), unitária.</summary>
        public double[] Normal;

        public bool IsPlane => Type == RegionFinder.igPlane;
        public double PlanMin => Math.Min(Max[0] - Min[0], Max[1] - Min[1]);
        public double PlanMax => Math.Max(Max[0] - Min[0], Max[1] - Min[1]);
    }

    public sealed class RegionOptions
    {
        /// <summary>Vizinha de um achado entra na região se couber na caixa dos achados + isto (mm) e for pequena.</summary>
        public double ExtendMm = 3.0;
        /// <summary>Dois achados se ligam por uma vizinha comum que caiba na caixa deles + isto (mm).</summary>
        public double LinkMm = 1.0;
        /// <summary>Face grande (piso, parede longa) não liga nem entra: maior lado em planta e área máximos.</summary>
        public double MaxFaceLengthMm = 20.0;
        public double MaxFaceAreaMm2 = 100.0;
        /// <summary>Canto vivo: aresta a até isto de Z (graus), entre duas faces planas a mais de 1° uma da outra.</summary>
        public double MaxCornerTiltDeg = 20.0;
        public double CornerSideTolMm = 0.05;
        /// <summary>Pegada do eletrodo da região: caixa + margem, no mínimo esta largura, e encostada na borda quando sobraria menos que ela.</summary>
        public double MarginMm = 0.25;
        public double MinWidthMm = 2.0;
    }

    /// <summary>Uma região-problema: achados (raios e cantos vivos) e as faces da feição deles.</summary>
    public sealed class EdmRegion
    {
        public readonly List<int> Faces = new List<int>();
        public int Radii, Corners;
        /// <summary>Caixa das faces (mm, eletrodo).</summary>
        public BoundingBox Box;
        /// <summary>Pegada do eletrodo destacado (mm, eletrodo), do fundo ao fechamento.</summary>
        public BoundingBox Footprint;
        public string Name;

        public string Describe() => string.Format(CultureInfo.InvariantCulture,
            "{0}: {1} raio(s) + {2} canto(s) vivo(s), {3} face(s), caixa x {4:0.##}…{5:0.##} y {6:0.##}…{7:0.##} z {8:0.##}…{9:0.##}; pegada x {10:0.##}…{11:0.##} y {12:0.##}…{13:0.##}",
            Name, Radii, Corners, Faces.Count, Box.MinX, Box.MaxX, Box.MinY, Box.MaxY, Box.MinZ, Box.MaxZ, Footprint.MinX, Footprint.MaxX, Footprint.MinY, Footprint.MaxY);
    }

    /// <summary>
    /// T5 — separar por REGIÃO (regra do Carlos, 2026-11-16: "canto vivo ou raio menor que 0,5 → impossível fabricar sem
    /// fio"). Lógica PURA sobre as faces do eletrodo inteiro:
    /// <list type="number">
    /// <item>achados = faces de raio que a fresa não faz + cantos vivos CÔNCAVOS entre duas paredes (aresta ~vertical);</item>
    /// <item>achados se ligam se se tocam ou dividem uma vizinha pequena que caiba na caixa deles;</item>
    /// <item>a região leva as vizinhas pequenas dentro da caixa dos achados + <see cref="RegionOptions.ExtendMm"/>
    /// (a parede GRANDE de um canto vivo é barreira: é o corpo, não a feição);</item>
    /// <item>regiões que se sobrepõem em planta, ou cujas pegadas se sobrepõem, viram uma.</item>
    /// </list>
    /// </summary>
    public static class RegionFinder
    {
        public const int igPlane = -1909484335;

        /// <summary>Face plana da lateral do prisma (na borda da pegada inteira).</summary>
        public static bool OnFootprint(FaceRec f, BoundingBox foot, double tol = 0.01)
        {
            if (!f.IsPlane) return false;
            bool x = f.Max[0] - f.Min[0] < 1e-3 && (Math.Abs(f.Min[0] - foot.MinX) < tol || Math.Abs(f.Min[0] - foot.MaxX) < tol);
            bool y = f.Max[1] - f.Min[1] < 1e-3 && (Math.Abs(f.Min[1] - foot.MinY) < tol || Math.Abs(f.Min[1] - foot.MaxY) < tol);
            return x || y;
        }

        private static double[] Corner(FaceRec f, int i) => new[] { (i & 1) == 0 ? f.Min[0] : f.Max[0], (i & 2) == 0 ? f.Min[1] : f.Max[1], (i & 4) == 0 ? f.Min[2] : f.Max[2] };

        /// <summary>De que lado do plano (ponto <paramref name="m"/>, normal <paramref name="n"/>) fica a face: +1 fora, −1 dentro, 0 dos dois.</summary>
        private static int Side(FaceRec f, double[] n, double[] m, double tol)
        {
            bool pos = false, neg = false;
            for (int i = 0; i < 8; i++)
            {
                double[] p = Corner(f, i);
                double d = (p[0] - m[0]) * n[0] + (p[1] - m[1]) * n[1] + (p[2] - m[2]) * n[2];
                if (d > tol) pos = true; else if (d < -tol) neg = true;
            }
            return pos && !neg ? 1 : neg && !pos ? -1 : 0;
        }

        /// <summary>
        /// T6: os cantos reprovados pelo JUIZ ÚNICO (<see cref="MillJudge"/>) como pares (face menor, face maior). O antigo
        /// teste próprio (só plano × plano, só vertical, lado pela caixa) foi aposentado: deixava escapar cilindro × parede,
        /// inclinados e cantos de alívio (INSPEÇÃO T5, 2026-11-16).
        /// </summary>
        public static List<(int small, int big)> CornersOf(IList<FaceRec> f, MillJudgeResult judged)
        {
            var res = new List<(int, int)>();
            if (judged == null) return res;
            foreach (MillHit h in judged.Failed.Where(x => x.IsEdge && x.FaceA >= 0 && x.FaceB >= 0 && x.FaceA < f.Count && x.FaceB < f.Count))
                res.Add(f[h.FaceA].AreaMm2 <= f[h.FaceB].AreaMm2 ? (h.FaceA, h.FaceB) : (h.FaceB, h.FaceA));
            return res;
        }

        /// <summary>Face plana numa parede de uma das pegadas recortadas.</summary>
        public static bool OnCut(FaceRec f, IList<BoundingBox> cuts, double tol = 0.01)
        {
            if (!f.IsPlane) return false;
            foreach (BoundingBox c in cuts)
            {
                bool inY = f.Min[1] >= c.MinY - tol && f.Max[1] <= c.MaxY + tol, inX = f.Min[0] >= c.MinX - tol && f.Max[0] <= c.MaxX + tol;
                if (f.Max[0] - f.Min[0] < 1e-3 && inY && (Math.Abs(f.Min[0] - c.MinX) < tol || Math.Abs(f.Min[0] - c.MaxX) < tol)) return true;
                if (f.Max[1] - f.Min[1] < 1e-3 && inX && (Math.Abs(f.Min[1] - c.MinY) < tol || Math.Abs(f.Min[1] - c.MaxY) < tol)) return true;
            }
            return false;
        }

        private static bool Valid(FaceRec f, double refZ, BoundingBox foot) => f.Min[2] < refZ - 1e-3 && !OnFootprint(f, foot);

        private static Dictionary<int, List<int>> EdgeMap(IList<FaceRec> f)
        {
            var map = new Dictionary<int, List<int>>();
            for (int i = 0; i < f.Count; i++)
                foreach (int e in f[i].Edges ?? new int[0])
                {
                    if (!map.TryGetValue(e, out var l)) map[e] = l = new List<int>();
                    if (!l.Contains(i)) l.Add(i);
                }
            return map;
        }

        private static BoundingBox BoxOf(IList<FaceRec> f, IEnumerable<int> ids)
        {
            var b = new BoundingBox { MinX = double.MaxValue, MinY = double.MaxValue, MinZ = double.MaxValue, MaxX = double.MinValue, MaxY = double.MinValue, MaxZ = double.MinValue };
            foreach (int i in ids)
            {
                b.MinX = Math.Min(b.MinX, f[i].Min[0]); b.MinY = Math.Min(b.MinY, f[i].Min[1]); b.MinZ = Math.Min(b.MinZ, f[i].Min[2]);
                b.MaxX = Math.Max(b.MaxX, f[i].Max[0]); b.MaxY = Math.Max(b.MaxY, f[i].Max[1]); b.MaxZ = Math.Max(b.MaxZ, f[i].Max[2]);
            }
            return b;
        }

        private static bool Within(FaceRec f, BoundingBox b, double e) =>
            f.Min[0] >= b.MinX - e && f.Max[0] <= b.MaxX + e && f.Min[1] >= b.MinY - e && f.Max[1] <= b.MaxY + e;

        private static bool PlanOverlap(BoundingBox a, BoundingBox b) =>
            Math.Min(a.MaxX, b.MaxX) - Math.Max(a.MinX, b.MinX) > 0 && Math.Min(a.MaxY, b.MaxY) - Math.Max(a.MinY, b.MinY) > 0;

        /// <summary>As regiões-problema do eletrodo inteiro. <paramref name="foot"/>: a pegada inteira (mm, eletrodo).</summary>
        /// <param name="corners">Os cantos reprovados pelo juiz (<see cref="CornersOf"/>); null = só os raios.</param>
        public static List<EdmRegion> Find(IList<FaceRec> f, double refZ, BoundingBox foot, RegionOptions o = null, IList<(int small, int big)> corners = null)
        {
            o = o ?? new RegionOptions();
            Dictionary<int, List<int>> byEdge = EdgeMap(f);
            var valid = new HashSet<int>(Enumerable.Range(0, f.Count).Where(i => Valid(f[i], refZ, foot)));
            HashSet<int> Nbr(int i) => new HashSet<int>((f[i].Edges ?? new int[0]).Where(byEdge.ContainsKey).SelectMany(e => byEdge[e]).Where(j => j != i && valid.Contains(j)));
            bool Small(int j) => f[j].PlanMax <= o.MaxFaceLengthMm && f[j].AreaMm2 <= o.MaxFaceAreaMm2;

            var items = new List<(int face, bool corner)>();
            foreach (int i in valid.Where(i => f[i].Finding).OrderBy(i => i)) items.Add((i, false));
            var barrier = new HashSet<int>();
            foreach (var c in corners ?? new List<(int small, int big)>()) { if (items.All(t => t.face != c.small)) items.Add((c.small, true)); barrier.Add(c.big); }
            barrier.ExceptWith(items.Select(t => t.face));

            var nb = items.Select(t => Nbr(t.face)).ToList();
            int[] par = Enumerable.Range(0, items.Count).ToArray();
            int Find_(int x) { while (par[x] != x) x = par[x] = par[par[x]]; return x; }
            for (int p = 0; p < items.Count; p++)
                for (int q = p + 1; q < items.Count; q++)
                {
                    int a = items[p].face, b = items[q].face;
                    BoundingBox u = BoxOf(f, new[] { a, b });
                    bool link = a == b || nb[p].Contains(b) ||
                                nb[p].Intersect(nb[q]).Any(j => !barrier.Contains(j) && Small(j) && Within(f[j], u, o.LinkMm));
                    if (link) par[Find_(p)] = Find_(q);
                }

            var regions = new List<EdmRegion>();
            foreach (var g in Enumerable.Range(0, items.Count).GroupBy(Find_))
            {
                var core = new HashSet<int>(g.Select(k => items[k].face));
                BoundingBox cb = BoxOf(f, core);
                var reg = new HashSet<int>(core);
                foreach (int k in g)
                    foreach (int j in nb[k])
                        if (!barrier.Contains(j) && f[j].AreaMm2 <= o.MaxFaceAreaMm2 && Within(f[j], cb, o.ExtendMm)) reg.Add(j);
                var r = new EdmRegion { Radii = g.Count(k => !items[k].corner), Corners = g.Count(k => items[k].corner) };
                r.Faces.AddRange(reg.OrderBy(i => i));
                r.Box = BoxOf(f, reg);
                regions.Add(r);
            }

            // sobreposição em planta (caixa ou pegada) → uma região só
            for (bool changed = true; changed;)
            {
                changed = false;
                foreach (EdmRegion r in regions) r.Footprint = FootprintOf(r.Box, foot, o);
                for (int i = 0; i < regions.Count && !changed; i++)
                    for (int j = i + 1; j < regions.Count && !changed; j++)
                        if (PlanOverlap(regions[i].Box, regions[j].Box) || PlanOverlap(regions[i].Footprint, regions[j].Footprint))
                        {
                            EdmRegion a = regions[i], b = regions[j];
                            var all = new HashSet<int>(a.Faces.Concat(b.Faces));
                            a.Faces.Clear(); a.Faces.AddRange(all.OrderBy(x => x));
                            a.Radii += b.Radii; a.Corners += b.Corners; a.Box = BoxOf(f, all);
                            regions.RemoveAt(j); changed = true;
                        }
            }
            CloseThinGaps(regions, foot, o);
            regions = regions.OrderBy(r => r.Box.MinY).ThenBy(r => r.Box.MinX).ToList();
            for (int i = 0; i < regions.Count; i++) regions[i].Name = "R" + (i + 1);
            return regions;
        }

        /// <summary>
        /// O alívio não pode deixar parede fina no principal: entre duas pegadas que se encaram (sobrepostas na outra
        /// direção) a menos de <see cref="RegionOptions.MinWidthMm"/>, a pegada MENOR cresce até encostar na outra.
        /// </summary>
        public static void CloseThinGaps(List<EdmRegion> regions, BoundingBox whole, RegionOptions o = null)
        {
            o = o ?? new RegionOptions();
            for (int pass = 0; pass < 20; pass++)
            {
                bool changed = false;
                for (int i = 0; i < regions.Count; i++)
                    for (int j = 0; j < regions.Count; j++)
                    {
                        if (i == j) continue;
                        BoundingBox a = regions[i].Footprint, b = regions[j].Footprint;
                        bool smallerA = a.SizeX * a.SizeY <= b.SizeX * b.SizeY;
                        // b à direita de a (em X), sobrepostas em Y
                        double gx = b.MinX - a.MaxX;
                        if (gx > 1e-6 && gx < o.MinWidthMm && Math.Min(a.MaxY, b.MaxY) - Math.Max(a.MinY, b.MinY) > 1e-6)
                        {
                            if (smallerA) regions[i].Footprint.MaxX = b.MinX; else regions[j].Footprint.MinX = a.MaxX;
                            changed = true;
                        }
                        double gy = b.MinY - a.MaxY;
                        if (gy > 1e-6 && gy < o.MinWidthMm && Math.Min(a.MaxX, b.MaxX) - Math.Max(a.MinX, b.MinX) > 1e-6)
                        {
                            if (smallerA) regions[i].Footprint.MaxY = b.MinY; else regions[j].Footprint.MinY = a.MaxY;
                            changed = true;
                        }
                    }
                if (!changed) return;
            }
        }

        /// <summary>
        /// Pegada do eletrodo da região: caixa + margem, no mínimo <see cref="RegionOptions.MinWidthMm"/> de largura,
        /// encostada na borda da pegada inteira quando o que sobraria do principal entre elas ficasse mais fino que isso,
        /// e recortada à pegada inteira.
        /// </summary>
        public static BoundingBox FootprintOf(BoundingBox box, BoundingBox whole, RegionOptions o = null)
        {
            o = o ?? new RegionOptions();
            double x0 = box.MinX - o.MarginMm, x1 = box.MaxX + o.MarginMm, y0 = box.MinY - o.MarginMm, y1 = box.MaxY + o.MarginMm;
            if (x1 - x0 < o.MinWidthMm) { double c = (x0 + x1) / 2; x0 = c - o.MinWidthMm / 2; x1 = c + o.MinWidthMm / 2; }
            if (y1 - y0 < o.MinWidthMm) { double c = (y0 + y1) / 2; y0 = c - o.MinWidthMm / 2; y1 = c + o.MinWidthMm / 2; }
            if (x0 - whole.MinX < o.MinWidthMm) x0 = whole.MinX;
            if (whole.MaxX - x1 < o.MinWidthMm) x1 = whole.MaxX;
            if (y0 - whole.MinY < o.MinWidthMm) y0 = whole.MinY;
            if (whole.MaxY - y1 < o.MinWidthMm) y1 = whole.MaxY;
            return new BoundingBox { MinX = Math.Max(x0, whole.MinX), MaxX = Math.Min(x1, whole.MaxX), MinY = Math.Max(y0, whole.MinY), MaxY = Math.Min(y1, whole.MaxY), MinZ = whole.MinZ, MaxZ = whole.MaxZ };
        }

        /// <summary>
        /// Regra do planejador para geometria idêntica, aplicada a duas regiões: as faces (tipo, raio, área) e os centros
        /// delas relativos ao centro da região coincidem com giro de 0/90/180/270° ("idêntica, giro N°") ou só espelhadas
        /// em X ou em Y ("espelhada" — outro eletrodo); senão "diferente".
        /// </summary>
        public static string Compare(IList<FaceRec> f, EdmRegion a, EdmRegion b, double tol = 0.05)
        {
            var pa = Points(f, a); var pb = Points(f, b);
            if (pa.Count != pb.Count) return "diferente";
            for (int rot = 0; rot < 4; rot++)
                if (Same(pa, pb, p => Rot(p, rot), tol)) return $"idêntica, giro {rot * 90}°";
            if (Same(pa, pb, p => new[] { -p[0], p[1], p[2] }, tol) || Same(pa, pb, p => new[] { p[0], -p[1], p[2] }, tol)) return "espelhada";
            return "diferente";
        }

        private static double[] Rot(double[] p, int k) => k == 0 ? p : k == 1 ? new[] { -p[1], p[0], p[2] } : k == 2 ? new[] { -p[0], -p[1], p[2] } : new[] { p[1], -p[0], p[2] };

        private static List<(int type, double r, double area, double[] c)> Points(IList<FaceRec> f, EdmRegion g)
        {
            double cx = (g.Box.MinX + g.Box.MaxX) / 2, cy = (g.Box.MinY + g.Box.MaxY) / 2;
            return g.Faces.Select(i => (f[i].Type, double.IsNaN(f[i].R) ? 0 : f[i].R, f[i].AreaMm2,
                new[] { (f[i].Min[0] + f[i].Max[0]) / 2 - cx, (f[i].Min[1] + f[i].Max[1]) / 2 - cy, (f[i].Min[2] + f[i].Max[2]) / 2 })).ToList();
        }

        private static bool Same(List<(int type, double r, double area, double[] c)> a, List<(int type, double r, double area, double[] c)> b, Func<double[], double[]> t, double tol)
        {
            var used = new bool[b.Count];
            foreach (var p in a)
            {
                double[] q = t(p.c);
                int k = -1;
                for (int j = 0; j < b.Count && k < 0; j++)
                    if (!used[j] && b[j].type == p.type && Math.Abs(b[j].r - p.r) < 0.01 && Math.Abs(b[j].area - p.area) < Math.Max(0.05, 0.02 * p.area) &&
                        Math.Abs(b[j].c[0] - q[0]) < tol && Math.Abs(b[j].c[1] - q[1]) < tol && Math.Abs(b[j].c[2] - q[2]) < tol) k = j;
                if (k < 0) return false;
                used[k] = true;
            }
            return true;
        }
    }
}
