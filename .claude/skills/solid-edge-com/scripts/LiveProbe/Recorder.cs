// Recorder — registra cada chamada feita ao Solid Edge com o que ela DEVOLVEU de fato.
//
// O valor do Probe não é "deu certo": é a trilha do que foi chamado, com qual assinatura,
// que tipo vivo voltou, quais out-params se preencheram e qual HRESULT saiu quando falhou.
// Um `catch` que engole o erro destrói justamente o dado que estamos coletando.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SeApiRecon.Probe;

internal sealed class CallRecord
{
    public string Scenario { get; set; } = "";
    public string Step { get; set; } = "";
    /// <summary>A chamada como foi escrita — é isso que vira snippet depois.</summary>
    public string Call { get; set; } = "";
    public bool Ok { get; set; }
    public string? ResultLiveType { get; set; }
    public string? ResultTypeLib { get; set; }
    public string? ResultInterfaceGuid { get; set; }
    public string? ResultClrType { get; set; }
    public string? ResultValue { get; set; }
    public Dictionary<string, string>? OutParams { get; set; }
    public string? ErrorType { get; set; }
    public string? Hresult { get; set; }
    public string? HresultName { get; set; }
    public string? ErrorMessage { get; set; }
    public long ElapsedMs { get; set; }
    public List<string> Notes { get; set; } = [];
}

[SupportedOSPlatform("windows")]
internal sealed class Recorder
{
    private readonly List<CallRecord> _records = [];
    private string _scenario = "(sem cenário)";

    public IReadOnlyList<CallRecord> Records => _records;
    public int Failures => _records.Count(r => !r.Ok);

    public void Scenario(string name)
    {
        _scenario = name;
        Console.WriteLine();
        Console.WriteLine($"=== {name} ".PadRight(78, '='));
    }

    /// <summary>Executa uma chamada que devolve valor, registrando tudo.</summary>
    public T? Do<T>(string step, string call, Func<T> action, params string[] notes)
    {
        var rec = new CallRecord { Scenario = _scenario, Step = step, Call = call };
        rec.Notes.AddRange(notes);
        var sw = Stopwatch.StartNew();
        T? value = default;
        try
        {
            value = action();
            sw.Stop();
            rec.Ok = true;
            Describe(rec, value);
        }
        catch (Exception ex)
        {
            sw.Stop();
            rec.Ok = false;
            Capture(rec, ex);
        }
        rec.ElapsedMs = sw.ElapsedMilliseconds;
        _records.Add(rec);
        Print(rec);
        return value;
    }

    /// <summary>Executa uma chamada sem retorno (ou cujos resultados saem por out-param).</summary>
    public bool Run(string step, string call, Action action,
        Func<Dictionary<string, string>>? outParams = null, params string[] notes)
    {
        var rec = new CallRecord { Scenario = _scenario, Step = step, Call = call };
        rec.Notes.AddRange(notes);
        var sw = Stopwatch.StartNew();
        try
        {
            action();
            sw.Stop();
            rec.Ok = true;
            if (outParams is not null) rec.OutParams = outParams();
        }
        catch (Exception ex)
        {
            sw.Stop();
            rec.Ok = false;
            Capture(rec, ex);
            // Mesmo falhando, os out-params podem ter sido parcialmente escritos —
            // e saber disso é um achado.
            if (outParams is not null)
            {
                try { rec.OutParams = outParams(); } catch { /* ignora */ }
            }
        }
        rec.ElapsedMs = sw.ElapsedMilliseconds;
        _records.Add(rec);
        Print(rec);
        return rec.Ok;
    }

    public void Note(string text)
    {
        _records.Add(new CallRecord { Scenario = _scenario, Step = "(nota)", Call = text, Ok = true });
        Console.WriteLine($"  nota: {text}");
    }

    private static void Describe(CallRecord rec, object? value)
    {
        if (value is null) { rec.ResultValue = "null"; return; }
        rec.ResultClrType = value.GetType().FullName;

        if (Marshal.IsComObject(value))
        {
            var id = ComLive.Identify(value);
            rec.ResultLiveType = id.InterfaceName;
            rec.ResultTypeLib = id.TypeLibName;
            rec.ResultInterfaceGuid = id.InterfaceGuid;
            if (id.Error is not null) rec.Notes.Add($"identificação: {id.Error}");
        }
        else
        {
            rec.ResultValue = value switch
            {
                Array a => $"Array[{a.Length}] de {a.GetType().GetElementType()?.Name}: " +
                           string.Join(", ", a.Cast<object?>().Take(8).Select(Fmt)),
                _ => Fmt(value),
            };
        }
    }

    private static string Fmt(object? v) => v switch
    {
        null => "null",
        double d => d.ToString("G17", System.Globalization.CultureInfo.InvariantCulture),
        float f => f.ToString("G9", System.Globalization.CultureInfo.InvariantCulture),
        _ => v.ToString() ?? "",
    };

