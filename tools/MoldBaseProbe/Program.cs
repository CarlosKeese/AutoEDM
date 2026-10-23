using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AutoEDM.Com;
using AutoEDM.Selection;

namespace AutoEDM.Tools.MoldBaseProbe
{
    /// <summary>
    /// Sonda do PORTA-MOLDE (Carlos, 2026-10-23): variáveis do Solid Edge guiando a geometria de uma
    /// placa. Só arquivos descartáveis na pasta temporária, fechados no fim.
    ///   --vars   variáveis criadas por código; retângulo ordenado com cotas ligadas a elas; editar e medir.
    /// </summary>
    internal static class Program
    {
        private static string _dir;

        [STAThread]
        private static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            _dir = Path.Combine(Path.GetTempPath(), "AutoEDM_MoldBaseProbe_" + DateTime.Now.ToString("HHmmss"));
            Directory.CreateDirectory(_dir);
            Console.WriteLine("pasta: " + _dir);
            string mode = args.Length > 0 ? args[0] : "--vars";
            if (mode == "--form")
            {
                string outDir = args.Length > 1 ? args[1] : _dir;
                foreach (int f in new[] { 1, 2, 3, 4 }) FormShot(Path.Combine(outDir, "form" + f + ".png"), f);
                return 0;
            }

            using (var connector = new SolidEdgeConnector())
            {
                dynamic app = connector.Connect(startIfNotRunning: false, makeVisible: false);
                if (app == null) { Console.WriteLine("Nenhuma Solid Edge aberta."); return 1; }
                Console.WriteLine($"SE {app.Version}; documentos abertos antes: {(int)app.Documents.Count}");
                CloseOurLeftovers(app);
                bool alerts = true;
                try { alerts = (bool)app.DisplayAlerts; app.DisplayAlerts = false; } catch { }
                try
                {
                    switch (mode)
                    {
                        case "--vars": return Vars(app);
                        case "--link": return Link(app);
                        case "--clip": return Clip(app);
                        case "--holes": return Holes(app);
                        case "--asm": return Asm(app);
                        case "--nested": return Nested(app);
                        case "--build": return BuildBase(app);
                        case "--learn": return Learn(app, args.Skip(1).ToArray());
                        case "--dims": return Dims(app, args.Skip(1).ToArray());
                        case "--faces": return Faces(app, args.Skip(1).ToArray());
                        case "--matrices": return Matrices(app, args.Skip(1).ToArray());
                        default: Console.WriteLine("modo desconhecido: " + mode); return 2;
                    }
                }
                catch (Exception ex) { Console.WriteLine("FALHOU: " + Msg(ex)); return 8; }
                finally { try { app.DisplayAlerts = alerts; } catch { } }
            }
        }

        /// <summary>Fecha, sem salvar, só os documentos das rodadas anteriores desta sonda (pasta temporária dela).</summary>
        private static void CloseOurLeftovers(dynamic app)
        {
            string tag = Path.Combine(Path.GetTempPath(), "AutoEDM_MoldBaseProbe_");
            for (int i = (int)app.Documents.Count; i >= 1; i--)
            {
                dynamic d = app.Documents.Item(i);
                string fn = Try(() => d.FullName);
                if (!fn.StartsWith(tag, StringComparison.OrdinalIgnoreCase)) { Console.WriteLine("  (aberto, não é da sonda: " + fn + ")"); continue; }
                try { d.Close(false); Console.WriteLine("  fechado: " + fn); } catch (Exception ex) { Console.WriteLine("  fechar " + fn + ": " + Msg(ex)); }
            }
        }

        // ------------------------------------------------------------------ --vars

        private static int Vars(dynamic app)
        {
            dynamic doc = app.Documents.Add("SolidEdge.PartDocument");
            T("ModelingMode = 2 (ordenado, peça nossa)", () => { doc.ModelingMode = 2; return (int)doc.ModelingMode; });
            dynamic vars = doc.Variables;
            Console.WriteLine($"Variables.Count na peça nova: {(int)vars.Count}");

            // 1) Como Add trata número cru, número com unidade e o UnitsType.
            dynamic vC = T("Add(Comprimento, \"296\", igUnitDistance=1)", () => vars.Add("Comprimento", "296", 1));
            dynamic vL = T("Add(Largura, \"246 mm\", 1)", () => vars.Add("Largura", "246 mm", 1));
            dynamic vE = T("Add(Espessura, \"46\") sem UnitsType", () => vars.Add("Espessura", "46"));
            dynamic vF = T("Add(Folga, \"Comprimento/2 - 20\", 1)", () => vars.Add("Folga", "Comprimento/2 - 20", 1));
            foreach (var v in new[] { (object)vC, vL, vE, vF }) DumpVar(v);

            // 2) Retângulo centrado no plano 1, lados amarrados, cotas de comprimento.
            dynamic plane = doc.RefPlanes.Item(1);
            Console.WriteLine($"RefPlanes.Item(1) = {Try(() => plane.Name)}");
            dynamic ps = doc.ProfileSets.Add();
            dynamic prof = ps.Profiles.Add(plane);
            dynamic lines = prof.Lines2d;
            double a = 0.148, b = 0.123;
            dynamic l1 = lines.AddBy2Points(-a, -b, a, -b);
            dynamic l2 = lines.AddBy2Points(a, -b, a, b);
            dynamic l3 = lines.AddBy2Points(a, b, -a, b);
            dynamic l4 = lines.AddBy2Points(-a, b, -a, -b);
            dynamic rel = prof.Relations2d;
            T("AddKeypoint ×4", () => { rel.AddKeypoint(l1, 1, l2, 0); rel.AddKeypoint(l2, 1, l3, 0); rel.AddKeypoint(l3, 1, l4, 0); rel.AddKeypoint(l4, 1, l1, 0); return "ok"; });
            T("AddHorizontal(l1,l3) / AddVertical(l2,l4)", () => { rel.AddHorizontal(l1); rel.AddHorizontal(l3); rel.AddVertical(l2); rel.AddVertical(l4); return "ok"; });

            // Centro: eixos projetados dos planos 2 e 3 + diagonal de construção com o meio nos dois.
            dynamic ax2 = T("ProjectRefPlane(Item 2)", () => prof.ProjectRefPlane(doc.RefPlanes.Item(2)));
            dynamic ax3 = T("ProjectRefPlane(Item 3)", () => prof.ProjectRefPlane(doc.RefPlanes.Item(3)));
            DumpLine("eixo 2", ax2); DumpLine("eixo 3", ax3);
            dynamic diag = lines.AddBy2Points(-a, -b, a, b);
            T("ToggleConstruction(diagonal)", () => { prof.ToggleConstruction(diag); return Try(() => diag.IsConstructionElement); });
            T("AddKeypoint diagonal↔cantos", () => { rel.AddKeypoint(diag, 0, l1, 0); rel.AddKeypoint(diag, 1, l3, 0); return "ok"; });
            T("AddPointOn(diag, meio=2, eixo2)", () => rel.AddPointOn(diag, 2, ax2, -1));
            T("AddPointOn(diag, meio=2, eixo3)", () => rel.AddPointOn(diag, 2, ax3, -1));

            dynamic dims = prof.Dimensions;
            T("Dimensions.Constraint = true", () => { dims.Constraint = true; return Try(() => dims.Constraint); });
            dynamic dC = T("AddLength(l1)", () => dims.AddLength(l1));
            dynamic dL = T("AddLength(l2)", () => dims.AddLength(l2));
            DumpDim("cota l1", dC); DumpDim("cota l2", dL);
            T("cota l1 .Constraint = true", () => { dC.Constraint = true; return Try(() => dC.Constraint); });
            T("cota l2 .Constraint = true", () => { dL.Constraint = true; return Try(() => dL.Constraint); });

            // Ligar: tentativa A = Dimension.Formula; tentativa B = Variables.Edit(nome da cota, ...).
            T("cota l1 .Formula = \"Comprimento\"", () => { dC.Formula = "Comprimento"; return Try(() => dC.Formula); });
            string dLName = Try(() => dL.DisplayName);
            T($"Variables.Edit(\"{dLName}\", \"Largura\")", () => { vars.Edit(dLName, "Largura"); return Try(() => dL.Formula); });
            DumpDim("cota l1 depois", dC); DumpDim("cota l2 depois", dL);
            T("Profile.End(1)", () => prof.End(1));
            Console.WriteLine($"ProfileSet.IsUnderDefined = {Try(() => ps.IsUnderDefined)}");

            object ext = T("AddFiniteExtrudedProtrusion 46 mm", () =>
            {
                var arr = new SolidEdgePart.Profile[] { (SolidEdgePart.Profile)prof };
                return ((object)doc.Models).GetType().InvokeMember("AddFiniteExtrudedProtrusion",
                    BindingFlags.InvokeMethod, null, doc.Models, new object[] { 1, arr, 2, 0.046 });
            });
            dynamic feat = Try(() => doc.Models.Item(1).ExtrudedProtrusions.Item(1));
            Console.WriteLine($"feature Status = {Try(() => (uint)(int)feat.Status)}; Depth = {Try(() => feat.Depth)}");
            Console.WriteLine("corpo: " + Range(doc));

            // 3) O que a tabela de variáveis mostra agora (nomes de sistema das cotas).
            ListVars("tabela depois da feature", vars);

            // 4) Profundidade da extrusão ligada à Espessura, pela tabela.
            dynamic depthVar = FindDimByValue(vars, 0.046);
            if (depthVar != null)
            {
                string n = Try(() => depthVar.DisplayName);
                T($"profundidade: Variables.Edit(\"{n}\", \"Espessura\")", () => { vars.Edit(n, "Espessura"); return Try(() => depthVar.Formula); });
            }
            else Console.WriteLine("profundidade: nenhuma cota de 46 mm na tabela");

            // 5) Mudar as variáveis e medir.
            T("Comprimento.Formula = \"346\"", () => { vC.Formula = "346"; return Try(() => vC.Value); });
            T("Variables.Edit(Largura, \"296\")", () => { vars.Edit("Largura", "296"); return Try(() => vL.Value); });
            T("Variables.Edit(Espessura, \"56\")", () => { vars.Edit("Espessura", "56"); return Try(() => vE.Value); });
            Console.WriteLine("corpo ANTES de Recompute: " + Range(doc));
            T("doc.Recompute()", () => { doc.Recompute(); return "ok"; });
            Console.WriteLine("corpo DEPOIS de Recompute: " + Range(doc));
            Console.WriteLine($"Folga (Comprimento/2-20) agora = {Try(() => vF.Value)} | Formula {Try(() => vF.Formula)}");

            // 6) Value: unidade em que vem, e escrita por Value.
            T("Comprimento.Value = 0.4 (m?)", () => { vC.Value = 0.4; return Try(() => vC.Formula); });
            T("doc.Recompute()", () => { doc.Recompute(); return "ok"; });
            Console.WriteLine("corpo com Comprimento.Value=0.4: " + Range(doc));
            ListVars("tabela final", vars);

            string path = Path.Combine(_dir, "Placa.par");
            T("SaveAs", () => { doc.SaveAs(path); return path; });
            doc.Close(false);
            return 0;
        }

