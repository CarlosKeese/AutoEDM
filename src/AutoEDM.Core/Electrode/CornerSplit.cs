using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AutoEDM.Electrode
{
    /// <summary>Um semiespaço a tirar do eletrodo (T6): o lado para onde <see cref="Normal"/> aponta sai (mm).</summary>
    public sealed class CutPlane
    {
        public double[] Point;
        /// <summary>Unitária, para o lado que SAI.</summary>
        public double[] Normal;
        public string What;
        /// <summary>Índice da face (parede) que deu o plano; −1 se não veio de uma face.</summary>
        public int Face = -1;

        public double Distance(double x, double y, double z) =>
            (x - Point[0]) * Normal[0] + (y - Point[1]) * Normal[1] + (z - Point[2]) * Normal[2];

        public CutPlane Shifted(double dx, double dy, double dz) =>
            new CutPlane { Point = new[] { Point[0] + dx, Point[1] + dy, Point[2] + dz }, Normal = (double[])Normal.Clone(), What = What, Face = Face };

        public CutPlane Flipped(string what) =>
            new CutPlane { Point = (double[])Point.Clone(), Normal = new[] { -Normal[0], -Normal[1], -Normal[2] }, What = what, Face = Face };

        public string Describe() => string.Format(CultureInfo.InvariantCulture,
            "plano por ({0:0.###}; {1:0.###}; {2:0.###}) n({3:0.####}, {4:0.####}, {5:0.####})", Point[0], Point[1], Point[2], Normal[0], Normal[1], Normal[2]);
    }

    /// <summary>A divisão NA QUINA escolhida: o plano (normal para um dos lados) e o que ele resolve.</summary>
    public sealed class CornerCut
    {
        /// <summary>Plano da parede, pelo meio da aresta reprovada (mm, referencial do eletrodo).</summary>
        public CutPlane Plane;
        public MillHit Hit;
        /// <summary>Achados reprovados (fora lascas) que ficam NO plano — viram face externa nas duas partes.</summary>
        public int Resolved;
        /// <summary>Achados reprovados que o plano atravessa (só são repartidos) e faces da ponta que ele corta.</summary>
        public int CrossedHits, CrossedFaces;
        public string Choice;
    }

    /// <summary>
    /// T6 — divisão NA QUINA (lógica pura). O corte nasce na aresta reprovada e segue o plano de uma das faces dela (a
    /// parede, com a saída que tiver) até sair do eletrodo: nas duas partes essa parede vira face EXTERNA livre. Entre os
    /// candidatos (o plano de cada parede plana de cada aresta reprovada; sem parede plana, o plano vertical que contém a
    /// aresta), vence o que resolve mais achados; no empate, o que reparte menos achados e corta menos faces.
    /// Um semiespaço não cria canto côncavo novo (material ∩ semiespaço só fecha ângulos); o juiz confere depois.
    /// </summary>
    public static class CornerSplitPlanner
    {
        public const double OnPlaneMm = 0.02, ParallelTol = 0.02;

        public static List<CutPlane> Candidates(IList<FaceRec> faces, MillHit h)
        {
            var list = new List<CutPlane>();
            if (!h.IsEdge || h.Mid == null) return list;
            foreach (int i in new[] { h.FaceA, h.FaceB })
            {
                if (i < 0 || i >= faces.Count) continue;
                FaceRec f = faces[i];
                if (!f.IsPlane || f.Normal == null || Math.Abs(f.Normal[2]) >= 0.2) continue;   // só parede (com saída), nunca piso
                list.Add(new CutPlane { Point = (double[])h.Mid.Clone(), Normal = (double[])f.Normal.Clone(), What = "parede " + MillJudge.FaceText(f), Face = i });
            }
            if (list.Count == 0 && h.Tangent != null && Math.Abs(h.Tangent[2]) < 0.94)
            {
                // sem parede plana (cilindro × cilindro, toro × parede inclinada…): o plano vertical que contém a aresta
                double[] n = { h.Tangent[1], -h.Tangent[0], 0.0 };
                double l = Math.Sqrt(n[0] * n[0] + n[1] * n[1]);
                if (l > 1e-6) list.Add(new CutPlane { Point = (double[])h.Mid.Clone(), Normal = new[] { n[0] / l, n[1] / l, 0.0 }, What = "plano vertical da aresta" });
            }
            return list;
        }

        /// <summary>O achado fica no plano: o meio a menos de <see cref="OnPlaneMm"/> e a tangente paralela a ele.</summary>
        public static bool OnPlane(CutPlane p, MillHit h)
        {
            if (h.Mid == null || h.Tangent == null) return false;
            if (Math.Abs(p.Distance(h.Mid[0], h.Mid[1], h.Mid[2])) > OnPlaneMm) return false;
            return Math.Abs(h.Tangent[0] * p.Normal[0] + h.Tangent[1] * p.Normal[1] + h.Tangent[2] * p.Normal[2]) < ParallelTol;
        }

        /// <summary>A caixa atravessa o plano (folga <paramref name="tol"/> dos dois lados).</summary>
        public static bool Crosses(CutPlane p, double[] mn, double[] mx, double tol = 0.05)
        {
            bool pos = false, neg = false;
            for (int i = 0; i < 8; i++)
            {
                double d = p.Distance((i & 1) == 0 ? mn[0] : mx[0], (i & 2) == 0 ? mn[1] : mx[1], (i & 4) == 0 ? mn[2] : mx[2]);
                if (d > tol) pos = true; else if (d < -tol) neg = true;
            }
            return pos && neg;
        }

        /// <summary>O melhor corte para os achados reprovados de <paramref name="r"/>; null se nenhum achado tem plano possível.</summary>
        public static CornerCut Choose(IList<FaceRec> faces, MillJudgeResult r, double refZ)
        {
            var edgesFailed = r.Failed.Where(h => h.IsEdge).ToList();
            var tip = faces.Where(f => f.Min[2] < refZ - 1e-3 && f.AreaMm2 > 1e-3).ToList();
            CornerCut best = null;
            var tried = new List<string>();
            foreach (MillHit h in edgesFailed)
                foreach (CutPlane p in Candidates(faces, h))
                {
                    int resolved = edgesFailed.Count(x => OnPlane(p, x));
                    int crossedHits = r.Failed.Count(x => x.Mid != null && !OnPlane(p, x) && x.FaceA >= 0 && x.FaceA < faces.Count &&
                                                          Crosses(p, faces[x.FaceA].Min, faces[x.FaceA].Max) && (x.FaceB < 0 || Crosses(p, faces[x.FaceB].Min, faces[x.FaceB].Max)));
                    int crossedFaces = tip.Count(f => Crosses(p, f.Min, f.Max));
                    var c = new CornerCut { Plane = p, Hit = h, Resolved = resolved, CrossedHits = crossedHits, CrossedFaces = crossedFaces };
                    if (best == null || Better(c, best)) best = c;
                    tried.Add(string.Format(CultureInfo.InvariantCulture, "{0}: resolve {1}, reparte {2}, corta {3} face(s)", p.What, resolved, crossedHits, crossedFaces));
                }
            if (best == null) return null;
            best.Choice = string.Format(CultureInfo.InvariantCulture,
                "na quina de {0} — {1} ({2}); resolve {3} achado(s), reparte {4}, corta {5} face(s) [{6} candidato(s)]",
                best.Hit.Describe(), best.Plane.Describe(), best.Plane.What, best.Resolved, best.CrossedHits, best.CrossedFaces, tried.Count);
            return best;
        }

        private static bool Better(CornerCut a, CornerCut b)
        {
            if (a.Resolved != b.Resolved) return a.Resolved > b.Resolved;
            if (a.CrossedHits != b.CrossedHits) return a.CrossedHits < b.CrossedHits;
            if (a.CrossedFaces != b.CrossedFaces) return a.CrossedFaces < b.CrossedFaces;
            return a.Hit.LengthMm > b.Hit.LengthMm;
        }

        /// <summary>
        /// A caixa em planta do que sobra do retângulo <paramref name="rect"/> × [<paramref name="zLo"/>, <paramref name="zHi"/>]
        /// do lado que FICA de todos os <paramref name="planes"/> (cortes nas duas alturas extremas, mais
        /// <paramref name="padMm"/>, sem passar do retângulo). Null se não sobra nada.
        /// </summary>
        public static BoundingBox? ClipFootprint(BoundingBox rect, IList<CutPlane> planes, double zLo, double zHi, double padMm = 0.05)
        {
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (double z in new[] { zLo, zHi })
            {
                var poly = new List<double[]> { new[] { rect.MinX, rect.MinY }, new[] { rect.MaxX, rect.MinY }, new[] { rect.MaxX, rect.MaxY }, new[] { rect.MinX, rect.MaxY } };
                foreach (CutPlane p in planes ?? new List<CutPlane>()) poly = ClipKeep(poly, p, z);
                foreach (double[] v in poly)
                {
                    minX = Math.Min(minX, v[0]); maxX = Math.Max(maxX, v[0]); minY = Math.Min(minY, v[1]); maxY = Math.Max(maxY, v[1]);
                }
            }
            if (minX > maxX || maxX - minX < 1e-3 || maxY - minY < 1e-3) return null;
            return new BoundingBox
            {
                MinX = Math.Max(rect.MinX, minX - padMm), MaxX = Math.Min(rect.MaxX, maxX + padMm),
                MinY = Math.Max(rect.MinY, minY - padMm), MaxY = Math.Min(rect.MaxY, maxY + padMm), MinZ = rect.MinZ, MaxZ = rect.MaxZ
            };
        }

        /// <summary>Sutherland–Hodgman: o polígono (na altura z) do lado em que a distância ao plano é ≤ 0.</summary>
        private static List<double[]> ClipKeep(List<double[]> poly, CutPlane p, double z)
        {
            var res = new List<double[]>();
            for (int i = 0; i < poly.Count; i++)
            {
                double[] a = poly[i], b = poly[(i + 1) % poly.Count];
                double da = p.Distance(a[0], a[1], z), db = p.Distance(b[0], b[1], z);
                if (da <= 0) res.Add(a);
                if ((da < 0 && db > 0) || (da > 0 && db < 0))
                {
                    double t = da / (da - db);
                    res.Add(new[] { a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1]) });
                }
            }
            return res;
        }
    }
}
