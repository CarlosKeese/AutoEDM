using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using AutoEDM.Com;
using AutoEDM.Selection;
using SolidEdgeConstants;

namespace AutoEDM.Tools.SelectionProbe
{
    /// <summary>
    /// Sonda do DESTAQUE AO PASSAR O MOUSE (Carlos, 2026-10-29). A seleção por raio do AutoEDM
    /// (VisibleFacePicker) acerta a face à vista, mas só no clique: o usuário não vê antes qual face
    /// vai pegar, como vê na seleção nativa. Aqui, com um comando próprio no mouse da SE:
    ///   • a cada MouseMove, a SE entregou geometria (o destaque nativo do modo de localização)?
    ///   • o nosso raio no MouseMove: quanto custa, e o realce acompanha o cursor?
    /// SÓ realce (HighlightSet) — o modelo não é alterado. Termina com Esc, botão direito ou após
    /// o tempo pedido. Uso: SelectionProbe [quick|simple|smart] [segundos]
    /// FORA do processo da SE cada chamada COM custa mais que dentro do add-in: os tempos daqui são
    /// um TETO (compare com os cliques "[vista] … ms" do log do add-in, medidos dentro).
    /// </summary>
    internal static class Program
    {
        private static dynamic _doc;
        private static VisibleScene _scene;
        private static object _highlight;
        private static string _lastKey;
        private static bool _done;
        private static readonly Stopwatch Since = Stopwatch.StartNew();
        private static long _lastMoveEnd;
        private static int _moves, _skipped, _nativeGraphic, _hits;
        private static readonly List<long> MoveMs = new List<long>();
        private static readonly List<long> ClickMs = new List<long>();

        [STAThread]
        private static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            string modeArg = args.Length > 0 ? args[0].ToLowerInvariant() : "quick";
            int seconds = args.Length > 1 && int.TryParse(args[1], out int s) ? s : 60;
            seLocateModes mode = modeArg == "smart" ? seLocateModes.seSmartLocate
                               : modeArg == "simple" ? seLocateModes.seLocateSimple : seLocateModes.seLocateQuickPick;

            using (var connector = new SolidEdgeConnector())
            {
                dynamic app = connector.Connect(startIfNotRunning: false, makeVisible: false);
                if (app == null) { Console.WriteLine("Nenhuma Solid Edge aberta."); return 1; }
                _doc = app.ActiveDocument;
                Console.WriteLine($"Documento ativo: {(string)_doc.Name}");

                var sw = Stopwatch.StartNew();
                _scene = VisibleScene.Build((object)_doc);
                Console.WriteLine($"Cena lida em {sw.ElapsedMilliseconds} ms: {_scene.Describe()}");

                var cmd = (SolidEdgeFramework.Command)app.CreateCommand((int)seCmdFlag.seNoDeactivate);
                cmd.Start();
                SolidEdgeFramework.Mouse mouse = cmd.Mouse;
                try { mouse.WindowTypes = 1; } catch { }
                try { mouse.LocateMode = (int)mode; } catch (Exception ex) { Console.WriteLine("LocateMode: " + Msg(ex)); }
                try { mouse.ClearLocateFilter(); mouse.AddToLocateFilter((int)seLocateFilterConstants.seLocateFace); } catch (Exception ex) { Console.WriteLine("filtro: " + Msg(ex)); }
                try { mouse.EnabledMove = true; } catch (Exception ex) { Console.WriteLine("EnabledMove: " + Msg(ex)); }

                var events = (SolidEdgeFramework.DISEMouseEvents_Event)mouse;
                SolidEdgeFramework.DISEMouseEvents_MouseMoveEventHandler onMove = (b, sh, x, y, z, w, kp, g) => OnMove(w, g);
                SolidEdgeFramework.DISEMouseEvents_MouseClickEventHandler onClick = (b, sh, x, y, z, w, kp, g) => OnClick(b, w, g);
                SolidEdgeFramework.DISECommandEvents_TerminateEventHandler onEnd = () => _done = true;
                events.MouseMove += onMove;
                events.MouseClick += onClick;
                ((SolidEdgeFramework.DISECommandEvents_Event)cmd).Terminate += onEnd;
                try { app.StatusBar = "AutoEDM (sonda): passe o mouse sobre as peças; clique em algumas faces. Esc encerra."; } catch { }
                Console.WriteLine($"Modo {mode}. Passe o mouse sobre as peças por até {seconds} s; Esc ou botão direito encerra.");

                // Prazo fixo a partir daqui (antes somava o sw, que seguia correndo: nunca vencia).
                double deadline = Since.Elapsed.TotalSeconds + seconds;
                long nextBeat = Since.ElapsedMilliseconds + 5000;
                while (!_done && Since.Elapsed.TotalSeconds < deadline)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(5);
                    if (Since.ElapsedMilliseconds < nextBeat) continue;
                    nextBeat += 5000;
                    Console.WriteLine($"  … {_moves} MouseMove, {ClickMs.Count} clique(s), {_hits} face(s) achada(s)");
                }

