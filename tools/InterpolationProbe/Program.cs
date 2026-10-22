using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AutoEDM.Com;
using AutoEDM.Modeling;
using AutoEDM.Selection;

namespace AutoEDM.Tools.InterpolationProbe
{
    /// <summary>
    /// Sonda do eletrodo de interpolação (2026-10-22). O 1º clique no botão real parou em
    /// <c>IntersectionCurves.Add</c>: (CopySurface, RefPlane) → E_INVALIDARG; (Face[] tipado, RefPlane)
    /// → DISP_E_TYPEMISMATCH. Aqui, numa peça DESCARTÁVEL (cilindro Ø20 → cópia da face lateral),
    /// cada forma de chamada é tentada e o resultado impresso. Nada do usuário é tocado.
    /// </summary>
    internal static class Program
    {
        private static SolidEdgeConnector Connector;
        [STAThread]
        private static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            using (var connector = new SolidEdgeConnector())
            {
                Connector = connector;
                dynamic app = connector.Connect(startIfNotRunning: false, makeVisible: false);
                if (app == null) { Console.WriteLine("Nenhuma Solid Edge aberta."); return 1; }

                if (args.Any(a => a == "--cadeia")) return Chain(app);
                string idsArg = args.Where(a => a.StartsWith("--ids=")).Select(a => a.Substring(6)).FirstOrDefault();
                if (idsArg != null) return CompareIds(app, idsArg.Split(',').Select(int.Parse).ToArray());
                if (args.Any(a => a == "--sujo"))
                {
                    // SÓ LEITURA na montagem ativa: a peça de cada ocorrência tem alteração não salva?
                    dynamic occs = app.ActiveDocument.Occurrences;
                    for (int i = 1; i <= (int)occs.Count; i++)
                    {
                        dynamic o = occs.Item(i);
                        string name = (string)o.Name, file = (string)o.OccurrenceFileName, dirty;
                        try { dirty = ((bool)o.OccurrenceDocument.Dirty).ToString(); } catch (Exception ex) { dirty = "? " + Msg(ex); }
                        string disk = System.IO.File.Exists(file) ? System.IO.File.GetLastWriteTime(file).ToString("yyyy-MM-dd HH:mm") : "não existe";
                        Console.WriteLine($"{name}: Dirty={dirty}; arquivo {disk} — {file}");
                    }
                    return 0;
                }
                if (args.Any(a => a == "--revolucao"))
                {
                    foreach (bool separateAxis in new[] { false, true })
                        foreach (bool relate in new[] { true })
                            RevolveVariant(app, separateAxis, relate);
                    return 0;
                }

                dynamic part = app.Documents.Add("SolidEdge.PartDocument");
                try
                {
                    try { if ((int)part.ModelingMode != 1) part.ModelingMode = 1; } catch { }
                    PrimitiveModeler.Build(part, new List<Primitive>
                    {
                        new Primitive { Kind = "cilindro", Name = "c", DiameterMm = 20, HeightMm = 10, PlaneIndex = 1 }
                    });

                    // Face lateral do cilindro (a que não é plana: caixa com Z de 0 a 10).
                    object lateral = null;
                    dynamic faces = part.Models.Item(1).Body.Faces[1];
                    for (int i = 1; i <= (int)faces.Count; i++)
                    {
                        object f = faces.Item(i);
                        if (FaceGeometry.TryGetRangeMm(f, out double[] mn, out double[] mx) && mx[2] - mn[2] > 5) lateral = f;
                    }
                    if (lateral == null) { Console.WriteLine("face lateral não achada"); return 2; }
                    object col = (object)part.Constructions.CopySurfaces;
                    object surf = Invoke(col, "Add", new object[] { 1, new[] { (SolidEdgeGeometry.Face)lateral }, Type.Missing, Type.Missing });
                    Console.WriteLine("CopySurface: " + Status(surf));

                    // Base planes: imprime o índice e o tipo COM de cada um.
                    object plane = null;
                    for (int i = 1; i <= 3; i++)
                    {
                        object p = part.RefPlanes.Item(i);
                        string normal = FaceGeometry.TryOneArrayOut(p, "GetNormal", out double[] n, out string why)
                            ? $"({n[0]:0.##}, {n[1]:0.##}, {n[2]:0.##})" : "? " + why;
                        Console.WriteLine($"RefPlanes.Item({i}): {ComDiagnostics.TypeNameOf(p)} normal {normal}");
                        if (n != null && Math.Abs(n[1]) > 0.999) plane = p;
                    }
                    if (plane == null) { Console.WriteLine("plano XZ não achado"); return 3; }

                    object surfFace = ((dynamic)surf).Faces[1].Item(1);
                    object surfBody = null;
                    try { surfBody = ((dynamic)surfFace).Body; } catch (Exception ex) { Console.WriteLine("Body da superfície: " + Msg(ex)); }

                    var set1 = new List<KeyValuePair<string, object>>
                    {
                        Kv("CopySurface", surf),
                        Kv("object[]{CopySurface}", new object[] { surf }),
                        Kv("Face", surfFace),
                        Kv("object[]{Face}", new object[] { surfFace }),
                        Kv("Body", surfBody),
                        Kv("object[]{Body}", new object[] { surfBody }),
                    };
                    var set2 = new List<KeyValuePair<string, object>>
                    {
                        Kv("RefPlane", plane),
                        Kv("object[]{RefPlane}", new object[] { plane }),
                    };

                    object ic = (object)part.Constructions.IntersectionCurves;
                    foreach (var a in set1.Where(x => x.Value != null))
                        foreach (var b in set2)
                        {
                            try
                            {
                                object curve = Invoke(ic, "Add", new[] { a.Value, b.Value });
                                Console.WriteLine($"OK  Add({a.Key}, {b.Key}) → status {Status(curve)}; arestas {EdgeCount(curve)}");
                                try { ((dynamic)curve).Delete(); } catch (Exception ex) { Console.WriteLine("    Delete: " + Msg(ex)); }
                            }
                            catch (Exception ex) { Console.WriteLine($"ERR Add({a.Key}, {b.Key}) → {Msg(ex)}"); }
                        }
                    return 0;
                }
                catch (Exception ex) { Console.WriteLine("FALHOU: " + Msg(ex)); return 8; }
                finally { try { part.Close(false); } catch { } }
            }
        }

