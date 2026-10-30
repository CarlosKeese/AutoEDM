using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using AutoEDM.Com;
using AutoEDM.Modeling;

namespace AutoEDM.Tools.UndoProbe
{
    /// <summary>
    /// Sonda da PRÉVIA dos botões (Carlos, 2026-10-29): a API não tem a prévia dinâmica dos recursos
    /// nativos, então a prévia teria de ser "criar de verdade e desfazer". Esta sonda mede, numa peça
    /// DESCARTÁVEL (nunca na ativa), as três formas de desfazer:
    ///   A. sem transação — quantos passos de desfazer as chamadas da API geram e se PartDocument.Undo os tira;
    ///   B. com transação no Command (ISECommandEx.BeginUndoTransaction/EndUndoTransaction) — vira 1 passo?
    ///   C. apagando as features criadas (o jeito do Preview do Criar Base).
    /// Em síncrono e em ordenado. --sig só imprime as assinaturas do interop.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            PrintSignatures();
            if (Array.IndexOf(args, "--sig") >= 0) return 0;
            if (Array.IndexOf(args, "--oring") >= 0) return ORingCycle(args);
            if (Array.IndexOf(args, "--zera") >= 0) return WhoClearsUndo();
            if (Array.IndexOf(args, "--plano") >= 0) return PlaneSeries();
            if (Array.IndexOf(args, "--minimo") >= 0) return MinimalRepro();
            if (Array.IndexOf(args, "--passos") >= 0) return ORingSteps();
            if (Array.IndexOf(args, "--lado") >= 0) return PlaneSideWithoutSketch();
            if (Array.IndexOf(args, "--ids") >= 0) return IdsSurviveUndo();
            if (Array.IndexOf(args, "--assoc") >= 0) return AssocProbe.Run();
            if (Array.IndexOf(args, "--refrig") >= 0) return AssocProbe.Refrig();