        // ------------------------------------------------------------------ --link

        /// <summary>
        /// Variável de uma peça MESTRE (e de uma MONTAGEM) guiando a variável de outra peça: pela área de
        /// transferência (CopyToClipboard → AddFromClipboard, o "Colar vínculo" da tabela) e por fórmula
        /// escrita à mão. Também: unidade escalar e Edit pelo nome de sistema.
        /// </summary>
        private static int Link(dynamic app)
        {
            // Mestre: peça com as variáveis da série.
            string mPath = Path.Combine(_dir, "Mestre.par");
            dynamic m = app.Documents.Add("SolidEdge.PartDocument");
            dynamic mv = m.Variables;
            T("mestre Add(Comprimento, 296)", () => mv.Add("Comprimento", "296", 1));
            dynamic sc = T("mestre Add(Serie, 2530, igUnitScalar=58)", () => mv.Add("Serie", "2530", 58));
            DumpVar(sc);
            dynamic sc2 = T("mestre Add(Ra, 1.6) sem UnitsType", () => mv.Add("Ra", "1.6"));
            DumpVar(sc2);
            T("mestre SaveAs", () => { m.SaveAs(mPath); return mPath; });

            // Alvo: peça com uma variável ligada à do mestre.
            dynamic p = app.Documents.Add("SolidEdge.PartDocument");
            dynamic pv = p.Variables;
            T("mestre CopyToClipboard(Comprimento)", () => { mv.CopyToClipboard("Comprimento"); return "ok"; });
            dynamic linked = T("alvo AddFromClipboard(CompMestre, 1)", () => pv.AddFromClipboard("CompMestre", 1));
            DumpVar(linked);
            DumpLink(linked);

            // Fórmulas escritas à mão, em várias grafias.
            string[] tries = {
                mPath + "!Comprimento",
                "'" + mPath + "'!Comprimento",
                "Mestre.par!Comprimento",
            };
            int k = 0;
            foreach (string f in tries)
            {
                k++;
                dynamic v = T($"alvo Add(F{k}, \"{f}\")", () => pv.Add("F" + k, f, 1));
                DumpVar(v); DumpLink(v);
            }

            // Mudar o mestre: o alvo acompanha com os dois abertos? E depois de salvar?
            T("mestre Edit(Comprimento, 400)", () => { mv.Edit("Comprimento", "400"); return "ok"; });
            Console.WriteLine($"alvo CompMestre = {Try(() => linked.Value)} (com os dois abertos, mestre não salvo)");
            T("mestre Save", () => { m.Save(); return "ok"; });
            Console.WriteLine($"alvo CompMestre = {Try(() => linked.Value)} (mestre salvo)");
            T("alvo UpdateAll?", () => { p.UpdateAll(); return "ok"; });
            T("alvo Recompute", () => { p.Recompute(); return "ok"; });
            Console.WriteLine($"alvo CompMestre = {Try(() => linked.Value)} (depois de Recompute)");
            string pPath = Path.Combine(_dir, "Alvo.par");
            T("alvo SaveAs + Close", () => { p.SaveAs(pPath); p.Close(); return "ok"; });
            T("mestre Edit(Comprimento, 500) + Save + Close", () => { mv.Edit("Comprimento", "500"); m.Save(); m.Close(); return "ok"; });
            p = app.Documents.Open(pPath);
            dynamic again = Try2(() => p.Variables.Item("CompMestre"));
            Console.WriteLine($"alvo reaberto: CompMestre = {Try(() => again.Value)}");
            DumpLink(again);

            // Edit pelo nome de SISTEMA (não localizado) e pelo nome de usuário.
            dynamic sv = Try2(() => p.Variables.Item(1));
            ListVars("alvo reaberto", p.Variables);
            p.Close(false);

            // Montagem como fonte.
            string aPath = Path.Combine(_dir, "Base.asm");
            dynamic asm = app.Documents.Add("SolidEdge.AssemblyDocument");
            dynamic av = asm.Variables;
            T("montagem Add(Curso, 60)", () => av.Add("Curso", "60", 1));
            T("montagem SaveAs", () => { asm.SaveAs(aPath); return aPath; });
            dynamic p2 = app.Documents.Add("SolidEdge.PartDocument");
            T("montagem CopyToClipboard(Curso)", () => { av.CopyToClipboard("Curso"); return "ok"; });
            dynamic fromAsm = T("peça AddFromClipboard(CursoAsm, 1)", () => p2.Variables.AddFromClipboard("CursoAsm", 1));
            DumpVar(fromAsm); DumpLink(fromAsm);
            T("montagem Edit(Curso, 80)", () => { av.Edit("Curso", "80"); return "ok"; });
            Console.WriteLine($"peça CursoAsm = {Try(() => fromAsm.Value)} (os dois abertos)");
            p2.Close(false);
            asm.Close(false);
            return 0;
        }

        // ------------------------------------------------------------------ --holes

