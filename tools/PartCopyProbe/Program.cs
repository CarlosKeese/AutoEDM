using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AutoEDM.Com;
using AutoEDM.Modeling;
using AutoEDM.Selection;

namespace AutoEDM.Tools.PartCopyProbe
{
    /// <summary>
    /// Sonda da CÓPIA DE PEÇA vinculada (Desenho de processo, Carlos 2026-10-22). Só arquivos
    /// descartáveis na pasta temporária: A (cilindro) → B ordenada com <c>Models.AddCopiedPart(A)</c>
    /// → A alterada e salva → B reaberta: o vínculo trouxe a alteração?
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            if (Array.IndexOf(args, "--processo") >= 0) return Process();
            if (Array.IndexOf(args, "--cores") >= 0) return Colors();
            if (Array.IndexOf(args, "--templates") >= 0) return Templates();
            string dir = Path.Combine(Path.GetTempPath(), "AutoEDM_PartCopyProbe_" + DateTime.Now.ToString("HHmmss"));
            Directory.CreateDirectory(dir);
            string a = Path.Combine(dir, "A.par"), b = Path.Combine(dir, "B_CDU.par");
            Console.WriteLine("pasta: " + dir);

            using (var connector = new SolidEdgeConnector())
            {
                dynamic app = connector.Connect(startIfNotRunning: false, makeVisible: false);
                if (app == null) { Console.WriteLine("Nenhuma Solid Edge aberta."); return 1; }
                try
                {
                    // A: cilindro Ø20 × 10.
                    dynamic pa = app.Documents.Add("SolidEdge.PartDocument");
                    PrimitiveModeler.Build(pa, new List<Primitive> { new Primitive { Kind = "cilindro", Name = "a", DiameterMm = 20, HeightMm = 10, PlaneIndex = 1 } });
                    pa.SaveAs(a); pa.Close();

                    // B: ordenada, cópia de A.
                    dynamic pb = app.Documents.Add("SolidEdge.PartDocument");
                    try { pb.ModelingMode = 2; } catch (Exception ex) { Console.WriteLine("ModelingMode=2: " + Msg(ex)); }
                    Console.WriteLine($"B ModelingMode = {(int)pb.ModelingMode}");
                    object model = pb.Models.AddCopiedPart(a);
                    Console.WriteLine($"AddCopiedPart: {(model == null ? "null" : "Model")}; Models.Count = {(int)pb.Models.Count}");
                    dynamic cp = pb.Models.Item(1).CopiedParts.Item(1);
                    Console.WriteLine($"CopiedPart: Status {(uint)(int)cp.Status}; FileName {(string)cp.FileName}; IsUpToDate {Try(() => cp.IsUpToDate)}; LinkMaterial {Try(() => cp.LinkMaterial)}");
                    Console.WriteLine("B corpo: " + Range(pb));
                    pb.SaveAs(b); pb.Close();

                    // A alterada: cilindro Ø10 × 5 em cima.
                    pa = app.Documents.Open(a);
                    PrimitiveModeler.Build(pa, new List<Primitive> { new Primitive { Kind = "cilindro", Name = "topo", DiameterMm = 10, HeightMm = 5, PlaneIndex = 1, LiftMm = 10 } });
                    Console.WriteLine("A depois da alteração: " + Range(pa));
                    pa.Save(); pa.Close();

                    // B reaberta.
                    pb = app.Documents.Open(b);
                    cp = pb.Models.Item(1).CopiedParts.Item(1);
                    Console.WriteLine($"B reaberta: {Range(pb)}; IsUpToDate {Try(() => cp.IsUpToDate)}");
                    try { cp.Update(); Console.WriteLine("Update(): OK"); } catch (Exception ex) { Console.WriteLine("Update(): " + Msg(ex)); }
                    Console.WriteLine($"B depois do Update: {Range(pb)}; IsUpToDate {Try(() => cp.IsUpToDate)}");
                    pb.Close(false);
                    return 0;
                }
                catch (Exception ex) { Console.WriteLine("FALHOU: " + Msg(ex)); return 8; }
            }
        }

        /// <summary>
        /// O ProcessDrawingBuilder de verdade num molde FALSO na pasta temporária: 1ª rodada só CDU
        /// (cria _CDU + _WP.asm), 2ª rodada CDU+CDT (cria só _CDT e acrescenta na montagem existente).
        /// </summary>
        private static int Process()
        {
            string root = Path.Combine(Path.GetTempPath(), "AutoEDM_ProcessProbe_" + DateTime.Now.ToString("HHmmss"), "MD-TESTE");
            Directory.CreateDirectory(Path.Combine(root, "02 - PROCESSO"));
            string a = Path.Combine(root, "99999.101.par");
            Console.WriteLine("molde falso: " + root);

            using (var connector = new SolidEdgeConnector())
            {
                dynamic app = connector.Connect(startIfNotRunning: false, makeVisible: false);
                if (app == null) { Console.WriteLine("Nenhuma Solid Edge aberta."); return 1; }
                try
                {
                    dynamic pa = app.Documents.Add("SolidEdge.PartDocument");
                    PrimitiveModeler.Build(pa, new List<Primitive> { new Primitive { Kind = "cilindro", Name = "a", DiameterMm = 20, HeightMm = 10, PlaneIndex = 1 } });
                    Console.WriteLine("original pintada: " + PaintFirstFace(pa, "ProbeAzul", System.Drawing.Color.FromArgb(0, 127, 255)));
                    Console.WriteLine("original: " + Tally(pa.Models.Item(1).Body));
                    pa.SaveAs(a); pa.Close();

                    foreach (var m in new[] { AutoEDM.Mold.ProcessMachine.Cdu, AutoEDM.Mold.ProcessMachine.Cdu | AutoEDM.Mold.ProcessMachine.Cdt })
                    {
                        var plan = AutoEDM.Mold.ProcessDrawingBuilder.Plan(a, m);
                        Console.WriteLine($"--- {m}: criar [{string.Join(", ", System.Linq.Enumerable.Select(plan.ToCreate, c => Path.GetFileName(c.Path)))}], " +
                                          $"montagem {(plan.AssemblyExists ? "EXISTE" : "nova")}, Ready {plan.Ready}");
                        bool ok = AutoEDM.Mold.ProcessDrawingBuilder.Build((object)app, plan, out string err);
                        Console.WriteLine(ok ? "Build OK" : "Build FALHOU: " + err);
                        dynamic asm = app.ActiveDocument;
                        Console.WriteLine($"  _WP.asm FaceStyles {Try(() => asm.FaceStyles.Count)} (PanFlight 86, ISO 55)");
                        dynamic occs = asm.Occurrences;
                        for (int i = 1; i <= (int)occs.Count; i++)
                            Console.WriteLine($"  ocorrência: {Path.GetFileName((string)occs.Item(i).OccurrenceFileName)}");
                        asm.Close(false);
                    }

                    foreach (string suffix in new[] { "_CDU", "_CDT" })
                    {
                        dynamic p = app.Documents.Open(Path.Combine(root, "02 - PROCESSO", "99999.101", "99999.101" + suffix + ".par"));
                        dynamic cp = p.Models.Item(1).CopiedParts.Item(1);
                        Console.WriteLine($"{suffix}: ModelingMode {(int)p.ModelingMode}; Status {(uint)(int)cp.Status}; vinculada a {Path.GetFileName((string)cp.FileName)}; " +
                                          $"LinkMaterial {Try(() => cp.LinkMaterial)}; corpo {Range(p)}");
                        Console.WriteLine($"{suffix}: construções {Try(() => (int)p.Constructions.CopyConstructions.Count)}; {Tally(p.Models.Item(1).Body)}");
                        Console.WriteLine($"{suffix}: template — FaceStyles {Try(() => p.FaceStyles.Count)} (PanFlight 84, ISO 60); " +
                                          $"Stock_X {Try(() => p.Variables.Item("Stock_X").Formula != null)}");
                        p.Close(false);
                    }

                    // A pintura sobrevive quando o vínculo traz geometria nova?
                    pa = app.Documents.Open(a);
                    PrimitiveModeler.Build(pa, new List<Primitive> { new Primitive { Kind = "cilindro", Name = "topo", DiameterMm = 10, HeightMm = 5, PlaneIndex = 1, LiftMm = 10 } });
                    pa.Save(); pa.Close();
                    dynamic pc = app.Documents.Open(Path.Combine(root, "02 - PROCESSO", "99999.101", "99999.101_CDU.par"));
                    Console.WriteLine($"_CDU depois do ressalto na original: corpo {Range(pc)}; {Tally(pc.Models.Item(1).Body)}");
                    pc.Close(false);
                    return 0;
                }
                catch (Exception ex) { Console.WriteLine("FALHOU: " + Msg(ex)); return 8; }
            }
        }

        /// <summary>
        /// As CORES da cópia (Carlos 2026-10-27: a peça de processo tem de trazer as cores da original,
        /// vinculadas). A com uma face pintada → B1 = <c>Models.AddCopiedPart</c> (sem CopyColors na
        /// typelib) e B2 = <c>CopyConstructions.Add(…, CopyColors=true)</c> + <c>Models.AddByConstruction</c>
        /// → a cor de A trocada e salva → B1/B2 reabertas: a cor nova chegou?
        /// </summary>
        private static int Colors()
        {
            string dir = Path.Combine(Path.GetTempPath(), "AutoEDM_PartCopyColors_" + DateTime.Now.ToString("HHmmss"));
            Directory.CreateDirectory(dir);
            string a = Path.Combine(dir, "A.par"), b1 = Path.Combine(dir, "B1_copiedpart.par"), b2 = Path.Combine(dir, "B2_construction.par");
            Console.WriteLine("pasta: " + dir);

            using (var connector = new SolidEdgeConnector())
            {
                dynamic app = connector.Connect(startIfNotRunning: false, makeVisible: false);
                if (app == null) { Console.WriteLine("Nenhuma Solid Edge aberta."); return 1; }
                try
                {
                    dynamic pa = app.Documents.Add("SolidEdge.PartDocument");
                    PrimitiveModeler.Build(pa, new List<Primitive> { new Primitive { Kind = "cilindro", Name = "a", DiameterMm = 20, HeightMm = 10, PlaneIndex = 1 } });
                    Console.WriteLine("A pintada: " + PaintFirstFace(pa, "ProbeAzul", System.Drawing.Color.FromArgb(0, 127, 255)));
                    Console.WriteLine("A: " + Tally(pa.Models.Item(1).Body));
                    pa.SaveAs(a); pa.Close();

                    // B1: Cópia de peça como hoje no botão.
                    dynamic p1 = app.Documents.Add("SolidEdge.PartDocument");
                    try { p1.ModelingMode = 2; } catch (Exception ex) { Console.WriteLine("B1 ModelingMode=2: " + Msg(ex)); }
                    p1.Models.AddCopiedPart(a);
                    dynamic cp = p1.Models.Item(1).CopiedParts.Item(1);
                    try { cp.LinkMaterial = true; } catch (Exception ex) { Console.WriteLine("B1 LinkMaterial: " + Msg(ex)); }
                    // Nomes fora da typelib: o IDispatch da feature conhece algum?
                    foreach (string name in new[] { "CopyColors", "LinkColors", "LinkColor", "CopyFaceStyles", "LinkFaceStyles", "CopyColor", "InheritColors" })
                    {
                        try
                        {
                            ((object)cp).GetType().InvokeMember(name, System.Reflection.BindingFlags.SetProperty, null, (object)cp, new object[] { true });
                            Console.WriteLine($"B1 CopiedPart.{name} = true: ACEITO");
                        }
                        catch (Exception ex) { Console.WriteLine($"B1 CopiedPart.{name}: {Msg(ex)}"); }
                    }
                    try { cp.Update(); } catch (Exception ex) { Console.WriteLine("B1 Update(): " + Msg(ex)); }
                    Console.WriteLine($"B1 CopiedPart status {(uint)(int)cp.Status}: {Tally(p1.Models.Item(1).Body)}");
                    p1.SaveAs(b1); p1.Close();

                    // B2: cópia de construção com CopyColors → corpo de projeto por AddByConstruction.
                    dynamic p2 = app.Documents.Add("SolidEdge.PartDocument");
                    try { p2.ModelingMode = 2; } catch (Exception ex) { Console.WriteLine("B2 ModelingMode=2: " + Msg(ex)); }
                    object cs = Invoke((object)p2.CoordinateSystems, "AddByMatrix",
                        new double[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 });
                    object cc = Invoke((object)p2.Constructions.CopyConstructions, "Add",
                        a, 1.0, 1.0, 1.0, Type.Missing, Type.Missing, cs,
                        true, false, false, false, Type.Missing, Type.Missing, true);
                    Console.WriteLine($"B2 CopyConstruction status {Try(() => (uint)(int)((dynamic)cc).Status)}; CopyColors {Try(() => ((dynamic)cc).CopyColors)}: {TallyFaces(((dynamic)cc).Faces[1])}");
                    object model = null;
                    try { model = Invoke((object)p2.Models, "AddByConstruction", cc); Console.WriteLine("B2 AddByConstruction(cc): OK"); }
                    catch (Exception ex) { Console.WriteLine("B2 AddByConstruction(cc): " + Msg(ex)); }
                    if (model == null)
                    {
                        try { model = Invoke((object)p2.Models, "AddByConstruction", (object)((dynamic)cc).Body); Console.WriteLine("B2 AddByConstruction(cc.Body): OK"); }
                        catch (Exception ex) { Console.WriteLine("B2 AddByConstruction(cc.Body): " + Msg(ex)); }
                    }
                    Console.WriteLine($"B2 Models.Count {(int)p2.Models.Count}" + ((int)p2.Models.Count > 0 ? ": " + Tally(p2.Models.Item(1).Body) : ""));
                    p2.SaveAs(b2); p2.Close();

                    // A com a cor trocada.
                    pa = app.Documents.Open(a);
                    Console.WriteLine("A repintada: " + PaintFirstFace(pa, "ProbeVerde", System.Drawing.Color.FromArgb(0, 200, 0)));
                    if (Array.IndexOf(Environment.GetCommandLineArgs(), "--geometria") >= 0)
                    {
                        // Força o recálculo do vínculo: a cor só acompanha quando a geometria muda?
                        PrimitiveModeler.Build(pa, new List<Primitive> { new Primitive { Kind = "cilindro", Name = "topo", DiameterMm = 10, HeightMm = 5, PlaneIndex = 1, LiftMm = 10 } });
                        Console.WriteLine("A com ressalto: " + Tally(pa.Models.Item(1).Body));
                    }
                    pa.Save(); pa.Close();

                    p1 = app.Documents.Open(b1);
                    cp = p1.Models.Item(1).CopiedParts.Item(1);
                    try { cp.Update(); } catch (Exception ex) { Console.WriteLine("B1 Update(): " + Msg(ex)); }
                    Console.WriteLine($"B1 reaberta: {Tally(p1.Models.Item(1).Body)}");
                    p1.Close(false);

                    p2 = app.Documents.Open(b2);
                    dynamic cc2 = p2.Constructions.CopyConstructions.Item(1);
                    try { cc2.Update(); } catch (Exception ex) { Console.WriteLine("B2 Update(): " + Msg(ex)); }
                    Console.WriteLine($"B2 reaberta: construção {TallyFaces(cc2.Faces[1])}" +
                                      ((int)p2.Models.Count > 0 ? "; modelo " + Tally(p2.Models.Item(1).Body) : ""));
                    p2.Close(false);
                    return 0;
                }
                catch (Exception ex) { Console.WriteLine("FALHOU: " + Msg(ex)); return 8; }
            }
        }

        /// <summary>
        /// Templates da casa (Carlos 2026-10-27: peças novas têm de seguir a iluminação e os estilos do
        /// PanFlight). Cria do template da biblioteca e do padrão da SE, compara e fecha sem salvar.
        /// </summary>
        private static int Templates()
        {
            const string dir = @"W:\01 - Biblioteca\Padrões\Preferences\Templates\PanFlight";
            using (var connector = new SolidEdgeConnector())
            {
                dynamic app = connector.Connect(startIfNotRunning: false, makeVisible: false);
                if (app == null) { Console.WriteLine("Nenhuma Solid Edge aberta."); return 1; }
                foreach (int t in new[] { 1, 3 }) Console.WriteLine($"GetDefaultTemplatePath({t}) = {Try(() => app.GetDefaultTemplatePath(t))}");
                var cases = new[]
                {
                    Tuple.Create("SolidEdge.PartDocument", Path.Combine(dir, "PanFlight Part.par")),
                    Tuple.Create("SolidEdge.PartDocument", (string)null),
                    Tuple.Create("SolidEdge.AssemblyDocument", Path.Combine(dir, "PanFlight Assembly.asm")),
                    Tuple.Create("SolidEdge.AssemblyDocument", (string)null),
                };
                foreach (var c in cases)
                {
                    string label = $"{c.Item1.Replace("SolidEdge.", "")} {(c.Item2 == null ? "(padrão da SE)" : Path.GetFileName(c.Item2))}";
                    dynamic doc = null;
                    try
                    {
                        doc = c.Item2 == null ? app.Documents.Add(c.Item1) : app.Documents.Add(c.Item1, c.Item2);
                        dynamic d = doc;
                        Console.WriteLine($"{label}: ModelingMode {Try(() => d.ModelingMode)}; Models {Try(() => d.Models.Count)}; " +
                                          $"Occurrences {Try(() => d.Occurrences.Count)}; FaceStyles {Try(() => d.FaceStyles.Count)}; " +
                                          $"Variables {Try(() => d.Variables.Count)}; Material {Try(() => d.Properties.Item("MechanicalModeling").Item("Material").Value)}");
                        var names = new List<string>();
                        try { for (int i = 1; i <= (int)d.Variables.Count; i++) names.Add(Try(() => d.Variables.Item(i).DisplayName) + "=" + Try(() => d.Variables.Item(i).Formula)); }
                        catch (Exception ex) { names.Add(Msg(ex)); }
                        if (names.Count > 0) Console.WriteLine("  variáveis: " + string.Join("; ", names));
                    }
                    catch (Exception ex) { Console.WriteLine($"{label}: FALHOU {Msg(ex)}"); }
                    finally { if (doc != null) { try { doc.Close(false); } catch { } } }
                }
                return 0;
            }
        }

        /// <summary>Pinta a 1ª face do corpo com um estilo novo (nome único da sonda).</summary>
        private static string PaintFirstFace(dynamic doc, string style, System.Drawing.Color color)
        {
            object face = doc.Models.Item(1).Body.Faces[1].Item(1);
            return AutoEDM.Electrode.FaceColorPainter.PaintWithNamedStyle(doc, new List<object> { face }, new[] { style }, style, color) ?? "nada pintado";
        }

        private static string Tally(dynamic body)
        {
            string bodyStyle = Try(() => FaceLookReader.StyleName((object)body.Style));
            return $"Body.Style {bodyStyle}; faces {TallyFaces(body.Faces[1])}";
        }

        /// <summary>Histograma "estilo (R,G,B) × n" das faces, pelo estilo POR FACE.</summary>
        private static string TallyFaces(dynamic faces)
        {
            var count = new SortedDictionary<string, int>();
            int n = (int)faces.Count;
            for (int i = 1; i <= n; i++)
            {
                FaceLook look = FaceLookReader.Resolve((object)faces.Item(i), null);
                string key = look == null ? "(sem estilo)" : $"{look.StyleName} {(look.Color is System.Drawing.Color c ? $"({c.R},{c.G},{c.B})" : "")}";
                key += " #" + Try(() => (int)faces.Item(i).ID);
                count[key] = count.TryGetValue(key, out int k) ? k + 1 : 1;
            }
            return string.Join("; ", System.Linq.Enumerable.Select(count, kv => $"{kv.Key} ×{kv.Value}"));
        }

        private static object Invoke(object target, string method, params object[] args) =>
            target.GetType().InvokeMember(method, System.Reflection.BindingFlags.InvokeMethod, null, target, args, null, CultureInfo.InvariantCulture, null);

        private static string Range(dynamic doc)
        {
            try
            {
                object body = doc.Models.Item(1).Body;
                return FaceGeometry.TryGetBodyRangeMm(body, out double[] mn, out double[] mx)
                    ? $"X {mn[0]:0.##}..{mx[0]:0.##}  Y {mn[1]:0.##}..{mx[1]:0.##}  Z {mn[2]:0.##}..{mx[2]:0.##}" : "sem caixa";
            }
            catch (Exception ex) { return "? " + Msg(ex); }
        }

        private static string Try(Func<object> f)
        {
            try { return Convert.ToString(f(), CultureInfo.InvariantCulture); } catch (Exception ex) { return "? " + Msg(ex); }
        }

        private static string Msg(Exception ex) => ex.GetBaseException().GetType().Name + ": " + ex.GetBaseException().Message;
    }
}
