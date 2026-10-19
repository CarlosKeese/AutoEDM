using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoEDM.Assembly;
using AutoEDM.Com;
using AutoEDM.Modeling;
using AutoEDM.Selection;

namespace AutoEDM.Tools.ElectrodeCopyProbe
{
    /// <summary>
    /// Sonda externa: levar as faces de queima para DENTRO da peça do eletrodo sem edição em
    /// contexto, pela rota que a rodada 2 do inter-part provou (2026-09-11): CopyConstructions.Add
    /// por ARQUIVO (traz o corpo da cavidade com as cores) → CopySurfaces INTRA-peça sobre a cópia.
    ///
    /// O que ainda não se sabe e esta sonda mede: onde a cópia cai (com e sem CoordinateSystem),
    /// se os IDs das faces se mantêm, se a cor chega na superfície e se a cópia do corpo pode ser
    /// solta/apagada depois. Tudo em arquivos DESCARTÁVEIS na pasta temporária; nada do usuário.
    /// </summary>
    internal static class Program
    {
        private const string Style = "EROS. 1,6";

        [STAThread]
        private static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            using (var connector = new SolidEdgeConnector())
            {
                dynamic app = connector.Connect(startIfNotRunning: false, makeVisible: false);
                if (app == null) { Console.WriteLine("Nenhuma Solid Edge aberta."); return 1; }

                if (args.Any(a => a == "--inspecionar")) return Inspect(app);
                if (args.Any(a => a == "--superficie")) return DumpSurface(app);
                if (args.Any(a => a == "--teste-array-dispatch"))
                {
                    // Peça DESCARTÁVEL: bloco → 2 CopySurfaces de faces vizinhas → StitchSurfaces.Add com
                    // o array de IDispatch. Prova o marshaling antes de usá-lo na peça do usuário.
                    dynamic part = app.Documents.Add("SolidEdge.PartDocument");
                    try
                    {
                        try { if ((int)part.ModelingMode != 1) part.ModelingMode = 1; } catch { }
                        PrimitiveModeler.Build(part, new List<Primitive> { new Primitive { Kind = "caixa", Name = "b", SizeXMm = 20, SizeYMm = 10, HeightMm = 5, PlaneIndex = 1 } });
                        dynamic all = part.Models.Item(1).Body.Faces[1];
                        var boxes = new List<(object f, double[] mn, double[] mx)>();
                        for (int i = 1; i <= (int)all.Count; i++)
                        {
                            object f = all.Item(i);
                            if (FaceGeometry.TryGetRangeMm(f, out double[] mn, out double[] mx)) boxes.Add((f, mn, mx));
                        }
                        double zTop = boxes.Max(x => x.mn[2]), xMax = boxes.Max(x => x.mn[0]);
                        var top = boxes.Where(x => x.mn[2] > zTop - 0.01).Select(x => x.f).ToList();
                        var side = boxes.Where(x => x.mn[0] > xMax - 0.01).Select(x => x.f).ToList();
                        Console.WriteLine($"faces: {boxes.Count}; topo {top.Count}; lateral {side.Count}");
                        object Copy(List<object> fs)
                        {
                            var typed = fs.Select(f => (SolidEdgeGeometry.Face)f).ToArray();
                            object col = (object)part.Constructions.CopySurfaces;
                            return Invoke(col, "Add", new object[] { typed.Length, typed, Type.Missing, Type.Missing });
                        }
                        // Relê as faces antes de CADA cópia: a 1ª reconstrói o modelo e envelhece as referências.
                        List<object> Fresh(Func<double[], double[], bool> pick)
                        {
                            var r = new List<object>();
                            dynamic fs = part.Models.Item(1).Body.Faces[1];
                            for (int i = 1; i <= (int)fs.Count; i++)
                            {
                                object f = fs.Item(i);
                                if (FaceGeometry.TryGetRangeMm(f, out double[] mn, out double[] mx) && pick(mn, mx)) r.Add(f);
                            }
                            return r;
                        }
                        object a = Copy(Fresh((mn, mx) => mn[2] > zTop - 0.01));
                        Console.WriteLine("1ª CopySurface: " + Status(a));
                        object b = Copy(Fresh((mn, mx) => mn[0] > xMax - 0.01));
                        Console.WriteLine($"CopySurfaces: {Status(a)} / {Status(b)}");
                        AutoEDM.Com.IDispatchItem[] arr;
                        try { arr = AutoEDM.Com.DispatchArray.From(new[] { a, b }); Console.WriteLine("cast p/ IDispatchItem: OK"); }
                        catch (Exception cx) { Console.WriteLine("cast p/ IDispatchItem FALHOU: " + Msg(cx)); throw; }
                        object TryStitch(System.Array sarr, string label)
                        {
                            try
                            {
                                object[] cargs = { sarr.Length, sarr, true, Type.Missing };
                                var mod = new ParameterModifier(4); mod[1] = true;
                                object stCol = (object)part.Constructions.StitchSurfaces;
                                object st = stCol.GetType().InvokeMember("Add", BindingFlags.InvokeMethod, null, stCol, cargs, new[] { mod }, CultureInfo.InvariantCulture, null);
                                Console.WriteLine($"Stitch [{label}]: OK; Status={Status(st)}; faces={(int)((dynamic)st).Faces[1].Count}");
                                return st;
                            }
                            catch (Exception sx) { Console.WriteLine($"Stitch [{label}] FALHOU: {Msg(sx)}"); return null; }
                        }
                        if (TryStitch(arr, "IDispatch de CopySurface") != null) return 0;
                        try { if (TryStitch(new[] { (SolidEdgePart.CopySurface)a, (SolidEdgePart.CopySurface)b }, "PIA CopySurface[]") != null) return 0; }
                        catch (Exception cx) { Console.WriteLine("cast PIA CopySurface: " + Msg(cx)); }
                        object bodyA = ((dynamic)a).Faces[1].Item(1).Body, bodyB = ((dynamic)b).Faces[1].Item(1).Body;
                        if (TryStitch(AutoEDM.Com.DispatchArray.From(new[] { bodyA, bodyB }), "IDispatch de Body") != null) return 0;
                        try { if (TryStitch(new[] { (SolidEdgeGeometry.Body)bodyA, (SolidEdgeGeometry.Body)bodyB }, "PIA Body[]") != null) return 0; }
                        catch (Exception cx) { Console.WriteLine("cast PIA Body: " + Msg(cx)); }
                        return 8;
                    }
                    catch (Exception ex) { Console.WriteLine("FALHOU: " + Msg(ex)); return 8; }
                    finally { try { part.Close(false); } catch { } }
                }
                string onActive = args.Where(a => a.StartsWith("--base-e-unir=")).Select(a => a.Substring(14)).FirstOrDefault();
                if (onActive != null)
                {
                    // ESCREVE na peça ativa — só com o consentimento do Carlos (2026-10-19: "deixei uma
                    // peça no jeito ... já salvei"). Confere o nome antes de tocar em qualquer coisa.
                    dynamic d = app.ActiveDocument;
                    if ((int)d.Type != 1 || !string.Equals((string)d.Name, onActive, StringComparison.OrdinalIgnoreCase))
                    { Console.WriteLine($"Peça ativa é '{(string)d.Name}', não '{onActive}' — nada feito."); return 6; }
                    foreach (string leftover in args.Where(a => a.StartsWith("--apagar=")).Select(a => a.Substring(9)))
                    {
                        // Só sobras que ESTA sonda criou numa rodada anterior, apagadas pelo nome exato.
                        bool done = false;
                        foreach (string coll in new[] { "CopySurfaces", "SurfaceByBoundaries" })
                        {
                            dynamic c = ((object)d.Constructions).GetType().InvokeMember(coll, BindingFlags.GetProperty, null, (object)d.Constructions, null);
                            for (int i = (int)c.Count; i >= 1 && !done; i--)
                            {
                                dynamic it = c.Item(i);
                                if ((string)it.Name != leftover) continue;
                                it.Delete(); done = true;
                                Console.WriteLine($"apagado: {leftover} ({coll})");
                            }
                        }
                        if (!done) Console.WriteLine($"não achei '{leftover}' — nada apagado.");
                    }
                    var b = new AutoEDM.Electrode.SurfaceBlockBuilder();
                    var opt = new AutoEDM.Electrode.BlockOverSurfacesOptions();
                    if (!args.Any(a => a == "--so-unir"))
                    {
                        var built = b.Build(d, opt, preview: false);
                        Console.WriteLine($"CRIAR BASE: bloco={built.BlockCreated} faixa={built.BandCreated} fixação={built.FixationApplied}");
                        foreach (string w in built.Warnings) Console.WriteLine("  ⚠ " + w);
                    }
                    var res = b.UniteSurfacesToBlock(app.ActiveDocument, new AutoEDM.Electrode.BlockOverSurfacesOptions());
                    Console.WriteLine($"UNIR: unida={res.SurfacesUnited}; regiões fechadas={res.SideGapsPatched}");
                    foreach (string w in res.Warnings) Console.WriteLine("  ⚠ " + w);
                    return res.SurfacesUnited ? 0 : 7;
                }
                if (args.Any(a => a == "--recursos"))
                {
                    // SÓ LÊ: a árvore da peça ativa, com o status de cada recurso.
                    dynamic d = app.ActiveDocument;
                    if ((int)d.Type == 3)
                    {
                        dynamic oc = d.Occurrences;
                        for (int i = 1; i <= (int)oc.Count; i++)
                        {
                            dynamic o = oc.Item(i);
                            string on = (string)o.Name;
                            string st = ""; foreach (string p in new[] { "Visible", "IsDisplayed", "Status" }) try { st += $" {p}={((object)o).GetType().InvokeMember(p, BindingFlags.GetProperty, null, (object)o, null)}"; } catch { }
                            Console.WriteLine($"ocorrência {on}:{st}");
                            if (on.IndexOf("_EE01", StringComparison.OrdinalIgnoreCase) >= 0) d = o.OccurrenceDocument;
                        }
                    }
                    Console.WriteLine($"PEÇA: {(string)d.Name}; FullName={(string)d.FullName}; Dirty={(bool)d.Dirty}");
                    foreach (string p in new[] { "InPlaceActivated" })
                        try { Console.WriteLine($"  {p} = {((object)d).GetType().InvokeMember(p, BindingFlags.GetProperty, null, (object)d, null)}"); } catch (Exception ex) { Console.WriteLine($"  {p}: {Msg(ex)}"); }
                    try { Console.WriteLine($"  Models.Count={(int)d.Models.Count}"); } catch (Exception ex) { Console.WriteLine("  Models: " + Msg(ex)); }
                    dynamic feats = d.DesignEdgebarFeatures;
                    for (int i = 1; i <= (int)feats.Count; i++)
                    {
                        dynamic f = feats.Item(i);
                        string nm = "?"; try { nm = (string)f.Name; } catch { }
                        Console.WriteLine($"  [{i}] {nm}  Status={Status((object)f)}");
                    }
                    foreach (dynamic w in app.Windows) try { Console.WriteLine($"janela: {(string)w.Caption}"); } catch { }
                    return 0;
                }
                string shot = args.Where(a => a.StartsWith("--imagem=")).Select(a => a.Substring(9)).FirstOrDefault();
                if (shot != null)
                {
                    // SÓ LÊ: salva a vista da janela ativa como está (nada de girar/ajustar a câmera).
                    dynamic w = app.ActiveWindow;
                    Console.WriteLine($"janela: {(string)w.Caption}; doc ativo {(string)app.ActiveDocument.Name}");
                    w.View.SaveAsImage(shot);
                    Console.WriteLine("imagem: " + shot);
                    return 0;
                }

                string dir = Path.Combine(Path.GetTempPath(), "claude", "u--Code-AutoEDM",
                    "f1d6bd47-5bee-4af4-b7eb-39eecfdbf7e3", "scratchpad", "eletrodo");
                Directory.CreateDirectory(dir);
                string cavPath = BuildCavity(app, dir);
                if (cavPath == null) return 2;
                if (args.Any(a => a == "--real")) return RunReal(connector, app, dir, cavPath);

                dynamic asm = app.Documents.Add("SolidEdge.AssemblyDocument");
                try
                {
                    dynamic occ = asm.Occurrences.AddByFilename(cavPath);
                    occ.PutTransform(0.100, 0.050, 0.020, 10 * Math.PI / 180, 0.0, 30 * Math.PI / 180);
                    var info = new OccurrenceInfo(occ, (string)occ.Name, occ.OccurrenceDocument);
                    OccurrenceTransform pose = AssemblyContext.TryGetPose(info);
                    Console.WriteLine("Pose da cavidade: " + (pose == null ? "ILEGÍVEL" : pose.Describe()));

                    // As faces "selecionadas": as do ressalto (Z local >= 30), do documento da peça,
                    // que é o que o embrulho da seleção entrega em .Object.
                    dynamic cavDoc = occ.OccurrenceDocument;
                    var picks = new List<FaceInfo>();
                    dynamic all = cavDoc.Models.Item(1).Body.Faces[1];
                    for (int i = 1; i <= (int)all.Count; i++)
                    {
                        FaceInfo f = FaceInfo.Read(all.Item(i), app);
                        if (f != null && f.Max[2] > 30 + 0.01) picks.Add(f);
                    }
                    Console.WriteLine($"Faces escolhidas: {picks.Count} (de {(int)all.Count}); cores: {string.Join(", ", picks.Select(p => p.Color).Distinct())}");
                    double cx = (picks.Min(p => p.Min[0]) + picks.Max(p => p.Max[0])) / 2;
                    double cy = (picks.Min(p => p.Min[1]) + picks.Max(p => p.Max[1])) / 2;
                    double cz = picks.Min(p => p.Min[2]);
                    Console.WriteLine($"Centro c (local da cavidade) = ({cx:0.###}; {cy:0.###}; {cz:0.###}) mm");

                    foreach (int mode in new[] { 2, 1 })
                    {
                        string mname = mode == 2 ? "ORDENADO" : "SÍNCRONO";
                        Console.WriteLine();
                        Console.WriteLine($"=========== {mname} ===========");
                        RunCase(app, cavPath, picks, null, mode, "A (sem CoordinateSystem)", null);

                        double[] m = OccurrenceTransform.Identity(pose?.Layout ?? MatrixLayout.TranslationLast)
                            .WithTranslationM(-cx / 1000, -cy / 1000, -cz / 1000).ToMatrix();
                        RunCase(app, cavPath, picks, m, mode, "B (CS na translação -c)", Path.Combine(dir, $"eletrodo_{mode}.par"));
                        double[] mp = OccurrenceTransform.Identity(pose?.Layout ?? MatrixLayout.TranslationLast)
                            .WithTranslationM(cx / 1000, cy / 1000, cz / 1000).ToMatrix();
                        RunCase(app, cavPath, picks, mp, mode, "C (CS na translação +c)", null);
                        if (pose != null) RunCase(app, cavPath, picks, pose.ToMatrix(), mode, "D (CS = pose da cavidade, com rotação)", null, pose);
                    }
                }
                finally { try { asm.Close(false); } catch { } }
            }
            return 0;
        }

