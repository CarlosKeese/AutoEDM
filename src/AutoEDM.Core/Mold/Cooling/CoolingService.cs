using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using AutoEDM.Diagnostics;

namespace AutoEDM.Mold.Cooling
{
    /// <summary>
    /// A ponte entre a janela/MCP e o núcleo da refrigeração: de onde vêm as linhas, as roscas de
    /// tubo disponíveis e o texto do plano. Mesmo caminho para os dois — o agente vê o mesmo plano
    /// que o Carlos vê na janela.
    /// </summary>
    public static class CoolingService
    {
        /// <summary>Diâmetros de canal do Carlos (2026-09-24).</summary>
        public static readonly double[] Diameters = { 6, 8, 10, 12 };

        private static List<PipeThread> _threads;

        /// <summary>
        /// Roscas de tubo da base de furos — lidas uma vez por sessão. A pasta vem da própria SE
        /// (<c>GetGlobalParameter(seApplicationGlobalHolesDatabaseFolder = 498)</c>, [in,out]) e, se
        /// ela não responder, da instalação padrão.
        /// </summary>
        public static List<PipeThread> Threads(object app)
        {
            if (_threads != null) return _threads;
            string fromApp = null;
            try
            {
                object[] args = { 498, "" };
                var mod = new ParameterModifier(2); mod[1] = true;
                app?.GetType().InvokeMember("GetGlobalParameter", BindingFlags.InvokeMethod, null, app, args,
                    new[] { mod }, CultureInfo.InvariantCulture, null);
                fromApp = args[1] as string;
            }
            catch (Exception e) { Log.Info("Refrigeração: pasta da base de furos não veio da SE (" + e.GetBaseException().Message + ") — usando a instalação padrão."); }

            string folder = HoleDatabase.FindFolder(fromApp);
            List<PipeThread> all = HoleDatabase.ReadPipeThreads(folder);
            // As normas DIN/GB/JIS/UNI/GOST repetem as mesmas G e R: 914 linhas na SE 2023. A lista
            // útil é a ISO (G, R, Rp) + a ANSI (NPT/NPSM); as outras só se essas não existirem.
            _threads = all.Where(t => t.Standard == "ISO Metric" || t.Standard == "ANSI Inch").ToList();
            if (_threads.Count == 0) _threads = all;
            Log.Info($"Refrigeração: {_threads.Count} rosca(s) de tubo lidas de '{folder ?? "(base de furos não encontrada)"}'.");
            return _threads;
        }

        /// <summary>Acha uma rosca pelo tamanho ("G1/4", "R 1/8-28", "1/8-27 NPT") — espaços não importam.</summary>
        public static PipeThread FindThread(IEnumerable<PipeThread> threads, string size)
        {
            if (string.IsNullOrWhiteSpace(size)) return null;
            string want = Squash(size);
            return threads.FirstOrDefault(t => Squash(t.Size) == want)
                   ?? threads.FirstOrDefault(t => Squash(t.Size).Contains(want));
        }