        /// <summary>
        /// Placa 296×246×46 por variáveis + 4 furos simétricos (EntreX × EntreY, Ø) passantes + bolsão
        /// centrado na face de cima (BolsaoX × BolsaoY × BolsaoProf). Volume conferido, variáveis
        /// alteradas, volume conferido de novo.
        /// </summary>
        private static int Holes(dynamic app)
        {
            dynamic doc = app.Documents.Add("SolidEdge.PartDocument");
            doc.ModelingMode = 2;
            dynamic vars = doc.Variables;
            vars.Add("Comprimento", "296", 1); vars.Add("Largura", "246", 1); vars.Add("Espessura", "46", 1);
            vars.Add("EntreX", "250", 1); vars.Add("EntreY", "196", 1); vars.Add("DiamFuro", "25", 1);
            vars.Add("BolsaoX", "150", 1); vars.Add("BolsaoY", "120", 1); vars.Add("BolsaoProf", "20", 1);

            // Placa (mesma receita do --vars).
            dynamic prof = NewProfile(doc, doc.RefPlanes.Item(1), out dynamic ps);
            CenteredRect(doc, prof, 0.148, 0.123, "Comprimento", "Largura", construction: false);
            T("placa End", () => prof.End(1));
            T("placa extrusão", () => ((object)doc.Models).GetType().InvokeMember("AddFiniteExtrudedProtrusion",
                BindingFlags.InvokeMethod, null, doc.Models, new object[] { 1, new[] { (SolidEdgePart.Profile)prof }, 2, 0.046 }));
            dynamic model = doc.Models.Item(1);
            LinkDimByValue(vars, 0.046, "Espessura");
            Console.WriteLine($"placa: {Range(doc)}; V = {Vol(doc):0} mm³ (esperado {296.0 * 246 * 46:0})");

            // Furos: retângulo de CONSTRUÇÃO centrado + 4 círculos nos cantos + Ø igual.
            dynamic hp = NewProfile(doc, doc.RefPlanes.Item(1), out dynamic hps);
            dynamic[] r = CenteredRect(doc, hp, 0.125, 0.098, "EntreX", "EntreY", construction: true);
            dynamic rel = hp.Relations2d;
            var circles = new List<object>();
            double[,] c = { { -0.125, -0.098 }, { 0.125, -0.098 }, { 0.125, 0.098 }, { -0.125, 0.098 } };
            for (int i = 0; i < 4; i++)
            {
                dynamic ci = hp.Circles2d.AddByCenterRadius(c[i, 0], c[i, 1], 0.0125);
                circles.Add(ci);
                int li = i; // canto i = início da linha i do retângulo (l1 começa em (-a,-b), l2 em (a,-b)...)
                T($"círculo {i}: AddKeypoint(centro=0, linha {li} início=0)", () => rel.AddKeypoint(ci, 0, r[li], 0));
            }
            for (int i = 1; i < 4; i++) { int k = i; T($"AddEqual(c0, c{k})", () => rel.AddEqual(circles[0], circles[k])); }
            dynamic dD = T("AddCircularDiameter(c0)", () => hp.Dimensions.AddCircularDiameter(circles[0]));
            T("Ø .Formula = DiamFuro", () => { dD.Constraint = true; dD.Formula = "DiamFuro"; return Try(() => dD.Value); });
            Console.WriteLine($"furos End(1) = {Try(() => hp.End(1))}; IsUnderDefined = {Try(() => hps.IsUnderDefined)}");
            dynamic cut = T("ExtrudedCutouts.AddThroughAllMulti(1, [perfil], 2)", () => ((object)model.ExtrudedCutouts).GetType().InvokeMember("AddThroughAllMulti",
                BindingFlags.InvokeMethod, null, model.ExtrudedCutouts, new object[] { 1, new[] { (SolidEdgePart.Profile)hp }, 2 }));
            Console.WriteLine($"  Status {Try(() => (uint)(int)cut.Status)}; V = {Vol(doc):0} mm³");
            double vHoles = 4 * Math.PI * 12.5 * 12.5 * 46;
            if (Math.Abs(Vol(doc) - (296.0 * 246 * 46 - vHoles)) > 1)
            {
                Console.WriteLine("  furos NÃO cortaram pelo lado 2 — apagando e tentando lado 1");
                T("Delete", () => { cut.Delete(); return "ok"; });
                hp = NewProfile(doc, doc.RefPlanes.Item(1), out hps);
                Console.WriteLine("  (refazer o perfil fica para a próxima rodada)");
            }

            // Bolsão: esboço na FACE de cima (Z = Espessura) — acompanha a espessura.
            dynamic top = TopFace(model);
            Console.WriteLine($"face de cima: {(top == null ? "não achada" : "ok")}");
            dynamic bp = null, bps = null;
            try { object face = top; bps = doc.ProfileSets.Add(); bp = bps.Profiles.Add(face); Console.WriteLine("OK  Profiles.Add(face de cima)"); }
            catch (Exception ex) { Console.WriteLine("ERR Profiles.Add(face de cima) → " + Msg(ex)); try { bps.Delete(); } catch { } bp = null; }
            if (bp == null)
            {
                dynamic tp = T("RefPlanes.AddParallelByDistance(Item 1, 46 mm, lado 2)", () => doc.RefPlanes.AddParallelByDistance(doc.RefPlanes.Item(1), 0.046, 2, Type.Missing, Type.Missing, Type.Missing, Type.Missing));
                LinkDimByValue(vars, 0.046, "Espessura");
                object plane = tp; bps = doc.ProfileSets.Add(); bp = bps.Profiles.Add(plane);
            }
            CenteredRect(doc, bp, 0.075, 0.060, "BolsaoX", "BolsaoY", construction: false);
            Console.WriteLine($"bolsão End(1) = {Try(() => bp.End(1))}; IsUnderDefined = {Try(() => bps.IsUnderDefined)}");
            double vBefore = Vol(doc);
            dynamic pocket = null;
            foreach (int side in new[] { 1, 2 })
            {
                pocket = T($"ExtrudedCutouts.AddFiniteMulti(1, [perfil], lado {side}, 20 mm)", () => ((object)model.ExtrudedCutouts).GetType().InvokeMember("AddFiniteMulti",
                    BindingFlags.InvokeMethod, null, model.ExtrudedCutouts, new object[] { 1, new[] { (SolidEdgePart.Profile)bp }, side, 0.020 }));
                double dv = vBefore - Vol(doc);
                Console.WriteLine($"  lado {side}: Status {Try(() => (uint)(int)pocket.Status)}; removido {dv:0} mm³ (esperado {150.0 * 120 * 20:0})");
                if (Math.Abs(dv - 150.0 * 120 * 20) < 1) break;
                T("Delete", () => { pocket.Delete(); return "ok"; });
                pocket = null;
                if (side == 1) { bp = NewProfile(doc, TopFace(model), out bps); CenteredRect(doc, bp, 0.075, 0.060, "BolsaoX", "BolsaoY", false); bp.End(1); }
            }
            LinkDimByValue(vars, 0.020, "BolsaoProf");
            ListVars("tabela", vars);

            // Alterar tudo e conferir.
            T("Edit Comprimento 346, Largura 296, Espessura 56", () => { vars.Edit("Comprimento", "346"); vars.Edit("Largura", "296"); vars.Edit("Espessura", "56"); return "ok"; });
            T("Edit EntreX 300, EntreY 246, DiamFuro 30", () => { vars.Edit("EntreX", "300"); vars.Edit("EntreY", "246"); vars.Edit("DiamFuro", "30"); return "ok"; });
            T("Edit BolsaoX 200, BolsaoY 150, BolsaoProf 25", () => { vars.Edit("BolsaoX", "200"); vars.Edit("BolsaoY", "150"); vars.Edit("BolsaoProf", "25"); return "ok"; });
            double exp = 346.0 * 296 * 56 - 4 * Math.PI * 15 * 15 * 56 - 200.0 * 150 * 25;
            Console.WriteLine($"depois: {Range(doc)}; V = {Vol(doc):0} mm³ (esperado {exp:0})");
            T("Recompute", () => { doc.Recompute(); return "ok"; });
            Console.WriteLine($"depois de Recompute: {Range(doc)}; V = {Vol(doc):0} mm³");
            Cylinders(model);
            string path = Path.Combine(_dir, "PlacaFuros.par");
            doc.SaveAs(path);
            Console.WriteLine("salva (aberta no SE para inspeção): " + path);
            return 0;
        }

        // ------------------------------------------------------------------ --asm

