using System;
using System.Globalization;
using System.IO;
using System.Linq;
using AutoEDM.Assembly;
using AutoEDM.Com;

namespace AutoEDM.Tools.PathFinderProbe
{
    /// <summary>
    /// Sonda externa do "Ordenar PathFinder". Por padrão SÓ LÊ a montagem ativa e mostra a ordem
    /// atual e a nova. Com <c>--teste</c> monta uma montagem DESCARTÁVEL (peças vazias na pasta
    /// temporária, colocadas fora de ordem), ordena, confere a leitura de volta e fecha sem salvar —
    /// nunca reordena a montagem do usuário.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            bool test = args.Any(a => a.Equals("--teste", StringComparison.OrdinalIgnoreCase));

            using (var connector = new SolidEdgeConnector())
            {
                dynamic app = connector.Connect(startIfNotRunning: false, makeVisible: false);
                if (app == null) { Console.WriteLine("Nenhuma Solid Edge aberta."); return 1; }

                if (!test)
                {
                    dynamic doc = connector.GetActiveDocument();
                    if (doc == null || (int)doc.Type != 3) { Console.WriteLine("O documento ativo não é uma montagem."); return 1; }
                    Console.WriteLine("Montagem: " + (string)doc.FullName);
                    PathFinderSortResult plan = PathFinderSorter.Plan((object)doc);
                    Console.WriteLine(plan.Summary());
                    Console.WriteLine("ATUAL: " + string.Join(" | ", plan.Before));
                    Console.WriteLine();
                    Console.WriteLine("NOVA:  " + string.Join(" | ", plan.After));
                    return plan.Ok ? 0 : 2;
                }
                return RunTest(app);
            }
        }

        private static int RunTest(dynamic app)
        {
            string dir = Path.Combine(Path.GetTempPath(), "claude", "u--Code-AutoEDM",
                "f1d6bd47-5bee-4af4-b7eb-39eecfdbf7e3", "scratchpad", "pathfinder");
            Directory.CreateDirectory(dir);
            string[] names = { "15335.200.par", "Parafuso M6.par", "15335.101.par", "15335.99.par", "15335.100.par", "15335.1000.par" };
            foreach (string n in names)
            {
                string p = Path.Combine(dir, n);
                if (File.Exists(p)) continue;
                dynamic part = app.Documents.Add("SolidEdge.PartDocument");
                part.SaveAs(p);
                part.Close(false);
            }

            dynamic asm = app.Documents.Add("SolidEdge.AssemblyDocument");
            try
            {
                if (Environment.GetCommandLineArgs().Any(a => a == "--semantica"))
                {
                    // A, B, C — e um ReorderOccurrence de cada tipo, lendo a ordem depois de cada um.
                    dynamic oc = asm.Occurrences;
                    dynamic A = oc.AddByFilename(Path.Combine(dir, names[0]));
                    dynamic B = oc.AddByFilename(Path.Combine(dir, names[1]));
                    dynamic C = oc.AddByFilename(Path.Combine(dir, names[2]));
                    Action<string> show = s =>
                    {
                        var l = new System.Collections.Generic.List<string>();
                        for (int i = 1; i <= (int)oc.Count; i++) l.Add((string)oc.Item(i).Name);
                        Console.WriteLine(s.PadRight(28) + string.Join(" | ", l));
                    };
                    show("inicial");
                    oc.ReorderOccurrence(C, A, true); show("Reorder(C, A, true)");
                    oc.ReorderOccurrence(C, A, false); show("Reorder(C, A, false)");
                    oc.ReorderOccurrence(A, B, true); show("Reorder(A, B, true)");
                    oc.ReorderOccurrence(A, C, false); show("Reorder(A, C, false)");
                    return 0;
                }
                int reps = Environment.GetCommandLineArgs().Any(a => a == "--muitas") ? 25 : 1;
                var rnd = new Random(7);
                foreach (string n in Enumerable.Repeat(names, reps).SelectMany(x => x).OrderBy(_ => reps > 1 ? rnd.Next() : 0))
                    asm.Occurrences.AddByFilename(Path.Combine(dir, n));
                asm.Occurrences.AddByFilename(Path.Combine(dir, "15335.100.par")); // 2ª posição do .100 no fim
                var sw = System.Diagnostics.Stopwatch.StartNew();

                PathFinderSortResult res = PathFinderSorter.Apply((object)asm);
                Console.WriteLine($"Apply: {sw.ElapsedMilliseconds} ms");
                if (reps > 1) { Console.WriteLine(res.Summary()); return res.Ok ? 0 : 4; }
                Console.WriteLine("Antes: " + string.Join(" | ", res.Before));
                Console.WriteLine(res.Summary());
                Console.WriteLine(res.Listing());

                // Leitura independente da que o Apply conferiu: nome e índice de cada item.
                dynamic occ = asm.Occurrences;
                for (int i = 1; i <= (int)occ.Count; i++) Console.WriteLine($"  Item({i}) = {(string)occ.Item(i).Name}");
                return res.Ok ? 0 : 4;
            }
            finally { try { asm.Close(false); } catch { } }
        }
    }
}