        /// <summary>
        /// A cadeia inteira da 2ª passada, numa peça descartável que imita a do botão: superfície
        /// copiada SEM sólido na peça e a origem no eixo do eletrodo. Cilindro Ø20 com centro em
        /// x = −22 → a parede mais perto do eixo fica em x = −12; o resultado esperado é Ø24 × 10.
        /// </summary>
        private static int Chain(dynamic app)
        {
            dynamic part = app.Documents.Add("SolidEdge.PartDocument");
            try
            {
                try { if ((int)part.ModelingMode != 1) part.ModelingMode = 1; } catch { }
                PrimitiveModeler.Build(part, new List<Primitive>
                {
                    new Primitive { Kind = "cilindro", Name = "c", DiameterMm = 20, HeightMm = 10, PlaneIndex = 1, CenterXMm = -22 }
                });
                object lateral = null;
                dynamic faces = part.Models.Item(1).Body.Faces[1];
                for (int i = 1; i <= (int)faces.Count; i++)
                {
                    object f = faces.Item(i);
                    if (FaceGeometry.TryGetRangeMm(f, out double[] mn, out double[] mx) && mx[2] - mn[2] > 5)
                    {
                        lateral = f;
                        Console.WriteLine($"face lateral: X {mn[0]:0.##}..{mx[0]:0.##}  Y {mn[1]:0.##}..{mx[1]:0.##}  Z {mn[2]:0.##}..{mx[2]:0.##}");
                    }
                }
                object col = (object)part.Constructions.CopySurfaces;
                object surf = Invoke(col, "Add", new object[] { 1, new[] { (SolidEdgeGeometry.Face)lateral }, Type.Missing, Type.Missing });
                Console.WriteLine("CopySurface: " + Status(surf));

                try { part.Models.Item(1).ExtrudedProtrusions.Item(1).Delete(); Console.WriteLine("sólido apagado (protrusão)."); }
                catch (Exception ex) { Console.WriteLine("apagar a protrusão: " + Msg(ex)); }
                Console.WriteLine($"Models.Count = {Count(part.Models)}; CopySurfaces.Count = {Count(part.Constructions.CopySurfaces)}");

                string err = AutoEDM.Electrode.ElectrodeBuilder.RevolveCopiedSurfaces((object)part, new[] { -12.0, 0.0, 5.0 }, out double d);
                Console.WriteLine(err == null ? $"RESULTADO: OK, Ø medido {d:0.000} mm (esperado 24)" : "RESULTADO: " + err);
                Console.WriteLine($"Models.Count = {Count(part.Models)}; IntersectionCurves.Count = {Count(part.Constructions.IntersectionCurves)}; " +
                                  $"ProfileSets.Count = {Count(part.ProfileSets)}; ModelingMode = {Count2(part)}");
                string models = Count(part.Models);
                if (models != "0")
                {
                    object body = part.Models.Item(1).Body;
                    if (FaceGeometry.TryGetBodyRangeMm(body, out double[] bmn, out double[] bmx))
                        Console.WriteLine($"corpo: X {bmn[0]:0.###}..{bmx[0]:0.###}  Y {bmn[1]:0.###}..{bmx[1]:0.###}  Z {bmn[2]:0.###}..{bmx[2]:0.###}");
                }
                return err == null ? 0 : 9;
            }
            catch (Exception ex) { Console.WriteLine("FALHOU: " + Msg(ex)); return 8; }
            finally { try { part.Close(false); } catch { } }
        }

