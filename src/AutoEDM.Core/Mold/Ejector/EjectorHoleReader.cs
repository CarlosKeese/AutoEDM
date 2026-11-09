using System;
using System.Collections.Generic;
using AutoEDM.Com;
using AutoEDM.Diagnostics;
using AutoEDM.Drawing;
using AutoEDM.Model;
using AutoEDM.Selection;

namespace AutoEDM.Mold.Ejector
{
    /// <summary>
    /// Lê da PEÇA o que o <see cref="EjectorReliefPlanner"/> precisa: a face plana selecionada e as
    /// faces de revolução/planas do corpo com eixo perpendicular a ela. Só leitura.
    ///
    /// Assinaturas (dump SE 2023, as mesmas já usadas no <see cref="TurnedProfileReader"/> e no O'ring):
    /// <c>Face.Geometry.Type</c> (GNTTypePropertyConstants — NÃO o <c>Face.Type</c>, que é sempre igFace),
    /// <c>Cylinder.GetCylinderData(base, eixo, raio)</c>, <c>Cone.GetConeData</c>, <c>Torus.GetTorusData</c>,
    /// <c>Plane.GetPlaneData(raiz, normal)</c>, <c>Face.GetRange</c> (via <see cref="FaceGeometry"/>),
    /// <c>Hole.HoleData.TreatmentType</c>, <c>Hole.Faces(igQueryAll)</c>, <c>Thread.Faces(igQueryAll)</c>.
    /// </summary>
    public static class EjectorHoleReader
    {
        private const int igPlane = -1909484335;
        private const int igCylinder = -114972029;
        private const int igCone = -114972031;
        private const int igTorus = -114972025;
        private const int igQueryAll = 1;
        private const int igTappedHole = 37;
        private const double FlatTol = 0.005;

        /// <summary>A face clicada como <see cref="ReliefFace"/>; null com o motivo em <paramref name="error"/>.</summary>
        public static ReliefFace ReadFace(object comFace, out string error)
        {
            error = null;
            if (comFace == null) { error = "nenhuma face selecionada."; return null; }
            if (GeomType(comFace) != igPlane) { error = "a face selecionada não é PLANA — clique na face de trás da placa (lado da placa extratora)."; return null; }
            // Caixa EXATA quando a SE der: o GetRange comum vem inflado (~0,005 mm por lado) e uma
            // face plana de verdade passava de "achatada" (1º run, MD-15428, 2026-11-09).
            if (!FaceGeometry.TryGetExactRangeMm(comFace, out double[] mn, out double[] mx) &&
                !FaceGeometry.TryGetRangeMm(comFace, out mn, out mx)) { error = "não deu para ler a caixa da face."; return null; }

            // O eixo sai da NORMAL do plano (exata); a caixa só serve de reserva.
            int axis = -1;
            double planeMm = double.NaN;
            double[] normal = null;
            object geo = null;
            try { geo = ((dynamic)comFace).Geometry; } catch { }
            object[] pd = geo == null ? null
                : TurnedProfileReader.TryCall(geo, "GetPlaneData", new object[] { new double[3], new double[3] }, 0, 1);
            if (pd != null)
            {
                double[] root = Mm(pd[0]);
                normal = Unit(pd[1]);
                if (normal != null)
                    for (int i = 0; i < 3; i++) if (Math.Abs(normal[i]) > 0.9999) { axis = i; planeMm = root[i]; break; }
            }
            if (axis < 0 && normal == null)
            {
                for (int i = 0; i < 3; i++) if (mx[i] - mn[i] <= FlatTol * 4) { axis = i; planeMm = (mn[i] + mx[i]) / 2.0; break; }
            }
            Log.Info(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Alívio de extratores: face — normal {0}, caixa X {1:0.###}…{2:0.###} Y {3:0.###}…{4:0.###} Z {5:0.###}…{6:0.###} mm.",
                normal == null ? "ilegível" : string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:0.####}; {1:0.####}; {2:0.####})", normal[0], normal[1], normal[2]),
                mn[0], mx[0], mn[1], mx[1], mn[2], mx[2]));
            if (axis < 0)
            {
                error = normal == null
                    ? "a face não é perpendicular a X, Y ou Z — o alívio só sai com o furo paralelo a um eixo."
                    : string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "a normal da face é ({0:0.###}; {1:0.###}; {2:0.###}) — não é paralela a X, Y nor Z da peça. O alívio só sai com o furo paralelo a um eixo.",
                        normal[0], normal[1], normal[2]).Replace(" nor ", " nem ");
                return null;
            }
            mn[axis] = mx[axis] = planeMm;