        /// <summary>
        /// Três placas (cada uma com Comprimento/Largura/Espessura) empilhadas por PutOrigin numa montagem
        /// nova; a espessura da do meio editada PELA OCORRÊNCIA (OccurrenceDocument.Variables), sem abrir
        /// a peça; caixas lidas antes/depois; reempilhamento; variáveis da montagem.
        /// </summary>
        private static int Asm(dynamic app)
        {
            var plates = new[] { Tuple.Create("PFI", 396.0, 296.0, 26.0), Tuple.Create("PS", 346.0, 296.0, 46.0), Tuple.Create("PCI", 346.0, 296.0, 56.0) };
            var paths = new List<string>();
            foreach (var pl in plates)
            {
                dynamic d = app.Documents.Add("SolidEdge.PartDocument");
                d.ModelingMode = 2;
                dynamic v = d.Variables;
                v.Add("Comprimento", pl.Item2.ToString(CultureInfo.InvariantCulture), 1);
                v.Add("Largura", pl.Item3.ToString(CultureInfo.InvariantCulture), 1);
                v.Add("Espessura", pl.Item4.ToString(CultureInfo.InvariantCulture), 1);
                dynamic pr = NewProfile(d, d.RefPlanes.Item(1), out dynamic s);
                CenteredRect(d, pr, pl.Item2 / 2000, pl.Item3 / 2000, "Comprimento", "Largura", construction: false);
                pr.End(1);
                ((object)d.Models).GetType().InvokeMember("AddFiniteExtrudedProtrusion", BindingFlags.InvokeMethod, null, d.Models,
                    new object[] { 1, new[] { (SolidEdgePart.Profile)pr }, 2, pl.Item4 / 1000 });
                LinkDimByValue(v, pl.Item4 / 1000, "Espessura");
                string p = Path.Combine(_dir, pl.Item1 + ".par");
                d.SaveAs(p); d.Close();
                paths.Add(p);
            }

            dynamic asm = app.Documents.Add("SolidEdge.AssemblyDocument");
            T("montagem SaveAs", () => { asm.SaveAs(Path.Combine(_dir, "PortaMolde.asm")); return "ok"; });
            var occs = new List<object>();
            double z = 0;
            for (int i = 0; i < paths.Count; i++)
            {
                dynamic o = asm.Occurrences.AddByFilename(paths[i]);
                double zi = z;
                T($"{plates[i].Item1}: PutOrigin(0,0,{zi})", () => { o.PutOrigin(0.0, 0.0, zi / 1000); return "ok"; });
                occs.Add(o);
                z += plates[i].Item4;
            }
            foreach (dynamic o in occs) Console.WriteLine($"  {Try(() => o.Name)}: {Box(o)}");

            // Editar a espessura da PS pela ocorrência.
            dynamic ps = occs[1];
            dynamic psDoc = T("PS: OccurrenceDocument", () => ps.OccurrenceDocument);
            T("PS: OccurrenceDocument.Variables.Edit(Espessura, 66)", () => { psDoc.Variables.Edit("Espessura", "66"); return Try(() => psDoc.Variables.Item("Espessura").Value); });
            Console.WriteLine($"  PS logo depois: {Box(ps)}");
            string psPath = paths[1];
            DateTime t0 = File.GetLastWriteTime(psPath);
            Console.WriteLine($"  PS: ReadOnly {Try(() => psDoc.ReadOnly)}; Dirty {Try(() => psDoc.Dirty)}; FullName {Try(() => psDoc.FullName)}");
            T("montagem Save (a peça alterada vai junto?)", () => { asm.Save(); return "ok"; });
            Console.WriteLine($"  PS.par no disco mudou com asm.Save? {File.GetLastWriteTime(psPath) != t0}; Dirty {Try(() => psDoc.Dirty)}");
            if (File.GetLastWriteTime(psPath) == t0)
            {
                dynamic opened = T("Documents.Open(PS.par)", () => app.Documents.Open(psPath));
                Console.WriteLine($"  mesmo objeto do OccurrenceDocument? {ReferenceEquals((object)opened, (object)psDoc)}; Espessura {Try(() => opened.Variables.Item("Espessura").Value)}; ReadOnly {Try(() => opened.ReadOnly)}");
                T("aberta: Save()", () => { opened.Save(); return "ok"; });
                Console.WriteLine($"  PS.par no disco mudou? {File.GetLastWriteTime(psPath) != t0}");
                T("aberta: Close()", () => { opened.Close(); return "ok"; });
            }
            Console.WriteLine($"  PS depois de salvar: {Box(ps)}");
            T("montagem UpdateAll", () => { asm.UpdateAll(); return "ok"; });
            Console.WriteLine($"  PS depois de UpdateAll: {Box(ps)}");
            foreach (dynamic o in occs) Console.WriteLine($"  {Try(() => o.Name)}: {Box(o)}");

            // Reempilhar pela espessura lida da variável.
            z = 0;
            foreach (dynamic o in occs)
            {
                double t = (double)o.OccurrenceDocument.Variables.Item("Espessura").Value * 1000;
                double zi = z;
                o.PutOrigin(0.0, 0.0, zi / 1000);
                z += t;
            }
            Console.WriteLine("reempilhado:");
            foreach (dynamic o in occs) Console.WriteLine($"  {Try(() => o.Name)}: {Box(o)}");

            // Variáveis da montagem: escalar e texto?
            dynamic av = asm.Variables;
            DumpVar(T("montagem Add(Serie, 3035, escalar)", () => av.Add("Serie", "3035", 58)));
            DumpVar(T("montagem Add(Curso, 60, distância)", () => av.Add("Curso", "60", 1)));
            DumpVar(T("montagem Add(Tipo, \"\\\"A\\\"\") texto?", () => av.Add("Tipo", "\"A\"")));
            T("montagem Save", () => { asm.Save(); return "ok"; });
            Console.WriteLine("montagem aberta no SE para inspeção: " + Path.Combine(_dir, "PortaMolde.asm"));
            return 0;
        }

        /// <summary>
        /// Montagem de cima → submontagem "Porta Molde.asm" → 2 placas. Pela ocorrência da submontagem:
        /// editar a variável de uma placa, reposicionar a outra (PutOrigin dentro da submontagem
        /// somente-leitura?), salvar a de cima e conferir nos arquivos. Depois, Documents.Open da
        /// submontagem já carregada: devolve o mesmo objeto? fica editável?
        /// </summary>
        private static int Nested(dynamic app)
        {
            string sub = Path.Combine(_dir, "Porta Molde");
            Directory.CreateDirectory(sub);
            string pA = Path.Combine(sub, "P2.par"), pB = Path.Combine(sub, "PS.par");
            MakePlate(app, pA, 346, 296, 46);
            MakePlate(app, pB, 346, 296, 56);

            dynamic sa = app.Documents.Add("SolidEdge.AssemblyDocument");
            dynamic oa = sa.Occurrences.AddByFilename(pA); oa.PutOrigin(0.0, 0.0, -0.046);
            dynamic ob = sa.Occurrences.AddByFilename(pB); ob.PutOrigin(0.0, 0.0, -0.102);
            string saPath = Path.Combine(sub, "Porta Molde.asm");
            sa.SaveAs(saPath); sa.Close();

            dynamic top = app.Documents.Add("SolidEdge.AssemblyDocument");
            string topPath = Path.Combine(_dir, "Molde.asm");
            top.SaveAs(topPath);
            dynamic so = top.Occurrences.AddByFilename(saPath);
            Console.WriteLine($"submontagem: Subassembly {Try(() => so.Subassembly)}; {Box(so)}");
            dynamic sdoc = so.OccurrenceDocument;
            Console.WriteLine($"  OccurrenceDocument: {Try(() => Microsoft.VisualBasic.Information.TypeName(sdoc))}; ReadOnly {Try(() => sdoc.ReadOnly)}; FullName {Try(() => sdoc.FullName)}");
            dynamic subOccs = T("sub.Occurrences", () => sdoc.Occurrences);
            Console.WriteLine($"  sub.Occurrences.Count = {Try(() => subOccs.Count)}");
            // Também pelas SubOccurrences da ocorrência de cima.
            dynamic subs = T("ocorrência.SubOccurrences", () => so.SubOccurrences);
            Console.WriteLine($"  SubOccurrences.Count = {Try(() => subs.Count)}");

            dynamic a2 = Try2(() => subOccs.Item(1)), b2 = Try2(() => subOccs.Item(2));
            dynamic aDoc = T("placa 1: OccurrenceDocument", () => a2.OccurrenceDocument);
            T("placa 1: Variables.Edit(Espessura, 66)", () => { aDoc.Variables.Edit("Espessura", "66"); return Try(() => aDoc.Variables.Item("Espessura").Value); });
            T("placa 2 (na sub): PutOrigin(0,0,-0.122)", () => { b2.PutOrigin(0.0, 0.0, -0.122); return "ok"; });
            Console.WriteLine($"  sub depois: {Box(so)}; Dirty sub {Try(() => sdoc.Dirty)}; Dirty placa {Try(() => aDoc.Dirty)}");
            DateTime tA = File.GetLastWriteTime(pA), tS = File.GetLastWriteTime(saPath);
            T("montagem de cima: Save", () => { top.Save(); return "ok"; });
            Console.WriteLine($"  gravou placa? {File.GetLastWriteTime(pA) != tA}; gravou sub? {File.GetLastWriteTime(saPath) != tS}");

            // Documents.Open da submontagem já carregada.
            dynamic opened = T("Documents.Open(Porta Molde.asm)", () => app.Documents.Open(saPath));
            Console.WriteLine($"  mesmo objeto? {ReferenceEquals((object)opened, (object)sdoc)}; ReadOnly {Try(() => opened.ReadOnly)}; Documents.Count {Try(() => app.Documents.Count)}");
            if (opened != null)
            {
                dynamic ob3 = Try2(() => opened.Occurrences.Item(2));
                T("aberta: PutOrigin(0,0,-0.150) na placa 2", () => { ob3.PutOrigin(0.0, 0.0, -0.150); return "ok"; });
                T("aberta: Save", () => { opened.Save(); return "ok"; });
                Console.WriteLine($"  gravou sub? {File.GetLastWriteTime(saPath) != tS}");
                T("aberta: Close", () => { opened.Close(); return "ok"; });
                Console.WriteLine($"  na de cima, depois: {Box(so)}");
            }
            T("montagem de cima: Close(false)", () => { top.Close(false); return "ok"; });

            // Reabrir tudo do disco e medir.
            dynamic re = app.Documents.Open(topPath);
            dynamic so2 = re.Occurrences.Item(1);
            Console.WriteLine($"reaberta do disco: {Box(so2)}");
            dynamic s2 = so2.OccurrenceDocument.Occurrences;
            for (int i = 1; i <= (int)s2.Count; i++) { dynamic o = s2.Item(i); Console.WriteLine($"  {Try(() => o.Name)}: Espessura {Try(() => o.OccurrenceDocument.Variables.Item("Espessura").Value)}"); }
            re.Close(false);
            return 0;
        }