        private static string Squash(string s) => new string((s ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

        /// <summary>Linhas da SELEÇÃO do SE; se nada útil estiver selecionado, de todos os esboços 3D.</summary>
        public static List<CoolingLine> ReadLines(dynamic partDoc, bool preferSelection, List<string> warnings, out string source)
        {
            if (preferSelection)
            {
                var items = new List<object>();
                try
                {
                    dynamic ss = partDoc.SelectSet;
                    int n = 0; try { n = (int)ss.Count; } catch { }
                    for (int i = 1; i <= n; i++) { try { items.Add((object)ss.Item(i)); } catch { } }
                }
                catch { }
                if (items.Count > 0)
                {
                    List<CoolingLine> sel = CoolingLineReader.FromItems(items, warnings);
                    if (sel.Count > 0) { source = $"seleção ({items.Count} item(ns))"; return sel; }
                }
            }
            List<CoolingLine> all = CoolingLineReader.AllSketch3D(partDoc, warnings, out int sketches);
            source = $"todos os esboços 3D da peça ({sketches})";
            return all;
        }

        /// <summary>Roscas padrão: engate G1/4, tampão G1/8 (quando a base de furos as tem).</summary>
        public static CoolingOptions DefaultOptions(IList<PipeThread> threads, double diameterMm = 8) => new CoolingOptions
        {
            DiameterMm = diameterMm,
            FittingThread = FindThread(threads, "G1/4"),
            PlugThread = FindThread(threads, "G1/8"),
        };

        /// <summary>
        /// O plano AUTOMÁTICO: mede na peça até onde vai o material de cada ponta (para prolongar os
        /// cantos até a face). Sem corpo legível, planeja sem prolongar e diz no <paramref name="note"/>.
        /// </summary>
        public static CoolingPlan BuildPlan(dynamic partDoc, IList<CoolingLine> lines, IDictionary<string, CoolingTerminal> overrides,
            CoolingOptions opt, out string note)
        {
            CoolingExitProbe probe = CoolingExitProbe.For(partDoc);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            CoolingPlan plan = CoolingPlanner.Plan(lines, overrides, opt, probe == null ? null : (Func<double[], double[], CoolingExit>)probe.Exit);
            note = probe == null
                ? "Não consegui ler o corpo da peça — sem prolongamento automático dos cantos."
                : $"Saídas medidas na peça ({probe.Queries} raio(s), {sw.ElapsedMilliseconds} ms).";
            Log.Info("Refrigeração: " + note);
            return plan;
        }

        public static string TerminalName(CoolingTerminal t)
        {
            switch (t)
            {
                case CoolingTerminal.Fitting: return "engate";
                case CoolingTerminal.Plug: return "tampão";
                case CoolingTerminal.Through: return "passante";
                default: return "cega";
            }
        }

        public static string Describe(IList<CoolingLine> lines, CoolingPlan plan, CoolingOptions opt)
        {
            var pt = CultureInfo.GetCultureInfo("pt-BR");
            var sb = new StringBuilder();
            sb.AppendLine($"{lines.Count} linha(s), canal Ø{opt.DiameterMm.ToString("0.##", pt)}, sobrefuro no cruzamento {opt.Overshoot.ToString("0.##", pt)} mm.");
            foreach (CoolingLine l in lines)
                sb.AppendLine(string.Format(pt, "  {0}: ({1:0.##}; {2:0.##}; {3:0.##}) → ({4:0.##}; {5:0.##}; {6:0.##})  {7:0.##} mm",
                    l.Name, l.StartMm[0], l.StartMm[1], l.StartMm[2], l.EndMm[0], l.EndMm[1], l.EndMm[2], l.LengthMm));
            sb.AppendLine("Bocas na face:");
            foreach (CoolingEnd e in plan.FreeEnds)
                sb.AppendLine(string.Format(pt, "  {0}: {1}{2} em ({3:0.##}; {4:0.##}; {5:0.##})", e.Key, TerminalName(e.Terminal),
                    e.Extended ? $" (prolongada {e.ExtensionMm:0.##} mm até a face)" : "", e.PointMm[0], e.PointMm[1], e.PointMm[2]));
            sb.AppendLine($"{plan.Holes.Count} furo(s):");
            foreach (CoolingHolePlan h in plan.Holes)
                sb.AppendLine(string.Format(pt, "  {0} — profundidade {1:0.##} mm, {2}",
                    h.Label, h.DepthMm, h.Thread != null ? $"rosca {h.ThreadDepthMm:0.#} mm (broca Ø{h.Thread.TapDrillMm:0.##})" : h.DrillPoint ? "ponta de broca" : "fundo reto"));
            foreach (string p in plan.Problems) sb.AppendLine("PROBLEMA: " + p);
            foreach (string n in plan.Notes) sb.AppendLine("ATENÇÃO: " + n);
            return sb.ToString();
        }
    }
}