            var f = new ReliefFace { Axis = axis, PlaneMm = planeMm, MinMm = mn, MaxMm = mx };
            try
            {
                // Desempate do lado do material pela caixa do corpo (o mesmo critério do O'ring na face plana).
                object body = ((dynamic)comFace).Body;
                if (body != null && FaceGeometry.TryGetBodyRangeMm(body, out double[] bMin, out double[] bMax))
                    f.IntoSignHint = (f.PlaneMm - bMin[axis]) >= (bMax[axis] - f.PlaneMm) ? -1 : 1;
            }
            catch (Exception e) { Log.Warn("Alívio de extratores: caixa do corpo ilegível — " + e.GetBaseException().Message); }
            return f;
        }

        /// <summary>As faces do corpo que podem ser parte de um furo perpendicular à face.</summary>
        public static List<HoleSurface> ReadSurfaces(dynamic doc, ReliefFace face, List<string> warnings)
        {
            var list = new List<HoleSurface>();
            HashSet<int> threaded = ThreadedFaceIds(doc, warnings);
            int ax = face.Axis, r1 = (ax + 1) % 3, r2 = (ax + 2) % 3;

            dynamic faces;
            try { faces = doc.Models.Item(1).Body.Faces[igQueryAll]; }
            catch (Exception e) { warnings.Add("faces do corpo ilegíveis — " + e.GetBaseException().Message); return list; }
            int n = 0; try { n = (int)faces.Count; } catch { }
            int unread = 0;

            for (int i = 1; i <= n; i++)
            {
                object f;
                try { f = (object)faces.Item(i); } catch { unread++; continue; }
                object geo = null;
                try { geo = ((dynamic)f).Geometry; } catch { }
                if (geo == null) { unread++; continue; }
                int type = 0;
                try { type = Convert.ToInt32(((dynamic)geo).Type); } catch { }

                HoleSurface s = null;
                object[] a;
                if (type == igCylinder && (a = TurnedProfileReader.TryCall(geo, "GetCylinderData", new object[] { new double[3], new double[3], 0.0 }, 0, 1, 2)) != null)
                    s = new HoleSurface { Kind = HoleSurfaceKind.Cylinder, AxisPointMm = Mm(a[0]), AxisDir = Unit(a[1]), RadiusMm = Units.MToMm(Convert.ToDouble(a[2])) };
                else if (type == igCone && (a = TurnedProfileReader.TryCall(geo, "GetConeData", new object[] { new double[3], new double[3], 0.0, 0.0, false }, 0, 1, 2, 3, 4)) != null)
                    s = new HoleSurface { Kind = HoleSurfaceKind.Cone, AxisPointMm = Mm(a[0]), AxisDir = Unit(a[1]) };
                else if (type == igTorus && (a = TurnedProfileReader.TryCall(geo, "GetTorusData", new object[] { new double[3], new double[3], 0.0, 0.0 }, 0, 1, 2, 3)) != null)
                    s = new HoleSurface { Kind = HoleSurfaceKind.Torus, AxisPointMm = Mm(a[0]), AxisDir = Unit(a[1]) };
                else if (type == igPlane && (a = TurnedProfileReader.TryCall(geo, "GetPlaneData", new object[] { new double[3], new double[3] }, 0, 1)) != null)
                    s = new HoleSurface { Kind = HoleSurfaceKind.Plane, AxisPointMm = Mm(a[0]), AxisDir = Unit(a[1]) };
                ComLifetime.Release(geo);

                // Só o que tem eixo (ou normal) paralelo ao da face interessa.
                if (s == null || s.AxisDir == null || Math.Abs(s.AxisDir[ax]) < 0.9999) continue;
                if (!FaceGeometry.TryGetRangeMm(f, out s.MinMm, out s.MaxMm)) { unread++; continue; }

                // Fora da face no plano dela (com folga de um Ø razoável): não é furo desta face.
                const double Margin = 30.0;
                if (s.MaxMm[r1] < face.MinMm[r1] - Margin || s.MinMm[r1] > face.MaxMm[r1] + Margin ||
                    s.MaxMm[r2] < face.MinMm[r2] - Margin || s.MinMm[r2] > face.MaxMm[r2] + Margin) continue;

                try { s.Id = (int)((dynamic)f).ID; } catch { }
                s.Threaded = s.Id != 0 && threaded.Contains(s.Id);
                s.Com = f;
                if (s.Kind == HoleSurfaceKind.Cylinder) ReadEdges(f, s);
                list.Add(s);
            }
            if (unread > 0) warnings.Add($"{unread} face(s) do corpo não puderam ser lidas — algum furo pode ter ficado de fora.");
            Log.Info($"Alívio de extratores: {n} face(s) no corpo, {list.Count} com eixo paralelo ao da face, {threaded.Count} face(s) de rosca na árvore.");
            return list;
        }

