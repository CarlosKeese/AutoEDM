# Ribbon add-in inside Solid Edge

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