        // ------------------------------------------------------------------ --build

        /// <summary>
        /// O MoldBaseBuilder de verdade num molde FALSO: postiço fixo 99999.101 (140×100, Z 0…32), postiço móvel
        /// 99999.201 (140×100, Z −45…0) e um produto (100×60, Z −12…6). Cria; troca série/P2/curso/montagem;
        /// força a 20.20 (guias na linha de centro → grupos refeitos); volta. Mede tudo e deixa aberto.
        /// </summary>
        private static int BuildBase(dynamic app)
        {
            string root = Path.Combine(_dir, "MD-99999");
            Directory.CreateDirectory(root);
            string Part(string name, double x, double y, double h, int side)
            {
                dynamic d = app.Documents.Add("SolidEdge.PartDocument");
                AutoEDM.Modeling.PrimitiveModeler.Build(d, new List<AutoEDM.Modeling.Primitive> {
                    new AutoEDM.Modeling.Primitive { Kind = "caixa", Name = name, SizeXMm = x, SizeYMm = y, HeightMm = h, PlaneIndex = 1, ExtrudeSide = side } });
                string p = Path.Combine(root, name + ".par");
                d.SaveAs(p); d.Close();
                return p;
            }
            string cav = Part("99999.101", 140, 100, 32, 2);
            string core = Part("99999.201", 140, 100, 45, 1);
            string prod = Part("Produto", 100, 60, 18, 2);

            dynamic asm = app.Documents.Add("SolidEdge.AssemblyDocument");
            asm.SaveAs(Path.Combine(root, "99999.asm"));
            asm.Occurrences.AddByFilename(cav);
            asm.Occurrences.AddByFilename(core);
            dynamic po = asm.Occurrences.AddByFilename(prod);
            po.PutOrigin(0.0, 0.0, -0.012);
            asm.Save();

            var opt = new AutoEDM.Mold.MoldBase.MoldBaseOptions();
            int Step(string label, AutoEDM.Mold.MoldBase.MoldBaseOptions o)
            {
                Console.WriteLine("==================== " + label);
                var scan = AutoEDM.Mold.MoldBase.MoldBaseBuilder.Scan(asm, AutoEDM.Mold.HeightAxis.Z);
                Console.WriteLine($"varredura: {scan.Items.Count} peça(s); existente: {(scan.Existing == null ? "não" : scan.Existing.OrderCode)}; problema: {scan.Problem}");
                foreach (var it in scan.Items)
                    Console.WriteLine($"  {it.Name}: produto {it.IsProduct}; X {it.MinMm[0]:0.#}…{it.MaxMm[0]:0.#} Y {it.MinMm[1]:0.#}…{it.MaxMm[1]:0.#} Z {it.MinMm[2]:0.#}…{it.MaxMm[2]:0.#}");
                var plan = AutoEDM.Mold.MoldBase.MoldBaseBuilder.Plan(scan, o);
                Console.WriteLine($"plano: {plan.Series} {plan.OrderCode}; P1 {plan.P1} P2 {plan.P2}; curso {plan.Stroke?.Stroke}; girado {plan.Rotated}; partição {plan.PartingMm}; altura total {plan.TotalHeight}; problema {plan.Problem}");
                foreach (var c in plan.Checks) Console.WriteLine("  " + c);
                foreach (var n in plan.Notes) Console.WriteLine("  nota: " + n);
                var t0 = DateTime.Now;
                var res = AutoEDM.Mold.MoldBase.MoldBaseBuilder.Apply(app, asm, scan, plan);
                Console.WriteLine($"Apply ({(DateTime.Now - t0).TotalSeconds:0.0} s): ok {res.Ok}; criado {res.Created}; {res.Message}");
                foreach (string w in res.Warnings) Console.WriteLine("  aviso: " + w);
                var after = AutoEDM.Mold.MoldBase.MoldBaseBuilder.Scan(asm, AutoEDM.Mold.HeightAxis.Z);
                if (after.Existing != null)
                {
                    Console.WriteLine($"  relido: {after.Existing.OrderCode} (P1 {after.Existing.P1}, P2 {after.Existing.P2}, curso {after.Existing.Stroke})");
                    Console.WriteLine($"  caixa do porta-molde: {Box(after.Existing.Occurrence)}");
                    dynamic subDoc = ((dynamic)after.Existing.Occurrence).OccurrenceDocument;
                    dynamic so = subDoc.Occurrences;
                    for (int i = 1; i <= (int)so.Count; i++)
                    {
                        dynamic o2 = so.Item(i);
                        dynamic pd = o2.OccurrenceDocument;
                        Console.WriteLine($"    {Try(() => o2.Name)}: {Box(o2)}; V = {Try(() => ((double)pd.Models.Item(1).Body.Volume * 1e9).ToString("0"))} mm³; recursos {Features(pd)}");
                    }
                }
                return res.Ok ? 0 : 1;
            }

            int fails = Step("1) criar Polimold automático (com colunas, buchas e pinos)", opt);
            var oB = opt.Clone(); oB.ForcedSeries = "30.35"; oB.ForcedP1 = 46; oB.ForcedP2 = 36; oB.ForcedStroke = 2;
            fails += Step("2) Polimold 30.35 46/36 curso 2 (= 15354 da biblioteca)", oB);
            var oM = opt.Clone(); oM.Family = AutoEDM.Mold.MoldBase.MoldBaseFamily.Mud;
            fails += Step("3) trocar para MUD (gaveta; a 200 engrossa para o postiço móvel e o curso some)", oM);
            var oM2 = oM.Clone(); oM2.ForcedP2 = 32;
            fails += Step("3b) gaveta com a 200 do padrão (32): rasgo sai da 200 e vai para a 201", oM2);
            var oY = opt.Clone(); oY.Family = AutoEDM.Mold.MoldBase.MoldBaseFamily.Babyplast;
            fails += Step("4) trocar para Babyplast", oY);
            var oZ = opt.Clone(); oZ.Family = AutoEDM.Mold.MoldBase.MoldBaseFamily.Zamak;
            fails += Step("4b) trocar para Zamak MINI", oZ);
            fails += Step("5) de volta ao Polimold automático", opt);
            string baseDir = AutoEDM.Mold.MoldBase.MoldBaseBuilder.FolderFor(Path.Combine(root, "99999.asm"));
            Console.WriteLine("arquivos na pasta: " + string.Join(", ", Directory.GetFileSystemEntries(baseDir).Select(Path.GetFileName)));
            asm.Save();
            Console.WriteLine("montagem aberta no SE: " + Path.Combine(root, "99999.asm"));
            return fails;
        }

        /// <summary>A janela do botão com uma varredura FALSA, desenhada num PNG (sem SE).</summary>
        private static int FormShot(string png, int family)
        {
            var scan = new AutoEDM.Mold.MoldBase.MoldBaseScan { AssemblyPath = @"W:\MD-99999\99999.asm", Axis = AutoEDM.Mold.HeightAxis.Z };
            scan.Items.Add(new AutoEDM.Mold.MoldBase.MoldBaseItem { Name = "99999.101.par:1", FileName = "99999.101.par", MinMm = new[] { -70.0, -50, 0 }, MaxMm = new[] { 70.0, 50, 32 } });
            scan.Items.Add(new AutoEDM.Mold.MoldBase.MoldBaseItem { Name = "99999.201.par:1", FileName = "99999.201.par", MinMm = new[] { -70.0, -50, -45 }, MaxMm = new[] { 70.0, 50, 0 } });
            scan.Items.Add(new AutoEDM.Mold.MoldBase.MoldBaseItem { Name = "Produto.par:1", FileName = "Produto.par", IsProduct = true, MinMm = new[] { -50.0, -30, -12 }, MaxMm = new[] { 50.0, 30, 6 } });
            var opt = new AutoEDM.Mold.MoldBase.MoldBaseOptions { Family = (AutoEDM.Mold.MoldBase.MoldBaseFamily)family };
            if (family == 3) foreach (var it in scan.Items) { for (int k = 0; k < 2; k++) { it.MinMm[k] /= 3; it.MaxMm[k] /= 3; } }
            if (family == 4) foreach (var it in scan.Items) { for (int k = 0; k < 2; k++) { it.MinMm[k] /= 1.6; it.MaxMm[k] /= 1.6; } }
            using (var form = new AutoEDM.AddIn.UI.MoldBaseForm(scan, AutoEDM.Mold.HeightAxis.Z, opt, o => AutoEDM.Mold.MoldBase.MoldBaseBuilder.Plan(scan, o)))
            {
                form.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
                form.Location = new System.Drawing.Point(-3000, -3000);
                form.Show();
                System.Windows.Forms.Application.DoEvents();
                using (var bmp = new System.Drawing.Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                    bmp.Save(png, System.Drawing.Imaging.ImageFormat.Png);
                }
                Console.WriteLine("png: " + png + "  plano pronto: " + form.Plan?.Ready + " " + form.Plan?.OrderCode);
                form.Close();
            }
            return 0;
        }