        /// <summary>
        /// Ponta a ponta pelo código REAL do botão: duas ocorrências da cavidade com poses
        /// diferentes, faces das duas, <c>CreateElectrodeFromFaces</c>, e a conferência na
        /// MONTAGEM — cada vértice das superfícies "Queima" (pela pose do eletrodo) tem de cair
        /// num vértice das faces de origem (pela pose da dona).
        /// </summary>
        private static int RunReal(SolidEdgeConnector connector, dynamic app, string dir, string cavPath)
        {
            string outDir = Path.Combine(dir, "Eletrodos");
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            dynamic asm = app.Documents.Add("SolidEdge.AssemblyDocument");
            try
            {
                string asmPath = Path.Combine(dir, "montagem_teste.asm");
                if (File.Exists(asmPath)) File.Delete(asmPath);
                asm.SaveAs(asmPath);

                dynamic o1 = asm.Occurrences.AddByFilename(cavPath);
                o1.PutTransform(0.100, 0.050, 0.020, Math.PI, 0.0, Math.PI / 2); // virada como a 14973.102
                dynamic o2 = asm.Occurrences.AddByFilename(cavPath);
                o2.PutTransform(0.180, -0.020, 0.025, 0.0, 0.0, -20 * Math.PI / 180);

                var picks = new List<AutoEDM.Electrode.PickedFace>();
                var origin = new List<(object face, OccurrenceTransform pose)>();
                foreach (dynamic o in new[] { o1, o2 })
                {
                    var info = new OccurrenceInfo(o, (string)o.Name, o.OccurrenceDocument);
                    OccurrenceTransform pose = AssemblyContext.TryGetPose(info);
                    dynamic all = o.OccurrenceDocument.Models.Item(1).Body.Faces[1];
                    for (int i = 1; i <= (int)all.Count; i++)
                    {
                        dynamic f = all.Item(i);
                        if (!FaceGeometry.TryGetRangeMm((object)f, out double[] mn, out double[] mx)) continue;
                        // 1ª ocorrência: o ressalto todo; 2ª: só o topo dele.
                        bool take = ReferenceEquals(o, o1) ? mx[2] > 30.01 : mn[2] > 41.99;
                        if (!take) continue;
                        picks.Add(new AutoEDM.Electrode.PickedFace((object)f, (object)o, (string)o.Name));
                        origin.Add(((object)f, pose));
                    }
                }
                Console.WriteLine($"Faces escolhidas: {picks.Count} ({string.Join(", ", picks.GroupBy(k => k.OccurrenceName).Select(g => g.Key + " x" + g.Count()))})");

                var builder = new AutoEDM.Electrode.ElectrodeBuilder(connector);
                var p = new AutoEDM.Model.ElectrodeParams { OutputFolder = outDir };
                AutoEDM.Electrode.ManualElectrodeResult r = builder.CreateElectrodeFromFaces(asm, p, picks);
                Console.WriteLine();
                Console.WriteLine(r.Message);
                if (!r.Created) return 4;

                // O eletrodo é a última ocorrência; as superfícies vivem no documento dele.
                dynamic eo = asm.Occurrences.Item((int)asm.Occurrences.Count);
                OccurrenceTransform ePose = AssemblyContext.TryGetPose(new OccurrenceInfo(eo, (string)eo.Name, eo.OccurrenceDocument));
                dynamic ed = eo.OccurrenceDocument;
                Console.WriteLine("Pose do eletrodo: " + ePose.Describe()); Console.WriteLine($"Eletrodo: {(string)eo.Name}; ModelingMode={(int)ed.ModelingMode}; Constructions.Count={(int)ed.Constructions.Count}; CoordinateSystems.Count={(int)ed.CoordinateSystems.Count}");

                var target = new List<double[]>();
                foreach (var (face, pose) in origin)
                    foreach (double[] v in Vertices(face)) target.Add(ToAsm(pose, v));

                dynamic surfs = ed.Constructions.CopySurfaces;
                double worst = 0; int nv = 0;
                for (int s = 1; s <= (int)surfs.Count; s++)
                {
                    dynamic surf = surfs.Item(s);
                    dynamic sf = surf.Faces[1];
                    var colors = new List<string>();
                    for (int i = 1; i <= (int)sf.Count; i++)
                    {
                        object f = sf.Item(i);
                        FaceInfo fi = FaceInfo.Read(f, app);
                        colors.Add(fi?.Color ?? "?");
                        foreach (double[] v in Vertices(f))
                        {
                            double[] a = ToAsm(ePose, v);
                            worst = Math.Max(worst, target.Min(t => Dist(t, a))); nv++;
                        }
                    }
                    Console.WriteLine($"  '{(string)surf.Name}': {(int)sf.Count} face(s); cores {string.Join(", ", colors.Distinct())}");
                }
                Console.WriteLine($"Conferência na montagem: {nv} vértice(s), pior distância a um vértice de origem = {worst:0.0000} mm");
                return worst < 0.001 ? 0 : 5;
            }
            finally { try { asm.Close(false); } catch { } }
        }