        private static void ReadEdges(object face, HoleSurface s)
        {
            try
            {
                dynamic edges = ((dynamic)face).Edges;
                int ne = (int)edges.Count;
                for (int i = 1; i <= ne; i++)
                {
                    object e = edges.Item(i);
                    if (FaceGeometry.TryGetRangeMm(e, out double[] mn, out double[] mx)) s.EdgeBoxesMm.Add(new[] { mn, mx });
                    ComLifetime.Release(e);
                }
            }
            catch (Exception ex) { Log.Warn($"  face {s.Id}: arestas ilegíveis — {ex.GetBaseException().Message} (comprimento pela caixa)."); }
        }

        /// <summary>IDs das faces dos recursos de rosca: Furo com <c>TreatmentType = igTappedHole</c> e Rosca.</summary>
        private static HashSet<int> ThreadedFaceIds(dynamic doc, List<string> warnings)
        {
            var ids = new HashSet<int>();
            dynamic model;
            try { model = doc.Models.Item(1); } catch { return ids; }

            try
            {
                dynamic holes = model.Holes;
                int n = (int)holes.Count;
                for (int i = 1; i <= n; i++)
                {
                    dynamic h = holes.Item(i);
                    int treatment = -1;
                    try { treatment = Convert.ToInt32(h.HoleData.TreatmentType); } catch { }
                    if (treatment == igTappedHole) AddFaceIds((object)h, ids);
                }
            }
            catch (Exception e) { Log.Info("Alívio de extratores: furos da árvore ilegíveis (" + e.GetBaseException().Message + ") — rosca só pelos recursos Rosca."); }

            try
            {
                dynamic threads = model.Threads;
                int n = (int)threads.Count;
                for (int i = 1; i <= n; i++) AddFaceIds((object)threads.Item(i), ids);
            }
            catch (Exception e) { Log.Info("Alívio de extratores: recursos Rosca ilegíveis (" + e.GetBaseException().Message + ")."); }
            return ids;
        }

        private static void AddFaceIds(object feature, HashSet<int> ids)
        {
            try
            {
                object faces = feature.GetType().InvokeMember("Faces", System.Reflection.BindingFlags.GetProperty, null, feature, new object[] { igQueryAll });
                dynamic c = faces;
                int n = (int)c.Count;
                for (int i = 1; i <= n; i++) { try { ids.Add((int)c.Item(i).ID); } catch { } }
            }
            catch { }
        }

        private static int GeomType(object face)
        {
            try { return Convert.ToInt32(((dynamic)face).Geometry.Type); } catch { return 0; }
        }

        private static double[] Mm(object o)
        {
            double[] v = Arr(o);
            return v == null ? null : new[] { Units.MToMm(v[0]), Units.MToMm(v[1]), Units.MToMm(v[2]) };
        }

        private static double[] Unit(object o)
        {
            double[] v = Arr(o);
            if (v == null) return null;
            double n = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
            return n < 1e-12 ? null : new[] { v[0] / n, v[1] / n, v[2] / n };
        }

        private static double[] Arr(object o)
        {
            if (!(o is Array a) || a.Length < 3) return null;
            return new[] { Convert.ToDouble(a.GetValue(0)), Convert.ToDouble(a.GetValue(1)), Convert.ToDouble(a.GetValue(2)) };
        }
    }
}
