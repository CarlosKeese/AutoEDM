// LiveProbe — pergunta à instância VIVA do Solid Edge o que ela realmente expõe.
//
// É o passo 4 do loop de descoberta: metadata estático acha tipos que o objeto em
// execução pode não implementar. Isto conecta, navega um caminho e despeja os membros
// que o objeto VIVO declara (via IDispatch -> ITypeInfo).
//
// Nunca abre, salva nem altera documento do usuário. Sem argumento de documento, só lê.
//
// Uso:
//   LiveProbe                              -> identifica a aplicação e lista Documents
//   LiveProbe --dump "ActiveDocument"      -> membros vivos do documento ativo
//   LiveProbe --dump "ActiveDocument.Models.Item(1).Body" --filter Face
//   LiveProbe --new part --dump "Models"   -> cria um doc PRÓPRIO (fecha sem salvar)
//
// Build: dotnet build -c Release   (net10.0-windows, x64)

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SeApiRecon.Probe;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string? dumpPath = null, filter = null, newDoc = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--dump": dumpPath = args[++i]; break;
                case "--filter": filter = args[++i]; break;
                case "--new": newDoc = args[++i]; break;
                case "-h" or "--help": Console.WriteLine(Usage); return 0;
            }
        }

        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.WriteLine($"runtime {RuntimeInformation.FrameworkDescription} / " +
                          $"{RuntimeInformation.ProcessArchitecture}, apartment " +
                          $"{Thread.CurrentThread.GetApartmentState()}");

        var filterOle = MessageFilter.Register();
        dynamic? app = null;
        dynamic? created = null;

        try
        {
            // Foto dos PIDs: se aparecer um Edge.exe novo, não anexamos — criamos.
            var before = EdgePids();

            Console.WriteLine("conectando (GetActiveObject, teto 90 s)...");
            var obj = ComInterop.GetActiveObjectWithTimeout(
                "SolidEdge.Application", TimeSpan.FromSeconds(90), out var how);
            Console.WriteLine($"  {how}");

            if (obj is null)
            {
                // SEM fallback por ProgID: medido que ele sobe uma 2ª instância invisível
                // em vez de anexar, e aí tudo "funciona" onde o usuário não vê.
                Console.Error.WriteLine("Não anexei à sessão aberta. Abra o Solid Edge e repita.");
                return 1;
            }

            var spawned = EdgePids().Except(before).ToArray();
            if (spawned.Length > 0)
            {
                Console.Error.WriteLine($"ABORTADO: surgiu Edge.exe novo (PID " +
                                        $"{string.Join(",", spawned)}) — não é a sessão do usuário.");
                return 4;
            }

            app = obj;
            Console.WriteLine($"  {(string)app.Name} {(string)app.Version}  " +
                              $"Visible={(bool)app.Visible}  Documents={(int)app.Documents.Count}");
            Console.WriteLine($"  tipo vivo: {ComLive.Identify((object)app)}");

            if (newDoc is not null)
            {
                var progId = newDoc.ToLowerInvariant() switch
                {
                    "part" => "SolidEdge.PartDocument",
                    "asm" or "assembly" => "SolidEdge.AssemblyDocument",
                    "draft" => "SolidEdge.DraftDocument",
                    "psm" or "sheetmetal" => "SolidEdge.SheetMetalDocument",
                    _ => newDoc,
                };
                Console.WriteLine($"criando documento PRÓPRIO: {progId}");
                created = app.Documents.Add(progId);
            }

            object target = created ?? (object)app;
            if (dumpPath is not null)
            {
                target = Walk(created ?? (object)app, dumpPath);
                Console.WriteLine($"\n{dumpPath}  ->  {ComLive.Identify(target)}");
            }

            Console.WriteLine($"\n=== membros VIVOS{(filter is null ? "" : $" contendo '{filter}'")} ===");
            var members = ComLive.Members(target, filter);
            if (members.Count == 0) Console.WriteLine("(nenhum — o objeto não expõe ITypeInfo?)");
            foreach (var m in members.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                Console.WriteLine("  " + m);
            Console.WriteLine($"\ntotal: {members.Count}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"falhou: {ex.GetType().Name} 0x{ex.HResult:X8} {ex.Message}");
            return 1;
        }
        finally
        {
            // Fecha SÓ o que este programa criou, sem salvar.
            if (created is not null)
            {
                try { created.Close(false); Console.WriteLine("documento próprio fechado."); }
                catch { /* ignora */ }
            }
            Console.WriteLine($"\nmessage filter: {filterOle.RetryCount} retries, " +
                              $"{filterOle.TimeoutCount} desistências");
            MessageFilter.Revoke();
        }
        return 0;
    }

    /// <summary>
    /// Navega "A.B.Item(1).C" a partir da raiz, por late binding. Suporta `Item(n)` e
    /// indexador numérico `Faces[1]` — que é como uma propget parametrizada se chama em C#.
    /// </summary>
    private static object Walk(object root, string path)
    {
        dynamic cur = root;
        foreach (var raw in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var step = raw.Trim();
            if (step.EndsWith(')') && step.Contains('('))
            {
                var name = step[..step.IndexOf('(')];
                var arg = step[(step.IndexOf('(') + 1)..^1];
                cur = int.TryParse(arg, out var n)
                    ? ((object)cur).GetType().InvokeMember(name,
                        System.Reflection.BindingFlags.InvokeMethod |
                        System.Reflection.BindingFlags.GetProperty,
                        null, cur, new object[] { n })!
                    : ((object)cur).GetType().InvokeMember(name,
                        System.Reflection.BindingFlags.InvokeMethod |
                        System.Reflection.BindingFlags.GetProperty,
                        null, cur, new object[] { arg })!;
            }
            else if (step.EndsWith(']') && step.Contains('['))
            {
                var name = step[..step.IndexOf('[')];
                var arg = int.Parse(step[(step.IndexOf('[') + 1)..^1]);
                cur = ((object)cur).GetType().InvokeMember(name,
                    System.Reflection.BindingFlags.GetProperty, null, cur, new object[] { arg })!;
            }
            else
            {
                cur = ((object)cur).GetType().InvokeMember(step,
                    System.Reflection.BindingFlags.GetProperty |
                    System.Reflection.BindingFlags.InvokeMethod,
                    null, cur, null)!;
            }
        }
        return (object)cur;
    }

    private static int[] EdgePids() =>
        System.Diagnostics.Process.GetProcessesByName("Edge")
            .Select(p => p.Id).OrderBy(x => x).ToArray();

    private const string Usage = """
        LiveProbe — despeja os membros que a instância VIVA do Solid Edge expõe.

          LiveProbe
          LiveProbe --dump "ActiveDocument" [--filter Style]
          LiveProbe --new part --dump "Models"

        --dump    caminho a navegar da Application (ou do doc criado com --new)
        --filter  só membros cujo nome contém este texto
        --new     part | asm | draft | psm — cria um documento PRÓPRIO e o fecha sem salvar
        """;
}
