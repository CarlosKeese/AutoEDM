using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AutoEDM.Com;
using AutoEDM.Modeling;
using AutoEDM.Mold.Cooling;
using AutoEDM.Sealing;
using AutoEDM.Selection;

namespace AutoEDM.Tools.UndoProbe
{
    /// <summary>
    /// Sonda da ASSOCIATIVIDADE da refrigeração (Carlos, 2026-10-30: "editar o rascunho 3D não
    /// atualiza os furos"). Numa peça DESCARTÁVEL: linha inclinada num esboço 3D, plano normal à linha
    /// na boca (<c>AddNormalToCurve</c>), furo nesse plano; depois muda o comprimento da linha pela
    /// API e mede se a boca do furo foi junto. Também testa se <c>Profile.IncludeEdge</c> da linha
    /// dá algo no esboço (para amarrar o centro) e imprime as assinaturas de furo/plano do interop.
    /// Uso: UndoProbe --assoc
    /// </summary>
    internal static class AssocProbe
    {
        private const int igCurveStart = 14, igCurveEnd = 15, igPivotStart = 3, igPivotEnd = 4;
        private const int igRegularHole = 33, igQueryAll = 1;
        private const string HoleName = "Furo assoc";

        /// <summary>
        /// O MODELADOR REAL de ponta a ponta numa placa descartável 160 × 100 × 40: L1 de face a face
        /// em X; L2 da face Y+ até cruzar L1 (sobrefuro); L3 sai de L1 e acaba DENTRO da placa
        /// (prolongamento até a face + tampão). Depois muda o comprimento de L2 e L3 e confere se os
        /// furos acompanham. Uso: UndoProbe --refrig
        /// </summary>
        public static int Refrig()
        {
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
                        new Primitive { Kind = "caixa", Name = "placa", SizeXMm = 160, SizeYMm = 100, HeightMm = 40, PlaneIndex = 1 } });
                    doc.ModelingMode = 2;
                    try { app.DoIdle(); } catch { }
                    dynamic sk = doc.Sketches3D.Add();
                    double[][] seg =
                    {
                        new double[] { -80, -20, 20, 80, -20, 20 },
                        new double[] { -40, 50, 20, -40, -20, 20 },
                        new double[] { 40, -20, 20, 40, 30, 20 },
                    };
                    foreach (double[] q in seg) sk.Lines3D.Add(q[0] / 1000, q[1] / 1000, q[2] / 1000, q[3] / 1000, q[4] / 1000, q[5] / 1000);
                    try { sk.Active = false; } catch { }
                    try { app.DoIdle(); } catch { }

                    var warnings = new List<string>();
                    List<CoolingLine> lines = CoolingService.ReadLines(doc, false, warnings, out string src);
                    CoolingOptions opt = CoolingService.DefaultOptions(CoolingService.Threads(app));
                    CoolingPlan plan = CoolingService.BuildPlan(doc, lines, null, opt, out string note);
                    Console.WriteLine(CoolingService.Describe(lines, plan, opt));
                    CoolingResult res = CoolingChannelModeler.Create(app, lines, plan, opt);
                    Console.WriteLine($"criados {res.Created}, falhas {res.Failed.Count}: {string.Join(" | ", res.Failed)}");
                    Console.WriteLine("ANTES:");
                    PrintHoles(doc);

