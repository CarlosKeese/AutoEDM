using System;
using System.Collections.Generic;
using System.Linq;
using AutoEDM.Machinability;
using AutoEDM.Selection;

namespace AutoEDM.Electrode
{
    /// <summary>
    /// T5: lê as faces do corpo do eletrodo para o <see cref="RegionFinder"/> — tipo, raio, convexo/côncavo
    /// (<see cref="BRepRadiusProbe.IsConcave"/>), caixa EXATA, área, arestas (<c>Edge.ID</c>), normal para fora dos planos
    /// (<c>Face.GetNormal</c> no meio da face) e o achado de raio de <see cref="BRepRadiusProbe.Probe"/>. Só leitura.
    /// </summary>
    public static class RegionFaceReader
    {
        private const int igCylinder = -114972029, igTorus = -114972025;

        public static List<FaceRec> Read(object partDoc) => ReadBody((object)((dynamic)partDoc).Models.Item(1).Body);

        public static List<FaceRec> ReadBody(object body)
        {
            var list = new List<FaceRec>();
            var radiusIds = new HashSet<int>(BRepRadiusProbe.Probe(body, ToolLadder.Shop()).Select(h => h.FaceId));
            dynamic faces = ((dynamic)body).Faces(1);   // igQueryAll
            int n = (int)faces.Count;
            for (int i = 1; i <= n; i++)
            {
                object f = faces.Item(i);
                if (!FaceGeometry.TryGetExactRangeMm(f, out double[] mn, out double[] mx)) continue;
                var r = new FaceRec { Min = mn, Max = mx };
                try { r.Id = (int)((dynamic)f).ID; } catch { }
                try { r.Type = (int)((dynamic)f).Geometry.Type; } catch { }
                try { r.AreaMm2 = (double)((dynamic)f).Area * 1e6; } catch { }
                r.Finding = r.Id != 0 && radiusIds.Contains(r.Id);
                if (r.Type == igCylinder || r.Type == igTorus)
                {
                    try { object g = (object)((dynamic)f).Geometry; r.R = r.Type == igCylinder ? (double)((dynamic)g).Radius * 1000 : (double)((dynamic)g).MinorRadius * 1000; } catch { }
                    bool? concave = BRepRadiusProbe.IsConcave(f, r.Type == igTorus);
                    r.Convex = concave.HasValue ? !concave.Value : (bool?)null;
                }
                if (r.IsPlane && EdmAssemblyBuilder.TryOutwardNormal(f, out double[] nv))
                {
                    double l = Math.Sqrt(nv[0] * nv[0] + nv[1] * nv[1] + nv[2] * nv[2]);
                    if (l > 1e-12) r.Normal = new[] { nv[0] / l, nv[1] / l, nv[2] / l };
                }
                var ids = new List<int>();
                try
                {
                    dynamic es = ((dynamic)f).Edges;
                    for (int j = 1; j <= (int)es.Count; j++) { try { ids.Add((int)((dynamic)es.Item(j)).ID); } catch { } }
                }
                catch { }
                r.Edges = ids.ToArray();
                list.Add(r);
            }
            return list;
        }
    }
}
