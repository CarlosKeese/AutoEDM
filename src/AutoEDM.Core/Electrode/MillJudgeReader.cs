using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutoEDM.Machinability;
using AutoEDM.Selection;

namespace AutoEDM.Electrode
{
    /// <summary>
    /// T6: o que o <see cref="MillJudge"/> precisa, lido do corpo do eletrodo. Só leitura. Cada aresta entre duas faces dá
    /// o meio e a tangente (<c>Edge.GetParamExtents</c> → <c>GetPointAtParam</c>/<c>GetTangent</c>, ou a corda) e a fração de
    /// 24 pontos a 0,02 mm em volta do meio que caem DENTRO do sólido (<c>Shell.IsPointInside</c>) — rota medida na
    /// INSPEÇÃO T5 (2026-11-16: 0 indeterminados nos 10 eletrodos do 15428.100).
    /// </summary>
    public static class MillJudgeReader
    {
        public sealed class Reading
        {
            public List<FaceRec> Faces;
            public List<EdgeSample> Edges;
            public MillJudgeResult Result;
        }

        /// <summary>Julga o eletrodo (todos os corpos da peça) abaixo de <paramref name="refZ"/> (mm, referencial da peça).</summary>
        public static MillJudgeResult Judge(object partDoc, double refZ, MillJudgeOptions o = null, IList<CutPlane> division = null) => Read(partDoc, refZ, o, division).Result;

        public static Reading Read(object partDoc, double refZ, MillJudgeOptions o = null, IList<CutPlane> division = null)
        {
            o = o ?? Options();
            var all = new Reading { Faces = new List<FaceRec>(), Edges = new List<EdgeSample>(), Result = new MillJudgeResult() };
            int models = 1;
            try { models = (int)((dynamic)partDoc).Models.Count; } catch { }
            for (int m = 1; m <= models; m++)
            {
                object body = (object)((dynamic)partDoc).Models.Item(m).Body;
                List<FaceRec> faces = RegionFaceReader.ReadBody(body);
                List<EdgeSample> edges = Sample(body, faces, refZ);
                MillJudgeResult r = MillJudge.Judge(faces, edges, refZ, o, division);
                int offset = all.Faces.Count;
                all.Faces.AddRange(faces);
                all.Edges.AddRange(edges);
                foreach (MillHit h in r.Failed.Concat(r.Slivers).Concat(r.Passed))
                {
                    if (h.FaceA >= 0) h.FaceA += offset;
                    if (h.FaceB >= 0) h.FaceB += offset;
                }
                foreach (EdgeSample e in edges) { e.FaceA += offset; e.FaceB += offset; }
                all.Result.Failed.AddRange(r.Failed);
                all.Result.Slivers.AddRange(r.Slivers);
                all.Result.Passed.AddRange(r.Passed);
            }
            return all;
        }

        /// <summary>A escada do config: menor raio e o alcance dele.</summary>
        public static MillJudgeOptions Options(double gapMm = 0)
        {
            ToolLadder ladder = ToolLadder.Shop();
            double r = ladder.SmallestRadiusMm;
            return new MillJudgeOptions { MinRadiusMm = r, ReachMm = ladder.ReachMm(r), GapMm = gapMm };
        }

        private static List<EdgeSample> Sample(object body, List<FaceRec> faces, double refZ)
        {
            var list = new List<EdgeSample>();
            var byEdge = new Dictionary<int, List<int>>();
            for (int i = 0; i < faces.Count; i++)
                foreach (int e in faces[i].Edges)
                {
                    if (!byEdge.TryGetValue(e, out List<int> l)) byEdge[e] = l = new List<int>();
                    if (!l.Contains(i)) l.Add(i);
                }
            object shell = (object)((dynamic)body).Shells.Item(1);
            dynamic edges = ((dynamic)body).Edges(1);   // igQueryAll
            int n = (int)edges.Count;
            for (int i = 1; i <= n; i++)
            {
                object edge = edges.Item(i);
                int id = 0; try { id = (int)((dynamic)edge).ID; } catch { }
                if (id == 0 || !byEdge.TryGetValue(id, out List<int> fs) || fs.Count != 2) continue;
                if (!MidAndTangent(edge, out double[] mid, out double[] t, out double[] a, out double[] b, out double len)) continue;
                if (Math.Min(a[2], b[2]) >= refZ - 1e-3 || mid[2] >= refZ - 1e-3) continue;
                list.Add(new EdgeSample { Id = id, FaceA = fs[0], FaceB = fs[1], Mid = mid, Tangent = t, LengthMm = len, MaterialFraction = MaterialFraction(shell, mid, t) });
            }
            return list;
        }