            using (var connector = new SolidEdgeConnector())
            {
                dynamic app = connector.Connect(startIfNotRunning: false, makeVisible: false);
                if (app == null) { Console.WriteLine("Nenhuma Solid Edge aberta."); return 1; }
                dynamic original = null;
                try { original = app.ActiveDocument; } catch { }

                foreach (int mode in new[] { 1, 2 })   // 1 = síncrono, 2 = ordenado
                {
                    dynamic doc = app.Documents.Add("SolidEdge.PartDocument");
                    try
                    {
                        try { doc.ModelingMode = mode; } catch (Exception ex) { Console.WriteLine($"ModelingMode={mode}: {Msg(ex)}"); }
                        Console.WriteLine();
                        Console.WriteLine($"===== peça descartável, ModelingMode = {(int)doc.ModelingMode} =====");
                        Build(doc, "base", Cyl(40, 20, 0));
                        Show(doc, "base");

                        RunVariant(app, doc, args.Length > 0 ? args[0] : "A");
                    }
                    catch (Exception ex) { Console.WriteLine("FALHOU: " + Msg(ex)); }
                    finally
                    {
                        try { doc.Close(false); } catch (Exception ex) { Console.WriteLine("Close: " + Msg(ex)); }
                    }
                }

                try { if (original != null) original.Activate(); } catch { }
                return 0;
            }
        }

        /// <summary>
        /// A  = sem transação, desfaz pela diferença da contagem de passos;
        /// B1 = BeginUndoTransaction … EndUndoTransaction e só depois Done = true;
        /// B2 = igual, com BeginTransparentUndoTransaction;
        /// B3 = Begin … End com o comando ainda vivo (contagem lida antes e depois do Done);
        /// C  = apaga as features novas pelo nome.
        /// </summary>
        private static void RunVariant(dynamic app, dynamic doc, string v)
        {
            Console.WriteLine($"--- variante {v}");
            int u0 = Undos(doc);
            List<string> before = Names(doc);
            object cmd = null;
            if (v.StartsWith("B"))
            {
                try { cmd = app.CreateCommand(2); ((dynamic)cmd).Start(); }
                catch (Exception ex) { Console.WriteLine("  CreateCommand/Start: " + Msg(ex)); }
                Show(doc, "comando iniciado");
                CallEx(cmd, v == "B2" ? "BeginTransparentUndoTransaction" : "BeginUndoTransaction", "AutoEDM prévia");
                Show(doc, "transação aberta");
            }

            double t = Build(doc, "2 cilindros", Cyl(10, 10, 20), Cyl(6, 5, 30));
            Show(doc, $"depois de criar ({t:0} ms)");

            if (cmd != null)
            {
                CallEx(cmd, "EndUndoTransaction");
                Show(doc, "transação fechada");
                if (v != "B3") { try { ((dynamic)cmd).Done = true; } catch (Exception ex) { Console.WriteLine("  Done: " + Msg(ex)); } Show(doc, "comando encerrado"); }
            }

            if (v == "C")
            {
                var sw = Stopwatch.StartNew();
                int deleted = 0;
                List<string> now = Names(doc);
                foreach (string n in now.Except(before).Reverse().ToList())
                {
                    try
                    {
                        dynamic feats = doc.DesignEdgebarFeatures;
                        for (int i = 1; i <= (int)feats.Count; i++)
                        {
                            dynamic f = feats.Item(i);
                            if ((string)f.Name != n) continue;
                            f.Delete(); deleted++; break;
                        }
                    }
                    catch (Exception ex) { Console.WriteLine($"  apagar {n}: {Msg(ex)}"); }
                }
                Show(doc, $"depois de apagar {deleted} feature(s) ({sw.ElapsedMilliseconds} ms)");
                return;
            }

            int steps = Undos(doc) - u0;
            Console.WriteLine($"  gerou {steps} passo(s) de desfazer.");
            if (v == "B4")
            {
                // O desfazer da INTERFACE (Ctrl+Z = PartEditUndo, 57643), não o da API.
                var sw = Stopwatch.StartNew();
                try { app.StartCommand(57643); app.DoIdle(); } catch (Exception ex) { Console.WriteLine("  StartCommand(PartEditUndo): " + Msg(ex)); }
                Show(doc, $"depois do Ctrl+Z da interface ({sw.ElapsedMilliseconds} ms)");
            }
            else
            {
                TimeUndo(doc, "Undo(1)", 1);
                if (steps > 1) TimeUndo(doc, $"Undo({steps - 1}) restante", steps - 1);
            }
            if (v == "B3") { try { ((dynamic)cmd).Done = true; } catch { } Show(doc, "comando encerrado no fim"); }
            List<string> after = Names(doc);
            Console.WriteLine("  voltou ao original: " + (after.SequenceEqual(before) ? "SIM" : "NÃO"));
        }

        // ------------------------------------------------------------------ prévia do O'ring

        /// <summary>
        /// Ciclo de prévia com o recurso REAL do botão O'ring (ORingGrooveModeler.Cut, com raio e
        /// quebra), numa peça ORDENADA descartável: corta, mede, desfaz pela contagem, confere se a
        /// face lida ANTES continua viva — 3 vezes, como quem ajusta o anel e olha de novo. Depois o
        /// mesmo com "apagar as features". "--peso N" põe N ressaltos na peça antes (peça mais pesada).
        /// </summary>
        private static int ORingCycle(string[] args)
        {
            int extra = 0;
            int ip = Array.IndexOf(args, "--peso");
            if (ip >= 0 && ip + 1 < args.Length) int.TryParse(args[ip + 1], out extra);

            using (var connector = new SolidEdgeConnector())
            {
                dynamic app = connector.Connect(startIfNotRunning: false, makeVisible: false);
                if (app == null) { Console.WriteLine("Nenhuma Solid Edge aberta."); return 1; }
                dynamic original = null;
                try { original = app.ActiveDocument; } catch { }
                dynamic doc = app.Documents.Add("SolidEdge.PartDocument");
                try
                {
                    // Corpo em SÍNCRONO (como as peças da casa) e o canal em ORDENADO: numa peça nova já
                    // ordenada, Models.Item(1).Body dá E_FAIL até a peça ser recalculada.
                    doc.ModelingMode = 1;
                    var prims = new List<Primitive> { Cyl(40, 30, 0) };
                    for (int i = 0; i < extra; i++)
                    {
                        double a = 2 * Math.PI * i / Math.Max(extra, 1);
                        prims.Add(new Primitive { Kind = "cilindro", Name = "r" + i, DiameterMm = 3, HeightMm = 2, PlaneIndex = 1, LiftMm = 30,
                                                  CenterXMm = 12 * Math.Cos(a), CenterYMm = 12 * Math.Sin(a) });
                    }
                    double tb = Build(doc, "peça", prims.ToArray());
                    Console.WriteLine($"===== O'ring: peça ordenada Ø40×30 + {extra} ressalto(s), montada em {tb:0} ms =====");
                    Show(doc, "base");

                    doc.ModelingMode = 2;
                    try { app.DoIdle(); } catch (Exception ex) { Console.WriteLine("  DoIdle: " + Msg(ex)); }
                    Show(doc, "em ordenado");
                    object face; Sealing.ORingTarget target;
                    if (!FindShaft(app, doc, out face, out target)) { Console.WriteLine("Face cilíndrica Ø40 + aresta não achadas."); return 2; }
                    Sealing.ORingGrooveSpec spec = Spec(target);
                    if (spec == null) { Console.WriteLine("Nenhum anel no catálogo para Ø40."); return 3; }
                    Console.WriteLine($"  alvo: Ø{target.SealingDiameterMm:0.##} {target.SuggestedKind}; anel {spec}");

                    for (int round = 1; round <= 3; round++)
                    {
                        // O corte mata os proxies de face: o alvo é relido a cada rodada.
                        var swRead = Stopwatch.StartNew();
                        if (round > 1 && !FindShaft(app, doc, out face, out target)) { Console.WriteLine($"  [{round}] alvo não relido."); break; }
                        if (round > 1) Console.WriteLine($"  [{round}] alvo relido em {swRead.ElapsedMilliseconds} ms");
                        int u0 = Undos(doc);
                        List<string> before = Names(doc);
                        var sw = Stopwatch.StartNew();
                        string name; List<string> problems;
                        bool ok = Sealing.ORingGrooveModeler.Cut(app, target, spec, 5.0, true, out name, out problems);
                        long tCut = sw.ElapsedMilliseconds;
                        int steps = Undos(doc) - u0;
                        Show(doc, $"[{round}] corte {(ok ? "OK" : "FALHOU")} '{name}' ({tCut} ms, {steps} passo(s){(problems.Count > 0 ? "; acabamento: " + string.Join("; ", problems) : "")})");
                        Console.WriteLine($"  [{round}] face lida antes, depois do corte: {Alive(face)}");
                        sw.Restart();
                        string how;
                        if (steps > 0)
                        {
                            how = $"Undo({steps})";
                            try { doc.Undo(steps); } catch (Exception ex) { Console.WriteLine($"  [{round}] Undo({steps}): {Msg(ex)}"); }
                        }
                        else
                        {
                            List<string> now = Names(doc);
                            how = $"pilha zerada — apagou {DeleteByName(doc, now.Except(before).Reverse().ToList())} feature(s)";
                        }
                        long tUndo = sw.ElapsedMilliseconds;
                        List<string> after = Names(doc);
                        Show(doc, $"[{round}] {how} ({tUndo} ms) — voltou ao original: {(after.SequenceEqual(before) ? "SIM" : "NÃO")}");
                        Console.WriteLine($"  [{round}] face lida antes, depois do desfazer: {Alive(face)}");
                    }

                    // Mesmo corte, apagando as features novas em vez de desfazer.
                    {
                        List<string> before = Names(doc);
                        int u0 = Undos(doc);
                        string name; List<string> problems;
                        var sw = Stopwatch.StartNew();
                        Sealing.ORingGrooveModeler.Cut(app, target, spec, 5.0, true, out name, out problems);
                        long tCut = sw.ElapsedMilliseconds;
                        sw.Restart();
                        List<string> now = Names(doc);
                        int deleted = 0;
                        foreach (string n in now.Except(before).Reverse().ToList())
                        {
                            dynamic feats = doc.DesignEdgebarFeatures;
                            for (int i = 1; i <= (int)feats.Count; i++)
                            {
                                dynamic f = feats.Item(i);
                                if ((string)f.Name != n) continue;
                                try { f.Delete(); deleted++; } catch (Exception ex) { Console.WriteLine($"  apagar {n}: {Msg(ex)}"); }
                                break;
                            }
                        }
                        List<string> after = Names(doc);
                        Show(doc, $"[apagar] corte {tCut} ms, apagou {deleted} feature(s) em {sw.ElapsedMilliseconds} ms — voltou: {(after.SequenceEqual(before) ? "SIM" : "NÃO")}, passos de desfazer +{Undos(doc) - u0}");
                        Console.WriteLine($"  [apagar] face lida antes: {Alive(face)}");
                    }
                    return 0;
                }
                catch (Exception ex) { Console.WriteLine("FALHOU: " + Msg(ex)); return 8; }
                finally
                {
                    try { doc.Close(false); } catch (Exception ex) { Console.WriteLine("Close: " + Msg(ex)); }
                    try { if (original != null) original.Activate(); } catch { }
                }
            }
        }

        /// <summary>
        /// Qual operação do corte do O'ring ZERA a pilha de desfazer? Cada uma isolada, numa peça
        /// descartável (corpo síncrono, depois ordenado), com a contagem antes e depois.
        /// </summary>
        private static int WhoClearsUndo()
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
                    Build(doc, "base", Cyl(40, 30, 0));
                    doc.ModelingMode = 2;
                    try { app.DoIdle(); } catch { }
                    Console.WriteLine($"===== quem zera a pilha? início: {Undos(doc)} passo(s)");

                    object plane = null;
                    var steps = new List<Tuple<string, Action>>
                    {
                        Tuple.Create<string, Action>("protrusão nova (controle)", () => Build(doc, "p", Cyl(10, 5, 30))),
                        Tuple.Create<string, Action>("renomear feature", () => { doc.DesignEdgebarFeatures.Item(1).Name = doc.DesignEdgebarFeatures.Item(1).Name; RenameLast(doc); }),
                        Tuple.Create<string, Action>("pintar face com estilo", () =>
                            Electrode.FaceColorPainter.PaintWithNamedStyle(doc, new List<object> { TopFace(doc) }, new[] { "Orange", "Laranja" }, "AutoEDM_Vedacao", System.Drawing.Color.Orange)),
                        Tuple.Create<string, Action>("RefPlanes.AddParallelByDistance", () =>
                            plane = (object)doc.RefPlanes.AddParallelByDistance(TopFace(doc), 0.005, 2, Type.Missing, Type.Missing, Type.Missing, Type.Missing)),
                        Tuple.Create<string, Action>("plano.Visible = false", () => ((dynamic)plane).Visible = false),
                        Tuple.Create<string, Action>("plano.Delete()", () => ((dynamic)plane).Delete()),
                        Tuple.Create<string, Action>("ProfileSets.Add()", () => plane = (object)doc.ProfileSets.Add()),
                        Tuple.Create<string, Action>("ProfileSet vazio .Delete()", () => ((dynamic)plane).Delete()),
                        Tuple.Create<string, Action>("ProfileSets.Add + Profiles.Add(RefPlane 1)", () => { dynamic ps = doc.ProfileSets.Add(); ps.Profiles.Add(doc.RefPlanes.Item(1)); plane = (object)ps; }),
                        Tuple.Create<string, Action>("ProfileSet com perfil .Delete()", () => ((dynamic)plane).Delete()),
                        Tuple.Create<string, Action>("plano paralelo 5 mm (lado 2)", () => plane = (object)doc.RefPlanes.AddParallelByDistance(TopFace(doc), 0.005, 2, Type.Missing, Type.Missing, Type.Missing, Type.Missing)),
                        Tuple.Create<string, Action>("Profiles.Add(plano novo)", () => { dynamic ps = doc.ProfileSets.Add(); _profile = (object)ps.Profiles.Add(plane); }),
                        Tuple.Create<string, Action>("ProfilePlaneFrame.Discover(perfil)", () => Sealing.ProfilePlaneFrame.Discover(_profile)),
                        Tuple.Create<string, Action>("corte do O'ring SEM acabamento", () =>
                        {
                            // Linha do tempo: a contagem lida a cada linha de log do corte.
                            int last = Undos(doc);
                            string prev = "(início)";
                            Action<Diagnostics.LogLevel, string> tap = (lvl, msg) =>
                            {
                                int now = Undos(doc);
                                if (now != last) Console.WriteLine($"      pilha {last} → {now}\n        depois de: {prev}\n        antes de:  {msg.Substring(0, Math.Min(140, msg.Length))}");
                                last = now;
                                prev = msg.Substring(0, Math.Min(140, msg.Length));
                            };
                            Diagnostics.Log.OnMessage += tap;
                            try { CutFresh(app, doc, false); } finally { Diagnostics.Log.OnMessage -= tap; }
                        }),
                        Tuple.Create<string, Action>("corte do O'ring COM acabamento", () => CutFresh(app, doc, true)),
                    };
                    foreach (var s in steps)
                    {
                        int before = Undos(doc);
                        var sw = Stopwatch.StartNew();
                        string err = null;
                        try { s.Item2(); } catch (Exception ex) { err = Msg(ex); }
                        int after = Undos(doc);
                        Console.WriteLine($"  {s.Item1,-36} {before,3} → {after,3}{(after < before ? "   <<< ZEROU/ENCOLHEU" : "")} ({sw.ElapsedMilliseconds} ms){(err != null ? "  erro: " + err : "")}");
                    }
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

        /// <summary>Planos de referência em série, na peça limpa: qual chamada zera a pilha, e em que modelagem.</summary>
        private static int PlaneSeries()
        {
            using (var connector = new SolidEdgeConnector())
            {
                dynamic app = connector.Connect(startIfNotRunning: false, makeVisible: false);
                if (app == null) { Console.WriteLine("Nenhuma Solid Edge aberta."); return 1; }
                dynamic original = null;
                try { original = app.ActiveDocument; } catch { }
                foreach (int mode in new[] { 1, 2 })
                {
                    dynamic doc = app.Documents.Add("SolidEdge.PartDocument");
                    try
                    {
                        doc.ModelingMode = 1;
                        Build(doc, "base", Cyl(40, 30, 0));
                        doc.ModelingMode = mode;
                        try { app.DoIdle(); } catch { }
                        Console.WriteLine($"===== planos em série, ModelingMode {mode}: início {Undos(doc)} passo(s)");
                        var calls = new List<Tuple<string, Func<object>>>
                        {
                            Tuple.Create<string, Func<object>>("paralelo à FACE do topo, 5 mm, lado 2", () => doc.RefPlanes.AddParallelByDistance(TopFace(doc), 0.005, 2, Type.Missing, Type.Missing, Type.Missing, Type.Missing)),
                            Tuple.Create<string, Func<object>>("paralelo à FACE do topo, 5 mm, lado 1", () => doc.RefPlanes.AddParallelByDistance(TopFace(doc), 0.005, 1, Type.Missing, Type.Missing, Type.Missing, Type.Missing)),
                            Tuple.Create<string, Func<object>>("paralelo à FACE do topo, 8 mm, lado 2", () => doc.RefPlanes.AddParallelByDistance(TopFace(doc), 0.008, 2, Type.Missing, Type.Missing, Type.Missing, Type.Missing)),
                            Tuple.Create<string, Func<object>>("paralelo ao RefPlane 1, 5 mm", () => doc.RefPlanes.AddParallelByDistance(doc.RefPlanes.Item(1), 0.005, 2, Type.Missing, Type.Missing, Type.Missing, Type.Missing)),
                            Tuple.Create<string, Func<object>>("protrusão (controle)", () => { Build(doc, "p", Cyl(10, 5, 30)); return null; }),
                            Tuple.Create<string, Func<object>>("paralelo à FACE do topo, 3 mm, lado 2", () => doc.RefPlanes.AddParallelByDistance(TopFace(doc), 0.003, 2, Type.Missing, Type.Missing, Type.Missing, Type.Missing)),
                        };
                        foreach (var c in calls)
                        {
                            int before = Undos(doc);
                            string err = null; object made = null;
                            try { made = c.Item2(); } catch (Exception ex) { err = Msg(ex); }
                            int after = Undos(doc);
                            Console.WriteLine($"  {c.Item1,-40} {before,3} → {after,3}{(after < before ? "   <<< ZEROU" : "")}  RefPlanes={Try(() => ((int)doc.RefPlanes.Count).ToString())}{(err != null ? "  erro: " + err : "")}");
                        }
                    }
                    catch (Exception ex) { Console.WriteLine("FALHOU: " + Msg(ex)); }
                    finally { try { doc.Close(false); } catch { } }
                }
                try { if (original != null) original.Activate(); } catch { }
                return 0;
            }
        }

        /// <summary>Cada combinação numa peça ORDENADA limpa: o que vem antes do plano paralelo faz ele zerar a pilha?</summary>
        private static int MinimalRepro()
        {
            using (var connector = new SolidEdgeConnector())
            {
                dynamic app = connector.Connect(startIfNotRunning: false, makeVisible: false);
                if (app == null) { Console.WriteLine("Nenhuma Solid Edge aberta."); return 1; }
                dynamic original = null;
                try { original = app.ActiveDocument; } catch { }
                var seqs = new List<Tuple<string, Action<dynamic>[]>>
                {
                    Tuple.Create("plano", new Action<dynamic>[] { Plane5 }),
                    Tuple.Create("plano, plano", new Action<dynamic>[] { Plane5, Plane5 }),
                    Tuple.Create("esboço em RefPlane 1 + apagar, plano", new Action<dynamic>[] { d => { dynamic ps = d.ProfileSets.Add(); ps.Profiles.Add(d.RefPlanes.Item(1)); ps.Delete(); }, Plane5 }),
                    Tuple.Create("pintar face, plano", new Action<dynamic>[] { d => Electrode.FaceColorPainter.PaintWithNamedStyle(d, new List<object> { TopFace(d) }, new[] { "Orange" }, "AutoEDM_Vedacao", System.Drawing.Color.Orange), Plane5 }),
                    Tuple.Create("renomear, plano", new Action<dynamic>[] { RenameLast, Plane5 }),
                    Tuple.Create("plano, esboço nele", new Action<dynamic>[] { Plane5, d => { dynamic ps = d.ProfileSets.Add(); ps.Profiles.Add(d.RefPlanes.Item((int)d.RefPlanes.Count)); } }),
                    Tuple.Create("plano, apagar o plano", new Action<dynamic>[] { Plane5, d => d.RefPlanes.Item((int)d.RefPlanes.Count).Delete() }),
                };
                foreach (var s in seqs)
                {
                    dynamic doc = app.Documents.Add("SolidEdge.PartDocument");
                    try
                    {
                        doc.ModelingMode = 1;
                        Build(doc, "base", Cyl(40, 30, 0));
                        doc.ModelingMode = 2;
                        try { app.DoIdle(); } catch { }
                        var trail = new List<string> { Undos(doc).ToString() };
                        string err = null;
                        foreach (Action<dynamic> a in s.Item2)
                        {
                            try { a(doc); } catch (Exception ex) { err = Msg(ex); }
                            trail.Add(Undos(doc).ToString());
                        }
                        bool zeroed = trail.Skip(1).Any(t => t == "0");
                        Console.WriteLine($"  {s.Item1,-40} pilha {string.Join(" → ", trail)}{(zeroed ? "   <<< ZEROU" : "")}{(err != null ? "  erro: " + err : "")}");
                    }
                    catch (Exception ex) { Console.WriteLine($"  {s.Item1}: FALHOU {Msg(ex)}"); }
                    finally { try { doc.Close(false); } catch { } }
                }
                try { if (original != null) original.Activate(); } catch { }
                return 0;
            }
        }

        /// <summary>O começo do corte do O'ring, chamada a chamada, com a pilha depois de cada uma.</summary>
        private static int ORingSteps()
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
                    Build(doc, "base", Cyl(40, 30, 0));
                    doc.ModelingMode = 2;
                    try { app.DoIdle(); } catch { }
                    object face = null, plane = null, ps = null, profile = null; Sealing.ORingTarget target = null;
                    var steps = new List<Tuple<string, Action>>
                    {
                        Tuple.Create<string, Action>("ler o alvo (ORingTargetReader)", () => FindShaft(app, doc, out face, out target)),
                        Tuple.Create<string, Action>("calcular o anel", () => Spec(target)),
                        Tuple.Create<string, Action>("app.ActiveDocument + Require(ordenado)", () => Com.ModelingEnvironment.Require(app.ActiveDocument, Com.ModelingEnv.Ordered, "sonda")),
                        Tuple.Create<string, Action>("model.Body.Faces[1].Count", () => { int n = (int)doc.Models.Item(1).Body.Faces[1].Count; }),
                        Tuple.Create<string, Action>("face do topo (Body.Faces + GetRange)", () => face = TopFace(doc)),
                        Tuple.Create<string, Action>("RefPlanes.AddParallelByDistance(face, 5, 2)", () => plane = (object)doc.RefPlanes.AddParallelByDistance(face, 0.005, 2, Type.Missing, Type.Missing, Type.Missing, Type.Missing)),
                        Tuple.Create<string, Action>("ProfileSets.Add()", () => ps = (object)doc.ProfileSets.Add()),
                        Tuple.Create<string, Action>("Profiles.Add(plano)", () => profile = (object)((dynamic)ps).Profiles.Add(plane)),
                        Tuple.Create<string, Action>("ProfilePlaneFrame.Discover", () => Sealing.ProfilePlaneFrame.Discover(profile)),
                    };
                    Console.WriteLine($"===== começo do corte, passo a passo: início {Undos(doc)}");
                    foreach (var s in steps)
                    {
                        int before = Undos(doc);
                        string err = null;
                        try { s.Item2(); } catch (Exception ex) { err = Msg(ex); }
                        int after = Undos(doc);
                        Console.WriteLine($"  {s.Item1,-46} {before,3} → {after,3}{(after < before ? "   <<< ZEROU" : "")}{(err != null ? "  erro: " + err : "")}");
                    }
                    return 0;
                }
                finally
                {
                    try { doc.Close(false); } catch { }
                    try { if (original != null) original.Activate(); } catch { }
                }
            }
        }

        /// <summary>
        /// O lado do plano paralelo SEM esboço de sondagem: GetRootPoint/GetNormal do RefPlane. E a
        /// sequência que o corte passaria a fazer — plano errado apagado sem ter tido esboço, esboço
        /// só no certo — conferida contra a pilha.
        /// </summary>
        private static int PlaneSideWithoutSketch()
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
                    Build(doc, "base", Cyl(40, 30, 0));
                    doc.ModelingMode = 2;
                    try { app.DoIdle(); } catch { }
                    Console.WriteLine($"===== lado do plano sem esboço: início {Undos(doc)}");
                    for (int i = 1; i <= 3; i++)
                    {
                        Sealing.ProfilePlaneFrame b = Sealing.ProfilePlaneFrame.OfRefPlane((object)doc.RefPlanes.Item(i));
                        Console.WriteLine(b == null ? $"  plano base {i}: ilegível"
                            : $"  plano base {i}: origem ({b.Origin[0]:0.###}; {b.Origin[1]:0.###}; {b.Origin[2]:0.###}) mm, normal ({b.Normal[0]:0.###}; {b.Normal[1]:0.###}; {b.Normal[2]:0.###}); pilha {Undos(doc)}");
                    }
                    object top = TopFace(doc);
                    var planes = new Dictionary<int, object>();
                    foreach (int side in new[] { 2, 1 })
                    {
                        object p = (object)doc.RefPlanes.AddParallelByDistance(top, 0.005, side, Type.Missing, Type.Missing, Type.Missing, Type.Missing);
                        planes[side] = p;
                        Console.WriteLine($"  lado {side}: raiz {Vec(p, "GetRootPoint", 1000)} mm, normal {Vec(p, "GetNormal", 1)}; pilha {Undos(doc)}");
                    }
                    // A peça vai de Z 0 a 30; o plano certo (para dentro) fica em Z = 25.
                    int good = Math.Abs(Coord(planes[2], 2) - 0.025) < 1e-6 ? 2 : 1, bad = 3 - good;
                    Console.WriteLine($"  lado certo: {good}");
                    try { ((dynamic)planes[bad]).Delete(); Console.WriteLine($"  apagou o plano do lado {bad}; pilha {Undos(doc)}"); }
                    catch (Exception ex) { Console.WriteLine($"  apagar o plano do lado {bad}: {Msg(ex)}; pilha {Undos(doc)}"); }
                    dynamic ps = doc.ProfileSets.Add();
                    object prof = (object)ps.Profiles.Add(planes[good]);
                    Sealing.ProfilePlaneFrame f = Sealing.ProfilePlaneFrame.Discover(prof);
                    Console.WriteLine($"  esboço no plano certo: {(f != null ? "OK" : "sem referencial")}; pilha {Undos(doc)}");
                    // E o caminho de hoje, para comparar: esboço no errado, descartado, depois outro plano.
                    dynamic ps2 = doc.ProfileSets.Add();
                    ps2.Profiles.Add(doc.RefPlanes.AddParallelByDistance(TopFace(doc), 0.005, bad, Type.Missing, Type.Missing, Type.Missing, Type.Missing));
                    ps2.Delete();
                    Console.WriteLine($"  (caminho de hoje) esboço no errado + Delete; pilha {Undos(doc)}");
                    doc.RefPlanes.AddParallelByDistance(TopFace(doc), 0.005, good, Type.Missing, Type.Missing, Type.Missing, Type.Missing);
                    Console.WriteLine($"  (caminho de hoje) plano novo depois; pilha {Undos(doc)}");
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

        /// <summary>
        /// A prévia do O'ring corta e desfaz; os proxies da face/aresta escolhidas morrem nos dois.
        /// Os Face.ID/Edge.ID sobrevivem (depois do Undo, a mesma face volta com o mesmo ID)?
        /// </summary>
        private static int IdsSurviveUndo()
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
                    Build(doc, "base", Cyl(40, 30, 0));
                    doc.ModelingMode = 2;
                    try { app.DoIdle(); } catch { }
                    object face; Sealing.ORingTarget target;
                    FindShaft(app, doc, out face, out target);
                    int faceId = (int)((dynamic)face).ID, edgeId = (int)((dynamic)target.Edge).ID;
                    Console.WriteLine($"===== IDs: face {faceId}, aresta {edgeId}; Ø{target.SealingDiameterMm:0.###}, aresta em {target.EdgeAxialMm:0.###}");
                    for (int round = 1; round <= 2; round++)
                    {
                        int u0 = Undos(doc);
                        string name; List<string> problems;
                        Sealing.ORingGrooveModeler.Cut(app, target, Spec(target), 5.0, true, out name, out problems);
                        int steps = Undos(doc) - u0;
                        doc.Undo(steps);
                        var sw = Stopwatch.StartNew();
                        object f2 = FaceById(doc, faceId), e2 = null;
                        if (f2 != null)
                        {
                            dynamic edges = ((dynamic)f2).Edges;
                            for (int j = 1; j <= (int)edges.Count; j++) { dynamic e = edges.Item(j); if ((int)e.ID == edgeId) { e2 = (object)e; break; } }
                        }
                        Sealing.ORingTarget t2 = f2 != null && e2 != null ? Sealing.ORingTargetReader.Read(f2, e2) : null;
                        Console.WriteLine($"  [{round}] corte {steps} passo(s), Undo; face {faceId}: {(f2 != null ? "ACHADA" : "sumiu")}, aresta {edgeId}: {(e2 != null ? "ACHADA" : "sumiu")}" +
                                          (t2 != null && t2.Ok ? $"; alvo relido Ø{t2.SealingDiameterMm:0.###}, aresta em {t2.EdgeAxialMm:0.###} ({sw.ElapsedMilliseconds} ms)" : "; alvo NÃO relido"));
                        if (t2 == null || !t2.Ok) return 4;
                        target = t2;
                    }
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

        private static object FaceById(dynamic doc, int id)
        {
            dynamic faces = doc.Models.Item(1).Body.Faces[1];
            for (int i = 1; i <= (int)faces.Count; i++) { dynamic f = faces.Item(i); if ((int)f.ID == id) return (object)f; }
            return null;
        }

        /// <summary>Vetor de um método com 1 parâmetro OUT double[3] (GetRootPoint, GetNormal).</summary>
        private static double[] ReadVec(object o, string method)
        {
            object[] a = { new double[3] };
            var mod = new ParameterModifier(1); mod[0] = true;
            o.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, o, a, new[] { mod }, null, null);
            return ((Array)a[0]).Cast<object>().Select(Convert.ToDouble).ToArray();
        }

        private static double Coord(object plane, int i) => ReadVec(plane, "GetRootPoint")[i];

        private static string Vec(object o, string method, double k)
        {
            try { double[] v = ReadVec(o, method); return $"({v[0] * k:0.###}; {v[1] * k:0.###}; {v[2] * k:0.###})"; }
            catch (Exception ex) { return "?" + Msg(ex); }
        }

        private static void Plane5(dynamic d) =>
            d.RefPlanes.AddParallelByDistance(TopFace(d), 0.005, 2, Type.Missing, Type.Missing, Type.Missing, Type.Missing);

        private static int DeleteByName(dynamic doc, List<string> names)
        {
            int deleted = 0;
            foreach (string n in names)
            {
                dynamic feats = doc.DesignEdgebarFeatures;
                for (int i = 1; i <= (int)feats.Count; i++)
                {
                    dynamic f = feats.Item(i);
                    if ((string)f.Name != n) continue;
                    try { f.Delete(); deleted++; } catch (Exception ex) { Console.WriteLine($"    apagar {n}: {Msg(ex)}"); }
                    break;
                }
            }
            return deleted;
        }

        private static object _profile;

        private static void RenameLast(dynamic doc)
        {
            dynamic feats = doc.DesignEdgebarFeatures;
            dynamic f = feats.Item((int)feats.Count);
            f.Name = "Renomeada pela sonda";
        }

        /// <summary>A face plana do topo do cilindro base (Z = 30), relida a cada chamada.</summary>
        private static object TopFace(dynamic doc)
        {
            dynamic faces = doc.Models.Item(1).Body.Faces[1];
            for (int i = 1; i <= (int)faces.Count; i++)
            {
                dynamic f = faces.Item(i);
                double[] mn = new double[3], mx = new double[3];
                object omn = mn, omx = mx;
                try { f.GetRange(ref omn, ref omx); } catch { continue; }
                mn = (double[])omn; mx = (double[])omx;
                if (Math.Abs(mn[2] - 0.03) < 1e-6 && Math.Abs(mx[2] - 0.03) < 1e-6 && mx[0] - mn[0] > 0.039) return (object)f;
            }
            throw new InvalidOperationException("face do topo não achada");
        }

        private static void CutFresh(dynamic app, dynamic doc, bool finish)
        {
            object face; Sealing.ORingTarget target;
            if (!FindShaft(app, doc, out face, out target)) throw new InvalidOperationException("alvo não achado");
            string name; List<string> problems;
            if (!Sealing.ORingGrooveModeler.Cut(app, target, Spec(target), 5.0, finish, out name, out problems))
                throw new InvalidOperationException("corte falhou");
        }

        private static bool FindShaft(dynamic app, dynamic doc, out object face, out Sealing.ORingTarget target)
        {
            face = null; target = null;
            dynamic faces;
            try { faces = doc.Models.Item(1).Body.Faces[1]; }
            catch (Exception ex)
            {
                // Ordenado: o corpo do Model pode não estar pronto; tenta pelo último recurso.
                Console.WriteLine("  Models.Item(1).Body.Faces: " + Msg(ex) + " — tentando ExtrudedProtrusions.Item(1).Body");
                faces = doc.Models.Item(1).ExtrudedProtrusions.Item(1).Body.Faces[1];
            }
            for (int i = 1; i <= (int)faces.Count; i++)
            {
                dynamic f = faces.Item(i);
                dynamic edges = f.Edges;
                for (int j = 1; j <= (int)edges.Count; j++)
                {
                    Sealing.ORingTarget t = Sealing.ORingTargetReader.Read((object)f, (object)edges.Item(j));
                    if (t == null || !t.Ok || !t.FaceIsCylindrical || Math.Abs(t.FaceDiameterMm - 40) > 0.01) continue;
                    face = (object)f; target = t;
                    return true;
                }
            }
            return false;
        }

        private static Sealing.ORingGrooveSpec Spec(Sealing.ORingTarget t)
        {
            var catalog = Sealing.ORingCatalog.LoadOrCreateDefault();
            var kind = t.SuggestedKind;
            var gland = new Sealing.ORingGlandContext
            {
                Sizing = Sealing.GrooveSizing.Table,
                PressureBar = Sealing.ORingGrooveRules.DefaultPressureBar,
                Fit = Sealing.IsoFits.DefaultFit,
                GrooveToleranceMm = Sealing.ORingGrooveRules.DefaultGrooveTolerance,
            };
            double section = Sealing.ORingGrooveCalculator.SuggestCrossSection(t.SealingDiameterMm, catalog.CrossSections);
            var cands = Sealing.ORingGrooveCalculator.Rank(catalog, kind, t.SealingDiameterMm, Sealing.SealMotion.Static, Sealing.Elastomer.Nbr,
                section, Sealing.FacePressure.Internal, 0.0, gland);
            if (cands.Count == 0) return null;
            var s = Sealing.ORingGrooveCalculator.Compute(cands[0].Ring, kind, t.SealingDiameterMm, Sealing.SealMotion.Static, Sealing.Elastomer.Nbr,
                Sealing.FacePressure.Internal, gland);
            Sealing.ORingGrooveCalculator.CheckPlacement(s, t, 5.0);
            return s;
        }

        /// <summary>O proxy da face ainda responde? (lê a área)</summary>
        private static string Alive(object face)
        {
            try { return $"VIVA (área {(double)((dynamic)face).Area * 1e6:0.#} mm²)"; }
            catch (Exception ex) { return "MORTA — " + Msg(ex); }
        }

        private static List<string> Names(dynamic doc)
        {
            var names = new List<string>();
            try { dynamic feats = doc.DesignEdgebarFeatures; for (int i = 1; i <= (int)feats.Count; i++) names.Add((string)feats.Item(i).Name); }
            catch { }
            return names;
        }

        // ------------------------------------------------------------------ assinaturas

        private static void PrintSignatures()
        {
            System.Reflection.Assembly interop = typeof(SolidEdgeFramework.Application).Assembly;
            Console.WriteLine("Interop: " + interop.GetName().Name + " " + interop.GetName().Version);
            foreach (string tn in new[] { "SolidEdgePart.PartDocument", "SolidEdgeFramework.ISECommandEx", "SolidEdgeFramework.ISECommandEx2", "SolidEdgeFramework.Command" })
            {
                Type ty = interop.GetType(tn);
                if (ty == null) { Console.WriteLine($"  {tn}: NÃO existe neste interop"); continue; }
                foreach (MethodInfo m in ty.GetMethods().Where(m => m.Name.IndexOf("Undo", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("Redo", StringComparison.OrdinalIgnoreCase) >= 0))
                    Console.WriteLine($"  {tn}.{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))}) : {m.ReturnType.Name}");
                if (tn.EndsWith("Command")) Console.WriteLine($"  {tn} implementa: {string.Join(", ", ty.GetInterfaces().Select(i => i.Name))}");
            }
        }

        /// <summary>
        /// ISECommandEx2 declarada à mão: o interop 219 não a tem (veio depois). Vtable pelo índice da
        /// typelib do SE 2026: IUnknown, as 9 entradas da ISECommand (memid 0x6000xxxx, na ordem do
        /// índice), a da ISECommandEx (0x6001) e as 2 da ISECommandEx2 (0x6002). Os slots da
        /// ISECommand só ocupam lugar — nunca são chamados por aqui.
        /// </summary>
        [ComImport, Guid("7c4aeeeb-93b0-48f4-b81d-07713327a293"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ISECommandEx2
        {
            void _Mouse(); void _Window(); void _GetDone(); void _PutDone();
            void _GetOnEditOwnerChange(); void _PutOnEditOwnerChange();
            void _GetOnEnvironmentChange(); void _PutOnEnvironmentChange(); void _Start();
            void BeginUndoTransaction([MarshalAs(UnmanagedType.BStr)] string name);
            void BeginTransparentUndoTransaction([MarshalAs(UnmanagedType.BStr)] string name);
            void EndUndoTransaction();
        }

        /// <summary>Método das interfaces estendidas do Command (a ISECommandEx2 declarada aqui; senão o interop; senão IDispatch).</summary>
        private static bool CallEx(object cmd, string method, params object[] args)
        {
            try
            {
                var ex2 = (ISECommandEx2)cmd;
                if (method == "BeginUndoTransaction") ex2.BeginUndoTransaction((string)args[0]);
                else if (method == "BeginTransparentUndoTransaction") ex2.BeginTransparentUndoTransaction((string)args[0]);
                else ex2.EndUndoTransaction();
                Console.WriteLine($"  ISECommandEx2 (declarada).{method}: OK");
                return true;
            }
            catch (Exception ex) { Console.WriteLine($"  ISECommandEx2 (declarada).{method}: {Msg(ex)}"); }

            System.Reflection.Assembly interop = typeof(SolidEdgeFramework.Application).Assembly;
            foreach (string tn in new[] { "SolidEdgeFramework.ISECommandEx", "SolidEdgeFramework.ISECommandEx2" })
            {
                Type ty = interop.GetType(tn);
                MethodInfo mi = ty?.GetMethod(method);
                if (mi == null) continue;
                try { mi.Invoke(cmd, args); Console.WriteLine($"  {tn}.{method}: OK"); return true; }
                catch (Exception ex) { Console.WriteLine($"  {tn}.{method}: {Msg(ex)}"); }
            }
            try
            {
                cmd.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, cmd, args);
                Console.WriteLine($"  IDispatch.{method}: OK");
                return true;
            }
            catch (Exception ex) { Console.WriteLine($"  IDispatch.{method}: {Msg(ex)}"); return false; }
        }

        // ------------------------------------------------------------------ peça

        private static Primitive Cyl(double d, double h, double lift) =>
            new Primitive { Kind = "cilindro", Name = $"Ø{d}", DiameterMm = d, HeightMm = h, PlaneIndex = 1, LiftMm = lift };

        private static double Build(dynamic doc, string what, params Primitive[] prims)
        {
            var sw = Stopwatch.StartNew();
            PrimitiveBuildResult r = PrimitiveModeler.Build(doc, new List<Primitive>(prims));
            if (r.Failed > 0 || r.Created == 0) Console.WriteLine($"  {what}: {r.Created} criado(s), {r.Failed} falha(s) — {r.Message} {string.Join(" ", r.Lines)}");
            return sw.Elapsed.TotalMilliseconds;
        }

        private static void TimeUndo(dynamic doc, string what, int n)
        {
            var sw = Stopwatch.StartNew();
            try { doc.Undo(n); }
            catch (Exception ex) { Console.WriteLine($"  {what}: {Msg(ex)}"); return; }
            Show(doc, $"{what} ({sw.ElapsedMilliseconds} ms)");
        }

        private static int DeleteLastFeatures(dynamic doc, int n)
        {
            int done = 0;
            try
            {
                dynamic feats = doc.DesignEdgebarFeatures;
                for (int i = (int)feats.Count; i >= 1 && done < n; i--)
                {
                    dynamic f = feats.Item(i);
                    string name = Try(() => (string)f.Name);
                    if (name.StartsWith("Sketch", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Esbo", StringComparison.OrdinalIgnoreCase)) continue;
                    f.Delete();
                    done++;
                }
            }
            catch (Exception ex) { Console.WriteLine("  apagar: " + Msg(ex)); }
            return done;
        }

        private static void Show(dynamic doc, string what) =>
            Console.WriteLine($"  {what}: features {Features(doc)} [{FeatureNames(doc)}], volume {Volume(doc)} mm³, passos de desfazer {Undos(doc)}");

        private static int Undos(dynamic doc) { try { return (int)doc.UndoTransactionCount(); } catch { return -1; } }

        private static int Features(dynamic doc) { try { return (int)doc.DesignEdgebarFeatures.Count; } catch { return -1; } }

        private static string FeatureNames(dynamic doc)
        {
            try
            {
                dynamic feats = doc.DesignEdgebarFeatures;
                var names = new List<string>();
                for (int i = 1; i <= (int)feats.Count; i++) names.Add(Try(() => (string)feats.Item(i).Name));
                return string.Join(", ", names);
            }
            catch (Exception ex) { return "?" + Msg(ex); }
        }

        private static string Volume(dynamic doc)
        {
            try { return ((double)doc.Models.Item(1).Body.Volume * 1e9).ToString("0"); }
            catch { return "—"; }
        }

        private static string Try(Func<string> f) { try { return f() ?? ""; } catch (Exception ex) { return "?" + Msg(ex); } }

        private static string Msg(Exception ex) => (ex.InnerException ?? ex).GetBaseException().Message;
    }
}
