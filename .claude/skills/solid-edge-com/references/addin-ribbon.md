# Ribbon add-in inside Solid Edge

> Two routes. The first section uses the **`SolidEdge.Community.AddIn`** NuGet (.NET
> Framework), which hides the COM plumbing and is the fastest path when it is available.
> **"The raw COM contract"** at the end of this file is the plumbing itself — the two
> interfaces, the exact registry keys, and the **.NET 10 comhost** route, since the
> community package and `mscoree.dll` do not cover modern .NET.

## Add-in (ribbon button inside Solid Edge)

To put a button inside SE (vs an external EXE), build a COM add-in with the
**`SolidEdge.Community.AddIn`** NuGet (id `SolidEdge.Community.AddIn`, brings
`Interop.SolidEdge`; compiles without SE installed). Verified pattern:

- Class `[ComVisible(true)] [Guid(...)] [ProgId(...)] MyAddIn : SolidEdgeCommunity.AddIn.SolidEdgeAddIn`.
- `OnConnection(Application, SeConnectMode, AddIn)` → `base.OnConnection(...)`, then
  `AddInEx.GuiVersion = 1` (**increment whenever you change the ribbon**, else it won't rebuild).
- `OnCreateRibbon(RibbonController controller, Guid environmentCategory, bool firstTime)`
  → `controller.Add<MyRibbon>(environmentCategory, firstTime)`.
- `[ComRegisterFunction] static OnRegister(Type t)`: `new RegistrationSettings(t){Enabled=true}`,
  `.Environments.Add(SolidEdgeSDK.EnvironmentCategories.AllDocumentEnvrionments)`,
  `.Titles/.Summaries.Add(culture, ...)`, `MyAddIn.Register(settings)`. `[ComUnregisterFunction]` → `Unregister(t)`.
- `MyRibbon : Ribbon`, ctor `LoadXml(assembly, "Namespace.Ribbon.xml")` (embedded resource);
  override `OnControlClick(RibbonControl control)` and dispatch on `control.CommandId`
  (= the `id` in the XML).
- `Ribbon.xml` root `<ribbon xmlns="http://github.com/SolidEdgeCommunity/SolidEdge/Ribbon">`
  → `<tab name><group name><button id size label screentip supertip imageId showLabel/>`.
  Without a real image the button shows label-only (fine); `imageId` maps to a bitmap
  resource (below).
- **Ribbon icons** come from a **Win32 `RT_BITMAP` resource embedded in the add-in DLL** —
  SE calls `AddIn.SetAddInInfo(hInstance, …)` and the XML `imageId` = the RT_BITMAP
  **resource ID**. `RibbonControl` has NO .NET image property. Embed via
  `<Win32Resource>icons.res</Win32Resource>` in the csproj (this replaces the default
  version-info resource). **No `rc.exe`?** Generate the `.res` by hand (format: a null
  RESOURCEHEADER, then per bitmap a RESOURCEHEADER with type ordinal `2`=RT_BITMAP + name
  ordinal = imageId + the **DIB** = a `System.Drawing` BMP minus its 14-byte
  `BITMAPFILEHEADER`, DWORD-aligned). Verify without SE: the C# compiler **rejects a
  malformed `.res`** at build; then `FindResource(hMod, id, RT_BITMAP)` confirms the
  bitmaps are embedded and the assembly still loads. SE only decides the final render
  (size/transparency).
- **In-process**: you get `Application` directly in `OnConnection` — no ROT/`GetActiveObject`.
  Don't release SE's `Application` on teardown (it's not yours). Reuse the same core
  logic the external GUI calls; the add-in is just another front-end.
- Register/unregister (admin, x64 regasm): `regasm /codebase MyAddIn.dll` /
  `regasm /codebase /u MyAddIn.dll`.
- **No admin? Register per-user in `HKCU\Software\Classes`** — COM and Solid Edge for
  the current user read HKCU\Software\Classes before HKLM, so a small tool can write
  the same keys there without elevation. Under `CLSID\{clsid}` write: `InprocServer32`
  default `mscoree.dll` + `ThreadingModel=Both`, `Class`, `Assembly` (full name),
  `RuntimeVersion` (`typeof(object).Assembly.ImageRuntimeVersion`), `CodeBase`
  (`file:///` + dll path), plus a version subkey mirror; then the SE keys
  `Implemented Categories\{SolidEdgeSDK.CATID.SolidEdgeAddIn}`, `AutoConnect=1` (DWORD),
  `Environment Categories\{env}`. Restart SE to load. (regasm has no HKCU mode; hand-write it.)

## The deploy-folder trap

**SE loads from the DEPLOY folder, NOT `bin\Debug` — so a plain rebuild changes
   NOTHING that SE sees.** The register tool copies the DLLs to a stable deploy dir
   (`%LOCALAPPDATA%\<app>\addin\`) and points the HKCU `CLSID\…\InprocServer32\CodeBase` there,
   so SE locks the deploy copy (letting you rebuild while SE is open). **To update the add-in you
   MUST: close SE fully (the whole process, not just the doc) → rebuild → re-run the register/
   deploy tool (it re-copies the fresh DLLs) → reopen SE.** Skipping the deploy step means SE
   keeps running the OLD assembly — this masquerades as "my fix did nothing" and burned multiple
   test rounds. Do this **even for a pure code change** (the version bump is a separate concern:
   bump `AddInEx.GuiVersion` only when the ribbon layout changes). **Stamp the loaded build in the
   log** — on `OnConnection`, log `File.GetLastWriteTime(typeof(X).Assembly.Location)` for the
   add-in AND core DLLs; the first log lines then prove which binary actually loaded, killing the
   "is this even my new code?" ambiguity for good.

**Stamp the PATH, not just the file name** (learned the hard way again, 2026-09-04). A stamp that
reads `AutoEDM.AddIn.dll @ 2026-09-04 12:08:44` cannot be told apart from a fresh build by anyone
reading the log — you have to already know which folder it came from. Log
`assembly.Location` in full: the deploy path in the line is what makes "this log is from the old
binary" visible at a glance instead of after an hour of debugging a fix that was never loaded.

**Give the loop one command.** A `deploy-dev.ps1` that (1) refuses to run while the SE process is
alive — `Get-Process Edge` (Solid Edge is `Edge.exe`; the browser is `msedge.exe`) — (2) builds,
(3) copies just the app's own DLLs over the deploy folder, and (4) prints their timestamps, turns
a four-step ritual that is easy to half-do into one command that cannot half-succeed. Only the
app assemblies need copying; the interop/community/System.* dependencies never change.

## Greying out a button when the document is wrong for it

Every command has preconditions — document type, and (for a part) **which modeling
environment its features belong to**. Enforce them in the ribbon, not only inside the
command: a greyed button says "not here" before the user commits, where a dialog after the
click only apologises. Keep both — the enabled state can be one poll stale.

- Put the preconditions in **one table keyed by `CommandId`** (title + document type +
  required `ModelingMode`), and let both the click handler and the enable logic read it.
  Scattering the check inside each command is how one of them ends up "helpfully" switching
  the document's environment instead of refusing (see `references/modeling-recipes.md`).
- `SolidEdgeCommunity.AddIn.RibbonControl.Enabled` is a **plain auto-property**. SE asks the
  add-in for command state through `ISEAddInEventsEx.OnCommandUpdateUI` while the tab is
  visible, and the framework answers with whatever that property holds *at that moment* —
  the base class implements the interface explicitly, so **there is no callback of your own
  to hook**. Keep the value fresh instead: a `System.Windows.Forms.Timer` (~750 ms) that
  walks `Ribbon.Controls`, re-evaluates each command and assigns `Enabled`. In-process, that
  timer runs on SE's own STA thread, so the COM reads need no marshaling.
- Make the tick **cheap, re-entrancy-guarded and totally swallowed**: reading
  `ActiveDocument.Type`/`.ModelingMode` is enough, one tick must not stack on a slow one, and
  a throw must never surface inside SE. Worst case the button stays clickable — which the
  click-time check already covers.
- Say the requirement in the **supertip** too ("requires ORDERED modeling"), so the greyed
  button explains itself on hover instead of just looking broken.

## Dialogs that need the user to pick in SE while they are open

A **modal** `ShowDialog()` freezes Solid Edge: with it up, nothing in the model can be clicked.
That is fine for a parameter dialog, and fatal for any "select a face, then an edge" flow.
For those, show the form **modeless** with `Show()`:

- It runs on SE's own STA thread and SE keeps pumping messages, so COM calls made from the
  form's handlers are in the right apartment — same as for modal dialogs.
- **Keep the instance in a static field.** A modeless `Form` that goes out of scope gets
  collected and vanishes from the screen with no error at all. Null the field in `FormClosed`,
  and on a second click of the ribbon button `BringToFront()`/`Activate()` the existing one
  instead of opening a duplicate.
- `TopMost = true` + `ShowInTaskbar = false` makes it behave like a tool palette rather than
  disappearing behind the SE window the moment the user clicks the model.
- Read the picks from `Document.SelectSet` on a button click. Remember the wrapper rule: a
  `SelectSet` item may not be the raw Face/Edge — try `.Object` before giving up.
- The ribbon command returns immediately after `Show()`, so a `===== FIM =====` log line right
  after it is a lie; log the close from the `FormClosed` handler instead.

## Picking an EDGE: SelectSet is a dead end, you need your own Command

**`SelectSet` NEVER yields an Edge in the Part environment.** What is locatable is decided by the
*active command*, and with SE's Select tool active that is faces/features/planes only. So the
"select first, then press a button to confirm" pattern works for a face and can never work for an
edge. The fix is to run **your own command with a locate filter** — exactly what SE's own features
do for their selection steps (`src/AutoEDM.AddIn/UI/SePicker.cs`):

```csharp
_cmd = app.CreateCommand((int)seCmdFlag.seNoDeactivate);   // =2; survives focus changes
_cmd.Start();
((SolidEdgeFramework.DISECommandEvents_Event)_cmd).Terminate += onTerminate;  // Esc / other cmd
_mouse = _cmd.Mouse;
_mouse.WindowTypes = 1;                                     // model windows (no enum in interop)
_mouse.LocateMode = (int)seLocateModes.seLocateQuickPick;   // =2, disambiguates edge vs face
_mouse.ClearLocateFilter();
_mouse.AddToLocateFilter((int)seLocateFilterConstants.seLocateEdge);   // =31; face = 32
((SolidEdgeFramework.DISEMouseEvents_Event)_mouse).MouseClick += onClick;
// void onClick(short btn, short shift, double x,y,z, object window, int keyPointType, object graphic)
//   `graphic` IS the located Face/Edge COM object — feed it straight to the geometry readers.
// End the step: unsubscribe, ClearLocateFilter(), _cmd.Done = true, release the RCWs.
```

- The `_Event` interfaces carry `[ComEventInterface]`, so casting the plain RCW and using `+=`
  works — no manual `IConnectionPoint.Advise` needed. **Keep the delegate in a field**, it is what
  you unsubscribe with.
- **Never tear the command down inside its own `MouseClick`.** Marshal out first
  (`Control.BeginInvoke`), then stop.
- `seLocateFilterConstants` (0–76), `seCmdFlag`, `seLocateModes`, `seButton` all live in the
  `SolidEdgeConstants` namespace of `Interop.SolidEdge` — reflect the DLL for the values.
- **Locate modes inside an add-in command (live logs, SE assembly, 2026-09-24):** only
  `seLocateQuickPick`(2) works. `seSmartLocate`(0) fires `MouseClick` with `graphic == null`
  (crosshair cursor, nothing picked). `seLocateSimple`(1) fires **no `MouseClick` at all** — the
  click falls through to SE's native Select (which grabs the whole occurrence), so it *looks*
  like a picker that prefers some part. QuickPick delivers a face but is **not depth-aware**:
  a whole round of picks all came from the same occurrence, and its hover pre-highlight lights
  the part BEHIND, which misleads the user even when your own pick is right. When you decide
  the face yourself (below), use SmartLocate and accept the empty click: events arrive, nothing
  misleading lights up. `seLocateFace` alone located only planar
  faces there — add the per-surface filters too (Plane 20, Cone 21, Sphere 22, Torus 23,
  ProjectedFace 24, RevolvedFace 25, BspSurfaceFace 26, RuledFace 28). For "the face the user
  sees", decide depth yourself: view ray from `Window.View.GetCamera` (11 by-ref args; eye,
  target, up, perspective, scale — order confirmed live) → per occurrence, inverse pose →
  `Body.FacesByRay` → exact hit on `Face.GetFacetData` → nearest wins
  (`AutoEDM.Selection.VisibleFacePicker`).
- **The (x, y, z) of `MouseClick` is NOT a point under the cursor** — a ray through it hit the
  plate behind (live, MD-15335). The point under the cursor comes from the PIXEL:
  `Cursor.Position` at click time → `ScreenToClient(Window.DrawHwnd)` (the DRAWING window, not
  `hWnd`) → `View.TransformDCToModel(x, y, [out] X, Y, Z)` (metres). Round trip
  `TransformModelToDC`/`TransformDCToModel` on a point of a selected face closed, and the ray
  through it picked exactly that face. External probes need `OleMessageFilter.Register()` or SE
  rejects calls while busy and occurrence reads come back missing.
- Highlight what was picked with `Document.HighlightSets.Add()` → `AddItem` / `Color` (COLORREF
  `r | g<<8 | b<<16`) / `Draw`, and drive the prompt through `Application.StatusBar`.

**Merging branches that both touched the ribbon (2026-10-10).** Command ids are plain integers
declared twice — once in the C# constants, once in `Ribbon.xml`. Two branches that each took "the
next free id" (here both 26) produce a **conflict in C# but NOT in the XML**: git merges the two
`<button id="26">` lines silently, and one button stops answering. On every merge, list the ids in
the XML (`grep 'button id='`) and renumber before building; then bump `GuiVersion` past BOTH
branches' values. Tooltip/supertip text lives in the same cached XML, so a text-only change needs
the bump too.

**Deploy from a machine without PowerShell 7.** Our scripts run on Windows PowerShell 5.1:
`powershell -ExecutionPolicy Bypass -File tools\deploy-dev.ps1 …` — `pwsh` may simply not exist.
Replacing the MCP server also needs the Claude Code app closed (its `AutoEDM.Mcp.exe` locks the
files); the add-in needs SE closed.

## The raw COM contract (measured 2026-10-17, SE 2026 build 226.00.08.004)

The notes above use the **`SolidEdge.Community.AddIn`** framework, which hides the COM
plumbing. This section is the plumbing itself — needed when that package is not an option
(for example on .NET 10, where it is not available), and useful for diagnosing why an
add-in does not load.

### Two interfaces, and everyone confuses them

| interface | IID | role |
|---|---|---|
| **`ISolidEdgeAddIn`** | `{d3f30ae5-2582-11d2-baf9-080036230602}` | **you IMPLEMENT this** |
| `ISEAddIn` | `{a50d497b-288a-11d2-b586-080036e8b802}` | **SE HANDS you this** (you call it) |

`ISolidEdgeAddIn` has exactly three methods:

```
OnConnection([in] IDispatch Application, [in] SeConnectMode ConnectMode,
             [in] AddIn* AddInInstance)                       dispid 1610678272
OnConnectToEnvironment([in] BSTR EnvCatID, [in] IDispatch pEnvironmentDispatch,
                       [in] VARIANT_BOOL bFirstTime)          dispid 1610678273
OnDisconnection([in] SeDisconnectMode DisconnectMode)         dispid 1610678274
```

`SeConnectMode`: `1 AtStartup / 2 ByUser / 3 Externally`.
`SeDisconnectMode`: `1 AtShutdown / 2 ByUser / 3 Externally`.

Getting this wrong fails **silently**: SE does `QueryInterface` for `ISolidEdgeAddIn`, does
not find it, and gives up without a message.

`ISEAddIn` (the object arriving as `AddInInstance`) is what you configure:

```
GuiVersion                                                    dispid 5  (get/put)
Description                                                   dispid 3  (get/put)
SetAddInInfo([in] int InstanceHandle, [in] BSTR EnvironmentCatID,
             [in] BSTR CategoryName, [in] int IDColorBitmapMedium,
             [in] int IDColorBitmapLarge, [in] int IDMonochromeBitmapMedium,
             [in] int IDMonochromeBitmapLarge, [in] int NumberOfCommands,
             [in] SAFEARRAY(BSTR)* CommandNames,
             [in,out] SAFEARRAY(int)* CommandIDs)             dispid 9
AddCommand([in] BSTR EnvironmentCatID, [in] BSTR CommandName,
           [in] int CommandID) -> int                         dispid 10
AddCommandBarButton([in] BSTR EnvironmentCatID, [in] BSTR CommandBarName,
                    [in] int CommandID) -> CommandBarButton   dispid 11
```

`ISEAddInEx.SetAddInInfoEx` (dispid 12) swaps `InstanceHandle` for
`[in] BSTR ResourceFilename` — the .NET-friendly form, since a managed assembly has no
native HINSTANCE to hand over. `ISEAddInEx2.SetAddInInfoEx2` (dispid 14) adds
`[in] SAFEARRAY(int)* CommandButtonStyles`.

### The `[in,out]` that bites

`SetAddInInfo` takes `CommandIDs` as an **`[in,out]` SAFEARRAY(int)** pointer: it goes in
with your IDs and comes back with the IDs **SE assigned**. Through `Type.InvokeMember` you
must mark positions 8 and 9 in a `ParameterModifier`, or the returned IDs are lost
silently (see `com-rules.md`). `ISEAddInEvents.OnCommandUpdateUI` has the same shape —
`[in,out] int* CommandFlags` and `[in,out] int* BitmapID` are **`ref`** in C#, not `out`;
declaring `out` discards what SE sent.

```
ISEAddInEvents  {0f539244-4816-11d2-b5ac-080036e8b802}
  OnCommand([in] int CommandID)                               dispid 1610678272
  OnCommandHelp([in] int hFrameWnd, [in] int HelpCommandID, [in] int CommandID)
                                                              dispid 1610678273
  OnCommandUpdateUI([in] int CommandID, [in,out] int* CommandFlags,
                    [out] BSTR* MenuItemText, [in,out] int* BitmapID)
                                                              dispid 1610678274
```

`OnCommandUpdateUI` is where the button enables/disables — the right place for the
environment check, per the rule in `SKILL.md`.

### Registry keys, read off two real add-ins

Reverse-engineered from the Siemens `Drawing Compare` add-in and a production third-party
add-in on the same machine:

```
HKCR (or HKCU\SOFTWARE\Classes)\CLSID\{YOUR-GUID}
  (default)                 = add-in name
  \InprocServer32
      (default)             = the hosting DLL  (see .NET 10 below)
      ThreadingModel        = Both
  \ProgId
      (default)             = Your.ProgId
  \Implemented Categories\{26B1D2D1-2B03-11d2-B589-080036E8B802}   <- SE add-in CATID
  \Environment Categories\{BAD41B8D-18FF-42c9-9611-8A00E6921AE8}   <- which environments
  \Summary
      409                   = a display string

HKCU\SOFTWARE\Siemens\Solid Edge\Version 226\AddIns\{YOUR-GUID}
  (default)                 = the name shown in the Add-In manager
  AutoConnect               = 1   (DWORD)
  Cookie                    = DWORD
```

Measured details that are not obvious:

- **`{26B1D2D1-...}` is NOT registered under `HKCR\Component Categories`.** There is no
  description for it anywhere; SE just looks for the key. So you write a category that is
  formally undeclared — that is normal here.
- **`{62C8FE65-4EBB-45E7-B440-6E39B2CDBF29}` is ".NET Category"**, a `regasm` artifact —
  *not* an SE requirement. A .NET 10 comhost add-in does not need it.
- **`Cookie` is not random: it is the first DWORD of your own CLSID.** Verified on three
  add-ins — `7A3C1E44` gives 2 050 760 260, `F9195FE9` gives 4 179 189 737,
  `B8F2B1E6` gives 3 102 912 998. SE writes it, but it is derived. **So seeing the right
  Cookie proves only that SE READ your registration — not that it loaded the add-in.**
- **The `AddIns` key exists only in HKCU**, never HKLM. Add-ins are per-user, so the whole
  registration can be done **without elevation**.
- **`Summary\409` is NOT what the Add-In manager displays.** Measured: with
  `Summary\409 = "SE-API-Recon Probe"` visible through HKCR, SE still wrote `"Sem Titulo"`
  ("Untitled") into the `AddIns` `(default)`. And `Drawing Compare` shows
  `"Drawing Compare Addin"` while its `Summary\409` is `"Drawing Compare"` — two different
  strings. The evidence points at **`ISEAddIn.Description`** (dispid 3), set by the add-in
  itself inside `OnConnection`, as the real source; an add-in that never connects therefore
  shows as "Untitled". **Set `Description` in `OnConnection`.**

### Add-ins load LAZILY

Measured on a freshly started SE with no document open: **no managed add-in DLL was in the
`Edge.exe` module list at all** — not a third-party one, not Siemens' own. Only
`mscoree.dll` and `mscoreei.dll` (the .NET Framework shim) were loaded.

Creating a document then made a third-party add-in connect and write its log. So
`OnConnection` can be deferred until an environment is activated. **An empty add-in log on
a just-started SE does not prove the add-in is broken** — open a document first.

### .NET 10: `mscoree.dll` is not an option any more

Both add-ins measured on this machine use the .NET Framework path:
`InprocServer32 = mscoree.dll` plus `Class` / `Assembly` / `CodeBase` / `RuntimeVersion`.
That shim belongs to CLR 4 and **will not load a .NET 10 assembly**.

The modern equivalent is the SDK's COM host:

```xml
<EnableComHosting>true</EnableComHosting>
```

which produces `MyAddIn.comhost.dll`. `InprocServer32` points at **the comhost**, with no
`Class`/`Assembly`/`RuntimeVersion` — the comhost resolves everything from
`.runtimeconfig.json`.

```
MyAddIn.comhost.dll          <- this is the InprocServer32
MyAddIn.dll
MyAddIn.runtimeconfig.json   <- without it the host cannot pick a runtime
MyAddIn.deps.json
```

**All four must sit together**; shipping only the comhost makes activation fail.

Verified on this machine: a `net10.0-windows` x64 add-in built this way **activates
correctly** — `CoCreateInstance` succeeded, `QueryInterface(ISolidEdgeAddIn)` returned
`S_OK`, the managed constructor ran, and **.NET 10.0.9 loaded side by side with .NET
Framework 4** in the same host process. So the COM object itself is sound.

**What is still unresolved:** on the measured machine SE *read* the registration (it wrote
the `Cookie`) but then **removed the `AutoConnect` value** and never instantiated the
class. Whether that is SE requiring explicit approval of a new add-in in the Add-In
manager, or a rejection of the comhost specifically, was **not determined**. If your .NET
10 add-in never gets `OnConnection`:

1. open the Add-In manager and check whether it is listed but unticked;
2. confirm `AutoConnect=1` survived the SE restart (if SE deleted it, SE disabled the
   add-in);
3. prove the COM side independently — `CoCreateInstance` the CLSID from a separate process
   and `QueryInterface` for `{d3f30ae5-2582-11d2-baf9-080036230602}`. If that works, the
   problem is SE's add-in loading, not your class.

### Diagnostic that pays for itself

Log in the **constructor**, not only in `OnConnection`. It separates "SE never instantiated
the class" from "SE instantiated it but never called `OnConnection`" — without that, an
empty log is ambiguous and you cannot tell where the flow stopped.

And log the **full path** plus `File.GetLastWriteTime` of the loaded assembly, the runtime
description, and the host process path and PID. A production add-in on this machine does
exactly that, and its first three log lines answer every "is this even my new build?"
question permanently.