        /// <summary>SÓ LÊ: documentos abertos; na montagem ativa, a pose de cada ocorrência e,
        /// nas de eletrodo, a caixa das superfícies "Queima" no espaço da montagem.</summary>
        private static int Inspect(dynamic app)
        {
            foreach (dynamic d in app.Documents) Console.WriteLine($"aberto: {(string)d.Name} (Type {(int)d.Type})");
            dynamic doc = app.ActiveDocument;
            Console.WriteLine($"ATIVO: {(string)doc.Name} (Type {(int)doc.Type})");
            dynamic asm = (int)doc.Type == 3 ? doc : null;
            if (asm == null) foreach (dynamic d in app.Documents) if ((int)d.Type == 3) asm = d;
            if (asm == null) return 0;
            dynamic occs = asm.Occurrences;
            for (int i = 1; i <= (int)occs.Count; i++)
            {
                dynamic o = occs.Item(i);
                string name = (string)o.Name;
                OccurrenceTransform pose = AssemblyContext.TryGetPose(new OccurrenceInfo(o, name, o.OccurrenceDocument));
                Console.WriteLine($"{name}: {(pose == null ? "pose ilegível" : pose.Describe())}" +
                    (pose == null ? "" : $"  X->({pose.R(0, 0):0.##},{pose.R(1, 0):0.##},{pose.R(2, 0):0.##}) Y->({pose.R(0, 1):0.##},{pose.R(1, 1):0.##},{pose.R(2, 1):0.##})"));
                if (pose == null || name.IndexOf("_EE", StringComparison.OrdinalIgnoreCase) < 0) continue;
                dynamic pd = o.OccurrenceDocument;
                try { Console.WriteLine($"   ModelingMode={(int)pd.ModelingMode}"); } catch { }
                try
                {
                    dynamic con = pd.Constructions;
                    Console.WriteLine($"   Constructions.Count={(int)con.Count}");
                    for (int k = 1; k <= (int)con.Count; k++)
                    {
                        dynamic it = con.Item(k);
                        string nm = "?"; try { nm = (string)it.Name; } catch { }
                        int nf = -1; try { nf = (int)it.Faces[1].Count; } catch { }
                        Console.WriteLine($"     [{k}] {nm} faces={nf}");
                    }
                    Console.WriteLine($"   CopySurfaces.Count={(int)pd.Constructions.CopySurfaces.Count}; CopyConstructions.Count={(int)pd.Constructions.CopyConstructions.Count}");
                }
                catch (Exception ex) { Console.WriteLine("   Constructions: " + Msg(ex)); }
                dynamic cs;
                try { cs = pd.Constructions.CopySurfaces; } catch { continue; }
                for (int s = 1; s <= (int)cs.Count; s++)
                {
                    dynamic surf = cs.Item(s);
                    var lo = new[] { double.MaxValue, double.MaxValue, double.MaxValue };
                    var hi = new[] { double.MinValue, double.MinValue, double.MinValue };
                    var llo = (double[])lo.Clone(); var lhi = (double[])hi.Clone();
                    dynamic sf = surf.Faces[1];
                    for (int k = 1; k <= (int)sf.Count; k++)
                        foreach (double[] v in Vertices((object)sf.Item(k)))
                        {
                            double[] a = ToAsm(pose, v);
                            for (int c = 0; c < 3; c++) { lo[c] = Math.Min(lo[c], a[c]); hi[c] = Math.Max(hi[c], a[c]); llo[c] = Math.Min(llo[c], v[c]); lhi[c] = Math.Max(lhi[c], v[c]); }
                        }
                    Console.WriteLine($"   '{(string)surf.Name}': {(int)sf.Count} face(s); LOCAL Z {llo[2]:0.###}…{lhi[2]:0.###}; MONTAGEM X {lo[0]:0.###}…{hi[0]:0.###} Y {lo[1]:0.###}…{hi[1]:0.###} Z {lo[2]:0.###}…{hi[2]:0.###}");
                }
            }
            return 0;
        }

