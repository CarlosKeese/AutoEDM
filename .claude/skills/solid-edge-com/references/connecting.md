# Connecting to Solid Edge

Measured against **SE 2026 build 226.00.08.004 x64**, .NET 10.0.9, x64, `[STAThread]`,
2026-10-17. Every claim below has a measurement behind it.

---

## `Marshal.GetActiveObject` does not exist in modern .NET

It lived in .NET Framework. P/Invoke the two calls yourself:

```csharp
[DllImport("ole32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
static extern void CLSIDFromProgID(string lpszProgID, out Guid lpclsid);

[DllImport("oleaut32.dll", PreserveSig = false)]
static extern void GetActiveObject(ref Guid rclsid, IntPtr pvReserved,
    [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);
```

`CLSIDFromProgID("SolidEdge.Application")` resolves in 1–2 ms and returns
`{ded89db0-45b6-11ce-b307-0800363a1e02}`.

A `COMException` with `MK_E_UNAVAILABLE (0x800401E3)` means **nothing in the ROT** — SE is
not open. That is not an error; treat it as "not running".

## `GetActiveObject` can HANG, and the message filter will not save you

Measured: it hung for **~28 s on three consecutive runs**, then resolved in **5 ms** on
every run afterwards. During the hangs:

- the OLE message filter recorded **0 incoming calls, 0 retries, 0 give-ups** — because a
  filter is only consulted when the server *rejects*, and a hang is not a rejection;
- moving the call to an **MTA** thread did **not** help (so it is not the STA message pump);
- the two `SolidEdgeMcp` client processes attached to SE were still running after the
  problem stopped, so they were not the cause either.

**Root cause: not established.** What *is* established is the defense. Do not let a hang
become a hung process:

```csharp
object? ComWithTimeout(Func<object?> call, TimeSpan timeout)
{
    object? result = null; Exception? error = null;
    using var done = new ManualResetEventSlim(false);
    var t = new Thread(() => {
        try { result = call(); } catch (Exception ex) { error = ex; } finally { done.Set(); }
    }) { IsBackground = true };
    t.SetApartmentState(ApartmentState.MTA);
    t.Start();
    if (!done.Wait(timeout)) throw new TimeoutException("GetActiveObject did not return");
    if (error is not null) throw error;
    return result;
}
```

Use a generous timeout (90 s) — 28 s was real, not pathological.

## `Activator.CreateInstance(ProgID)` does NOT attach — it starts a hidden instance

This is the most dangerous failure mode found, because **it looks like success**.

When the ROT lookup hung, a "plan B" ProgID activation was used, on the common assumption
that SE is a single-instance class that would hand back the open session. Measured instead:

| | before | after `CreateInstance` |
|---|---|---|
| `Edge.exe` PIDs | `[29076]` | `[29076, 19632]` |
| `Application.Visible` | — | **`False`** |
| new PID's `MainWindowTitle` | — | **empty** |

The returned object was a fully functional `Application`. **61 test calls ran to
completion** against it — creating documents, extruding, measuring faces — in a **second,
invisible instance**, while the user's session did not move a pixel. The phantom instance
also **outlived the program** and had to be killed by PID.

A silent ProgID fallback therefore converts "I could not connect" into "I connected to the
wrong place", and the only tell is `Visible == False`.

### Attach safely

```csharp
var before = Process.GetProcessesByName("Edge").Select(p => p.Id).ToArray();
dynamic app = /* GetActiveObject with timeout */;
var spawned = Process.GetProcessesByName("Edge").Select(p => p.Id).Except(before).ToArray();
if (spawned.Length > 0)
    throw new InvalidOperationException(
        $"connected to a NEW instance (PID {string.Join(",", spawned)}), not the user's session");
```

### Starting SE on purpose is fine — but make it visible

```csharp
dynamic app = Activator.CreateInstance(
    Type.GetTypeFromProgID("SolidEdge.Application", throwOnError: true)!)!;
app.Visible = true;     // without this it runs headless and "nothing happens"
```

## Message filter

Required, and it must handle **both** rejection reasons. Treating only
`SERVERCALL_RETRYLATER` lets through the `RPC_E_CALL_REJECTED` you get while SE has a modal
dialog up.

```csharp
public int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType)
{
    if (dwRejectType is not (SERVERCALL_RETRYLATER or SERVERCALL_REJECTED)) return -1;
    if (dwTickCount >= _timeoutMs) return -1;   // give up: bounded, or a modal hangs you forever
    return 150;                                  // ms before retrying
}
public int MessagePending(IntPtr h, int tick, int type) => PENDINGMSG_WAITDEFPROCESS;
public int HandleInComingCall(int t, IntPtr h, int tick, IntPtr info) => SERVERCALL_ISHANDLED;
```

Register with `CoRegisterMessageFilter` on the **same STA thread** that talks to SE;
revoke with `null` at the end.

**Honesty about the validation:** across six measured runs the filter registered and
revoked correctly but the counters all ended at **zero** — SE was idle, no modal, calls
short. So the retry path itself was **never exercised** here. Keep the filter (a long
recompute or a modal changes that instantly), but do not treat "it worked" as evidence
that the retry logic is correct.

## Application state to save and restore

```csharp
bool alerts = (bool)app.DisplayAlerts;   // was True on this machine
app.DisplayAlerts = false;               // a modal otherwise hangs the automation
try { /* work */ } finally { app.DisplayAlerts = alerts; }
```

Also read before you write anything:

```csharp
int open = (int)app.Documents.Count;
// > 0: the user has work open. List FullName (read-only) and stop, or confine
//      yourself strictly to documents you create.
```

## Timings measured (so you know what is slow vs hung)

| call | time |
|---|---|
| `CLSIDFromProgID` | 1–2 ms |
| `GetActiveObject` (normal) | 5 ms |
| `GetActiveObject` (the hang) | ~28 000 ms |
| `Documents.Add(...)` | 400–3 800 ms |
| `Application.Name` (first call) | ~85 ms |
| most property reads | 0–20 ms |
| `SaveAs` | 120–900 ms |

`Documents.Add` taking a couple of seconds is **normal**, not a hang.

## Identifying what you are holding

A COM object arrives as `System.__ComObject`, whose .NET type name tells you nothing. Ask
the object:

```csharp
[ComImport, Guid("00020400-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IDispatchLite
{
    [PreserveSig] int GetTypeInfoCount(out uint pctinfo);
    [PreserveSig] int GetTypeInfo(uint iTInfo, int lcid, out ITypeInfo? ppTInfo);
}

// ((IDispatchLite)obj).GetTypeInfo(0, 0, out var ti);
// ti.GetDocumentation(-1, out string name, out _, out _, out _);
// ti.GetContainingTypeLib(out var lib, out _);  -> which typelib it belongs to
```

Declaring only the first two vtable slots is enough and is safe, because they come first
after `IUnknown`.

This is how you learn that `Documents.Add("SolidEdge.SheetMetalDocument")` returns a
`SheetMetalDocument` declared in the **`SolidEdgePart`** typelib, that
`Application.ApplicationEvents` reports itself as **`Application`**, and that
`CreateReference` returns a `Reference` from **`SolidEdgeFramework`** rather than
`SolidEdgeAssembly`. Guessing the owning library wrongly is a common cause of failed
early-binding casts.