        private static bool MidAndTangent(object edge, out double[] m, out double[] t, out double[] a, out double[] b, out double len)
        {
            m = null; t = null; len = 0;
            if (!EdgeGeometry.TryGetEndPointsMm(edge, out a, out b, out string _)) return false;
            try
            {
                object[] ext = { 0.0, 0.0 };
                var mod = new ParameterModifier(2); mod[0] = true; mod[1] = true;
                edge.GetType().InvokeMember("GetParamExtents", BindingFlags.InvokeMethod, null, edge, ext, new[] { mod }, null, null);
                double u = (Convert.ToDouble(ext[0]) + Convert.ToDouble(ext[1])) / 2;
                var m3 = new ParameterModifier(3); m3[1] = true; m3[2] = true;
                object[] pa = { 1, new[] { u }, new double[3] };
                edge.GetType().InvokeMember("GetPointAtParam", BindingFlags.InvokeMethod, null, edge, pa, new[] { m3 }, null, null);
                object[] ta = { 1, new[] { u }, new double[3] };
                edge.GetType().InvokeMember("GetTangent", BindingFlags.InvokeMethod, null, edge, ta, new[] { m3 }, null, null);
                if (pa[2] is double[] p && ta[2] is double[] tv && p.Length >= 3 && tv.Length >= 3)
                {
                    double l = Math.Sqrt(tv[0] * tv[0] + tv[1] * tv[1] + tv[2] * tv[2]);
                    if (l > 1e-12) { m = new[] { p[0] * 1000, p[1] * 1000, p[2] * 1000 }; t = new[] { tv[0] / l, tv[1] / l, tv[2] / l }; }
                }
            }
            catch { }
            double[] d = { b[0] - a[0], b[1] - a[1], b[2] - a[2] };
            double chord = Math.Sqrt(d[0] * d[0] + d[1] * d[1] + d[2] * d[2]);
            if (m == null)
            {
                if (chord < 1e-9) return false;
                m = new[] { (a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2 };
                t = new[] { d[0] / chord, d[1] / chord, d[2] / chord };
            }
            len = chord;
            return true;
        }

        /// <summary>Fração de 24 pontos a 0,02 mm do meio (no plano normal à aresta) que caem dentro do sólido; NaN se metade não se leu.</summary>
        private static double MaterialFraction(object shell, double[] m, double[] t)
        {
            double[] u = Math.Abs(t[2]) < 0.9 ? new[] { 0.0, 0.0, 1.0 } : new[] { 1.0, 0.0, 0.0 };
            double[] e1 = { t[1] * u[2] - t[2] * u[1], t[2] * u[0] - t[0] * u[2], t[0] * u[1] - t[1] * u[0] };
            double l1 = Math.Sqrt(e1[0] * e1[0] + e1[1] * e1[1] + e1[2] * e1[2]);
            e1 = new[] { e1[0] / l1, e1[1] / l1, e1[2] / l1 };
            double[] e2 = { t[1] * e1[2] - t[2] * e1[1], t[2] * e1[0] - t[0] * e1[2], t[0] * e1[1] - t[1] * e1[0] };
            const int n = 24;
            int inside = 0, ok = 0;
            var mod = new ParameterModifier(1); mod[0] = true;
            for (int k = 0; k < n; k++)
            {
                double ang = (k + 0.5) * 2 * Math.PI / n, r = 0.02;
                double[] p =
                {
                    (m[0] + r * (Math.Cos(ang) * e1[0] + Math.Sin(ang) * e2[0])) / 1000,
                    (m[1] + r * (Math.Cos(ang) * e1[1] + Math.Sin(ang) * e2[1])) / 1000,
                    (m[2] + r * (Math.Cos(ang) * e1[2] + Math.Sin(ang) * e2[2])) / 1000
                };
                try
                {
                    object v = shell.GetType().InvokeMember("IsPointInside", BindingFlags.GetProperty, null, shell, new object[] { p }, new[] { mod }, null, null);
                    ok++;
                    if (Convert.ToBoolean(v)) inside++;
                }
                catch { }
            }
            return ok < n / 2 ? double.NaN : (double)inside / ok;
        }
    }
}
