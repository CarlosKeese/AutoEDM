using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AutoEDM.Com;
using AutoEDM.Diagnostics;
using AutoEDM.Electrode;
using AutoEDM.Model;
using AutoEDM.Selection;
using AutoEDM.UI;

namespace AutoEDM
{
    /// <summary>
    /// MVP console driver.
    ///
    /// Assembly mode (electrode planning — the real workflow):
    ///   AutoEDM.exe plan <montagem.asm> [R] [G] [B]
    ///     -> conecta, acha a ocorrência com faces na cor de queima, calcula
    ///        bounding box, blank e offsets por passe, e imprime o plano de
    ///        construção (não-destrutivo).
    ///
    /// Part mode (só teste da seleção por cor):
    ///   AutoEDM.exe faces <peca.par> [R] [G] [B]
    ///
    /// STA é obrigatório para automação do Solid Edge.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // Sem argumentos: abre a GUI de monitoramento (uso normal).
            if (args.Length == 0)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                return 0;
            }

            // Com argumentos: modos de console para depuração.
            return RunConsole(args);
        }

        private static int RunConsole(string[] args)
        {
            if (args.Length < 2)
            {
                PrintUsage();
                return 1;
            }

            string mode = args[0].ToLowerInvariant();
            string path = args[1];
            Color burn = ParseColor(args, startIndex: 2, fallback: Color.FromArgb(255, 0, 0));

            using (var connector = new SolidEdgeConnector())
            {
                try
                {
                    dynamic app = connector.Connect(startIfNotRunning: true, makeVisible: true);

                    switch (mode)
                    {
                        case "plan":
                            return RunPlan(connector, path, burn);
                        case "faces":
                            return RunFaces(connector, app, path, burn);
                        default:
                            PrintUsage();
                            return 1;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Erro fatal.", ex);
                    return -1;
                }
            }
        }

        private static int RunPlan(SolidEdgeConnector connector, string asmPath, Color burn)
        {
            if (!File.Exists(asmPath)) { Log.Error($"Arquivo não encontrado: {asmPath}"); return 2; }

            // Cores/Ra vêm do RaColorMap (padrão da engenharia); 'burn' é ignorado no plano.
            var p = new ElectrodeParams { ElectrodeName = "ELD-001", Material = "Grafite" };

            var builder = new ElectrodeBuilder(connector);
            var plan = builder.PlanFromAssembly(asmPath, p);

            Console.WriteLine();
            Log.Info("===== PLANO DE CONSTRUÇÃO =====");
            Log.Info($"Ocorrência-alvo : {plan.TargetOccurrenceName ?? "(nenhuma)"}");
            foreach (var w in plan.Warnings) Log.Warn("  ! " + w);

            foreach (var r in plan.Regions)
            {
                Log.Info($"Detalhe D{r.DetailIndex:00}  Ra {r.Ra} µm  cor RGB({r.Color.R},{r.Color.G},{r.Color.B})  " +
                         $"{r.FaceCount} face(s)  blank {r.SelectedBlank?.Describe() ?? "(pendente)"}");
                foreach (var pp in r.Passes)
                    Log.Info($"    {pp.ElectrodeFileName}: offset {pp.InwardOffsetMm:F3} mm");
                foreach (var w in r.Warnings) Log.Warn("    ! " + w);
            }

            return plan.TargetOccurrenceName == null ? 2 : 0;
        }

        private static int RunFaces(SolidEdgeConnector connector, dynamic app, string parPath, Color burn)
        {
            if (!File.Exists(parPath)) { Log.Error($"Arquivo não encontrado: {parPath}"); return 2; }

            dynamic doc = connector.OpenDocument(parPath);
            var selector = new FaceSelector();
            var faces = selector.SelectByColor(doc, app, burn, 8);

            if (faces.Count == 0) { Log.Warn("Nenhuma face na cor de queima."); return 2; }
            foreach (var f in faces.Take(50)) Log.Info("  " + f);
            return 0;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Uso:");
            Console.WriteLine("  AutoEDM.exe plan  <montagem.asm> [R] [G] [B]   # plano de eletrodo (recomendado)");
            Console.WriteLine("  AutoEDM.exe faces <peca.par>     [R] [G] [B]   # só testar seleção por cor");
            Console.WriteLine("Exemplo: AutoEDM.exe plan C:\\moldes\\cav.asm 255 0 0");
        }

        private static Color ParseColor(string[] args, int startIndex, Color fallback)
        {
            if (args.Length >= startIndex + 3
                && int.TryParse(args[startIndex], out int r)
                && int.TryParse(args[startIndex + 1], out int g)
                && int.TryParse(args[startIndex + 2], out int b))
            {
                return Color.FromArgb(
                    Math.Min(255, Math.Max(0, r)),
                    Math.Min(255, Math.Max(0, g)),
                    Math.Min(255, Math.Max(0, b)));
            }
            return fallback;
        }
    }
}
