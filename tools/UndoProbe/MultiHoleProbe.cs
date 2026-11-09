using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutoEDM.Com;
using AutoEDM.Model;
using AutoEDM.Modeling;
using AutoEDM.Sealing;
using AutoEDM.Selection;

namespace AutoEDM.Tools.UndoProbe
{
    /// <summary>
    /// Sonda do ALÍVIO DE EXTRATORES agrupado (Carlos, 2026-11-09). Fatos já medidos:
    /// <list type="bullet">
    /// <item>Vários Holes2d num perfil só: <c>Profile.End</c> colapsa 3 → 1; sem End o furo FALHA.</item>
    /// <item>Gravador: o Furo de 3 furos feito à mão = UserDefinedPattern + 3 HoleGeometries + UM ProfileSet
    ///   com 3 Profiles (um furo por perfil). Não entra em Model.Holes.</item>
    /// <item><c>UserDefinedPatterns.AddByProfiles(3, Profile[], semente Hole)</c> com perfis presos por
    ///   IncludeEdge+AddConcentric DERRUBOU a SE (crash nativo, 10:22).</item>
    /// </list>
    /// Uma variante por execução, numa peça DESCARTÁVEL (fechada sem salvar) — receita do bloco da
    /// AssocProbe: caixa em SÍNCRONO, depois ordenado (direto em ordenado nem o controle saía).
    ///   UndoProbe --multifuro controle | set | padrao-simples | padrao-set
    /// As duas "padrao-*" podem DERRUBAR a Solid Edge: só com nada importante aberto.
    /// </summary>
    internal static class MultiHoleProbe
    {
        private const int igRegularHole = 33;
        private const double TopMm = 40;                 // caixa 160 × 100 × 40 no plano 1
        private static readonly double[][] Centers = { new[] { -40.0, 0 }, new[] { 0.0, 0 }, new[] { 40.0, 0 } };

        public static int Run(string variant)
        {
            Console.WriteLine($"===== multifuro: {variant} =====");
            using (var connector = new SolidEdgeConnector())
            {
                dynamic app = connector.Connect(startIfNotRunning: false, makeVisible: false);
                if (app == null) { Console.WriteLine("Nenhuma Solid Edge aberta."); return 1; }
                dynamic original = null;
                try { original = app.ActiveDocument; } catch { }
                dynamic doc = app.Documents.Add("SolidEdge.PartDocument");
                try
                {
                    doc.ModelingMode = 1;
                    PrimitiveModeler.Build(doc, new List<Primitive> {
                        new Primitive { Kind = "caixa", Name = "placa", SizeXMm = 160, SizeYMm = 100, HeightMm = TopMm, PlaneIndex = 1 } });
                    doc.ModelingMode = 2;
                    try { app.DoIdle(); } catch { }

                    object top = TopPlane(doc, out int side);
                    if (top == null) { Console.WriteLine("plano do topo / lado do furo não achados."); return 2; }
                    Console.WriteLine($"topo achado; lado do furo = {side}");
                    object hd = HoleData(doc, 5);

                    switch (variant)
                    {
                        case "controle": Control(doc, top, side, hd); break;
                        case "set": OneSetManyProfiles(doc, top, side, hd); break;
                        case "padrao-simples": Pattern(doc, top, side, hd, sameSet: false); break;
                        case "padrao-set": Pattern(doc, top, side, hd, sameSet: true); break;
                        case "padrao-manual": PatternLikeUi(app, doc, top, side, hd); break;
                        case "padrao-preso": PatternTied(app, doc, top, side, hd); return 0;
                        case "botao": { object docObj = doc; EjectorButtonProbe.Run(app, doc, top, side, new Func<double, object>(d => HoleData(docObj, d))); return 0; }
                        default: Console.WriteLine("variante desconhecida"); return 3;
                    }
                    Console.WriteLine("furos Ø5 no corpo: " + CountHoles(doc) + " de 3");
                    Console.WriteLine("recursos: " + Features(doc));
                    return 0;
                }
                catch (Exception ex) { Console.WriteLine("FALHOU: " + Msg(ex)); return 8; }
                finally
                {
                    try { doc.Close(false); } catch { }
                    try { if (original != null) original.Activate(); } catch { }
                }
            }
        }