        private static string Features(dynamic pd)
        {
            var names = new List<string>();
            try
            {
                dynamic m = pd.Models.Item(1);
                foreach (string coll in new[] { "ExtrudedProtrusions", "ExtrudedCutouts" })
                {
                    dynamic items = ((object)m).GetType().InvokeMember(coll, BindingFlags.GetProperty, null, m, null);
                    for (int i = 1; i <= (int)items.Count; i++) names.Add(Try(() => items.Item(i).Name));
                }
            }
            catch (Exception ex) { names.Add(Msg(ex)); }
            return string.Join(", ", names);
        }

        private static void MakePlate(dynamic app, string path, double c, double a, double t)
        {
            dynamic d = app.Documents.Add("SolidEdge.PartDocument");
            d.ModelingMode = 2;
            dynamic v = d.Variables;
            v.Add("Comprimento", c.ToString(CultureInfo.InvariantCulture), 1);
            v.Add("Largura", a.ToString(CultureInfo.InvariantCulture), 1);
            v.Add("Espessura", t.ToString(CultureInfo.InvariantCulture), 1);
            dynamic pr = NewProfile(d, d.RefPlanes.Item(1), out dynamic s);
            CenteredRect(d, pr, c / 2000, a / 2000, "Comprimento", "Largura", construction: false);
            pr.End(1);
            ((object)d.Models).GetType().InvokeMember("AddFiniteExtrudedProtrusion", BindingFlags.InvokeMethod, null, d.Models,
                new object[] { 1, new[] { (SolidEdgePart.Profile)pr }, 2, t / 1000 });
            dynamic depth = FindDimByValue(v, t / 1000);
            v.Edit((string)depth.SystemName, "Espessura");
            d.SaveAs(path); d.Close();
        }

        // ------------------------------------------------------------------ --learn

        /// <summary>
        /// SÓ LEITURA. Abre cada montagem de porta-molde (que o usuário NÃO tenha aberta), lista as ocorrências
        /// (2 níveis) com a caixa na montagem e, por peça distinta, a caixa local e os cilindros (Ø, faixa).
        /// Fecha sem salvar. Para aprender os padrões dos porta-moldes da casa.
        /// </summary>
        private static int Learn(dynamic app, string[] paths)
        {
            foreach (string path in paths)
            {
                Console.WriteLine("##### " + path);
                bool userHasIt = false;
                for (int i = 1; i <= (int)app.Documents.Count; i++)
                    if (string.Equals(Try(() => app.Documents.Item(i).FullName), path, StringComparison.OrdinalIgnoreCase)) userHasIt = true;
                if (userHasIt) { Console.WriteLine("  aberto pelo usuário — pulado."); continue; }
                dynamic doc;
                try { doc = app.Documents.Open(path); }
                catch (Exception ex) { Console.WriteLine("  não abriu: " + Msg(ex)); continue; }
                var parts = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                try { DumpOccs(doc, 0, parts); }
                catch (Exception ex) { Console.WriteLine("  varredura: " + Msg(ex)); }
                NamedVars("  vars da montagem", doc);
                foreach (var kv in parts) DumpPart(kv.Key, kv.Value);
                try { doc.Close(false); } catch (Exception ex) { Console.WriteLine("  fechar: " + Msg(ex)); }
            }
            return 0;
        }

        /// <summary>SÓ LEITURA: abre cada peça (que o usuário não tenha aberta), imprime a caixa do corpo, fecha sem salvar.</summary>
        private static int Dims(dynamic app, string[] paths)
        {
            foreach (string path in paths)
            {
                bool userHasIt = false;
                for (int i = 1; i <= (int)app.Documents.Count; i++)
                    if (string.Equals(Try(() => app.Documents.Item(i).FullName), path, StringComparison.OrdinalIgnoreCase)) userHasIt = true;
                if (userHasIt || !File.Exists(path)) { Console.WriteLine($"DIMS {path}: {(userHasIt ? "aberta pelo usuário" : "não existe")}"); continue; }
                dynamic d = null;
                try
                {
                    d = app.Documents.Open(path);
                    object body = d.Models.Item(1).Body;
                    Console.WriteLine(FaceGeometry.TryGetBodyRangeMm(body, out double[] mn, out double[] mx)
                        ? $"DIMS {Path.GetFileName(path)}: {mx[0] - mn[0]:0.##} x {mx[1] - mn[1]:0.##} x {mx[2] - mn[2]:0.##}"
                        : $"DIMS {Path.GetFileName(path)}: ?");
                }
                catch (Exception ex) { Console.WriteLine($"DIMS {Path.GetFileName(path)}: {Msg(ex)}"); }
                finally { try { if (d != null) d.Close(false); } catch { } }
            }
            return 0;
        }

        /// <summary>SÓ LEITURA: abre cada peça (que o usuário não tenha aberta) e lista as faces planas perpendiculares a X, Y ou Z (posição e faixa). Fecha sem salvar.</summary>
        private static int Faces(dynamic app, string[] paths)
        {
            foreach (string path in paths)
            {
                bool userHasIt = false;
                for (int i = 1; i <= (int)app.Documents.Count; i++)
                    if (string.Equals(Try(() => app.Documents.Item(i).FullName), path, StringComparison.OrdinalIgnoreCase)) userHasIt = true;
                if (userHasIt || !File.Exists(path)) { Console.WriteLine($"FACES {path}: {(userHasIt ? "aberta pelo usuário" : "não existe")}"); continue; }
                dynamic d = null;
                try
                {
                    d = app.Documents.Open(path);
                    dynamic faces = d.Models.Item(1).Body.Faces[6]; // igQueryPlane
                    var rows = new List<string>();
                    for (int i = 1; i <= (int)faces.Count; i++)
                    {
                        if (!FaceGeometry.TryGetRangeMm(faces.Item(i), out double[] a, out double[] b)) continue;
                        int k = Enumerable.Range(0, 3).FirstOrDefault(j => b[j] - a[j] <= 0.01);
                        if (b[k] - a[k] > 0.01) continue;
                        string[] n = { "X", "Y", "Z" };
                        int u = (k + 1) % 3, v = (k + 2) % 3;
                        rows.Add($"{n[k]} {a[k],8:0.##}  {n[u]} {a[u]:0.#}..{b[u]:0.#}  {n[v]} {a[v]:0.#}..{b[v]:0.#}");
                    }
                    rows.Sort(StringComparer.Ordinal);
                    Console.WriteLine($"FACES {Path.GetFileName(path)}: {rows.Count} planas");
                    foreach (string r in rows) Console.WriteLine("  " + r);
                    dynamic cyl = d.Models.Item(1).Body.Faces[10]; // igQueryCylinder
                    for (int i = 1; i <= (int)cyl.Count; i++)
                        if (FaceGeometry.TryGetRangeMm(cyl.Item(i), out double[] a, out double[] b))
                            Console.WriteLine($"  Ø{(double)cyl.Item(i).Geometry.Radius * 2000:0.##} centro ({(a[0] + b[0]) / 2:0.#}; {(a[1] + b[1]) / 2:0.#}) Z {a[2]:0.#}..{b[2]:0.#}");
                }
                catch (Exception ex) { Console.WriteLine($"FACES {Path.GetFileName(path)}: {Msg(ex)}"); }
                finally { try { if (d != null) d.Close(false); } catch { } }
            }
            return 0;
        }

