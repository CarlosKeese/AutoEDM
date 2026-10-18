// Infraestrutura COM do Probe: conexão, message filter e identificação de objeto vivo.
//
// Três problemas que todo cliente de automação do Solid Edge enfrenta e que não têm
// solução em biblioteca:
//
//   1. `Marshal.GetActiveObject` NÃO EXISTE no .NET moderno (ficou no .NET Framework).
//      Para pegar a instância já aberta é preciso P/Invoke na Running Object Table.
//   2. O SE é um servidor ocupado e de thread única. Sem um IOleMessageFilter, qualquer
//      chamada feita enquanto ele recalcula ou mostra modal volta como
//      RPC_E_CALL_REJECTED (0x80010001) ou RPC_E_SERVERCALL_RETRYLATER (0x8001010A).
//   3. Um objeto COM devolvido chega como `System.__ComObject`: o nome do tipo .NET não
//      diz nada. O nome REAL da interface está no ITypeInfo do próprio objeto vivo —
//      é essa a "verdade do objeto", e é o que prevê o que dá para chamar nele.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

namespace SeApiRecon.Probe;

[SupportedOSPlatform("windows")]
internal static class ComInterop
{
    [DllImport("ole32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void CLSIDFromProgID(string lpszProgID, out Guid lpclsid);

    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(
        ref Guid rclsid, IntPtr pvReserved,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

    /// <summary>
    /// Substituto de <c>Marshal.GetActiveObject</c>, que não existe no .NET moderno.
    /// Devolve null quando não há instância registrada na ROT (SE fechado).
    /// </summary>
    public static object? TryGetActiveObject(string progId)
    {
        Guid clsid;
        try
        {
            Diag($"CLSIDFromProgID(\"{progId}\") ...");
            CLSIDFromProgID(progId, out clsid);
            Diag($"  -> CLSID {clsid:B}");
        }
        catch (COMException ex)
        {
            Diag($"  -> CLSIDFromProgID falhou 0x{ex.HResult:X8}");
            return null;
        }

        try
        {
            Diag("GetActiveObject (ROT) ...");
            GetActiveObject(ref clsid, IntPtr.Zero, out var obj);
            Diag("  -> obtido da ROT");
            return obj;
        }
        catch (COMException ex)
        {
            // MK_E_UNAVAILABLE (0x800401E3) = nada na ROT. Não é erro, é "não está aberto".
            Diag($"  -> GetActiveObject falhou 0x{ex.HResult:X8}");
            return null;
        }
    }

    /// <summary>
    /// Mesmo que <see cref="TryGetActiveObject"/>, mas numa thread MTA separada e com
    /// teto de tempo.
    ///
    /// Por que isso é necessário: a ROT devolve o objeto já marshalado para o apartment
    /// de QUEM chama. Pedir isso de uma STA que não está bombeando mensagens, enquanto o
    /// servidor (STA único do SE) atende outro cliente, trava sem timeout e sem erro —
    /// não é rejeição, então o message filter nunca é consultado. Chamando de uma MTA, o
    /// marshaling não depende da bomba de mensagens da nossa thread.
    /// </summary>
    public static object? GetActiveObjectWithTimeout(string progId, TimeSpan timeout, out string how)
    {
        object? result = null;
        Exception? error = null;
        var done = new ManualResetEventSlim(false);

        var t = new Thread(() =>
        {
            try { result = TryGetActiveObject(progId); }
            catch (Exception ex) { error = ex; }
            finally { done.Set(); }
        })
        { IsBackground = true };
        t.SetApartmentState(ApartmentState.MTA);
        t.Start();

        if (!done.Wait(timeout))
        {
            how = $"TIMEOUT depois de {timeout.TotalSeconds:0}s (thread MTA abandonada)";
            return null;
        }
        if (error is not null)
        {
            how = $"exceção {error.GetType().Name} 0x{error.HResult:X8}";
            return null;
        }
        how = result is null ? "nada na ROT" : "ok via thread MTA";
        return result;
    }

    private static void Diag(string msg) =>
        Console.Error.WriteLine($"[com {DateTime.Now:HH:mm:ss.fff}] {msg}");

    public static object? CreateFromProgId(string progId)
    {
        var type = Type.GetTypeFromProgID(progId, throwOnError: false);
        return type is null ? null : Activator.CreateInstance(type);
    }
}

/// <summary>
/// IDispatch reduzido aos dois primeiros slots da vtable, que é tudo que precisamos
/// para chegar ao ITypeInfo do objeto vivo. Declarar só os dois funciona porque eles
/// são os primeiros depois de IUnknown.
/// </summary>
[ComImport]
[Guid("00020400-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDispatchLite
{
    [PreserveSig] int GetTypeInfoCount(out uint pctinfo);
    [PreserveSig] int GetTypeInfo(uint iTInfo, int lcid, out ITypeInfo? ppTInfo);
}

/// <summary>Identidade de um objeto COM vivo, lida do próprio objeto.</summary>
internal sealed record LiveIdentity(
    string? InterfaceName,
    string? TypeLibName,
    string? InterfaceGuid,
    string? Doc,
    int MemberCount,
    string? Error)
{
    public override string ToString() =>
        Error is not null ? $"<{Error}>" :
        $"{InterfaceName ?? "?"} ({TypeLibName ?? "?"})";
}

[SupportedOSPlatform("windows")]
internal static class ComLive
{
    private const int MemberIdNil = -1;

    /// <summary>
    /// Pergunta ao objeto o que ele é. É a diferença entre "System.__ComObject" e
    /// saber que você tem um <c>SolidEdgePart.Models</c> de <c>SolidEdgePart</c>.
    /// </summary>
    public static LiveIdentity Identify(object? obj)
    {
        if (obj is null) return new LiveIdentity(null, null, null, null, 0, "null");
        if (!Marshal.IsComObject(obj))
            return new LiveIdentity(obj.GetType().FullName, "(não é COM)", null, null, 0, null);

        try
        {
            if (obj is not IDispatchLite disp)
                return new LiveIdentity(null, null, null, null, 0, "não expõe IDispatch");

            var hr = disp.GetTypeInfo(0, 0, out var ti);
            if (hr != 0 || ti is null)
                return new LiveIdentity(null, null, null, null, 0, $"GetTypeInfo hr=0x{hr:X8}");

            try
            {
                ti.GetDocumentation(MemberIdNil, out var name, out var doc, out _, out _);

                string? libName = null;
                try
                {
                    ti.GetContainingTypeLib(out var lib, out _);
                    lib.GetDocumentation(MemberIdNil, out var ln, out _, out _, out _);
                    libName = ln;
                    Marshal.ReleaseComObject(lib);
                }
                catch { /* opcional */ }

                string? guid = null;
                var members = 0;
                var attrPtr = IntPtr.Zero;
                try
                {
                    ti.GetTypeAttr(out attrPtr);
                    var attr = Marshal.PtrToStructure<TYPEATTR>(attrPtr);
                    guid = attr.guid.ToString("B");
                    members = attr.cFuncs;
                }
                catch { /* opcional */ }
                finally { if (attrPtr != IntPtr.Zero) ti.ReleaseTypeAttr(attrPtr); }

                return new LiveIdentity(name, libName, guid,
                    string.IsNullOrWhiteSpace(doc) ? null : doc, members, null);
            }
            finally { Marshal.ReleaseComObject(ti); }
        }
        catch (Exception ex)
        {
            return new LiveIdentity(null, null, null, null, 0, $"{ex.GetType().Name} 0x{ex.HResult:X8}");
        }
    }

    /// <summary>
    /// Lista os membros que o objeto VIVO declara. É o "live dump" — confirma que um
    /// membro existe de verdade neste objeto, e não apenas na typelib.
    /// </summary>
    public static List<string> Members(object? obj, string? filter = null)
    {
        var result = new List<string>();
        if (obj is null || obj is not IDispatchLite disp) return result;
        if (disp.GetTypeInfo(0, 0, out var ti) != 0 || ti is null) return result;

        var attrPtr = IntPtr.Zero;
        try
        {
            ti.GetTypeAttr(out attrPtr);
            var attr = Marshal.PtrToStructure<TYPEATTR>(attrPtr);
            for (var i = 0; i < attr.cFuncs; i++)
            {
                var fdPtr = IntPtr.Zero;
                try
                {
                    ti.GetFuncDesc(i, out fdPtr);
                    var fd = Marshal.PtrToStructure<FUNCDESC>(fdPtr);
                    var names = new string[fd.cParams + 1];
                    ti.GetNames(fd.memid, names, names.Length, out var fetched);
                    if (fetched == 0) continue;
                    var kind = fd.invkind switch
                    {
                        INVOKEKIND.INVOKE_PROPERTYGET => "get",
                        INVOKEKIND.INVOKE_PROPERTYPUT => "put",
                        INVOKEKIND.INVOKE_PROPERTYPUTREF => "putref",
                        _ => "fn",
                    };
                    var line = $"{kind,-6} {names[0]}({fd.cParams})  dispid={fd.memid}";
                    if (filter is null || names[0].Contains(filter, StringComparison.OrdinalIgnoreCase))
                        result.Add(line);
                }
                catch { /* membro problemático: segue */ }
                finally { if (fdPtr != IntPtr.Zero) ti.ReleaseFuncDesc(fdPtr); }
            }
        }
        catch { /* ignora */ }
        finally
        {
            if (attrPtr != IntPtr.Zero) ti.ReleaseTypeAttr(attrPtr);
            Marshal.ReleaseComObject(ti);
        }
        return result;
    }
}

// ---------------------------------------------------------------------------
// Message filter
// ---------------------------------------------------------------------------

[ComImport]
[Guid("00000016-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IOleMessageFilter
{
    [PreserveSig] int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);
    [PreserveSig] int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);
    [PreserveSig] int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
}

/// <summary>
/// Filtro de mensagens OLE. Sem ele, chamar o SE enquanto ele está ocupado devolve
/// RPC_E_CALL_REJECTED. O filtro precisa reagir aos DOIS motivos de rejeição
/// (SERVERCALL_RETRYLATER e SERVERCALL_REJECTED) e ter um teto de espera, senão um
/// diálogo modal aberto no SE pendura a automação para sempre.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class MessageFilter : IOleMessageFilter
{
    private const int ServerCallIsHandled = 0;
    private const int ServerCallRetryLater = 2;
    private const int ServerCallRejected = 1;
    private const int PendingMsgWaitDefProcess = 2;

    private readonly int _timeoutMs;
    public int RetryCount { get; private set; }
    public int TimeoutCount { get; private set; }
    public int IncomingCalls { get; private set; }

    private MessageFilter(int timeoutMs) => _timeoutMs = timeoutMs;

    [DllImport("ole32.dll")]
    private static extern int CoRegisterMessageFilter(
        IOleMessageFilter? newFilter, out IOleMessageFilter? oldFilter);

    public static MessageFilter Register(int timeoutMs = 60_000)
    {
        var filter = new MessageFilter(timeoutMs);
        var hr = CoRegisterMessageFilter(filter, out _);
        if (hr != 0) throw new COMException("CoRegisterMessageFilter falhou", hr);
        return filter;
    }

    public static void Revoke() => CoRegisterMessageFilter(null, out _);

    public int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo)
    {
        IncomingCalls++;
        return ServerCallIsHandled;
    }

    public int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType)
    {
        // Tratar APENAS RetryLater deixa passar o RPC_E_CALL_REJECTED que aparece quando
        // o SE está com modal — por isso os dois entram.
        if (dwRejectType is not (ServerCallRetryLater or ServerCallRejected))
            return -1;

        if (dwTickCount >= _timeoutMs)
        {
            TimeoutCount++;
            return -1;          // desiste: a chamada falha com RPC_E_CALL_REJECTED
        }

        RetryCount++;
        return 150;             // ms até tentar de novo
    }

    public int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType)
        => PendingMsgWaitDefProcess;
}