        /// <summary>Plano do topo (paralelo ao plano 1 a 40 mm) e o lado do furo que corta material — por teste.</summary>
        private static object TopPlane(dynamic doc, out int holeSide)
        {
            holeSide = 0;
            object hd = HoleData(doc, 3);
            // Plano NOVO por tentativa e nada apagado: apagar o furo de teste desconectou o plano
            // (CO_E_OBJNOTCONNECTED, 10:35 — o mesmo do add-in). Furo de teste Ø3 que sobra não conta.
            int k = 0;
            foreach (int ps in new[] { 1, 2 })
                foreach (int s in new[] { 1, 2 })
                {
                    object plane;
                    try { plane = doc.RefPlanes.AddParallelByDistance(doc.RefPlanes.Item(1), TopMm / 1000, ps, Type.Missing, Type.Missing, Type.Missing); }
                    catch (Exception ex) { Console.WriteLine($"  plano lado {ps}: {Msg(ex)}"); continue; }
                    dynamic prof = doc.ProfileSets.Add().Profiles.Add(plane);
                    prof.Holes2d.Add(0.07, 0.045 - 0.01 * k++);
                    prof.End(1);
                    object f;
                    try { f = doc.Models.Item(1).Holes.AddFinite(prof, s, 0.003, hd); }
                    catch (Exception ex) { Console.WriteLine($"  teste plano {ps} lado {s}: {Msg(ex)}"); continue; }
                    int n = FaceCount(f);
                    Console.WriteLine($"  teste plano {ps} lado {s}: {Status(f)}, {n} face(s)");
                    if (n > 0)
                    {
                        holeSide = s;
                        return doc.RefPlanes.AddParallelByDistance(doc.RefPlanes.Item(1), TopMm / 1000, ps, Type.Missing, Type.Missing, Type.Missing);
                    }
                }
            return null;
        }

        private static void Control(dynamic doc, object plane, int side, object hd)
        {
            dynamic prof = doc.ProfileSets.Add().Profiles.Add(plane);
            prof.Holes2d.Add(Centers[0][0] / 1000, Centers[0][1] / 1000);
            Console.WriteLine("End(1) = " + Try(() => Convert.ToString(prof.End(1))));
            object f = doc.Models.Item(1).Holes.AddFinite(prof, side, 0.010, hd);
            Console.WriteLine($"controle: {Status(f)}, {FaceCount(f)} face(s)");
        }

        /// <summary>Como a gravação mostrou: UM ProfileSet com um perfil por furo.</summary>
        private static void OneSetManyProfiles(dynamic doc, object plane, int side, object hd)
        {
            dynamic ps = doc.ProfileSets.Add();
            var profs = new List<object>();
            foreach (double[] c in Centers)
            {
                dynamic prof;
                try { prof = ps.Profiles.Add(plane); }
                catch (Exception ex) { Console.WriteLine($"Profiles.Add nº {profs.Count + 1} no MESMO set: {Msg(ex)}"); break; }
                prof.Holes2d.Add(c[0] / 1000, c[1] / 1000);
                Console.WriteLine($"perfil {profs.Count + 1}: End(1) = {Try(() => Convert.ToString(prof.End(1)))}");
                profs.Add((object)prof);
            }
            Console.WriteLine($"ProfileSet.Profiles.Count = {Try(() => Convert.ToString(ps.Profiles.Count))}");
            if (profs.Count == 0) return;
            object f = doc.Models.Item(1).Holes.AddFinite(profs[0], side, 0.010, hd);
            Console.WriteLine($"Holes.AddFinite(1º perfil): {Status(f)}, {FaceCount(f)} face(s)");
        }

