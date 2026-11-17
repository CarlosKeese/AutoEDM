using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AutoEDM.Electrode
{
    /// <summary>Por que a face entra num eletrodo.</summary>
    public enum BurnKind
    {
        /// <summary>Estilo da casa "17 - EROS. CAVIDADES" (rosa): é para erodir.</summary>
        ErodedCavity,
        /// <summary>Cor de rugosidade de erosão ("EROS. 0,1" … "EROS. 6,3"): o Ra vem da cor.</summary>
        RaColor,
        /// <summary>Estilo "16" (roxo, cavidade USINADA) que a fresa não alcança: raio, diâmetro ou profundidade.</summary>
        MachinedNotMillable
    }

    /// <summary>Uma face de queima, já no referencial da MONTAGEM (mm).</summary>
    public sealed class SmartFace
    {
        public int FaceId;
        public BurnKind Kind;
        public double Ra;
        public double AreaMm2;
        public double[] MinMm, MaxMm;
        /// <summary>A face COM de origem. A lógica do plano não a toca.</summary>
        public object ComFace;
    }

    /// <summary>Faces de queima ligadas por aresta, numa mesma peça.</summary>
    public sealed class SmartRegion
    {
        public string Owner;
        /// <summary>A ocorrência dona (opaca para o plano).</summary>
        public object OwnerTag;
        public readonly List<SmartFace> Faces = new List<SmartFace>();

        public BoundingBox Box
        {
            get
            {
                var b = new BoundingBox
                {
                    MinX = double.MaxValue, MinY = double.MaxValue, MinZ = double.MaxValue,
                    MaxX = double.MinValue, MaxY = double.MinValue, MaxZ = double.MinValue
                };
                foreach (SmartFace f in Faces)
                {
                    b.MinX = Math.Min(b.MinX, f.MinMm[0]); b.MaxX = Math.Max(b.MaxX, f.MaxMm[0]);
                    b.MinY = Math.Min(b.MinY, f.MinMm[1]); b.MaxY = Math.Max(b.MaxY, f.MaxMm[1]);
                    b.MinZ = Math.Min(b.MinZ, f.MinMm[2]); b.MaxZ = Math.Max(b.MaxZ, f.MaxMm[2]);
                }
                return b;
            }
        }
    }

    /// <summary>Onde um eletrodo é usado: ponto de queima na montagem e giro em Z.</summary>
    public sealed class SmartPlacement
    {
        public double XMm, YMm, ZMm;
        /// <summary>0, 90, 180 ou 270: o MESMO eletrodo serve girado.</summary>
        public int RotZDeg;
        public string Owner;
    }

    /// <summary>Um eletrodo do plano: uma ou mais regiões, um blank, uma ou mais posições.</summary>
    public sealed class SmartElectrode
    {
        public int Index;
        public string Owner;
        public object OwnerTag;
        public readonly List<SmartRegion> Regions = new List<SmartRegion>();
        public readonly List<SmartPlacement> Placements = new List<SmartPlacement>();
        public readonly List<string> Notes = new List<string>();
        public BlankChoice Blank;

        public IEnumerable<SmartFace> Faces => Regions.SelectMany(r => r.Faces);
        public int FaceCount => Regions.Sum(r => r.Faces.Count);
        /// <summary>O acabamento mais fino pedido pelas faces manda.</summary>
        public double Ra => Faces.Min(f => f.Ra);

        /// <summary>
        /// Pedaço de um eletrodo dividido (T2): a pegada EXATA do prisma (mm, montagem, já com a margem nas bordas de
        /// fora; nas divisas não há margem). Null: eletrodo inteiro, pegada = caixa das faces + margem.
        /// </summary>
        public BoundingBox? Footprint;
        /// <summary>Fundo da queima do pedaço (mm, montagem). Só vale com <see cref="Footprint"/>.</summary>
        public double PieceDeepestZMm;
        /// <summary>Fundo do eletrodo INTEIRO de que o pedaço saiu (mm, montagem): é onde o prisma do pedaço começa.</summary>
        public double ParentDeepestZMm;
        /// <summary>Pedaço de qual eletrodo do plano ("E01"), e a divisão dele.</summary>
        public string PieceOf;
        /// <summary>T2c: o eletrodo INTEIRO de que o pedaço saiu (para desfazer a divisão que não resolve).</summary>
        public SmartElectrode Parent;
        /// <summary>T5: eletrodo de uma região destacada (null no principal e no inteiro).</summary>
        public EdmRegion Region;
        /// <summary>T5: o PRINCIPAL — pegadas das regiões destacadas a tirar dele (mm, referencial do eletrodo inteiro).</summary>
        public List<BoundingBox> CutOutLocal;
        public SplitPiece Piece;
        public SplitPlan Split;
        /// <summary>T6: pedaço da divisão NA QUINA — os semiespaços a tirar (mm, montagem; o lado da normal sai) e a profundidade.</summary>
        public List<CutPlane> Planes;
        public int CornerDepth;
        /// <summary>T7: enchimentos dos raios resolvidos pela divisão (caixas em mm, montagem).</summary>
        public List<TipCut> CornerFillers;
        /// <summary>T7: a parte do plano de onde este eletrodo saiu.</summary>
        public CornerCell Cell;
        /// <summary>O pedaço sai marcado "corte a fio": o motivo.</summary>
        public string WireCut;
        /// <summary>Raios que a fresa não faz, previstos pela peça (mm, montagem), dentro da pegada e abaixo do fechamento.</summary>
        public List<SplitFinding> PredictedFindings;

        public BoundingBox Box
        {
            get
            {
                BoundingBox b = Regions[0].Box;
                foreach (SmartRegion r in Regions.Skip(1)) b = SmartElectrodePlanner.Union(b, r.Box);
                if (Footprint.HasValue)
                {
                    // Pedaço: a caixa é a pegada do pedaço (sem a margem), do fundo do pedaço até o topo da queima.
                    BoundingBox f = Footprint.Value;
                    b = new BoundingBox { MinX = f.MinX, MaxX = f.MaxX, MinY = f.MinY, MaxY = f.MaxY, MinZ = PieceDeepestZMm, MaxZ = b.MaxZ };
                }
                return b;
            }
        }

        /// <summary>A pegada do prisma em planta (mm, montagem): a do pedaço, ou a caixa das faces + <paramref name="marginMm"/>.</summary>
        public BoundingBox PlanFootprint(double marginMm)
        {
            if (Footprint.HasValue) return Footprint.Value;
            BoundingBox b = Box;
            return new BoundingBox { MinX = b.MinX - marginMm, MaxX = b.MaxX + marginMm, MinY = b.MinY - marginMm, MaxY = b.MaxY + marginMm, MinZ = b.MinZ, MaxZ = b.MaxZ };
        }

        public double CenterXMm => (Box.MinX + Box.MaxX) / 2.0;
        public double CenterYMm => (Box.MinY + Box.MaxY) / 2.0;
        /// <summary>O ponto mais fundo da queima (a origem da peça do eletrodo fica aqui).</summary>
        public double DeepestZMm => Box.MinZ;

        public string Describe()
        {
            BoundingBox b = Box;
            var sb = new StringBuilder();
            if (Footprint.HasValue)
                sb.Append(string.Format(CultureInfo.InvariantCulture,
                    "E{0:00}  pedaço de {1}: pegada {2:0.0} × {3:0.0} mm (x {4:0.##}…{5:0.##}, y {6:0.##}…{7:0.##})  profundidade {8:0.0} mm  Ra {9:0.0}  {10} achado(s) na divisa  ",
                    Index, PieceOf, b.SizeX, b.SizeY, b.MinX, b.MaxX, b.MinY, b.MaxY, b.SizeZ, Ra, Piece?.OnBorder.Count ?? 0));
            else
                sb.Append(string.Format(CultureInfo.InvariantCulture,
                    "E{0:00}  {1} região(ões), {2} face(s)  pegada {3:0.0} × {4:0.0} mm  profundidade {5:0.0} mm  Ra {6:0.0}  ",
                    Index, Regions.Count, FaceCount, b.SizeX, b.SizeY, b.SizeZ, Ra));
            sb.Append(Blank != null ? Blank.Describe() : "SEM BARRA (comprar material)");
            if (Placements.Count > 1)
                sb.Append($"  — usado em {Placements.Count} posições ({string.Join(", ", Placements.Select(p => p.RotZDeg + "°"))})");
            if (WireCut != null) sb.Append("  — CORTE A FIO: " + WireCut);
            return sb.ToString();
        }
    }

    public sealed class SmartPlanOptions
    {
        /// <summary>Quanto a pegada do prisma passa da caixa das faces de queima, por lado.</summary>
        public double MarginMm = 1.0;
        /// <summary>Regiões da mesma peça a até esta distância (em planta) podem virar um eletrodo só.</summary>
        public double MergeGapMm = 5.0;
        public bool Merge = true;
        public string Material = "Cobre";
        public double BarMaxMm = 500.0;
        public double BandHeightMm = 5.0;
        public double BandMarginMm = 0.5;
        public double BlockOversizeMm = 0.5;
        /// <summary>Pescoço acima do plano de fechamento (a base fica esta distância acima da peça).</summary>
        public double NeckMm = 1.0;
        /// <summary>Menor bloco que ainda serve de base acima da faixa de medição.</summary>
        public double MinBlockMm = 3.0;
        /// <summary>Tolerância (mm) para dizer que duas geometrias são idênticas.</summary>
        public double SameShapeTolMm = 0.03;
    }

    /// <summary>
    /// O plano do "Criar Eletrodos": regiões → eletrodos. Lógica pura (sem COM), sobre caixas em mm
    /// no referencial da montagem. Regras do Carlos (2026-11-10):
    /// <list type="bullet">
    /// <item>duas ou mais regiões viram UM eletrodo quando a pegada unida cabe num perfil de cobre do catálogo;</item>
    /// <item>geometria idêntica em mais de um lugar = um eletrodo só, usado nas várias posições.</item>
    /// </list>
    /// </summary>
    public static class SmartElectrodePlanner
    {
        public static BoundingBox Union(BoundingBox a, BoundingBox b) => new BoundingBox
        {
            MinX = Math.Min(a.MinX, b.MinX), MinY = Math.Min(a.MinY, b.MinY), MinZ = Math.Min(a.MinZ, b.MinZ),
            MaxX = Math.Max(a.MaxX, b.MaxX), MaxY = Math.Max(a.MaxY, b.MaxY), MaxZ = Math.Max(a.MaxZ, b.MaxZ)
        };

        /// <summary>Folga em planta entre duas caixas (0 se uma invade ou toca a outra).</summary>
        public static double PlanGap(BoundingBox a, BoundingBox b)
        {
            double gx = Math.Max(0, Math.Max(a.MinX - b.MaxX, b.MinX - a.MaxX));
            double gy = Math.Max(0, Math.Max(a.MinY - b.MaxY, b.MinY - a.MaxY));
            return Math.Max(gx, gy);
        }

        /// <summary>
        /// A barra para uma pegada (caixa das faces): o bloco fica centrado na origem do eletrodo, que é o
        /// centro da pegada, e cresce a margem do prisma + o maior entre sobremetal e margem da faixa.
        /// </summary>
        public static BlankChoice BlankFor(BoundingBox box, IBlankLibrary blanks, SmartPlanOptions o)
        {
            var fp = new BoundingBox
            {
                MinX = box.MinX - o.MarginMm, MaxX = box.MaxX + o.MarginMm, MinY = box.MinY - o.MarginMm, MaxY = box.MaxY + o.MarginMm,
                MinZ = box.MinZ, MaxZ = box.MaxZ
            };
            return BlankForFootprint(fp, box.SizeZ, blanks, o);
        }

        /// <summary>
        /// A barra para uma pegada de PRISMA já pronta (com a margem; é o caso do pedaço de um eletrodo dividido) e a
        /// profundidade da queima nela: o bloco cresce só o maior entre sobremetal e margem da faixa.
        /// </summary>
        public static BlankChoice BlankForFootprint(BoundingBox footprint, double depthMm, IBlankLibrary blanks, SmartPlanOptions o)
        {
            double grow = Math.Max(o.BlockOversizeMm, o.BandMarginMm);
            double hx = footprint.SizeX / 2.0 + grow, hy = footprint.SizeY / 2.0 + grow;
            var need = new BoundingBox { MinX = -hx, MaxX = hx, MinY = -hy, MaxY = hy, MinZ = 0.0, MaxZ = depthMm };
            // Barra DEITADA: a altura do eletrodo inteiro sai da espessura da barra — ponta (profundidade da
            // queima) + pescoço + faixa + um bloco mínimo têm de caber nela. O catálogo só confere a pegada.
            double stack = depthMm + o.NeckMm + o.BandHeightMm + o.MinBlockMm;
            return blanks.BlankChoices(need, o.Material, o.BarMaxMm, o.BandHeightMm)
                         .FirstOrDefault(c => !c.LaidDown || (c.TotalHeightMm ?? 0.0) >= stack - 1e-9);
        }

        /// <summary>Altura da ponta: do fundo da queima até o topo do pescoço.</summary>
        public static double TipHeight(BoundingBox box, SmartPlanOptions o) => box.SizeZ + o.NeckMm;

        public static List<SmartElectrode> Plan(IEnumerable<SmartRegion> regions, IBlankLibrary blanks, SmartPlanOptions o = null)
        {
            o = o ?? new SmartPlanOptions();
            var list = new List<SmartElectrode>();
            foreach (SmartRegion r in regions.Where(r => r.Faces.Count > 0))
            {
                var e = new SmartElectrode { Owner = r.Owner, OwnerTag = r.OwnerTag };
                e.Regions.Add(r);
                list.Add(e);
            }

            if (o.Merge) MergeWhatFits(list, blanks, o);

            foreach (SmartElectrode e in list)
            {
                e.Blank = BlankFor(e.Box, blanks, o);
                if (e.Blank == null)
                    e.Notes.Add(string.Format(CultureInfo.InvariantCulture,
                        "pegada {0:0.0} × {1:0.0} mm não cabe em nenhuma barra de {2} — comprar material ou dividir à mão.",
                        e.Box.SizeX, e.Box.SizeY, o.Material));
                e.Placements.Add(new SmartPlacement { XMm = e.CenterXMm, YMm = e.CenterYMm, ZMm = e.DeepestZMm, RotZDeg = 0, Owner = e.Owner });
            }

            list = FoldIdentical(list, o);

            // Maiores primeiro; o índice é o da peça (…_EE01, _EE02).
            list = list.OrderByDescending(e => e.Box.SizeX * e.Box.SizeY).ThenBy(e => e.CenterYMm).ThenBy(e => e.CenterXMm).ToList();
            for (int i = 0; i < list.Count; i++) list[i].Index = i + 1;
            return list;
        }

        /// <summary>Une, par a par, os eletrodos da MESMA peça mais próximos enquanto a pegada unida couber numa barra.</summary>
        private static void MergeWhatFits(List<SmartElectrode> list, IBlankLibrary blanks, SmartPlanOptions o)
        {
            while (true)
            {
                int bi = -1, bj = -1;
                double bestGap = double.MaxValue, bestArea = double.MaxValue;
                for (int i = 0; i < list.Count; i++)
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        if (!ReferenceEquals(list[i].OwnerTag, list[j].OwnerTag) || list[i].Owner != list[j].Owner) continue;
                        BoundingBox a = list[i].Box, b = list[j].Box;
                        double gap = PlanGap(a, b);
                        if (gap > o.MergeGapMm) continue;
                        BoundingBox u = Union(a, b);
                        if (BlankFor(u, blanks, o) == null) continue;
                        double area = u.SizeX * u.SizeY;
                        if (gap < bestGap - 1e-9 || (Math.Abs(gap - bestGap) <= 1e-9 && area < bestArea))
                        { bestGap = gap; bestArea = area; bi = i; bj = j; }
                    }
                if (bi < 0) return;
                list[bi].Regions.AddRange(list[bj].Regions);
                list.RemoveAt(bj);
            }
        }

        /// <summary>Eletrodos de geometria idêntica (transladada e/ou girada em Z de 90 em 90°) viram um só, com várias posições.</summary>
        private static List<SmartElectrode> FoldIdentical(List<SmartElectrode> list, SmartPlanOptions o)
        {
            var kept = new List<SmartElectrode>();
            foreach (SmartElectrode e in list)
            {
                bool folded = false;
                foreach (SmartElectrode k in kept)
                {
                    int? rot = SameShapeRotation(k, e, o.SameShapeTolMm);
                    if (rot == null) continue;
                    k.Placements.Add(new SmartPlacement { XMm = e.CenterXMm, YMm = e.CenterYMm, ZMm = e.DeepestZMm, RotZDeg = rot.Value, Owner = e.Owner });
                    folded = true;
                    break;
                }
                if (!folded) kept.Add(e);
            }
            return kept;
        }

        private struct FaceShape { public double Area, Cx, Cy, Cz, Dx, Dy, Dz; }

        private static List<FaceShape> ShapeOf(SmartElectrode e)
        {
            double cx = e.CenterXMm, cy = e.CenterYMm, z0 = e.DeepestZMm;
            return e.Faces.Select(f => new FaceShape
            {
                Area = f.AreaMm2,
                Cx = (f.MinMm[0] + f.MaxMm[0]) / 2.0 - cx, Cy = (f.MinMm[1] + f.MaxMm[1]) / 2.0 - cy, Cz = (f.MinMm[2] + f.MaxMm[2]) / 2.0 - z0,
                Dx = f.MaxMm[0] - f.MinMm[0], Dy = f.MaxMm[1] - f.MinMm[1], Dz = f.MaxMm[2] - f.MinMm[2]
            }).ToList();
        }

        private static FaceShape Rotate(FaceShape s, int deg)
        {
            int c = deg == 0 ? 1 : deg == 180 ? -1 : 0;
            int n = deg == 90 ? 1 : deg == 270 ? -1 : 0;
            bool swap = deg == 90 || deg == 270;
            return new FaceShape
            {
                Area = s.Area, Cz = s.Cz, Dz = s.Dz,
                Cx = c * s.Cx - n * s.Cy, Cy = n * s.Cx + c * s.Cy,
                Dx = swap ? s.Dy : s.Dx, Dy = swap ? s.Dx : s.Dy
            };
        }

        /// <summary>
        /// Se <paramref name="b"/> é o eletrodo <paramref name="a"/> transladado e girado em Z, devolve o giro
        /// (0, 90, 180, 270) que leva A até B; senão null. Compara face a face: área, centro e medidas da caixa.
        /// Imagem ESPELHADA não conta — eletrodo espelhado é outra peça.
        /// </summary>
        public static int? SameShapeRotation(SmartElectrode a, SmartElectrode b, double tolMm = 0.03)
        {
            List<FaceShape> A = ShapeOf(a), B = ShapeOf(b);
            if (A.Count == 0 || A.Count != B.Count) return null;
            if (Math.Abs(a.Ra - b.Ra) > 1e-9) return null;

            foreach (int deg in new[] { 0, 180, 90, 270 })
            {
                var rotated = A.Select(s => Rotate(s, deg)).ToList();   // A girado tem de cair em cima de B
                var used = new bool[B.Count];
                bool all = true;
                foreach (FaceShape s in rotated)
                {
                    int hit = -1;
                    for (int j = 0; j < B.Count; j++)
                    {
                        if (used[j]) continue;
                        FaceShape t = B[j];
                        if (Math.Abs(s.Area - t.Area) > 0.02 + 1e-3 * s.Area) continue;
                        if (Math.Abs(s.Cx - t.Cx) > tolMm || Math.Abs(s.Cy - t.Cy) > tolMm || Math.Abs(s.Cz - t.Cz) > tolMm) continue;
                        if (Math.Abs(s.Dx - t.Dx) > tolMm || Math.Abs(s.Dy - t.Dy) > tolMm || Math.Abs(s.Dz - t.Dz) > tolMm) continue;
                        hit = j; break;
                    }
                    if (hit < 0) { all = false; break; }
                    used[hit] = true;
                }
                if (all) return deg;
            }
            return null;
        }
    }
}