        /// <summary>SÓ LEITURA: abre cada montagem (que o usuário não tenha aberta) e imprime a pose (R linha a linha, t em mm) de cada ocorrência do 1º nível. Fecha sem salvar.</summary>
        private static int Matrices(dynamic app, string[] paths)
        {
            foreach (string path in paths)
            {
                bool userHasIt = false;
                for (int i = 1; i <= (int)app.Documents.Count; i++)
                    if (string.Equals(Try(() => app.Documents.Item(i).FullName), path, StringComparison.OrdinalIgnoreCase)) userHasIt = true;
                if (userHasIt || !File.Exists(path)) { Console.WriteLine($"MATRIX {path}: {(userHasIt ? "aberta pelo usuário" : "não existe")}"); continue; }
                dynamic d = null;
                try
                {
                    d = app.Documents.Open(path);
                    Console.WriteLine("##### " + path);
                    dynamic occs = d.Occurrences;
                    for (int i = 1; i <= (int)occs.Count; i++)
                    {
                        dynamic o = occs.Item(i);
                        string file = Path.GetFileName(Try(() => o.OccurrenceFileName) ?? "?");
                        var pose = AutoEDM.Assembly.AssemblyContext.TryGetPose(new AutoEDM.Assembly.OccurrenceInfo(o, file, null));
                        if (pose == null) { Console.WriteLine($"  {file}: sem pose"); continue; }
                        string r = string.Join(" ", Enumerable.Range(0, 9).Select(k => pose.R(k / 3, k % 3).ToString("0.###", CultureInfo.InvariantCulture)));
                        Console.WriteLine($"  {file} | R [{r}] | t ({pose.TxM * 1000:0.###}; {pose.TyM * 1000:0.###}; {pose.TzM * 1000:0.###}) | {pose.Layout}");
                    }
                }
                catch (Exception ex) { Console.WriteLine($"MATRIX {Path.GetFileName(path)}: {Msg(ex)}"); }
                finally { try { if (d != null) d.Close(false); } catch { } }
            }
            return 0;
        }

        private static void DumpOccs(dynamic asm, int depth, Dictionary<string, object> parts)
        {
            dynamic occs = asm.Occurrences;
            for (int i = 1; i <= (int)occs.Count; i++)
            {
                dynamic o = occs.Item(i);
                string file = Try(() => o.OccurrenceFileName);
                bool sub = false;
                try { sub = (bool)o.Subassembly; } catch { }
                Console.WriteLine($"{new string(' ', 2 + 2 * depth)}{(sub ? "[A] " : "")}{Path.GetFileName(file)} | {Box(o)}");
                if (sub && depth < 2) { try { DumpOccs(o.OccurrenceDocument, depth + 1, parts); } catch (Exception ex) { Console.WriteLine("    sub: " + Msg(ex)); } }
                else if (!sub && !parts.ContainsKey(file)) { try { parts[file] = (object)o.OccurrenceDocument; } catch { } }
            }
        }

        private static void DumpPart(string file, object docObj)
        {
            dynamic pd = docObj;
            try
            {
                dynamic model = pd.Models.Item(1);
                object body = model.Body;
                string dims = FaceGeometry.TryGetBodyRangeMm(body, out double[] mn, out double[] mx)
                    ? $"{mx[0] - mn[0]:0.##} x {mx[1] - mn[1]:0.##} x {mx[2] - mn[2]:0.##} (X {mn[0]:0.#}..{mx[0]:0.#} Y {mn[1]:0.#}..{mx[1]:0.#} Z {mn[2]:0.#}..{mx[2]:0.#})" : "?";
                var cyl = new List<string>();
                dynamic faces = ((dynamic)body).Faces[10];
                int n = (int)faces.Count;
                for (int i = 1; i <= n && cyl.Count < 40; i++)
                {
                    dynamic f = faces.Item(i);
                    double d = (double)f.Geometry.Radius * 2000;
                    string r = FaceGeometry.TryGetRangeMm(f, out double[] a, out double[] b)
                        ? $"[{a[0]:0.#},{a[1]:0.#},{a[2]:0.#}]-[{b[0]:0.#},{b[1]:0.#},{b[2]:0.#}]" : "";
                    cyl.Add($"Ø{d:0.##}{r}");
                }
                Console.WriteLine($"  PEÇA {Path.GetFileName(file)}: {dims}; {n} cil.: {string.Join(" ", cyl)}");
            }
            catch (Exception ex) { Console.WriteLine($"  PEÇA {Path.GetFileName(file)}: {Msg(ex)}"); }
            NamedVars("    vars", pd);
            try
            {
                dynamic feats = pd.DesignEdgebarFeatures;
                var names = new List<string>();
                for (int i = 1; i <= (int)feats.Count && names.Count < 80; i++) names.Add(Try(() => feats.Item(i).Name));
                Console.WriteLine("    recursos: " + string.Join(", ", names));
            }
            catch (Exception ex) { Console.WriteLine("    recursos: " + Msg(ex)); }
        }

        /// <summary>Variáveis com nome dado pelo usuário ou com fórmula (as cotas soltas "V123" ficam de fora).</summary>
        private static void NamedVars(string label, dynamic doc)
        {
            try
            {
                dynamic list = doc.Variables.Query("*", 2, 3, false);
                var items = new List<string>();
                for (int i = 1; i <= (int)list.Count; i++)
                {
                    dynamic v = list.Item(i);
                    string name = Try(() => v.DisplayName) ?? Try(() => v.Name) ?? "?";
                    string formula = Try(() => v.Formula) ?? "";
                    if (Regex.IsMatch(name, @"^V\d+$") && formula.Length == 0) continue;
                    object value = null;
                    try { value = v.Value; } catch { }
                    string shown = value is double d ? (Math.Abs(d) < 10 ? (d * 1000).ToString("0.###") + "mm|" + d.ToString("0.####") : d.ToString("0.###")) : Convert.ToString(value);
                    items.Add($"{name}={shown}{(formula.Length > 0 ? " [" + formula + "]" : "")}");
                }
                Console.WriteLine($"{label} ({items.Count}): {string.Join("; ", items)}");
            }
            catch (Exception ex) { Console.WriteLine($"{label}: {Msg(ex)}"); }
        }

        private static string Box(dynamic occ)
        {
            try
            {
                object mn = new double[3], mx = new double[3];
                occ.GetRangeBox(ref mn, ref mx);
                var a = (double[])mn; var b = (double[])mx;
                return $"X {a[0] * 1000:0.##}..{b[0] * 1000:0.##}  Y {a[1] * 1000:0.##}..{b[1] * 1000:0.##}  Z {a[2] * 1000:0.##}..{b[2] * 1000:0.##}";
            }
            catch (Exception ex) { return "GetRangeBox: " + Msg(ex); }
        }

        private static dynamic NewProfile(dynamic doc, object planeOrFace, out dynamic profileSet)
        {
            profileSet = doc.ProfileSets.Add();
            return profileSet.Profiles.Add(planeOrFace);
        }

        /// <summary>Retângulo centrado na origem do esboço (meio da diagonal sobre os dois eixos projetados), lados guiados por variáveis.</summary>
        private static dynamic[] CenteredRect(dynamic doc, dynamic prof, double a, double b, string varX, string varY, bool construction)
        {
            dynamic lines = prof.Lines2d;
            dynamic l1 = lines.AddBy2Points(-a, -b, a, -b);
            dynamic l2 = lines.AddBy2Points(a, -b, a, b);
            dynamic l3 = lines.AddBy2Points(a, b, -a, b);
            dynamic l4 = lines.AddBy2Points(-a, b, -a, -b);
            dynamic rel = prof.Relations2d;
            rel.AddKeypoint(l1, 1, l2, 0); rel.AddKeypoint(l2, 1, l3, 0); rel.AddKeypoint(l3, 1, l4, 0); rel.AddKeypoint(l4, 1, l1, 0);
            rel.AddHorizontal(l1); rel.AddHorizontal(l3); rel.AddVertical(l2); rel.AddVertical(l4);
            dynamic ax2 = prof.ProjectRefPlane(doc.RefPlanes.Item(2));
            dynamic ax3 = prof.ProjectRefPlane(doc.RefPlanes.Item(3));
            dynamic diag = lines.AddBy2Points(-a, -b, a, b);
            prof.ToggleConstruction(diag);
            rel.AddKeypoint(diag, 0, l1, 0); rel.AddKeypoint(diag, 1, l3, 0);
            rel.AddPointOn(diag, 2, ax2, -1); rel.AddPointOn(diag, 2, ax3, -1);
            if (construction) { prof.ToggleConstruction(l1); prof.ToggleConstruction(l2); prof.ToggleConstruction(l3); prof.ToggleConstruction(l4); }
            dynamic dims = prof.Dimensions;
            dims.Constraint = true;
            dynamic dx = dims.AddLength(l1); dx.Constraint = true; dx.Formula = varX;
            dynamic dy = dims.AddLength(l2); dy.Constraint = true; dy.Formula = varY;
            return new[] { l1, l2, l3, l4 };
        }