                try { events.MouseMove -= onMove; events.MouseClick -= onClick; } catch { }
                try { ((SolidEdgeFramework.DISECommandEvents_Event)cmd).Terminate -= onEnd; } catch { }
                try { cmd.Done = true; } catch { }
                ClearHighlight();
                try { app.StatusBar = ""; } catch { }

                Console.WriteLine();
                Console.WriteLine($"MouseMove recebidos: {_moves}; pulados pelo intervalo mínimo: {_skipped}; com geometria NATIVA: {_nativeGraphic}");
                Console.WriteLine($"Raio no movimento: {MoveMs.Count} leitura(s), face achada em {_hits}; {Stats(MoveMs)}");
                Console.WriteLine($"Raio no clique: {ClickMs.Count}; {Stats(ClickMs)}");
                return 0;
            }
        }

        /// <summary>Pelo menos 30 ms entre duas leituras: o realce acompanha sem afogar a SE.</summary>
        private static void OnMove(object window, object graphic)
        {
            _moves++;
            if (graphic != null) _nativeGraphic++;
            if (Since.ElapsedMilliseconds - _lastMoveEnd < 30) { _skipped++; return; }
            var sw = Stopwatch.StartNew();
            VisiblePick pick = Pick(window);
            MoveMs.Add(sw.ElapsedMilliseconds);
            _lastMoveEnd = Since.ElapsedMilliseconds;
            if (pick?.Face == null) { if (_lastKey != null) { ClearHighlight(); _lastKey = null; } return; }
            _hits++;
            string key = pick.Face.OccurrenceName + "#" + Id(pick.Face.Face);
            if (key == _lastKey) return;
            _lastKey = key;
            ShowHighlight(pick.HighlightItem);
        }

        private static void OnClick(short button, object window, object graphic)
        {
            if (button != (short)seButton.seLEFT) { _done = true; return; }
            var sw = Stopwatch.StartNew();
            VisiblePick pick = Pick(window);
            ClickMs.Add(sw.ElapsedMilliseconds);
            Console.WriteLine($"  clique: {(pick?.Face != null ? $"'{pick.Face.OccurrenceName}' face {Id(pick.Face.Face)}" : "nada")}; " +
                              $"SE entregou {(graphic != null ? "geometria" : "nada")}; {sw.ElapsedMilliseconds} ms");
        }

        private static VisiblePick Pick(object window)
        {
            try
            {
                System.Drawing.Point p = Cursor.Position;
                if (!VisibleFacePicker.TryScreenToModel(window, p.X, p.Y, out double[] pointM)) return null;
                if (!VisibleFacePicker.TryReadCamera(window, out double[] eye, out double[] target, out bool persp)) return null;
                if (!RayMath.TryViewRay(eye, target, persp, pointM, out double[] origin, out double[] dir)) return null;
                return VisibleFacePicker.PickNearest(_doc, origin, dir, _scene);
            }
            catch (Exception ex) { Console.WriteLine("  raio: " + Msg(ex)); return null; }
        }

        private static void ShowHighlight(object item)
        {
            if (item == null) return;
            try
            {
                if (_highlight == null) _highlight = (object)_doc.HighlightSets.Add();
                dynamic hs = _highlight;
                hs.RemoveAll();
                hs.Color = 0 | (200 << 8) | (255 << 16);   // amarelo-claro, perto do destaque nativo
                hs.AddItem(item);
                hs.Draw();
            }
            catch (Exception ex) { Console.WriteLine("  realce: " + Msg(ex)); }
        }

        private static void ClearHighlight()
        {
            object s = _highlight;
            _highlight = null;
            if (s == null) return;
            try { dynamic hs = s; hs.RemoveAll(); hs.Draw(); hs.Delete(); } catch { }
        }

        private static string Id(object face) { try { return Convert.ToString(((dynamic)face).ID); } catch { return "?"; } }

        private static string Stats(List<long> ms)
        {
            if (ms.Count == 0) return "sem amostras";
            var s = ms.OrderBy(x => x).ToList();
            return $"mín {s[0]} / mediana {s[s.Count / 2]} / p90 {s[(int)(s.Count * 0.9)]} / máx {s[s.Count - 1]} ms";
        }

        private static string Msg(Exception ex) => (ex.InnerException ?? ex).GetBaseException().Message;
    }
}