        /// <summary>SÓ LÊ a PEÇA ativa: cada superfície de construção, suas faces (tipo, normal se
        /// plana, caixa) e as arestas ABERTAS (usadas por uma face só da superfície), com as pontas.</summary>
        private static int DumpSurface(dynamic app)
        {
            dynamic doc = app.ActiveDocument;
            Console.WriteLine($"ATIVO: {(string)doc.Name} (Type {(int)doc.Type}); ModelingMode={(int)doc.ModelingMode}");
            try { Console.WriteLine($"Models.Count={(int)doc.Models.Count}; Body faces={(int)doc.Models.Item(1).Body.Faces[1].Count}"); } catch (Exception ex) { Console.WriteLine("Models: " + Msg(ex)); }
            dynamic con = doc.Constructions;
            var items = new List<object>();
            Console.WriteLine($"Constructions.Count={(int)con.Count}");
            foreach (string coll in new[] { "StitchSurfaces", "CopySurfaces", "SurfaceByBoundaries", "ExtrudedSurfaces", "BlueSurfs", "OffsetSurfaces" })
            {
                try
                {
                    object c = con.GetType().InvokeMember(coll, BindingFlags.GetProperty, null, (object)con, null);
                    int cnt = (int)((dynamic)c).Count;
                    Console.WriteLine($"  {coll}.Count={cnt}");
                    for (int q = 1; q <= cnt; q++) items.Add((object)((dynamic)c).Item(q));
                }
                catch (Exception ex) { Console.WriteLine($"  {coll}: {Msg(ex)}"); }
            }
            for (int k = 1; k <= items.Count; k++)
            {
                dynamic it = items[k - 1];
                dynamic faces;
                try { faces = it.Faces[1]; } catch { continue; }
                string nm = "?"; try { nm = (string)it.Name; } catch { }
                bool vis = true; try { vis = (bool)it.Visible; } catch { }
                int n = (int)faces.Count;
                Console.WriteLine($"[{k}] {nm} — {n} face(s){(vis ? "" : " (oculta)")}");
                foreach (string prop in new[] { "Heal", "Tolerance" })
                    try { Console.WriteLine($"   {prop} = {((object)it).GetType().InvokeMember(prop, BindingFlags.GetProperty, null, (object)it, null)}"); } catch (Exception ex) { Console.WriteLine($"   {prop}: {Msg(ex)}"); }
                var uses = new Dictionary<int, int>();
                var edgeObj = new Dictionary<int, object>();
                for (int i = 1; i <= n; i++)
                {
                    dynamic f = faces.Item(i);
                    FaceGeometry.TryGetRangeMm((object)f, out double[] mn, out double[] mx);
                    string kind = "?";
                    try
                    {
                        dynamic g = f.Geometry;
                        object[] a = { new double[3], new double[3] };
                        var mod = new ParameterModifier(2); mod[0] = true; mod[1] = true;
                        object go = (object)g;
                        go.GetType().InvokeMember("GetPlaneData", BindingFlags.InvokeMethod, null, go, a, new[] { mod }, null, null);
                        var nrm = (double[])a[1];
                        kind = $"plano n=({nrm[0]:0.##},{nrm[1]:0.##},{nrm[2]:0.##})";
                    }
                    catch { kind = "curva"; }
                    double area = 0; try { area = (double)f.Area * 1e6; } catch { }
                    Console.WriteLine($"   face {(int)f.ID}: {kind}, área {area:0.##}, X {mn[0]:0.##}…{mx[0]:0.##} Y {mn[1]:0.##}…{mx[1]:0.##} Z {mn[2]:0.##}…{mx[2]:0.##}");
                    dynamic es = f.Edges;
                    for (int e = 1; e <= (int)es.Count; e++)
                    {
                        dynamic ed = es.Item(e);
                        int id = (int)ed.ID;
                        uses[id] = uses.TryGetValue(id, out int u) ? u + 1 : 1;
                        edgeObj[id] = (object)ed;
                    }
                }
                foreach (var kv in uses.Where(x => x.Value == 1))
                {
                    dynamic ed = edgeObj[kv.Key];
                    string pts = "";
                    try
                    {
                        object s0 = new double[3], e0 = new double[3];
                        ed.GetEndPoints(ref s0, ref e0);
                        var a = (double[])s0; var b = (double[])e0;
                        pts = $"({a[0] * 1000:0.0000},{a[1] * 1000:0.0000},{a[2] * 1000:0.0000}) → ({b[0] * 1000:0.0000},{b[1] * 1000:0.0000},{b[2] * 1000:0.0000})";
                    }
                    catch (Exception ex) { pts = Msg(ex); }
                    Console.WriteLine($"   ABERTA {kv.Key}: {pts}");
                }
            }
            return 0;
        }

