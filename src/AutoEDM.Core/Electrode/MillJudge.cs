using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AutoEDM.Electrode
{
    /// <summary>Uma aresta do eletrodo medida para o juiz (mm, referencial da peça do eletrodo).</summary>
    public sealed class EdgeSample
    {
        public int Id;
        /// <summary>Índices das duas faces (na lista de <see cref="FaceRec"/>).</summary>
        public int FaceA, FaceB;
        /// <summary>Ponto do meio (no meio do parâmetro) e tangente unitária ali.</summary>
        public double[] Mid, Tangent;
        public double LengthMm;
        /// <summary>Fração de pontos a 0,02 mm do meio, no plano normal à aresta, que caem DENTRO do sólido (NaN: não lida).</summary>
        public double MaterialFraction = double.NaN;
    }

    public sealed class MillJudgeOptions
    {
        /// <summary>Menor raio côncavo que a fresa faz (escada do config; Carlos: R0,5).</summary>
        public double MinRadiusMm = 0.5;
        /// <summary>T8 (Carlos, 2026-11-17): o GAP do Ra que será aplicado no eletrodo. O raio côncavo do eletrodo final é
        /// r + GAP: passa se r + GAP ≥ <see cref="MinRadiusMm"/>. Canto vivo continua reprovado.</summary>
        public double GapMm;
        /// <summary>Alcance da menor fresa (parede mais alta que isto não sai).</summary>
        public double ReachMm = 10.0;
        /// <summary>Lasca: achado menor que isto não reprova, vai para a lista à parte.</summary>
        public double SliverMm = 0.15;
        /// <summary>Canal mais estreito que isto não sai (fresa Ø1).</summary>
        public double MinChannelMm = 1.0;
        /// <summary>Aresta côncava: mais que isto de material em volta do meio dela.</summary>
        public double ConcaveFraction = 0.55;
        /// <summary>Até isto de Z a aresta é "vertical"; a partir de <see cref="HorizontalTiltDeg"/>, "horizontal".</summary>
        public double VerticalTiltDeg = 20.0, HorizontalTiltDeg = 70.0;
    }

    public enum MillHitKind { SharpVertical, SharpInclined, HorizontalNotFloor, WallFloor, SmallRadius, Radius, Undercut, DivisionUndercut, NarrowChannel, BeyondReach, Unknown }

    /// <summary>Um veredito do juiz: uma aresta côncava, um raio, uma face em contra-saída, um canal ou uma parede alta.</summary>
    public sealed class MillHit
    {
        public MillHitKind Kind;
        public bool Pass;
        /// <summary>Reprovado, mas menor que <see cref="MillJudgeOptions.SliverMm"/>: lasca (não reprova).</summary>
        public bool Sliver;
        /// <summary>Faces (índices) — B = −1 quando o achado é uma face só.</summary>
        public int FaceA = -1, FaceB = -1;
        public int EdgeId;
        public double[] Mid, Tangent;
        public double LengthMm, TiltDeg = double.NaN, EmptyDeg = double.NaN;
        public string FacesText, Why;

        public bool IsEdge => Kind == MillHitKind.SharpVertical || Kind == MillHitKind.SharpInclined || Kind == MillHitKind.HorizontalNotFloor ||
                              Kind == MillHitKind.WallFloor || Kind == MillHitKind.Unknown;

        public string KindText
        {
            get
            {
                switch (Kind)
                {
                    case MillHitKind.SharpVertical: return "canto vivo vertical";
                    case MillHitKind.SharpInclined: return "canto vivo inclinado";
                    case MillHitKind.HorizontalNotFloor: return "aresta horizontal que não é parede×piso";
                    case MillHitKind.WallFloor: return "parede×piso";
                    case MillHitKind.SmallRadius: return "raio côncavo < fresa";
                    case MillHitKind.Radius: return "raio côncavo";
                    case MillHitKind.Undercut: return "face em contra-saída";
                    case MillHitKind.DivisionUndercut: return "face da divisão em contra-saída (saída da parede)";
                    case MillHitKind.NarrowChannel: return "canal estreito";
                    case MillHitKind.BeyondReach: return "parede além do alcance";
                    default: return "aresta côncava indeterminada";
                }
            }
        }

        public string Describe()
        {
            var inv = CultureInfo.InvariantCulture;
            string s = KindText;
            if (!double.IsNaN(EmptyDeg)) s += string.Format(inv, " (vão {0:0}°)", EmptyDeg);
            if (!double.IsNaN(TiltDeg)) s += string.Format(inv, ", {0:0.#}° de Z", TiltDeg);
            s += string.Format(inv, ", {0:0.###} mm em ({1:0.###}; {2:0.###}; {3:0.###})", LengthMm, Mid[0], Mid[1], Mid[2]);
            if (!string.IsNullOrEmpty(FacesText)) s += ": " + FacesText;
            return s;
        }
    }

    public sealed class MillJudgeResult
    {
        public readonly List<MillHit> Failed = new List<MillHit>();
        public readonly List<MillHit> Slivers = new List<MillHit>();
        public readonly List<MillHit> Passed = new List<MillHit>();
        public bool Millable => Failed.Count == 0;
        public int Corners => Failed.Count(h => h.IsEdge);
        public int Radii => Failed.Count(h => h.Kind == MillHitKind.SmallRadius);
        /// <summary>Reprovados fora a contra-saída das faces da divisão (o plano inclinado de uma parede com saída deixa a
        /// face do OUTRO lado com a saída ao contrário).</summary>
        public int FailedExceptDivision => Failed.Count(h => h.Kind != MillHitKind.DivisionUndercut);

        public string Summary()
        {
            string lasca = Slivers.Count > 0 ? $" ({Slivers.Count} lasca(s) < 0,15 mm à parte)" : "";
            if (Failed.Count == 0) return "fresável (juiz zerado)" + lasca;
            var parts = Failed.GroupBy(h => h.KindText).Select(g => g.Count() + " " + g.Key);
            return "NÃO fresável: " + string.Join(", ", parts) + lasca;
        }
    }

    /// <summary>
    /// T6 — o JUIZ ÚNICO de fresabilidade do eletrodo (o teste da INSPEÇÃO T5 levado ao Core). Lógica pura sobre as faces
    /// (<see cref="FaceRec"/>) e as arestas medidas (<see cref="EdgeSample"/>), com a fresa vindo da ponta (−Z local).
    /// Passa só: parede × piso com o piso voltado para a fresa; raio côncavo ≥ a menor fresa; canal ≥ Ø da fresa;
    /// parede dentro do alcance. Toda aresta côncava abaixo do fechamento conta (qualquer par de tipos de face, qualquer
    /// inclinação), inclusive cantos de alívio. Achado menor que <see cref="MillJudgeOptions.SliverMm"/> é "lasca".
    /// </summary>
    public static class MillJudge
    {
        public const int igPlane = -1909484335, igCyl = -114972029, igTorus = -114972025, igCone = -114972031, igSphere = -114972027;

        private static string TypeName(int t) =>
            t == igPlane ? "plano" : t == igCyl ? "cilindro" : t == igTorus ? "toro" : t == igCone ? "cone" : t == igSphere ? "esfera" : "outra";

        public static string FaceText(FaceRec f)
        {
            var inv = CultureInfo.InvariantCulture;
            string s = TypeName(f.Type);
            if (!double.IsNaN(f.R)) s += " R" + f.R.ToString("0.###", inv) + (f.Convex == true ? " convexo" : f.Convex == false ? " côncavo" : "");
            if (f.Normal != null) s += string.Format(inv, " n({0:0.###},{1:0.###},{2:0.###})", f.Normal[0], f.Normal[1], f.Normal[2]);
            return s;
        }

        private static bool ConcaveRadius(FaceRec f) => f.Convex == false && !double.IsNaN(f.R);

        /// <summary>A pegada (caixa em planta) das faces da ponta.</summary>
        public static BoundingBox TipFootprint(IList<FaceRec> faces, double refZ)
        {
            var tip = faces.Where(f => f.Min[2] < refZ - 1e-3).ToList();
            if (tip.Count == 0) return new BoundingBox();
            return new BoundingBox
            {
                MinX = tip.Min(f => f.Min[0]), MaxX = tip.Max(f => f.Max[0]), MinY = tip.Min(f => f.Min[1]), MaxY = tip.Max(f => f.Max[1]), MinZ = 0, MaxZ = refZ
            };
        }

        /// <param name="division">Planos da divisão na quina (mm, mesmo referencial): a face em contra-saída que fica num
        /// deles é a face da divisão (classificada à parte, mas reprova).</param>
        public static MillJudgeResult Judge(IList<FaceRec> faces, IList<EdgeSample> edges, double refZ, MillJudgeOptions o = null, IList<CutPlane> division = null)
        {
            o = o ?? new MillJudgeOptions();
            var res = new MillJudgeResult();
            BoundingBox foot = TipFootprint(faces, refZ);
            var tip = Enumerable.Range(0, faces.Count).Where(i => faces[i].Min[2] < refZ - 1e-3).ToList();

            void Add(MillHit h, double size)
            {
                if (h.Pass) res.Passed.Add(h);
                else if (size < o.SliverMm) { h.Sliver = true; res.Slivers.Add(h); }
                else res.Failed.Add(h);
            }

            // 1) arestas côncavas (material em volta do meio da aresta)
            foreach (EdgeSample e in edges ?? new List<EdgeSample>())
            {
                if (e.FaceA < 0 || e.FaceB < 0 || e.FaceA >= faces.Count || e.FaceB >= faces.Count || e.Mid == null || e.Tangent == null) continue;
                FaceRec A = faces[e.FaceA], B = faces[e.FaceB];
                if (ConcaveRadius(A) || ConcaveRadius(B)) continue;   // o raio côncavo é julgado pela face dele
                // (sem pular a lateral do prisma: a amostra de material já diz que a aresta dela é convexa, e uma parede de
                // verdade na borda da caixa da ponta não pode escapar)
                if (e.Mid[2] >= refZ - 1e-3) continue;
                var h = new MillHit
                {
                    FaceA = e.FaceA, FaceB = e.FaceB, EdgeId = e.Id, Mid = e.Mid, Tangent = e.Tangent, LengthMm = e.LengthMm,
                    FacesText = FaceText(A) + " × " + FaceText(B)
                };
                if (double.IsNaN(e.MaterialFraction))
                {
                    h.Kind = MillHitKind.Unknown; h.Why = "o lado do material não se leu: conta como não fresável";
                    Add(h, e.LengthMm);
                    continue;
                }
                if (e.MaterialFraction < o.ConcaveFraction) continue;   // convexa ou tangente
                h.TiltDeg = Math.Acos(Math.Min(1, Math.Abs(e.Tangent[2]))) * 180 / Math.PI;
                h.EmptyDeg = (1 - e.MaterialFraction) * 360;
                if (h.TiltDeg <= o.VerticalTiltDeg)
                {
                    h.Kind = MillHitKind.SharpVertical; h.Why = "canto vivo entre paredes: a fresa deixa raio de " + o.MinRadiusMm.ToString("0.0##", CultureInfo.InvariantCulture);
                }
                else if (h.TiltDeg < o.HorizontalTiltDeg)
                {
                    h.Kind = MillHitKind.SharpInclined; h.Why = "canto vivo inclinado: nem topo reto nem esférica deixam aresta viva";
                }
                else if (IsWallFloor(A, B))
                {
                    h.Kind = MillHitKind.WallFloor; h.Pass = true; h.Why = "parede×piso vivo com o piso voltado para a fresa: topo reto faz";
                }
                else
                {
                    h.Kind = MillHitKind.HorizontalNotFloor; h.Why = "aresta horizontal que não é parede×piso voltado para a fresa";
                }
                Add(h, e.LengthMm);
            }

            // 2) raios côncavos (o canto é a própria face)
            foreach (int i in tip.Where(i => ConcaveRadius(faces[i])))
            {
                FaceRec f = faces[i];
                double size = Math.Max(f.Max[0] - f.Min[0], Math.Max(f.Max[1] - f.Min[1], f.Max[2] - f.Min[2]));
                bool ok = f.R + o.GapMm >= o.MinRadiusMm - 1e-6;
                var inv = CultureInfo.InvariantCulture;
                string rg = o.GapMm > 0 ? string.Format(inv, "raio {0:0.###} + GAP {1:0.###} = {2:0.###}", f.R, o.GapMm, f.R + o.GapMm) : "raio " + f.R.ToString("0.###", inv);
                var h = new MillHit
                {
                    Kind = ok ? MillHitKind.Radius : MillHitKind.SmallRadius, Pass = ok, FaceA = i, LengthMm = size, Mid = Center(f), FacesText = FaceText(f),
                    Why = ok ? rg + " ≥ a menor fresa" : rg + " < " + o.MinRadiusMm.ToString("0.0##", inv) + ": nenhuma fresa faz"
                };
                Add(h, size);
            }

            // 3) faces em contra-saída (normal para a base: a fresa vindo da ponta não as enxerga)
            foreach (int i in tip.Where(i => faces[i].Normal != null && faces[i].Normal[2] > 0.02 && faces[i].AreaMm2 > 1e-3 && !RegionFinder.OnFootprint(faces[i], foot)))
            {
                FaceRec f = faces[i];
                double size = Math.Max(f.Max[0] - f.Min[0], Math.Max(f.Max[1] - f.Min[1], f.Max[2] - f.Min[2]));
                CutPlane on = division?.FirstOrDefault(p => OnPlane(f, p));
                Add(on == null
                    ? new MillHit { Kind = MillHitKind.Undercut, FaceA = i, LengthMm = size, Mid = Center(f), FacesText = FaceText(f), Why = "face voltada para a base" }
                    : new MillHit
                    {
                        Kind = MillHitKind.DivisionUndercut, FaceA = i, LengthMm = size, Mid = Center(f), FacesText = FaceText(f),
                        Why = string.Format(CultureInfo.InvariantCulture, "a divisão seguiu a parede com saída: deste lado a face fica {0:0.##}° em contra-saída", Math.Asin(Math.Min(1, f.Normal[2])) * 180 / Math.PI)
                    }, size);
            }

            // 4) canais estreitos e 5) paredes além do alcance
            var walls = tip.Where(i => faces[i].Normal != null && Math.Abs(faces[i].Normal[2]) < 0.2 && faces[i].AreaMm2 > 1e-3 && !RegionFinder.OnFootprint(faces[i], foot)).ToList();
            for (int a = 0; a < walls.Count; a++)
                for (int b = a + 1; b < walls.Count; b++)
                {
                    FaceRec p = faces[walls[a]], q = faces[walls[b]];
                    if (p.Normal[0] * q.Normal[0] + p.Normal[1] * q.Normal[1] > -0.98) continue;
                    double[] cp = Center(p), cq = Center(q);
                    double d1 = (cq[0] - cp[0]) * p.Normal[0] + (cq[1] - cp[1]) * p.Normal[1];
                    double d2 = (cp[0] - cq[0]) * q.Normal[0] + (cp[1] - cq[1]) * q.Normal[1];
                    if (d1 <= 1e-3 || d2 <= 1e-3 || d1 >= o.MinChannelMm) continue;
                    bool ox = Math.Min(p.Max[0], q.Max[0]) - Math.Max(p.Min[0], q.Min[0]) > -0.01, oy = Math.Min(p.Max[1], q.Max[1]) - Math.Max(p.Min[1], q.Min[1]) > -0.01;
                    bool oz = Math.Min(p.Max[2], q.Max[2]) - Math.Max(p.Min[2], q.Min[2]) > 0.05;
                    if (!(ox && oy && oz)) continue;
                    Add(new MillHit
                    {
                        Kind = MillHitKind.NarrowChannel, FaceA = walls[a], FaceB = walls[b], LengthMm = Math.Min(p.Max[2], q.Max[2]) - Math.Max(p.Min[2], q.Min[2]),
                        Mid = new[] { (cp[0] + cq[0]) / 2, (cp[1] + cq[1]) / 2, (cp[2] + cq[2]) / 2 }, FacesText = FaceText(p) + " × " + FaceText(q),
                        Why = string.Format(CultureInfo.InvariantCulture, "canal de {0:0.###} mm, mais estreito que a fresa", d1)
                    }, double.MaxValue);
                }
            foreach (int i in walls.Where(i => faces[i].Max[2] - faces[i].Min[2] > o.ReachMm))
            {
                FaceRec f = faces[i];
                Add(new MillHit
                {
                    Kind = MillHitKind.BeyondReach, FaceA = i, LengthMm = f.Max[2] - f.Min[2], Mid = Center(f), FacesText = FaceText(f),
                    Why = string.Format(CultureInfo.InvariantCulture, "alcance da menor fresa = {0:0.##} mm", o.ReachMm)
                }, double.MaxValue);
            }
            return res;
        }

        /// <summary>T9: "fresável (juiz zerado)" ou "precisa dividir: N achado(s)" — nunca "corte a fio" (decisão do Carlos).</summary>
        public static string Verdict(MillJudgeResult r)
        {
            if (r.Millable) return "fresável (juiz zerado)" + (r.Slivers.Count > 0 ? $", {r.Slivers.Count} lasca(s) < 0,15 mm" : "");
            return $"precisa dividir: {r.Failed.Count} achado(s) — " + string.Join(", ", r.Failed.GroupBy(h => h.KindText).Select(g => g.Count() + " " + g.Key));
        }

        /// <summary>
        /// T9: uma linha por achado reprovado, com a posição em mm na MONTAGEM _EDM (local + <paramref name="originMm"/>),
        /// o comprimento e o motivo; as lascas numa linha só.
        /// </summary>
        public static List<string> ReportLines(MillJudgeResult r, double[] originMm)
        {
            var inv = CultureInfo.InvariantCulture;
            double[] o = originMm ?? new double[3];
            var lines = new List<string>();
            foreach (MillHit h in r.Failed.OrderBy(x => x.Kind).ThenBy(x => x.Mid?[0] ?? 0).ThenBy(x => x.Mid?[1] ?? 0))
            {
                string kind = h.Kind == MillHitKind.SmallRadius && h.FacesText != null ? h.KindText + " (" + h.FacesText + ")" : h.KindText;
                string pos = h.Mid == null ? "?" : string.Format(inv, "x {0:0.###}, y {1:0.###}, z {2:0.###}", h.Mid[0] + o[0], h.Mid[1] + o[1], h.Mid[2] + o[2]);
                string extra = !double.IsNaN(h.EmptyDeg) ? string.Format(inv, ", vão {0:0}°, {1:0.#}° de Z", h.EmptyDeg, h.TiltDeg) : "";
                lines.Add(string.Format(inv, "{0} em ({1}) na montagem, {2:0.###} mm{3} — {4}", kind, pos, h.LengthMm, extra, h.Why));
            }
            if (r.Slivers.Count > 0)
                lines.Add($"{r.Slivers.Count} lasca(s) < 0,15 mm (não reprovam): " + string.Join(", ", r.Slivers.GroupBy(h => h.KindText).Select(g => g.Count() + " " + g.Key)));
            return lines;
        }

        /// <summary>Parede × piso: um plano de normal −Z (voltado para a fresa) e uma parede (plana quase vertical, cilindro ou cone).</summary>
        public static bool IsWallFloor(FaceRec a, FaceRec b)
        {
            bool Floor(FaceRec f) => f.Normal != null && f.Normal[2] < -0.9;
            bool Wall(FaceRec f) => (f.Normal != null && Math.Abs(f.Normal[2]) < 0.2) || (f.Normal == null && (f.Type == igCyl || f.Type == igCone));
            return (Floor(a) && Wall(b)) || (Floor(b) && Wall(a));
        }

        /// <summary>Face plana no plano <paramref name="p"/>: normal paralela e os cantos da caixa a menos de 0,02 mm dele.</summary>
        private static bool OnPlane(FaceRec f, CutPlane p)
        {
            if (f.Normal == null) return false;
            double dot = f.Normal[0] * p.Normal[0] + f.Normal[1] * p.Normal[1] + f.Normal[2] * p.Normal[2];
            if (Math.Abs(dot) < 0.9995) return false;
            // a caixa de uma face inclinada não fica no plano; vale o centro
            double[] c = Center(f);
            return Math.Abs(p.Distance(c[0], c[1], c[2])) < 0.05;
        }

        private static double[] Center(FaceRec f) => new[] { (f.Min[0] + f.Max[0]) / 2, (f.Min[1] + f.Max[1]) / 2, (f.Min[2] + f.Max[2]) / 2 };
    }
}