        /// <summary>Semente (1º furo) + UserDefinedPatterns.AddByProfiles com os outros, sem aresta incluída nem relação.</summary>
        private static void Pattern(dynamic doc, object plane, int side, object hd, bool sameSet)
        {
            dynamic seedProf = doc.ProfileSets.Add().Profiles.Add(plane);
            seedProf.Holes2d.Add(Centers[0][0] / 1000, Centers[0][1] / 1000);
            seedProf.End(1);
            object seed = doc.Models.Item(1).Holes.AddFinite(seedProf, side, 0.010, hd);
            Console.WriteLine($"semente: {Status(seed)}, {FaceCount(seed)} face(s)");

            var profs = new List<SolidEdgePart.Profile>();
            dynamic shared = sameSet ? doc.ProfileSets.Add() : null;
            foreach (double[] c in Centers.Skip(1))
            {
                dynamic set = sameSet ? shared : doc.ProfileSets.Add();
                dynamic prof = set.Profiles.Add(plane);
                prof.Holes2d.Add(c[0] / 1000, c[1] / 1000);
                Console.WriteLine($"perfil: End(1) = {Try(() => Convert.ToString(prof.End(1)))}");
                profs.Add((SolidEdgePart.Profile)(object)prof);
            }
            Console.WriteLine($"chamando AddByProfiles({profs.Count}, Profile[], semente) — se a SE cair, foi aqui.");
            Console.Out.Flush();
            object coll = (object)doc.Models.Item(1).UserDefinedPatterns;
            object udp = coll.GetType().InvokeMember("AddByProfiles", BindingFlags.InvokeMethod, null, coll,
                new object[] { profs.Count, profs.ToArray(), seed });
            Console.WriteLine($"padrão: {Status(udp)}, {FaceCount(udp)} face(s), NumberInputFeatures = {Try(() => Convert.ToString(((dynamic)udp).NumberInputFeatures))}");
        }

        /// <summary>
        /// Igual ao Furo múltiplo da interface (gravador): UM ProfileSet com um perfil por furo, a semente
        /// no 1º perfil e AddByProfiles com TODOS os perfis (o da semente incluso).
        /// </summary>
        private static void PatternLikeUi(dynamic app, dynamic doc, object plane, int side, object hd)
        {
            dynamic ps = doc.ProfileSets.Add();
            var profs = new List<SolidEdgePart.Profile>();
            foreach (double[] c in Centers)
            {
                dynamic prof = ps.Profiles.Add(plane);
                prof.Holes2d.Add(c[0] / 1000, c[1] / 1000);
                Console.WriteLine($"perfil {profs.Count + 1}: End(1) = {Try(() => Convert.ToString(prof.End(1)))}");
                profs.Add((SolidEdgePart.Profile)(object)prof);
            }
            object seed = doc.Models.Item(1).Holes.AddFinite(profs[0], side, 0.010, hd);
            Console.WriteLine($"semente (perfil 1): {Status(seed)}, {FaceCount(seed)} face(s)");
            Console.WriteLine($"chamando AddByProfiles({profs.Count}, Profile[] com a semente, semente) — se a SE cair, foi aqui.");
            Console.Out.Flush();
            object coll = (object)doc.Models.Item(1).UserDefinedPatterns;
            object udp = coll.GetType().InvokeMember("AddByProfiles", BindingFlags.InvokeMethod, null, coll,
                new object[] { profs.Count, profs.ToArray(), seed });
            Console.WriteLine($"padrão: {Status(udp)}, {FaceCount(udp)} face(s), NumberInputFeatures = {Try(() => Convert.ToString(((dynamic)udp).NumberInputFeatures))}");
            try { doc.Models.Item(1).Recompute(); } catch (Exception ex) { Console.WriteLine("Recompute: " + Msg(ex)); }
            try { app.DoIdle(); } catch { }
            Console.WriteLine($"depois do Recompute: padrão {Status(udp)}, {FaceCount(udp)} face(s); semente {Status(seed)}, {FaceCount(seed)} face(s)");
        }