        /// <summary>
        /// Uma peça descartável por variante: cilindro → cópia da face lateral → apaga a protrusão
        /// (peça sem sólido, como a do botão) → seção pelo XZ → esboço → revolução.
        /// </summary>
        private static void RevolveVariant(dynamic app, bool separateAxis, bool relate)
        {
            string tag = $"[eixo {(separateAxis ? "SEPARADO (construção)" : "= segmento do perfil")}, pontas {(relate ? "AMARRADAS" : "soltas")}]";
            Console.WriteLine("==== " + tag);
            dynamic part = app.Documents.Add("SolidEdge.PartDocument");
            try
            {
                try { if ((int)part.ModelingMode != 1) part.ModelingMode = 1; } catch { }
                PrimitiveModeler.Build(part, new List<Primitive>
                {
                    new Primitive { Kind = "cilindro", Name = "c", DiameterMm = 20, HeightMm = 10, PlaneIndex = 1, CenterXMm = -22 }
                });
                object lateral = null;
                dynamic faces = part.Models.Item(1).Body.Faces[1];
                for (int i = 1; i <= (int)faces.Count; i++)
                {
                    object f = faces.Item(i);
                    if (FaceGeometry.TryGetRangeMm(f, out double[] mn, out double[] mx) && mx[2] - mn[2] > 5) lateral = f;
                }
                object surf = Invoke((object)part.Constructions.CopySurfaces, "Add",
                    new object[] { 1, new[] { (SolidEdgeGeometry.Face)lateral }, Type.Missing, Type.Missing });
                try { part.Models.Item(1).ExtrudedProtrusions.Item(1).Delete(); }
                catch (Exception ex) { Console.WriteLine("  apagar a protrusão: " + Msg(ex)); }
                Console.WriteLine($"  Models.Count = {Count(part.Models)}; CopySurface {Status(surf)}");

                object plane = part.RefPlanes.Item(3);   // XZ (normal −Y), confirmado nesta SE
                object face = ((dynamic)surf).Faces[1].Item(1);
                object curve = Invoke((object)part.Constructions.IntersectionCurves, "Add", new[] { face, plane });
                object edge = null;
                dynamic edges = ((dynamic)curve).Edges[1];
                for (int i = 1; i <= (int)edges.Count; i++)
                {
                    object e = edges.Item(i);
                    FaceGeometry.TryTwoPointOutMm(e, "GetEndPoints", out double[] s, out double[] en, out _);
                    if (s[0] > -20) edge = e;   // o lado x = −12
                }

                dynamic ps = part.ProfileSets.Add();
                dynamic profile = ps.Profiles.Add(plane);
                object[] inc = { edge, null };
                var mod = new ParameterModifier(2); mod[1] = true;
                ((object)profile).GetType().InvokeMember("IncludeEdge", BindingFlags.InvokeMethod, null, (object)profile, inc, new[] { mod }, null, null);
                dynamic included = profile.Lines2d.Item(1);
                double ix1, iy1, ix2, iy2;
                included.GetStartPoint(out ix1, out iy1);
                included.GetEndPoint(out ix2, out iy2);
                Console.WriteLine($"  incluída: ({ix1 * 1000:0.###}, {iy1 * 1000:0.###}) → ({ix2 * 1000:0.###}, {iy2 * 1000:0.###}) mm no esboço");

                // Ponto 3D → esboço.
                double[] P(double x, double z)
                {
                    object[] a = { x / 1000.0, 0.0, z / 1000.0, 0.0, 0.0 };
                    var m = new ParameterModifier(5); m[3] = true; m[4] = true;
                    ((object)profile).GetType().InvokeMember("Convert3DCoordinate", BindingFlags.InvokeMethod, null, (object)profile, a, new[] { m }, null, null);
                    return new[] { Convert.ToDouble(a[3]), Convert.ToDouble(a[4]) };
                }
                double[] top = P(-12, 10), axTop = P(0, 10), axBot = P(0, 0), bot = P(-12, 0);
                dynamic l1 = profile.Lines2d.AddBy2Points(top[0], top[1], axTop[0], axTop[1]);
                dynamic l2 = profile.Lines2d.AddBy2Points(axTop[0], axTop[1], axBot[0], axBot[1]);
                dynamic l3 = profile.Lines2d.AddBy2Points(axBot[0], axBot[1], bot[0], bot[1]);

                if (relate)
                {
                    // A ponta da incluída que bate com o topo (y do esboço maior/menor depende do plano).
                    bool startIsTop = Math.Abs(iy1 - top[1]) < 1e-6 && Math.Abs(ix1 - top[0]) < 1e-6;
                    int incTop = startIsTop ? 0 : 1, incBot = startIsTop ? 1 : 0;
                    TryStep(() => profile.Relations2d.AddKeypoint(included, incTop, l1, 0), "  rel incluída-topo × l1");
                    TryStep(() => profile.Relations2d.AddKeypoint(l1, 1, l2, 0), "  rel l1 × l2");
                    TryStep(() => profile.Relations2d.AddKeypoint(l2, 1, l3, 0), "  rel l2 × l3");
                    TryStep(() => profile.Relations2d.AddKeypoint(l3, 1, included, incBot), "  rel l3 × incluída-base");
                }

                object axisLine = (object)l2;
                if (separateAxis)
                {
                    double[] a1 = P(0, -1), a2 = P(0, 11);
                    axisLine = profile.Lines2d.AddBy2Points(a1[0], a1[1], a2[0], a2[1]);
                    TryStep(() => profile.ToggleConstruction(axisLine), "  eixo separado → construção");
                }
                object refAxis = profile.SetAxisOfRevolution(axisLine);
                foreach (int crit in new[] { 1 | 8192 })
                {
                    try { Console.WriteLine($"  End({crit}) = {(int)profile.End(crit)}"); } catch (Exception ex) { Console.WriteLine($"  End({crit}): {Msg(ex)}"); }
                }

                var arr = new[] { (SolidEdgePart.Profile)profile };
                var tries = new List<Tuple<string, object, string, object[]>>
                {
                    Tuple.Create("Models", (object)part.Models, "AddFiniteRevolvedProtrusionSync", new object[] { 1, arr, refAxis, 2, 2 * Math.PI }),
                    Tuple.Create("Models", (object)part.Models, "AddFiniteRevolvedProtrusionSync", new object[] { 1, arr, refAxis, 1, 2 * Math.PI }),
                    Tuple.Create("Models", (object)part.Models, "AddFiniteRevolvedProtrusion", new object[] { 1, arr, refAxis, 2, 2 * Math.PI }),
                    Tuple.Create("Models", (object)part.Models, "AddFiniteRevolvedProtrusion", new object[] { 1, arr, refAxis, 1, 2 * Math.PI }),
                    Tuple.Create("Models", (object)part.Models, "AddFiniteRevolvedProtrusion", new object[] { 1, arr, refAxis, 3, 2 * Math.PI }),
                };
                foreach (var t in tries)
                {
                    try
                    {
                        object r = Invoke(t.Item2, t.Item3, t.Item4);
                        Console.WriteLine($"  OK  {t.Item1}.{t.Item3}(lado {t.Item4[3]}) → {Status(r)}; Models.Count = {Count(part.Models)}");
                        string models = Count(part.Models);
                        if (models != "0" && FaceGeometry.TryGetBodyRangeMm((object)part.Models.Item(1).Body, out double[] bmn, out double[] bmx))
                            Console.WriteLine($"      corpo X {bmn[0]:0.###}..{bmx[0]:0.###}  Y {bmn[1]:0.###}..{bmx[1]:0.###}  Z {bmn[2]:0.###}..{bmx[2]:0.###}");
                        return;
                    }
                    catch (Exception ex) { Console.WriteLine($"  ERR {t.Item1}.{t.Item3}(lado {t.Item4[3]}) → {Msg(ex)}"); }
                }
            }
            catch (Exception ex) { Console.WriteLine("  FALHOU: " + Msg(ex)); }
            finally { try { part.Close(false); } catch { } }
        }