        // (Removido: o teste de costura numa CÓPIA da peça — SaveCopyAs + Open + Close de uma peça
        // em edição em contexto apagou o corpo do original, 2026-10-19. Ver errors.md da skill.)

        private static List<int> OpenEdges(dynamic surfaceFeature)
        {
            var uses = new Dictionary<int, int>();
            dynamic faces = surfaceFeature.Faces[1];
            for (int i = 1; i <= (int)faces.Count; i++)
            {
                dynamic es = faces.Item(i).Edges;
                for (int e = 1; e <= (int)es.Count; e++) { int id = (int)es.Item(e).ID; uses[id] = uses.TryGetValue(id, out int u) ? u + 1 : 1; }
            }
            return uses.Where(x => x.Value == 1).Select(x => x.Key).ToList();
        }

        private static IEnumerable<double[]> Vertices(object face)
        {
            dynamic vs = ((dynamic)face).Vertices;
            for (int i = 1; i <= (int)vs.Count; i++)
            {
                object buf = new double[3];
                vs.Item(i).GetPointData(ref buf);
                var p = (double[])buf;
                yield return new[] { p[0] * 1000, p[1] * 1000, p[2] * 1000 };
            }
        }

        private static double[] ToAsm(OccurrenceTransform pose, double[] mm)
        {
            pose.TransformPointM(mm[0] / 1000, mm[1] / 1000, mm[2] / 1000, out double x, out double y, out double z);
            return new[] { x * 1000, y * 1000, z * 1000 };
        }