        /// <summary>
        /// A receita da interface com o VÍNCULO do botão: 3 furos Ø5 "de extrator" (um recurso cada), depois o
        /// grupo de alívio Ø6 — cada perfil com a aresta da boca incluída (construção) e Hole2d concêntrico a ela.
        /// </summary>
        private static void PatternTied(dynamic app, dynamic doc, object plane, int side, object hd5)
        {
            foreach (double[] c in Centers)
            {
                dynamic p = doc.ProfileSets.Add().Profiles.Add(plane);
                p.Holes2d.Add(c[0] / 1000, c[1] / 1000);
                p.End(1);
                object f = doc.Models.Item(1).Holes.AddFinite(p, side, 0.030, hd5);
                Console.WriteLine($"furo de extrator Ø5 em X={c[0]}: {Status(f)}, {FaceCount(f)} face(s)");
            }
            ProfilePlaneFrame pf = ProfilePlaneFrame.OfRefPlane(plane);
            Console.WriteLine($"plano: origem ({pf.Origin[0]:0.##}, {pf.Origin[1]:0.##}, {pf.Origin[2]:0.##}), normal ({pf.Normal[0]:0.##}, {pf.Normal[1]:0.##}, {pf.Normal[2]:0.##})");
            object hd6 = HoleData(doc, 6);

            dynamic ps = doc.ProfileSets.Add();
            var profs = new List<SolidEdgePart.Profile>();
            int tied = 0;
            foreach (double[] c in Centers)
            {
                object edge = MouthEdge(doc, pf, c);
                dynamic prof = ps.Profiles.Add(plane);
                object hole2d = (object)prof.Holes2d.Add(c[0] / 1000, c[1] / 1000);
                string tie = "sem aresta";
                if (edge != null)
                {
                    int before = ((int)prof.Circles2d.Count) + ((int)prof.Arcs2d.Count);
                    object[] args = { edge, null };
                    var mod = new ParameterModifier(2); mod[1] = true;
                    try { ((object)prof).GetType().InvokeMember("IncludeEdge", BindingFlags.InvokeMethod, null, (object)prof, args, new[] { mod }, null, null); }
                    catch (Exception ex) { tie = "IncludeEdge: " + Msg(ex); }
                    int nc = (int)prof.Circles2d.Count;
                    object circle = nc > 0 ? (object)prof.Circles2d.Item(nc) : null;
                    if (circle != null && ((int)prof.Circles2d.Count) + ((int)prof.Arcs2d.Count) == before + 1)
                    {
                        try { prof.ToggleConstruction(circle); } catch (Exception ex) { tie = "ToggleConstruction: " + Msg(ex); }
                        try
                        {
                            object rel = prof.Relations2d;
                            rel.GetType().InvokeMember("AddConcentric", BindingFlags.InvokeMethod, null, rel, new object[] { hole2d, circle, Type.Missing });
                            tie = "preso"; tied++;
                        }
                        catch (Exception ex) { tie = "AddConcentric: " + Msg(ex); }
                    }
                }
                Console.WriteLine($"perfil X={c[0]}: {tie}; End(1) = {Try(() => Convert.ToString(prof.End(1)))}");
                profs.Add((SolidEdgePart.Profile)(object)prof);
            }
            object seed = doc.Models.Item(1).Holes.AddFinite(profs[0], side, 0.010, hd6);
            Console.WriteLine($"semente Ø6: {Status(seed)}, {FaceCount(seed)} face(s); {tied}/3 perfis presos");
            Console.WriteLine($"chamando AddByProfiles(3, perfis PRESOS, semente) — se a SE cair, foi aqui.");
            Console.Out.Flush();
            object coll = (object)doc.Models.Item(1).UserDefinedPatterns;
            object udp = coll.GetType().InvokeMember("AddByProfiles", BindingFlags.InvokeMethod, null, coll,
                new object[] { profs.Count, profs.ToArray(), seed });
            Console.WriteLine($"padrão: {Status(udp)}, {FaceCount(udp)} face(s), NumberInputFeatures = {Try(() => Convert.ToString(((dynamic)udp).NumberInputFeatures))}");
            Console.WriteLine("alívios Ø6 no corpo: " + CountDia(doc, 6) + " de 3; recursos: " + Features(doc));

            // Acompanha? Move o 1º furo de extrator 10 mm em X pelo Hole2d do perfil DELE e recalcula.
            try
            {
                dynamic baseHole = doc.Models.Item(1).Holes.Item(doc.Models.Item(1).Holes.Count - 3 + 1);
                Console.WriteLine($"movendo {Try(() => (string)baseHole.Name)} (X=-40 → X=-30)");
                dynamic bp = baseHole.Profile;
                Console.WriteLine($"  Profile: {(bp == null ? "null" : "ok")}, Holes2d = {Try(() => Convert.ToString(bp.Holes2d.Count))}");
                bp.Holes2d.Item(1).SetCenterPoint(-0.030, 0.0);
                try { bp.End(1); } catch { }
                doc.Models.Item(1).Recompute();
                try { app.DoIdle(); } catch { }
                Console.WriteLine("  depois: Ø5 em X=-30? " + At(doc, 5, -30) + "; Ø6 em X=-30? " + At(doc, 6, -30) + "; Ø6 ainda em X=-40? " + At(doc, 6, -40));
            }
            catch (Exception ex) { Console.WriteLine("  mover: " + Msg(ex)); }
        }