        private static void TryStep(Action a, string label)
        {
            try { a(); Console.WriteLine($"{label}: OK"); }
            catch (Exception ex) { Console.WriteLine($"{label}: {Msg(ex)}"); }
        }

        /// <summary>
        /// As faces pedidas (por ID) na peça ABERTA da 1ª ocorrência da montagem ativa (só leitura) e
        /// na cópia do ARQUIVO dela (CopyConstructions numa peça descartável): o ID se mantém? Se não,
        /// qual face da cópia tem a mesma área?
        /// </summary>
        private static int CompareIds(dynamic app, int[] ids)
        {
            dynamic occ = app.ActiveDocument.Occurrences.Item(1);
            string file = (string)occ.OccurrenceFileName;
            Console.WriteLine("ocorrência: " + (string)occ.Name);
            var open = new Dictionary<int, double>();
            dynamic body = occ.OccurrenceDocument.Models.Item(1).Body;
            dynamic of = body.Faces[1];
            int openCount = (int)of.Count;
            for (int i = 1; i <= openCount; i++)
            {
                dynamic f = of.Item(i);
                int id = (int)f.ID;
                if (ids.Contains(id)) { open[id] = (double)f.Area * 1e6; Console.WriteLine($"ABERTA  #{id}: área {open[id]:0.0000} mm²; tipo {GeomType(f)}"); }
            }
            Console.WriteLine($"ABERTA: {openCount} faces no corpo; IDs pedidos achados: {open.Count}/{ids.Length}");

            string diaArg = Environment.GetCommandLineArgs().Where(a => a.StartsWith("--previa=")).Select(a => a.Substring(9)).FirstOrDefault();
            if (diaArg != null)
            {
                // O botão inteiro em modo PRÉVIA: as duas passadas em peças descartáveis, nada gravado.
                var picks = new List<AutoEDM.Electrode.PickedFace>();
                for (int i = 1; i <= openCount; i++)
                {
                    dynamic f = of.Item(i);
                    if (ids.Contains((int)f.ID)) picks.Add(new AutoEDM.Electrode.PickedFace((object)f, (object)occ, (string)occ.Name));
                }
                var builder = new AutoEDM.Electrode.ElectrodeBuilder(Connector);
                AutoEDM.Electrode.ManualElectrodeResult r = builder.CreateInterpolationElectrode(
                    (object)app.ActiveDocument, new AutoEDM.Model.ElectrodeParams(), picks,
                    double.Parse(diaArg, CultureInfo.InvariantCulture), previewOnly: true);
                Console.WriteLine("PRÉVIA: " + r.Message);
                return 0;
            }
            if (Environment.GetCommandLineArgs().Contains("--estilos"))
            {
                // SÓ LEITURA: o ID das faces antes e depois de FaceLookReader.ReadFeatureStyles.
                var keep = new List<object>();
                for (int i = 1; i <= openCount; i++) { dynamic f = of.Item(i); if (ids.Contains((int)f.ID)) keep.Add((object)f); }
                Console.WriteLine("antes:  " + string.Join(", ", keep.Select(f => "#" + IdOf(f) + " " + GeomType(f))));
                var styles = AutoEDM.Selection.FaceLookReader.ReadFeatureStyles((object)occ.OccurrenceDocument);
                Console.WriteLine($"ReadFeatureStyles: {styles.Count} face(s) com estilo de feature");
                Console.WriteLine("depois: " + string.Join(", ", keep.Select(f => "#" + IdOf(f) + " " + GeomType(f))));
                foreach (object f in keep) AutoEDM.Selection.FaceLookReader.Resolve(f, styles);
                Console.WriteLine("após Resolve: " + string.Join(", ", keep.Select(f => "#" + IdOf(f))));
                return 0;
            }
            if (Environment.GetCommandLineArgs().Contains("--copiador"))
            {
                // O copiador do Core, como o botão o chama, com as faces da peça aberta.
                var src = new AutoEDM.Electrode.BurnSource
                {
                    Label = (string)occ.Name, FilePath = file, SourceDocument = occ.OccurrenceDocument,
                    Placement = AutoEDM.Assembly.OccurrenceTransform.Identity(),
                };
                for (int i = 1; i <= openCount; i++)
                {
                    dynamic f = of.Item(i);
                    if (ids.Contains((int)f.ID)) src.Faces.Add((object)f);
                }
                dynamic doc = app.Documents.Add("SolidEdge.PartDocument");
                try
                {
                    try { if ((int)doc.ModelingMode != 1) doc.ModelingMode = 1; } catch { }
                    var res = AutoEDM.Electrode.BurnSurfaceCopier.Copy((object)doc, new[] { src });
                    Console.WriteLine("COPIADOR: " + res.Summary());
                }
                finally { try { doc.Close(false); } catch { } }
                return 0;
            }

            dynamic part = app.Documents.Add("SolidEdge.PartDocument");
            try
            {
                try { if ((int)part.ModelingMode != 1) part.ModelingMode = 1; } catch { }
                object cs = Type.Missing;
                if (Environment.GetCommandLineArgs().Contains("--cs"))
                    cs = Invoke((object)part.CoordinateSystems, "AddByMatrix", new object[] { AutoEDM.Assembly.OccurrenceTransform.Identity().ToMatrix() });
                object cc = Invoke((object)part.Constructions.CopyConstructions, "Add", new object[]
                    { file, 1.0, 1.0, 1.0, Type.Missing, Type.Missing, cs, true, false, false, false, Type.Missing, Type.Missing, true });
                dynamic cf = ((dynamic)cc).Faces[1];
                int n = (int)cf.Count;
                Console.WriteLine($"CÓPIA: status {Status(cc)}; {n} faces");
                for (int i = 1; i <= n; i++)
                {
                    dynamic f = cf.Item(i);
                    int id = (int)f.ID;
                    double area = (double)f.Area * 1e6;
                    bool idHit = ids.Contains(id);
                    bool areaHit = open.Values.Any(a => Math.Abs(a - area) < 1e-3);
                    if (idHit || areaHit)
                        Console.WriteLine($"CÓPIA   #{id}: área {area:0.0000} mm²; tipo {GeomType(f)}{(idHit ? "  ← mesmo ID" : "")}{(areaHit ? "  ← mesma área" : "")}");
                }
                return 0;
            }
            catch (Exception ex) { Console.WriteLine("FALHOU: " + Msg(ex)); return 8; }
            finally { try { part.Close(false); } catch { } }
        }