        private static double Dist(double[] a, double[] b) =>
            Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]) + (a[2] - b[2]) * (a[2] - b[2]));

        private static void RunCase(dynamic app, string cavPath, List<FaceInfo> picks, double[] csMatrix,
                                    int mode, string label, string savePath, OccurrenceTransform expected = null)
        {
            Console.WriteLine($"--- {label}");
            dynamic part = app.Documents.Add("SolidEdge.PartDocument");
            try
            {
                try { if ((int)part.ModelingMode != mode) part.ModelingMode = mode; } catch (Exception ex) { Console.WriteLine("  ModelingMode: " + Msg(ex)); }
                Console.WriteLine($"  ModelingMode = {(int)part.ModelingMode}");

                object cs = Type.Missing;
                if (csMatrix != null)
                {
                    try
                    {
                        object coll = (object)part.CoordinateSystems;
                        cs = Invoke(coll, "AddByMatrix", new object[] { csMatrix });
                        Console.WriteLine("  CoordinateSystems.AddByMatrix: OK");
                    }
                    catch (Exception ex) { Console.WriteLine("  AddByMatrix FALHOU: " + Msg(ex)); return; }
                }

                object col = (object)part.Constructions.CopyConstructions;
                object cc;
                try
                {
                    cc = Invoke(col, "Add", new object[]
                    {
                        cavPath, 1.0, 1.0, 1.0, Type.Missing, Type.Missing, cs,
                        true, false, false, false, Type.Missing, Type.Missing, true
                    });
                }
                catch (Exception ex) { Console.WriteLine("  CopyConstructions.Add FALHOU: " + Msg(ex)); return; }
                dynamic dcc = cc;
                Console.WriteLine($"  CopyConstructions.Add: OK; Status={Status(cc)}");

                var copyFaces = new List<FaceInfo>();
                dynamic cf = dcc.Faces[1];
                for (int i = 1; i <= (int)cf.Count; i++) { FaceInfo f = FaceInfo.Read(cf.Item(i), app); if (f != null) copyFaces.Add(f); }
                Console.WriteLine($"  Faces na cópia: {copyFaces.Count}");
                if (expected != null)
                {
                    // Com rotação a caixa muda de tamanho: casa pelo ID e compara com a caixa da
                    // face original levada pela matriz (local -> "montagem").
                    foreach (FaceInfo p in picks)
                    {
                        FaceInfo c = copyFaces.FirstOrDefault(x => x.Id == p.Id);
                        if (c == null) { Console.WriteLine($"  face {p.Id}: SEM PAR"); continue; }
                        OccurrenceTransform.MapBoxMm(expected, OccurrenceTransform.Identity(expected.Layout), p.Min, p.Max, out double[] emn, out double[] emx);
                        double err = Enumerable.Range(0, 3).Max(k => Math.Max(Math.Abs(c.Min[k] - emn[k]), Math.Abs(c.Max[k] - emx[k])));
                        Console.WriteLine($"  face {p.Id}: erro contra R·p+t = {err:0.0000} mm");
                    }
                    return;
                }

                // Casamento 1: mesmo ID.  Casamento 2: mesma área e mesmo tamanho de caixa.
                int sameId = 0;
                var matched = new List<FaceInfo>();
                foreach (FaceInfo p in picks)
                {
                    FaceInfo byId = copyFaces.FirstOrDefault(c => c.Id == p.Id);
                    if (byId != null && byId.SameShape(p)) sameId++;
                    FaceInfo byShape = byId != null && byId.SameShape(p) ? byId : copyFaces.FirstOrDefault(c => c.SameShape(p));
                    if (byShape == null) { Console.WriteLine($"  face {p.Id}: SEM PAR na cópia"); continue; }
                    matched.Add(byShape);
                    Console.WriteLine($"  face {p.Id} -> {byShape.Id}: deslocamento ({byShape.Min[0] - p.Min[0]:0.###}; {byShape.Min[1] - p.Min[1]:0.###}; {byShape.Min[2] - p.Min[2]:0.###}) mm; cor {byShape.Color}");
                }
                Console.WriteLine($"  IDs mantidos: {sameId}/{picks.Count}; casadas: {matched.Count}/{picks.Count}");
                if (savePath == null || matched.Count == 0) return;

                // CopySurfaces INTRA-peça sobre as faces casadas.
                object surf;
                try
                {
                    var typed = matched.Select(f => (SolidEdgeGeometry.Face)f.Com).ToArray();
                    object sc = (object)part.Constructions.CopySurfaces;
                    surf = Invoke(sc, "Add", new object[] { typed.Length, typed, Type.Missing, Type.Missing });
                    Console.WriteLine($"  CopySurfaces.Add: OK; Status={Status(surf)}");
                }
                catch (Exception ex) { Console.WriteLine("  CopySurfaces.Add FALHOU: " + Msg(ex)); return; }
                ReportSurface(app, surf, "superfície copiada");

                TryStep(() => ((dynamic)surf).DropParents(), "CopySurface.DropParents()");
                ReportSurface(app, surf, "depois do DropParents");
                TryStep(() => dcc.Visible = false, "CopyConstruction.Visible = false");
                TryStep(() => dcc.BreakLinks(), "CopyConstruction.BreakLinks()");
                try { Console.WriteLine($"  CopyConstruction.IsBroken = {(bool)dcc.IsBroken}"); } catch (Exception ex) { Console.WriteLine("  IsBroken: " + Msg(ex)); }
                TryStep(() => dcc.Delete(), "CopyConstruction.Delete()");
                ReportSurface(app, surf, "depois de apagar a cópia do corpo");
                try { Console.WriteLine($"  Constructions.Count = {(int)part.Constructions.Count}"); } catch { }

                try { if (File.Exists(savePath)) File.Delete(savePath); part.SaveAs(savePath); Console.WriteLine("  salvo: " + savePath); }
                catch (Exception ex) { Console.WriteLine("  SaveAs: " + Msg(ex)); }
            }
            finally { try { part.Close(false); } catch { } }
        }

        private static void ReportSurface(dynamic app, object surf, string label)
        {
            try
            {
                dynamic s = surf;
                dynamic faces = s.Faces[1];
                int n = (int)faces.Count;
                var colors = new List<string>();
                for (int i = 1; i <= n; i++) { FaceInfo f = FaceInfo.Read(faces.Item(i), app); if (f != null) colors.Add(f.Color); }
                Console.WriteLine($"  [{label}] {n} face(s); Status={Status(surf)}; cores: " +
                    string.Join(", ", colors.GroupBy(c => c).Select(g => $"{g.Key} x{g.Count()}")));
            }
            catch (Exception ex) { Console.WriteLine($"  [{label}] ilegível: {Msg(ex)}"); }
        }

        private static string BuildCavity(dynamic app, string dir)
        {
            string path = Path.Combine(dir, "cavidade_teste2.par");
            if (File.Exists(path)) return path;
            dynamic part = app.Documents.Add("SolidEdge.PartDocument");
            try
            {
                try { if ((int)part.ModelingMode != 1) part.ModelingMode = 1; } catch { }
                var prims = new List<Primitive>
                {
                    new Primitive { Kind = "caixa", Name = "base",     SizeXMm = 100, SizeYMm = 80, HeightMm = 30, PlaneIndex = 1 },
                    new Primitive { Kind = "caixa", Name = "ressalto", SizeXMm = 40,  SizeYMm = 30, HeightMm = 12, PlaneIndex = 1, LiftMm = 30, CenterXMm = 15, CenterYMm = 10 },
                };
                PrimitiveBuildResult built = PrimitiveModeler.Build(part, prims);
                Console.WriteLine("Cavidade de teste: " + built.Message);
                if (!built.Ok) return null;

                var list = new List<object>();
                dynamic all = part.Models.Item(1).Body.Faces[1];
                for (int i = 1; i <= (int)all.Count; i++)
                {
                    dynamic f = all.Item(i);
                    if (FaceGeometry.TryGetRangeMm((object)f, out double[] mn, out double[] mx) && mx[2] > 30 + 0.01) list.Add((object)f);
                }
                string got = AutoEDM.Electrode.FaceColorPainter.PaintWithNamedStyle((object)part, list, new[] { Style }, Style, System.Drawing.Color.FromArgb(0, 127, 255));
                Console.WriteLine($"  pintura {Style}: {list.Count} face(s) -> {got}");
                part.SaveAs(path);
                return path;
            }
            finally { try { part.Close(false); } catch { } }
        }

        private sealed class FaceInfo
        {
            public object Com; public int Id; public double Area; public double[] Min, Max; public string Color;

            public static FaceInfo Read(dynamic face, dynamic app)
            {
                var f = new FaceInfo { Com = (object)face };
                try { f.Id = (int)face.ID; } catch { }
                try { f.Area = (double)face.Area * 1e6; } catch { }
                if (!FaceGeometry.TryGetRangeMm((object)face, out f.Min, out f.Max)) return null;
                f.Color = new FaceStyleColorReader().TryReadColor(face, app, out System.Drawing.Color c, out string src)
                    ? $"({c.R},{c.G},{c.B})" : "?";
                return f;
            }

            public bool SameShape(FaceInfo o) =>
                Math.Abs(Area - o.Area) < 1e-3 &&
                Enumerable.Range(0, 3).All(k => Math.Abs((Max[k] - Min[k]) - (o.Max[k] - o.Min[k])) < 1e-3);
        }

        private static object Invoke(object target, string method, object[] args) =>
            target.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, target, args, null, CultureInfo.InvariantCulture, null);

        private static string Status(object feature)
        {
            try { return ((uint)(int)((dynamic)feature).Status[1]).ToString(); } catch { }
            try { return ((uint)(int)((dynamic)feature).Status).ToString(); } catch (Exception ex) { return "? " + Msg(ex); }
        }

        private static void TryStep(Action a, string label)
        {
            try { a(); Console.WriteLine($"  {label}: OK"); }
            catch (Exception ex) { Console.WriteLine($"  {label} FALHOU: {Msg(ex)}"); }
        }

        private static string Msg(Exception ex) => ex.GetBaseException().GetType().Name + ": " + ex.GetBaseException().Message;
    }
}
