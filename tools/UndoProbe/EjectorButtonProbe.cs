using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutoEDM.Model;
using AutoEDM.Mold.Ejector;
using AutoEDM.Selection;

namespace AutoEDM.Tools.UndoProbe
{
    /// <summary>
    /// O botão "Alívio de extratores" de ponta a ponta numa peça DESCARTÁVEL (2026-11-09): caixa 160 × 100 × 40,
    /// 3 furos Ø5 e 2 Ø8 passantes pelo topo; lê a face de cima, planeja e chama o mesmo
    /// <see cref="EjectorReliefModeler.Create"/> do add-in. Espera 2 recursos (um por grupo), todos os
    /// alívios no corpo. <c>UndoProbe --multifuro botao</c>.
    /// </summary>
    internal static class EjectorButtonProbe
    {
        private static readonly double[][] Five = { new[] { -40.0, -20 }, new[] { 0.0, -20 }, new[] { 40.0, -20 } };
        private static readonly double[][] Eight = { new[] { -40.0, 25 }, new[] { 40.0, 25 } };

        public static void Run(dynamic app, dynamic doc, object plane, int side, Func<double, object> holeData)
        {
            foreach (var set in new[] { new { D = 5.0, C = Five }, new { D = 8.0, C = Eight } })
                foreach (double[] c in set.C)
                {
                    dynamic p = doc.ProfileSets.Add().Profiles.Add(plane);
                    p.Holes2d.Add(c[0] / 1000, c[1] / 1000);
                    p.End(1);
                    object f = doc.Models.Item(1).Holes.AddFinite(p, side, 0.040, holeData(set.D));
                    Console.WriteLine($"furo de extrator Ø{set.D} em ({c[0]}; {c[1]}): {Convert.ToString(((dynamic)f).Status)}");
                }
            try { app.DoIdle(); } catch { }

            object top = TopFace(doc);
            if (top == null) { Console.WriteLine("face de cima não achada"); return; }
            ReliefFace rf = EjectorHoleReader.ReadFace(top, out string err);
            if (rf == null) { Console.WriteLine("ReadFace: " + err); return; }
            var warnings = new List<string>();
            List<HoleSurface> surfaces = EjectorHoleReader.ReadSurfaces(doc, rf, warnings);
            var opt = new EjectorReliefOptions();
            EjectorReliefPlan plan = EjectorReliefPlanner.Plan(rf, surfaces, opt);
            Console.WriteLine(EjectorReliefPlanner.Describe(plan, opt));
            Console.WriteLine("chamando EjectorReliefModeler.Create — se a SE cair, foi aqui.");
            Console.Out.Flush();
            EjectorReliefResult res = EjectorReliefModeler.Create(app, top, plan);
            Console.WriteLine($"RESULTADO: {res.Created} alívio(s) em {res.Features} recurso(s); falhas: {string.Join(" | ", res.Failed)}");

            dynamic feats = doc.DesignEdgebarFeatures;
            for (int i = 1; i <= (int)feats.Count; i++)
            {
                dynamic f = feats.Item(i);
                string name = "";
                try { name = (string)f.Name; } catch { }
                if (name.StartsWith("Alív")) Console.WriteLine($"  árvore: {name} [tipo {Convert.ToString(f.Type)}]");
            }
        }

        private static object TopFace(dynamic doc)
        {
            object best = null; double bestZ = double.MinValue;
            dynamic faces = doc.Models.Item(1).Body.Faces[1];
            for (int i = 1; i <= (int)faces.Count; i++)
            {
                object f = faces.Item(i);
                if (!FaceGeometry.TryGetRangeMm(f, out double[] mn, out double[] mx)) continue;
                if (mx[2] - mn[2] > 0.02 || mx[0] - mn[0] < 100) continue;
                if (mn[2] > bestZ) { best = f; bestZ = mn[2]; }
            }
            Console.WriteLine($"face de cima em Z = {bestZ:0.###}");
            return best;
        }
    }
}