    private static void Capture(CallRecord rec, Exception ex)
    {
        // Um erro de binder (late binding) embrulha o COMException de verdade.
        var root = ex;
        while (root.InnerException is not null) root = root.InnerException;

        rec.ErrorType = ex.GetType().Name;
        rec.ErrorMessage = Collapse(ex.Message);
        var hr = root is COMException ? root.HResult : ex.HResult;
        rec.Hresult = $"0x{hr:X8}";
        rec.HresultName = Hresults.Name(hr);
        if (!ReferenceEquals(root, ex))
            rec.Notes.Add($"causa raiz: {root.GetType().Name}: {Collapse(root.Message)}");
    }

    private static string Collapse(string s) =>
        string.Join(" ", s.Split('\n', '\r').Select(x => x.Trim()).Where(x => x.Length > 0));

    private static void Print(CallRecord rec)
    {
        if (rec.Ok)
        {
            var what = rec.ResultLiveType is not null
                ? $"-> {rec.ResultLiveType} ({rec.ResultTypeLib})"
                : rec.ResultValue is not null ? $"-> {Trunc(rec.ResultValue, 90)}" : "ok";
            Console.WriteLine($"  OK    {rec.Step,-42} {what}  [{rec.ElapsedMs} ms]");
            if (rec.OutParams is not null)
                foreach (var (k, v) in rec.OutParams)
                    Console.WriteLine($"          out {k,-26} = {Trunc(v, 80)}");
        }
        else
        {
            Console.WriteLine($"  FALHA {rec.Step,-42} {rec.Hresult} {rec.HresultName}  [{rec.ElapsedMs} ms]");
            Console.WriteLine($"          {Trunc(rec.ErrorMessage ?? "", 150)}");
            if (rec.OutParams is not null)
                foreach (var (k, v) in rec.OutParams)
                    Console.WriteLine($"          out {k,-26} = {Trunc(v, 80)} (após a falha)");
        }
        foreach (var n in rec.Notes.Where(n => !n.StartsWith("causa raiz")))
            Console.WriteLine($"          · {n}");
    }

    private static string Trunc(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    public void SaveJson(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            generatedAtUtc = DateTime.UtcNow.ToString("O"),
            total = _records.Count,
            failures = Failures,
            records = _records,
        }, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }));
        Console.Error.WriteLine($"Probe: escrito {path}");
    }

    /// <summary>Resumo por cenário, para o fim da execução.</summary>
    public string Summary()
    {
        var sb = new StringBuilder();
        foreach (var g in _records.Where(r => r.Step != "(nota)").GroupBy(r => r.Scenario))
        {
            var ok = g.Count(r => r.Ok);
            var bad = g.Count(r => !r.Ok);
            sb.AppendLine($"  {g.Key,-44} {ok,3} ok, {bad,3} falha(s)");
        }
        return sb.ToString();
    }
}

internal static class Hresults
{
    private static readonly Dictionary<int, string> Known = new()
    {
        [unchecked((int)0x80010001)] = "RPC_E_CALL_REJECTED",
        [unchecked((int)0x8001010A)] = "RPC_E_SERVERCALL_RETRYLATER",
        [unchecked((int)0x80010108)] = "RPC_E_DISCONNECTED",
        [unchecked((int)0x800706BA)] = "RPC_S_SERVER_UNAVAILABLE",
        [unchecked((int)0x80004002)] = "E_NOINTERFACE",
        [unchecked((int)0x80004005)] = "E_FAIL",
        [unchecked((int)0x80070057)] = "E_INVALIDARG",
        [unchecked((int)0x8002000E)] = "DISP_E_BADPARAMCOUNT",
        [unchecked((int)0x8002000F)] = "DISP_E_PARAMNOTOPTIONAL",
        [unchecked((int)0x80020005)] = "DISP_E_TYPEMISMATCH",
        [unchecked((int)0x80020003)] = "DISP_E_MEMBERNOTFOUND",
        [unchecked((int)0x80020006)] = "DISP_E_UNKNOWNNAME",
        [unchecked((int)0x80020009)] = "DISP_E_EXCEPTION",
        [unchecked((int)0x80020008)] = "DISP_E_BADVARTYPE",
        [unchecked((int)0x8002802B)] = "TYPE_E_ELEMENTNOTFOUND",
        [unchecked((int)0x80029C4A)] = "TYPE_E_CANTLOADLIBRARY",
        [unchecked((int)0x800401E3)] = "MK_E_UNAVAILABLE",
        [unchecked((int)0x800401F3)] = "CO_E_CLASSSTRING",
        [unchecked((int)0x800401FD)] = "CO_E_OBJNOTCONNECTED",
        [unchecked((int)0x80040154)] = "REGDB_E_CLASSNOTREG",
        [unchecked((int)0x8000FFFF)] = "E_UNEXPECTED",
        [unchecked((int)0x80131509)] = "InvalidOperationException (CLR)",
        // Em late binding com `dynamic`, membro inexistente NÃO vira DISP_E_MEMBERNOTFOUND:
        // vira RuntimeBinderException do C#. Mesma causa, outro código.
        [unchecked((int)0x80131500)] = "RuntimeBinderException — MEMBRO NÃO EXISTE no objeto",
        [unchecked((int)0x80040B50)] = "SE: item não encontrado na coleção (nome/índice inválido)",
        [unchecked((int)0x8002000B)] = "DISP_E_BADINDEX",
        [0] = "S_OK",
    };

    public static string Name(int hr) => Known.TryGetValue(hr, out var n) ? n : "(desconhecido)";
}