        private static void LinkDimByValue(dynamic vars, double valueM, string varName)
        {
            dynamic v = FindDimByValue(vars, valueM);
            if (v == null) { Console.WriteLine($"  cota de {valueM * 1000} mm não achada na tabela"); return; }
            string sys = Try(() => v.SystemName), disp = Try(() => v.DisplayName);
            T($"Edit pelo nome de SISTEMA '{sys}' = {varName}", () => { vars.Edit(sys, varName); return Try(() => v.Formula); });
            if (Try(() => v.Formula) != varName) T($"Edit pelo nome exibido '{disp}' = {varName}", () => { vars.Edit(disp, varName); return Try(() => v.Formula); });
        }

        private static dynamic TopFace(dynamic model)
        {
            dynamic faces = model.Body.Faces[6]; // igQueryPlane
            dynamic best = null; double bestZ = double.MinValue;
            for (int i = 1; i <= (int)faces.Count; i++)
            {
                dynamic f = faces.Item(i);
                if (!FaceGeometry.TryGetRangeMm(f, out double[] mn, out double[] mx)) continue;
                if (Math.Abs(mx[2] - mn[2]) < 1e-3 && mx[2] > bestZ) { bestZ = mx[2]; best = f; }
            }
            return best;
        }

        private static void Cylinders(dynamic model)
        {
            try
            {
                dynamic faces = model.Body.Faces[10]; // igQueryCylinder
                var s = new List<string>();
                for (int i = 1; i <= (int)faces.Count; i++) s.Add(((double)faces.Item(i).Geometry.Radius * 2000).ToString("0.##"));
                Console.WriteLine($"faces cilíndricas: {faces.Count} — Ø {string.Join(", ", s)}");
            }
            catch (Exception ex) { Console.WriteLine("cilindros: " + Msg(ex)); }
        }

        private static double Vol(dynamic doc)
        {
            try { return (double)doc.Models.Item(1).Body.Volume * 1e9; } catch { return double.NaN; }
        }

        /// <summary>Só o "Colar vínculo": CopyToClipboard com novas tentativas, e a fórmula que nasce.</summary>
        private static int Clip(dynamic app)
        {
            string mPath = Path.Combine(_dir, "Mestre.par");
            dynamic m = app.Documents.Add("SolidEdge.PartDocument");
            dynamic mv = m.Variables;
            mv.Add("Comprimento", "296", 1);
            m.SaveAs(mPath);
            bool copied = false;
            for (int i = 1; i <= 10 && !copied; i++)
            {
                try { mv.CopyToClipboard("Comprimento"); copied = true; Console.WriteLine($"CopyToClipboard OK na tentativa {i}"); }
                catch (Exception ex) { Console.WriteLine($"CopyToClipboard tentativa {i}: {Msg(ex)}"); System.Threading.Thread.Sleep(400); }
            }
            try
            {
                string fmts = string.Join(", ", System.Windows.Forms.Clipboard.GetDataObject()?.GetFormats() ?? new string[0]);
                Console.WriteLine("formatos na área de transferência: " + fmts);
            }
            catch (Exception ex) { Console.WriteLine("ler área de transferência: " + Msg(ex)); }
            if (!copied) { m.Close(false); return 3; }

            dynamic p = app.Documents.Add("SolidEdge.PartDocument");
            dynamic linked = T("AddFromClipboard(CompMestre, 1)", () => p.Variables.AddFromClipboard("CompMestre", 1));
            DumpVar(linked); DumpLink(linked);
            T("mestre Edit(Comprimento, 400)", () => { mv.Edit("Comprimento", "400"); return "ok"; });
            Console.WriteLine($"alvo CompMestre = {Try(() => linked.Value)} (os dois abertos, mestre não salvo)");
            T("mestre Save", () => { m.Save(); return "ok"; });
            Console.WriteLine($"alvo CompMestre = {Try(() => linked.Value)} (mestre salvo)");
            string pPath = Path.Combine(_dir, "Alvo.par");
            T("alvo SaveAs + Close", () => { p.SaveAs(pPath); p.Close(); return "ok"; });
            T("mestre Edit(500) + Save + Close", () => { mv.Edit("Comprimento", "500"); m.Save(); m.Close(); return "ok"; });
            p = app.Documents.Open(pPath);
            dynamic again = Try2(() => p.Variables.Item("CompMestre"));
            Console.WriteLine($"alvo reaberto com o mestre FECHADO: CompMestre = {Try(() => again.Value)}");
            DumpVar(again); DumpLink(again);
            p.Close(false);
            return 0;
        }

        private static void DumpLink(object o)
        {
            if (o == null) return;
            dynamic v = o;
            try { bool linked; v.HasExternalLink(out linked); Console.WriteLine($"    HasExternalLink = {linked}"); }
            catch (Exception ex) { Console.WriteLine("    HasExternalLink: " + Msg(ex)); }
            try { string sn, sd; v.GetExternalLinkInfo(out sn, out sd); Console.WriteLine($"    GetExternalLinkInfo = '{sn}' em '{sd}'"); }
            catch (Exception ex) { Console.WriteLine("    GetExternalLinkInfo: " + Msg(ex)); }
        }

        private static dynamic Try2(Func<object> f)
        {
            try { return f(); } catch (Exception ex) { Console.WriteLine("  " + Msg(ex)); return null; }
        }

        // ------------------------------------------------------------------ apoio

        private static void DumpVar(object v)
        {
            if (v == null) return;
            dynamic d = v;
            Console.WriteLine($"  var {Try(() => d.Name)}: Formula '{Try(() => d.Formula)}', Value {Try(() => d.Value)}, UnitsType {Try(() => d.UnitsType)}, " +
                              $"VariableType {Try(() => d.VariableType)}, DisplayName '{Try(() => d.DisplayName)}', SystemName '{Try(() => d.SystemName)}', Expose {Try(() => d.Expose)}");
        }

        private static void DumpDim(string label, object o)
        {
            if (o == null) { Console.WriteLine($"  {label}: null"); return; }
            dynamic d = o;
            Console.WriteLine($"  {label}: Value {Try(() => d.Value)}, Constraint {Try(() => d.Constraint)}, Formula '{Try(() => d.Formula)}', " +
                              $"DisplayName '{Try(() => d.DisplayName)}', SystemName '{Try(() => d.SystemName)}', Type {Try(() => d.Type)}");
        }

        private static void DumpLine(string label, object o)
        {
            if (o == null) { Console.WriteLine($"  {label}: null"); return; }
            try
            {
                dynamic l = o;
                double sx, sy, ex, ey;
                l.GetStartPoint(out sx, out sy); l.GetEndPoint(out ex, out ey);
                Console.WriteLine($"  {label}: ({sx * 1000:0.##},{sy * 1000:0.##}) → ({ex * 1000:0.##},{ey * 1000:0.##}); construção {Try(() => l.IsConstructionElement)}");
            }
            catch (Exception ex) { Console.WriteLine($"  {label}: {Msg(ex)}"); }
        }

        private static void ListVars(string label, dynamic vars)
        {
            Console.WriteLine($"--- {label}: Variables.Count = {Try(() => vars.Count)}");
            dynamic list = T("Query(\"*\", NameByBoth=2, VarTypeBoth=3)", () => vars.Query("*", 2, 3, false));
            if (list == null) return;
            int n = (int)list.Count;
            for (int i = 1; i <= n; i++)
            {
                dynamic v = list.Item(i);
                Console.WriteLine($"  [{i}] Name '{Try(() => v.Name)}' Display '{Try(() => v.DisplayName)}' System '{Try(() => v.SystemName)}' " +
                                  $"Formula '{Try(() => v.Formula)}' Value {Try(() => v.Value)} Type {Try(() => v.Type)} VarType {Try(() => v.VariableType)}");
            }
        }

        private static dynamic FindDimByValue(dynamic vars, double valueM)
        {
            try
            {
                dynamic list = vars.Query("*", 2, 1, false);
                for (int i = 1; i <= (int)list.Count; i++)
                {
                    dynamic v = list.Item(i);
                    if (Math.Abs((double)v.Value - valueM) < 1e-7 && string.IsNullOrEmpty((string)v.Formula)) return v;
                }
            }
            catch (Exception ex) { Console.WriteLine("FindDimByValue: " + Msg(ex)); }
            return null;
        }

        private static dynamic T(string label, Func<object> f)
        {
            try { object r = f(); Console.WriteLine($"OK  {label} → {Show(r)}"); return r; }
            catch (Exception ex) { Console.WriteLine($"ERR {label} → {Msg(ex)}"); return null; }
        }

        private static string Show(object r)
        {
            if (r == null) return "null";
            if (r is string || r.GetType().IsPrimitive) return Convert.ToString(r, CultureInfo.InvariantCulture);
            return Microsoft.VisualBasic.Information.TypeName(r);
        }

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