        private static string IdOf(object face)
        {
            try { return ((int)((dynamic)face).ID).ToString(); } catch (Exception ex) { return "? " + Msg(ex); }
        }

        private static string GeomType(dynamic face)
        {
            try { return ((int)face.Geometry.Type).ToString(); } catch (Exception ex) { return "? " + Msg(ex); }
        }

        private static string Count2(dynamic doc)
        {
            try { return ((int)doc.ModelingMode).ToString(); } catch (Exception ex) { return "? " + Msg(ex); }
        }

        private static string Count(dynamic collection)
        {
            try { return ((int)collection.Count).ToString(); } catch (Exception ex) { return "? " + Msg(ex); }
        }

        private static KeyValuePair<string, object> Kv(string k, object v) => new KeyValuePair<string, object>(k, v);

        private static string EdgeCount(object curve)
        {
            try { return ((int)((dynamic)curve).Edges[1].Count).ToString(); } catch (Exception ex) { return "? " + Msg(ex); }
        }

        private static object Invoke(object target, string method, object[] args) =>
            target.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, target, args, null, CultureInfo.InvariantCulture, null);

        private static string Status(object feature)
        {
            try { return ((uint)(int)((dynamic)feature).Status).ToString(); } catch (Exception ex) { return "? " + Msg(ex); }
        }

        private static string Msg(Exception ex) => ex.GetBaseException().GetType().Name + ": " + ex.GetBaseException().Message;
    }
}
