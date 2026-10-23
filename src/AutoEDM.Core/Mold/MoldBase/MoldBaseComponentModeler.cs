using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AutoEDM.Diagnostics;
using AutoEDM.Model;

namespace AutoEDM.Mold.MoldBase
{
    /// <summary>
    /// Coluna, bucha ou pino do porta-molde: cilindros coaxiais empilhados em Z (um recurso por trecho) e, na
    /// bucha, um recorte passante. Peça ORDENADA nova; a origem fica na base do componente (o menor Z da
    /// especificação), então o chamador posiciona a ocorrência em <c>posição + MinZ</c>. Sem variáveis: o
    /// componente é peça de catálogo — tamanho novo é código novo, peça nova.
    /// Conferida pela caixa (Ø maior × comprimento) e pelo volume da união dos trechos menos o furo.
    /// </summary>
    public static class MoldBaseComponentModeler
    {
        private const int IgLeft = 1, IgRight = 2;

        public static bool Create(dynamic doc, ComponentSpec spec, out string error)
        {
            error = null;
            try { doc.ModelingMode = 2; } catch (Exception e) { error = "ordenado: " + Msg(e); return false; }
            double z0 = spec.MinZ;
            dynamic model = null;
            foreach (RoundSegment s in spec.Solids.OrderBy(x => x.Z0))
            {
                object profile = Circle(doc, s.Z0 - z0, s.Diameter, out error);
                if (profile == null) return false;
                double depth = Units.MmToM(s.Z1 - s.Z0);
                try
                {
                    if (model == null)
                    {
                        ((object)doc.Models).GetType().InvokeMember("AddFiniteExtrudedProtrusion", BindingFlags.InvokeMethod, null, doc.Models,
                            new object[] { 1, new[] { (SolidEdgePart.Profile)profile }, IgRight, depth });
                        model = doc.Models.Item(1);
                    }
                    else
                    {
                        object f = model.ExtrudedProtrusions.AddFinite((SolidEdgePart.Profile)profile, IgLeft, IgRight, depth);
                        if (Failed(f)) { error = $"trecho Ø{F(s.Diameter)} nasceu com falha."; return false; }
                    }
                }
                catch (Exception e) { error = $"trecho Ø{F(s.Diameter)}: {Msg(e)}"; return false; }
                Hide(profile);
            }

            if (spec.Bore != null)
            {
                object profile = Circle(doc, spec.Bore.Z0 - z0, spec.Bore.Diameter, out error);
                if (profile == null) return false;
                try
                {
                    object cut = model.ExtrudedCutouts.AddFinite((SolidEdgePart.Profile)profile, IgLeft, IgRight, Units.MmToM(spec.Bore.Z1 - spec.Bore.Z0));
                    if (Failed(cut)) { error = "furo da bucha nasceu com falha."; return false; }
                }
                catch (Exception e) { error = "furo da bucha: " + Msg(e); return false; }
                Hide(profile);
            }
            return Verify(doc, spec, out error);
        }

        /// <summary>Círculo centrado na origem num plano paralelo ao da base, a <paramref name="z"/> mm (≥ 0) para +Z.</summary>
        private static object Circle(dynamic doc, double z, double diameter, out string error)
        {
            error = null;
            try
            {
                object plane = doc.RefPlanes.Item(1);
                if (z > 1e-6)
                {
                    plane = doc.RefPlanes.AddParallelByDistance(doc.RefPlanes.Item(1), Units.MmToM(z), IgRight,
                        Type.Missing, Type.Missing, Type.Missing, Type.Missing);
                    Hide(plane);
                }
                dynamic ps = doc.ProfileSets.Add();
                dynamic prof = ps.Profiles.Add(plane);
                prof.Circles2d.AddByCenterRadius(0.0, 0.0, Units.MmToM(diameter) / 2);
                int end = Convert.ToInt32(prof.End(1));
                if (end != 0) { error = $"círculo Ø{F(diameter)} recusado (End = {end})."; return null; }
                return (object)prof;
            }
            catch (Exception e) { error = $"esboço Ø{F(diameter)} a {F(z)}: {Msg(e)}"; return null; }
        }

        /// <summary>Volume da união dos trechos coaxiais (por fatias entre as mudanças de Ø) menos o furo, em mm³.</summary>
        public static double ExpectedVolume(ComponentSpec spec)
        {
            var cuts = spec.Solids.SelectMany(s => new[] { s.Z0, s.Z1 }).Distinct().OrderBy(z => z).ToList();
            double v = 0;
            for (int i = 0; i + 1 < cuts.Count; i++)
            {
                double a = cuts[i], b = cuts[i + 1], mid = (a + b) / 2;
                double d = spec.Solids.Where(s => s.Z0 <= mid && s.Z1 >= mid).Select(s => s.Diameter).DefaultIfEmpty(0).Max();
                double bore = spec.Bore != null && spec.Bore.Z0 <= mid && spec.Bore.Z1 >= mid && d > 0 ? spec.Bore.Diameter : 0;
                v += Math.PI / 4 * (d * d - bore * bore) * (b - a);
            }
            return v;
        }

        private static bool Verify(dynamic doc, ComponentSpec spec, out string error)
        {
            error = null;
            try
            {
                object body = doc.Models.Item(1).Body;
                if (!AutoEDM.Selection.FaceGeometry.TryGetBodyRangeMm(body, out double[] mn, out double[] mx)) { Log.Warn($"  [{spec.Code}] caixa ilegível."); return true; }
                double len = spec.MaxZ - spec.MinZ, d = spec.MaxDiameter;
                bool box = Math.Abs(mx[2] - mn[2] - len) < 0.01 && Math.Abs(mn[2]) < 0.01 && Math.Abs(mx[0] - mn[0] - d) < 0.01;
                double vol = Convert.ToDouble(doc.Models.Item(1).Body.Volume) * 1e9, want = ExpectedVolume(spec);
                bool volOk = double.IsNaN(vol) || Math.Abs(vol - want) <= Math.Max(1.0, want * 0.002);
                Log.Info(string.Format(CultureInfo.InvariantCulture, "  [{0}] Ø{1:0.##} × {2:0.##} (Z {3:0.##}..{4:0.##}), volume {5:0} mm³ (esperado {6:0}){7}",
                    spec.Code, mx[0] - mn[0], mx[2] - mn[2], mn[2], mx[2], vol, want, box && volOk ? " ✓" : " ✗"));
                if (!box || !volOk) error = $"{spec.Code}: o componente não ficou com a medida da especificação (veja o log).";
                return box && volOk;
            }
            catch (Exception e) { Log.Warn($"  [{spec.Code}] conferência: {Msg(e)}"); return true; }
        }

        private static bool Failed(object feature)
        {
            if (feature == null) return true;
            try { return unchecked((uint)Convert.ToInt32(((dynamic)feature).Status)) != 1216476310u; }
            catch { return false; }
        }

        private static void Hide(object o) { try { ((dynamic)o).Visible = false; } catch { } }
        private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        private static string Msg(Exception e) => e.GetBaseException().Message;
    }
}