        /// <summary>Aresta circular Ø5 da boca, no plano do esboço, centrada em c (XY).</summary>
        private static object MouthEdge(dynamic doc, ProfilePlaneFrame pf, double[] c)
        {
            int ax = Math.Abs(pf.Normal[2]) > 0.9 ? 2 : Math.Abs(pf.Normal[1]) > 0.9 ? 1 : 0;
            double at = pf.Origin[ax];
            dynamic faces = doc.Models.Item(1).Body.Faces[1];
            for (int i = 1; i <= (int)faces.Count; i++)
            {
                object f = faces.Item(i);
                if (!FaceGeometry.TryGetRangeMm(f, out double[] mn, out double[] mx)) continue;
                if (Math.Abs(mx[0] - mn[0] - 5) > 0.05 || Math.Abs(mx[1] - mn[1] - 5) > 0.05) continue;
                if (Math.Abs((mx[0] + mn[0]) / 2 - c[0]) > 0.05 || Math.Abs((mx[1] + mn[1]) / 2 - c[1]) > 0.05) continue;
                dynamic edges = ((dynamic)f).Edges;
                for (int k = 1; k <= (int)edges.Count; k++)
                {
                    object e = edges.Item(k);
                    if (FaceGeometry.TryGetRangeMm(e, out double[] a, out double[] b) && b[ax] - a[ax] < 0.02 && Math.Abs(a[ax] - at) < 0.05
                        && b[0] - a[0] > 4.9 && b[1] - a[1] > 4.9) return e;
                }
            }
            Console.WriteLine($"  aresta da boca em X={c[0]} não achada");
            return null;
        }

        private static string At(dynamic doc, double d, double x)
        {
            try
            {
                dynamic faces = doc.Models.Item(1).Body.Faces[1];
                for (int i = 1; i <= (int)faces.Count; i++)
                {
                    if (!FaceGeometry.TryGetRangeMm((object)faces.Item(i), out double[] mn, out double[] mx)) continue;
                    if (Math.Abs(mx[0] - mn[0] - d) < 0.05 && Math.Abs(mx[1] - mn[1] - d) < 0.05 && Math.Abs((mx[0] + mn[0]) / 2 - x) < 0.05) return "SIM";
                }
                return "não";
            }
            catch (Exception ex) { return "ilegível (" + Msg(ex) + ")"; }
        }