                    foreach (var mv in new[] { new { Line = 2, Len = 60.0 }, new { Line = 3, Len = 40.0 } })
                    {
                        try { doc.Sketches3D.Item(1).Lines3D.Item(mv.Line).Length = mv.Len / 1000; }
                        catch (Exception ex) { Console.WriteLine($"L{mv.Line}.Length: {Msg(ex)}"); continue; }
                        try { doc.Models.Item(1).Recompute(); } catch { }
                        try { app.DoIdle(); } catch { }
                        Console.WriteLine($"DEPOIS de L{mv.Line} = {mv.Len} mm: {Lines(doc)}");
                        PrintHoles(doc);
                    }
                    return res.Failed.Count == 0 ? 0 : 2;
                }
                catch (Exception ex) { Console.WriteLine("FALHOU: " + Msg(ex)); return 8; }
                finally
                {
                    try { doc.Close(false); } catch { }
                    try { if (original != null) original.Activate(); } catch { }
                }
            }
        }

        private static void PrintHoles(dynamic doc)
        {
            dynamic feats = doc.DesignEdgebarFeatures;
            for (int i = 1; i <= (int)feats.Count; i++)
            {
                object f = (object)feats.Item(i);
                string name = Try(() => (string)((dynamic)f).Name);
                if (!(name.StartsWith("Refrigera") || name.StartsWith("Engate") || name.StartsWith("Tamp"))) continue;
                Console.WriteLine($"  {name}: {Box(f)}");
            }
            Console.WriteLine($"  volume {Volume(doc):0}");
        }

        private static string Box(object f)
        {
            try
            {
                string status = Try(() => Convert.ToString(f.GetType().InvokeMember("Status", BindingFlags.GetProperty, null, f, null)));
                dynamic faces = f.GetType().InvokeMember("Faces", BindingFlags.GetProperty, null, f, new object[] { igQueryAll });
                double[] mn = { 1e9, 1e9, 1e9 }, mx = { -1e9, -1e9, -1e9 };
                int n = (int)faces.Count;
                for (int j = 1; j <= n; j++)
                    if (FaceGeometry.TryGetRangeMm((object)faces.Item(j), out double[] a, out double[] b))
                        for (int k = 0; k < 3; k++) { mn[k] = Math.Min(mn[k], a[k]); mx[k] = Math.Max(mx[k], b[k]); }
                return n == 0 ? $"{status}, sem faces" : $"{status}, X {mn[0]:0.#}…{mx[0]:0.#} Y {mn[1]:0.#}…{mx[1]:0.#} Z {mn[2]:0.#}…{mx[2]:0.#}";
            }
            catch (Exception ex) { return "ilegível: " + Msg(ex); }
        }

        public static int Run()
        {
            PrintSig();
            using (var connector = new SolidEdgeConnector())
            {
                dynamic app = connector.Connect(startIfNotRunning: false, makeVisible: false);
                if (app == null) { Console.WriteLine("Nenhuma Solid Edge aberta."); return 1; }
                dynamic original = null;
                try { original = app.ActiveDocument; } catch { }
                int rc = 0;
                // Rodada 1 (2026-10-30): finito, de-até e de-até+4 com a linha inclinada — o plano e a
                // boca acompanham; de-até segue as duas pontas; AtDistance(+4) caiu PARA DENTRO.
                // Rodada 2: linha ao longo de X segue igual; AtDistance(−4) cai NA ponta (sem sobrefuro).
                // Rodada 3: planos PARALELOS aos normais — sobrefuro 4 mm além da outra ponta e boca
                // prolongada 10 mm além do fim da linha (canal que acaba antes da face).
                // Rodada 3: paralelos acompanham o plano-pai (boca +10 e sobrefuro −4), mas o +10 é FIXO:
                // linha encurtada, a boca já não chega à face. Rodada 4: começar NA FACE (AddFromTo
                // com a face da peça como "de"), perfil no plano normal da ponta da linha.
                foreach (var run in new[] { new { Extent = "face", Axis = true } })
                {
                    Console.WriteLine();
                    Console.WriteLine($"===== profundidade: {run.Extent}, linha {(run.Axis ? "ao longo de X" : "inclinada")} =====");
                    rc = Math.Max(rc, RunOne(app, run.Extent, run.Axis));
                }
                try { if (original != null) original.Activate(); } catch { }
                return rc;
            }
        }

        /// <summary>finito = AddFinite com o comprimento da linha; de-até = AddFromTo do plano da boca
        /// até um plano normal na outra ponta; de-até±4 = AddNormalToCurveAtDistance(±4 mm) na outra ponta.
        /// <paramref name="axis"/>: linha ao longo de X (o caso comum), de parede a parede do cilindro.</summary>
        private static int RunOne(dynamic app, string extent, bool axis)
        {
            {
                dynamic doc = app.Documents.Add("SolidEdge.PartDocument");
                try
                {
                    doc.ModelingMode = 1;
                    // "face": placa (caixa 120 × 80 × 40), para a boca numa face PLANA como a do molde —
                    // a parede do cilindro como "de" no AddFromTo não furou nada (rodada 4).
                    PrimitiveModeler.Build(doc, new List<Primitive> { extent == "face"
                        ? new Primitive { Kind = "caixa", Name = "base", SizeXMm = 120, SizeYMm = 80, HeightMm = 40, PlaneIndex = 1 }
                        : new Primitive { Kind = "cilindro", Name = "base", DiameterMm = 120, HeightMm = 40, PlaneIndex = 1, LiftMm = 0 } });
                    doc.ModelingMode = 2;
                    try { app.DoIdle(); } catch { }
                    Console.WriteLine($"corpo: {BodyRange(doc)}; volume {Volume(doc):0}");

                    // Boca no FIM da linha (E, acima do topo): mudar o comprimento mexe o fim, e o
                    // furo tem de ir junto. Linha inclinada para a boca andar em X e Z.
                    double[] s = axis ? new double[] { -20, 0, 20 } : new double[] { 0, 0, 5 };
                    double[] e = axis ? new double[] { extent == "par" || extent == "face" ? 50 : 60, 0, 20 } : new double[] { 15, 0, 45 };
                    double[] dir = Unit(new[] { e[0] - s[0], e[1] - s[1], e[2] - s[2] });
                    dynamic sk = doc.Sketches3D.Add();
                    Console.WriteLine($"Sketches3D.Add: {Try(() => (string)sk.Name)}");
                    dynamic line = sk.Lines3D.Add(s[0] / 1000, s[1] / 1000, s[2] / 1000, e[0] / 1000, e[1] / 1000, e[2] / 1000);
                    Console.WriteLine($"Lines3D.Add: comprimento {Try(() => ((double)line.Length * 1000).ToString("0.###"))} mm");
                    try { sk.Active = false; } catch (Exception ex) { Console.WriteLine("Sketch3D.Active=false: " + Msg(ex)); }
                    try { app.DoIdle(); } catch { }
                    Console.WriteLine("linhas lidas: " + Lines(doc));

                    object edge = CoolingLineReader.FindEdge(doc, s, e);
                    if (edge == null) { Console.WriteLine("aresta da linha NÃO achada — fim."); return 2; }
                    EdgeGeometry.TryGetEndPointsMm(edge, out double[] ea, out double[] eb, out _);
                    bool mouthAtEdgeStart = Dist(ea, e) < 1e-3;
                    Console.WriteLine($"aresta: {P(ea)} → {P(eb)}; boca no {(mouthAtEdgeStart ? "início" : "fim")} da aresta");

                    object plane = null;
                    for (int orient = 1; orient <= 3 && plane == null; orient++)
                    {
                        try
                        {
                            object rp = (object)doc.RefPlanes;
                            plane = rp.GetType().InvokeMember("AddNormalToCurve", BindingFlags.InvokeMethod, null, rp,
                                new object[] { edge, mouthAtEdgeStart ? igCurveStart : igCurveEnd, (object)doc.RefPlanes.Item(orient),
                                               mouthAtEdgeStart ? igPivotStart : igPivotEnd, Type.Missing, Type.Missing });
                            Console.WriteLine($"AddNormalToCurve (orientação RefPlanes.Item({orient})): ok");
                        }
                        catch (Exception ex) { Console.WriteLine($"AddNormalToCurve (orientação {orient}): {Msg(ex)}"); }
                    }
                    if (plane == null) return 3;
                    Console.WriteLine("plano: " + PlaneText(plane));
                    double[] mouth = e;
                    if (extent == "par")
                    {
                        // Boca na face, 10 mm além do fim da linha: plano paralelo ao normal.
                        mouth = new[] { e[0] + 10 * dir[0], e[1] + 10 * dir[1], e[2] + 10 * dir[2] };
                        plane = ParallelThrough(doc, plane, 10, mouth);
                        if (plane == null) return 6;
                        Console.WriteLine("boca prolongada: " + PlaneText(plane));
                    }

                    dynamic ps = doc.ProfileSets.Add();
                    dynamic prof = ps.Profiles.Add(plane);
                    ProfilePlaneFrame fr = ProfilePlaneFrame.Discover((object)prof);
                    double x = 0, y = 0;
                    if (fr == null || !fr.TryTo2dMm((object)prof, mouth, out x, out y)) Console.WriteLine("referencial do esboço ilegível — centro em (0, 0)");
                    Console.WriteLine($"boca no esboço: ({x:0.###}, {y:0.###}) mm");

                    // IncludeEdge da linha (perpendicular ao plano): vira ponto? O out-param vem vazio
                    // (modeling-recipes.md) — conta as coleções antes e depois.
                    string before = Counts(prof);
                    try
                    {
                        object[] args = { edge, null };
                        var mod = new ParameterModifier(2); mod[1] = true;
                        ((object)prof).GetType().InvokeMember("IncludeEdge", BindingFlags.InvokeMethod, null, (object)prof, args,
                            new[] { mod }, CultureInfo.InvariantCulture, null);
                        Console.WriteLine($"IncludeEdge: ok; esboço antes {before}, depois {Counts(prof)}; out = {(args[1] == null ? "vazio" : ComDiagnostics.TypeNameOf(args[1]))}");
                    }
                    catch (Exception ex) { Console.WriteLine($"IncludeEdge: {Msg(ex)}; esboço {Counts(prof)}"); }

                    dynamic h2d = prof.Holes2d.Add(x / 1000, y / 1000);
                    int pts = Count(prof, "Points2d");
                    if (pts > 0)
                    {
                        object pt = (object)prof.Points2d.Item(pts);
                        foreach (int[] k in new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 0, 1 } })
                            try { prof.Relations2d.AddKeypoint(h2d, k[0], pt, k[1]); Console.WriteLine($"AddKeypoint(furo {k[0]}, ponto {k[1]}): ok"); break; }
                            catch (Exception ex) { Console.WriteLine($"AddKeypoint(furo {k[0]}, ponto {k[1]}): {Msg(ex)}"); }
                    }
                    Console.WriteLine($"Profile.End(1) = {Try(() => Convert.ToString(prof.End(1)))}");

                    object hd = HoleData(doc, 8);
                    double v0 = Volume(doc), length = Dist(s, e);

                    // Plano final, na OUTRA ponta da linha (aresta relida: a de antes pode ter morrido).
                    object toPlane = null;
                    if (extent != "finito")
                    {
                        object edge2 = CoolingLineReader.FindEdge(doc, s, e) ?? edge;
                        object rp = (object)doc.RefPlanes;
                        int pt = mouthAtEdgeStart ? igCurveEnd : igCurveStart, pv = mouthAtEdgeStart ? igPivotEnd : igPivotStart;
                        if (extent == "par" || extent == "face")
                        {
                            object end =rp.GetType().InvokeMember("AddNormalToCurve", BindingFlags.InvokeMethod, null, rp,
                                new object[] { edge2, pt, (object)doc.RefPlanes.Item(1), pv, Type.Missing, Type.Missing });
                            toPlane = ParallelThrough(doc, end, 4, new[] { s[0] - 4 * dir[0], s[1] - 4 * dir[1], s[2] - 4 * dir[2] });
                            if (toPlane == null) return 7;
                            Console.WriteLine("plano final (sobrefuro): " + PlaneText(toPlane));
                        }
                        else try
                        {
                            toPlane = extent == "de-até"
                                ? rp.GetType().InvokeMember("AddNormalToCurve", BindingFlags.InvokeMethod, null, rp,
                                    new object[] { edge2, pt, (object)doc.RefPlanes.Item(1), pv, Type.Missing, Type.Missing })
                                : rp.GetType().InvokeMember("AddNormalToCurveAtDistance", BindingFlags.InvokeMethod, null, rp,
                                    new object[] { edge2, pt, (object)doc.RefPlanes.Item(1), pv, extent.EndsWith("-4") ? -0.004 : 0.004, Type.Missing, Type.Missing });
                            Console.WriteLine("plano final: " + PlaneText(toPlane));
                        }
                        catch (Exception ex) { Console.WriteLine("plano final: " + Msg(ex)); return 5; }
                    }

                    object from = plane;
                    if (extent == "face")
                    {
                        // Rodada 5: a face como "de" não furou (nem cilíndrica nem plana). Plano paralelo
                        // à face, preso a ela: 0 mm se a SE aceitar, senão 0,01 mm para fora.
                        object face = FaceAtX(doc, 60);
                        if (face == null) { Console.WriteLine("face X=60 NÃO achada"); return 9; }
                        from = null;
                        foreach (double d in new[] { 0.0, 0.01 })
                        {
                            from = ParallelThrough(doc, face, d, new[] { 60 + d, 0, 20 });
                            if (from != null) { Console.WriteLine($"plano de entrada: paralelo à face a {d} mm — {PlaneText(from)}"); break; }
                        }
                        if (from == null) return 9;
                    }

                    object feat = null;
                    foreach (int side in new[] { 1, 2 })
                    {
                        try
                        {
                            feat = toPlane == null
                                ? (object)doc.Models.Item(1).Holes.AddFinite(prof, side, length / 1000, hd)
                                : (object)doc.Models.Item(1).Holes.AddFromTo(prof, from, toPlane, hd);
                        }
                        catch (Exception ex) { Console.WriteLine($"Holes.{(toPlane == null ? "AddFinite" : "AddFromTo")}(lado {side}): {Msg(ex)}"); continue; }
                        double v1 = Volume(doc);
                        Console.WriteLine($"furo lado {side}: volume {v0:0} → {v1:0}");
                        if (v1 < v0 - 1) { try { ((dynamic)feat).Name = HoleName; } catch { } break; }
                        try { ((dynamic)feat).Delete(); } catch { }
                        feat = null;
                    }
                    if (feat == null) { Console.WriteLine("nenhum lado furou — fim."); return 4; }
                    Console.WriteLine("ANTES: " + HoleBox(doc));

                    foreach (double newLen in new[] { 50.0, 30.0 })
                    {
                        dynamic l = null;
                        try { l = doc.Sketches3D.Item(1).Lines3D.Item(1); } catch (Exception ex) { Console.WriteLine("Lines3D.Item(1): " + Msg(ex)); }
                        try { l.Length = newLen / 1000; Console.WriteLine($"Line3D.Length = {newLen} mm: ok"); }
                        catch (Exception ex) { Console.WriteLine($"Line3D.Length = {newLen}: {Msg(ex)}"); continue; }
                        try { doc.Models.Item(1).Recompute(); } catch (Exception ex) { Console.WriteLine("Model.Recompute: " + Msg(ex)); }
                        try { app.DoIdle(); } catch { }
                        Console.WriteLine($"  linhas: {Lines(doc)}");
                        Console.WriteLine($"  plano: {PlaneText(plane)}{(toPlane != null ? "; final " + PlaneText(toPlane) : "")}");
                        Console.WriteLine($"  DEPOIS: {HoleBox(doc)}");
                    }
                    return 0;
                }
                catch (Exception ex) { Console.WriteLine("FALHOU: " + Msg(ex)); return 8; }
                finally
                {
                    try { doc.Close(false); } catch { }
                }
            }
        }

        /// <summary>Plano paralelo a <paramref name="basePlane"/> a <paramref name="distMm"/>, do lado que passa por <paramref name="targetMm"/>.</summary>
        private static object ParallelThrough(dynamic doc, object basePlane, double distMm, double[] targetMm)
        {
            foreach (int side in new[] { 1, 2 })
            {
                object p;
                try { p = (object)doc.RefPlanes.AddParallelByDistance(basePlane, distMm / 1000, side, Type.Missing, Type.Missing, Type.Missing); }
                catch (Exception ex) { Console.WriteLine($"AddParallelByDistance(lado {side}): {Msg(ex)}"); continue; }
                ProfilePlaneFrame f = ProfilePlaneFrame.OfRefPlane(p);
                double d = f == null ? double.NaN : f.SignedDistance(targetMm);
                Console.WriteLine($"  paralelo lado {side}: {PlaneText(p)}; alvo a {d:0.###} mm");
                if (Math.Abs(d) < 0.01) return p;
                try { ((dynamic)p).Visible = false; } catch { }
            }
            return null;
        }

        /// <summary>A face plana da placa em X = <paramref name="xMm"/> (caixa dela achatada em X).</summary>
        private static object FaceAtX(dynamic doc, double xMm)
        {
            dynamic faces = doc.Models.Item(1).Body.Faces[igQueryAll];
            for (int i = 1; i <= (int)faces.Count; i++)
            {
                object f = (object)faces.Item(i);
                if (FaceGeometry.TryGetRangeMm(f, out double[] a, out double[] b) && Math.Abs(a[0] - xMm) < 0.01 && Math.Abs(b[0] - xMm) < 0.01) return f;
            }
            return null;
        }

        private static double[] Unit(double[] v)
        {
            double n = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
            return new[] { v[0] / n, v[1] / n, v[2] / n };
        }

        private static void PrintSig()
        {
            System.Reflection.Assembly interop = typeof(SolidEdgeFramework.Application).Assembly;
            foreach (var tm in new[] { new[] { "SolidEdgePart.Holes", "Add" }, new[] { "SolidEdgePart.RefPlanes", "AddNormalToCurve" },
                                       new[] { "SolidEdgePart.Profile", "IncludeEdge" }, new[] { "SolidEdgePart.Profile", "ProjectEdge" } })
            {
                Type ty = interop.GetType(tm[0]);
                if (ty == null) { Console.WriteLine($"{tm[0]}: não existe no interop"); continue; }
                foreach (MethodInfo m in ty.GetMethods().Where(m => m.Name.StartsWith(tm[1])))
                    Console.WriteLine($"{ty.Name}.{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
            }
        }

        private static object HoleData(dynamic doc, double diaMm)
        {
            object hdc = (object)doc.HoleDataCollection;
            var a = new object[21];
            for (int i = 0; i < a.Length; i++) a[i] = Type.Missing;
            a[0] = igRegularHole; a[1] = diaMm / 1000; a[20] = true;
            return hdc.GetType().InvokeMember("Add", BindingFlags.InvokeMethod, null, hdc, a);
        }

        /// <summary>Caixa das faces do furo (relido pelo nome — o proxy morre no recálculo) e o status.</summary>
        private static string HoleBox(dynamic doc)
        {
            try
            {
                dynamic feats = doc.DesignEdgebarFeatures;
                for (int i = 1; i <= (int)feats.Count; i++)
                {
                    object f = (object)feats.Item(i);
                    if (Try(() => (string)((dynamic)f).Name) != HoleName) continue;
                    string status = Try(() => Convert.ToString(f.GetType().InvokeMember("Status", BindingFlags.GetProperty, null, f, null)));
                    dynamic faces = f.GetType().InvokeMember("Faces", BindingFlags.GetProperty, null, f, new object[] { igQueryAll });
                    double[] mn = { 1e9, 1e9, 1e9 }, mx = { -1e9, -1e9, -1e9 };
                    int n = (int)faces.Count;
                    for (int j = 1; j <= n; j++)
                        if (FaceGeometry.TryGetRangeMm((object)faces.Item(j), out double[] a, out double[] b))
                            for (int k = 0; k < 3; k++) { mn[k] = Math.Min(mn[k], a[k]); mx[k] = Math.Max(mx[k], b[k]); }
                    return $"furo {status}, {n} face(s), caixa X {mn[0]:0.##}…{mx[0]:0.##} Y {mn[1]:0.##}…{mx[1]:0.##} Z {mn[2]:0.##}…{mx[2]:0.##}; volume {Volume(doc):0}";
                }
                return "furo não achado pelo nome";
            }
            catch (Exception ex) { return "furo ilegível: " + Msg(ex); }
        }

        private static string Lines(dynamic doc)
        {
            List<CoolingLine> ls = CoolingLineReader.AllSketch3D(doc, null, out int n);
            return $"{n} esboço(s) 3D, " + string.Join("; ", ls.Select(l => $"{P(l.StartMm)} → {P(l.EndMm)}"));
        }

        private static string PlaneText(object plane)
        {
            ProfilePlaneFrame f = ProfilePlaneFrame.OfRefPlane(plane);
            return f == null ? "geometria ilegível" : $"origem {P(f.Origin)}, normal {P(f.Normal)}";
        }

        private static string Counts(dynamic prof) =>
            $"pontos {Count(prof, "Points2d")}, linhas {Count(prof, "Lines2d")}, furos {Count(prof, "Holes2d")}";

        private static int Count(object prof, string coll)
        {
            try { dynamic c = prof.GetType().InvokeMember(coll, BindingFlags.GetProperty, null, prof, null); return (int)c.Count; }
            catch { return -1; }
        }

        private static string BodyRange(dynamic doc)
        {
            try
            {
                object[] args = { null, null };
                var mod = new ParameterModifier(2); mod[0] = true; mod[1] = true;
                object body = (object)doc.Models.Item(1).Body;
                body.GetType().InvokeMember("GetRange", BindingFlags.InvokeMethod, null, body, args, new[] { mod }, null, null);
                double[] a = ((Array)args[0]).Cast<object>().Select(Convert.ToDouble).Select(v => v * 1000).ToArray();
                double[] b = ((Array)args[1]).Cast<object>().Select(Convert.ToDouble).Select(v => v * 1000).ToArray();
                return $"{P(a)} … {P(b)}";
            }
            catch (Exception ex) { return "caixa ilegível: " + Msg(ex); }
        }

        private static double Volume(dynamic doc) { try { return (double)doc.Models.Item(1).Body.Volume * 1e9; } catch { return double.NaN; } }

        private static double Dist(double[] a, double[] b) => Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]) + (a[2] - b[2]) * (a[2] - b[2]));

        private static string P(double[] p) => p == null ? "?" : string.Format(CultureInfo.InvariantCulture, "({0:0.##}, {1:0.##}, {2:0.##})", p[0], p[1], p[2]);

        private static string Try(Func<string> f) { try { return f() ?? ""; } catch (Exception ex) { return "?" + Msg(ex); } }

        private static string Msg(Exception ex) => (ex.InnerException ?? ex).GetBaseException().Message;
    }
}