        private static string CountDia(dynamic doc, double d)
        {
            var hit = new HashSet<int>();
            try
            {
                dynamic faces = doc.Models.Item(1).Body.Faces[1];
                for (int i = 1; i <= (int)faces.Count; i++)
                {
                    if (!FaceGeometry.TryGetRangeMm((object)faces.Item(i), out double[] mn, out double[] mx)) continue;
                    if (Math.Abs(mx[0] - mn[0] - d) > 0.05 || Math.Abs(mx[1] - mn[1] - d) > 0.05) continue;
                    for (int k = 0; k < Centers.Length; k++)
                        if (Math.Abs((mx[0] + mn[0]) / 2 - Centers[k][0]) < 0.05 && Math.Abs((mx[1] + mn[1]) / 2 - Centers[k][1]) < 0.05) hit.Add(k);
                }
            }
            catch (Exception ex) { return "corpo ilegível (" + Msg(ex) + ")"; }
            return hit.Count.ToString();
        }

        private static object HoleData(dynamic doc, double dMm)
        {
            object hdc = doc.HoleDataCollection;
            var a = new object[21];
            for (int i = 0; i < a.Length; i++) a[i] = Type.Missing;
            a[0] = igRegularHole; a[1] = Units.MmToM(dMm); a[20] = true;
            return hdc.GetType().InvokeMember("Add", BindingFlags.InvokeMethod, null, hdc, a);
        }

        private static string CountHoles(dynamic doc)
        {
            var hit = new HashSet<int>();
            try
            {
                dynamic faces = doc.Models.Item(1).Body.Faces[1];
                for (int i = 1; i <= (int)faces.Count; i++)
                {
                    if (!FaceGeometry.TryGetRangeMm((object)faces.Item(i), out double[] mn, out double[] mx)) continue;
                    if (Math.Abs(mx[0] - mn[0] - 5) > 0.05 || Math.Abs(mx[1] - mn[1] - 5) > 0.05) continue;
                    for (int k = 0; k < Centers.Length; k++)
                        if (Math.Abs((mx[0] + mn[0]) / 2 - Centers[k][0]) < 0.05 && Math.Abs((mx[1] + mn[1]) / 2 - Centers[k][1]) < 0.05) hit.Add(k);
                }
            }
            catch (Exception ex) { return "corpo ilegível (" + Msg(ex) + ")"; }
            return hit.Count.ToString();
        }

        private static string Features(dynamic doc)
        {
            var names = new List<string>();
            try
            {
                dynamic feats = doc.DesignEdgebarFeatures;
                for (int i = 1; i <= (int)feats.Count; i++)
                {
                    dynamic f = feats.Item(i);
                    names.Add($"{Try(() => (string)f.Name)}[{Try(() => Convert.ToString(f.Type))}]");
                }
            }
            catch (Exception ex) { names.Add("ilegível: " + Msg(ex)); }
            return string.Join(", ", names);
        }

        private static int FaceCount(object f)
        {
            if (f == null) return -1;
            try { return (int)((dynamic)f.GetType().InvokeMember("Faces", BindingFlags.GetProperty, null, f, new object[] { 1 })).Count; }
            catch { return -1; }
        }

        private static string Status(object f)
        {
            string s = Try(() => Convert.ToString(((dynamic)f).Status));
            return s == "1216476310" ? "OK" : s == "1216476311" ? "FALHOU" : s;
        }

        private static string Try(Func<string> f) { try { return f() ?? ""; } catch (Exception ex) { return "?" + Msg(ex); } }
        private static string Msg(Exception ex) => (ex.InnerException ?? ex).GetBaseException().Message;
    }
}
