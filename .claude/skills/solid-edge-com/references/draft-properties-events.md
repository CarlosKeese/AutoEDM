# Draft sheets, file properties, and events

Measured against **SE 2026 build 226.00.08.004 x64**, pt-BR install, 2026-10-17.

---

# Draft: a new document has SIX sheets, not one

```
Documents.Add("SolidEdge.DraftDocument")  ->  DraftDocument (SolidEdgeDraft)
doc.Sheets.Count                          ->  6
doc.Sheets.Item(1..6).Name                ->  Folha1, Folha-A4, Folha-A3,
                                               Folha-A2, Folha-A1, 2D Model
doc.Sheets.Item(1).DrawingViews.Count     ->  0
```

**`DraftDocument.Sheets` mixes working sheets, background sheets and the 2D-model sheet.**
Iterating it to "process every sheet of the drawing" will try to process four format
templates (A1–A4) and a 2D model sheet.

The separation lives in a different collection, **`DraftDocument.Sections` (dispid 4199)**:

```
doc.Sections.Count -> 3
```

| # | `Name` | `Type` | `Sheets.Count` |
|---|---|---|---|
| 1 | `Section1` | **0** `igWorkingSection` | **1** |
| 2 | `Backgrounds` | **1** `igBackgroundSection` | **4** |
| 3 | `2D Model` | **3** `ig2dModelSection` | **1** |

1 + 4 + 1 = 6, which closes against `Sheets.Count`.

`SheetSectionTypeConstants`: `-1 Unknown · 0 Working · 1 Background · 2 DrawingView ·
3 2dModel · 4 BlockView`.

### Iterate only the drawing sheets

```csharp
static IEnumerable<dynamic> WorkingSheets(dynamic draftDoc)
{
    for (int s = 1; s <= (int)draftDoc.Sections.Count; s++)
    {
        dynamic sec = draftDoc.Sections.Item(s);
        if (Convert.ToInt64(sec.Type) != 0) continue;      // igWorkingSection
        for (int i = 1; i <= (int)sec.Sheets.Count; i++)
            yield return sec.Sheets.Item(i);
    }
}
```

Other measured facts:

- `doc.Sheets.Add()` took the count 6 → **7**; the new sheet joins the working section.
- `DraftDocument.ActiveSheet` is dispid 4198.
- `Sheet` has `Index` (105), `Name` (106), `Background` (128, returns another `Sheet*`) and
  `BackgroundVisible` (129). The background sheet is *referenced by* the working sheet, not
  chosen by name.
- Sheet names are **translated** ("Folha1") — except "2D Model", which stayed English.
  See `localization.md`.

**Not tested:** creating a drawing view from a model (`DrawingViews.AddPartView` and
family). Real gap.

---

# File properties

```
Document.Properties -> PropertySets     Count = 7
  SummaryInformation · ExtendedSummaryInformation · DocumentSummaryInformation
  Custom · ProjectInformation · VersionHistory · MechanicalModeling
```

`SummaryInformation` has **17** members.

**Set names are invariant English; the property names inside them are TRANSLATED.**
`Properties.Item("SummaryInformation").Item("Title")` gives `DISP_E_BADINDEX` on a pt-BR
install because the property is called `"Título"`. Full table and the
language-independent patterns are in **`localization.md`** — read that before writing any
property code.

For your own data use the `Custom` set, where you own the name:

```csharp
doc.Properties.Item("Custom").Add("ReconTag", "v1");   // -> PropertyEx
```

---

# Events

## Two conditions, and the event never arrives without both

### 1. The typed delegate from the interop

`Application.ApplicationEvents` is **dispid 75**. A detail worth knowing: the live type of
that object reports itself as **`Application (SolidEdgeFramework)`** — the `Application`
coclass implements the event interfaces itself; there is no separate `ApplicationEvents`
object to find.

```
SolidEdge.Framework.Interop.ISEApplicationEvents_Event
  event    : AfterNewDocumentOpen
  delegate : ISEApplicationEvents_AfterNewDocumentOpenEventHandler
```

These are part of the **230 event delegates** that `interop.SolidEdgeFrameworkLib.dll`
carries (see `inventory-and-binding.md`). They are the practical path for events in C#,
instead of hand-writing a COM sink.

And you can subscribe **without a compile-time reference**, by reflection:

```csharp
object src   = app.ApplicationEvents;
Type   iface = ResolveInteropType("SolidEdge.Framework.Interop.ISEApplicationEvents_Event");
EventInfo ev = iface.GetEvent("AfterNewDocumentOpen");
Delegate  h  = Delegate.CreateDelegate(ev.EventHandlerType!, typeof(MyClass), nameof(MyClass.OnNewDoc));

ev.AddEventHandler(src, h);
try { /* … */ }
finally { ev.RemoveEventHandler(src, h); }   // ALWAYS remove
```

The handler signature must match the delegate — here `void OnNewDoc(object theDocument)`.
Always remove: a handler left attached to an application-level COM object keeps a live
reference.

### 2. A MESSAGE PUMP, or nothing is delivered

A COM callback destined for an **STA** is delivered as a window message. In a console
`[STAThread]` with no message loop it sits in the queue and **never runs** — no error, no
warning, looking exactly like "the event doesn't fire".

Measured: `Documents.Add` + 3 s of pumping → **1 event delivered**, with the document
arriving in the handler (live type `PartDocument`).

```csharp
[DllImport("user32.dll")] static extern bool PeekMessageW(out Msg m, IntPtr h, uint a, uint b, uint r);
[DllImport("user32.dll")] static extern bool TranslateMessage(ref Msg m);
[DllImport("user32.dll")] static extern IntPtr DispatchMessageW(ref Msg m);

static void Pump(TimeSpan d)
{
    var until = DateTime.UtcNow + d;
    while (DateTime.UtcNow < until)
    {
        while (PeekMessageW(out var m, IntPtr.Zero, 0, 0, 1 /*PM_REMOVE*/))
        { TranslateMessage(ref m); DispatchMessageW(ref m); }
        Thread.Sleep(10);
    }
}
```

In an **in-process add-in this is free** — SE already pumps its own loop. In an external
`.exe` it is your job. In WinForms/WPF the UI loop handles it.

## The 16 `ISEApplicationEvents` events

| dispid | event | parameters |
|---|---|---|
| 1610678272 | `AfterActiveDocumentChange` | `theDocument` |
| 1610678273 | `AfterCommandRun` | `int theCommandID` |
| 1610678274 | `AfterDocumentOpen` | `theDocument` |
| 1610678275 | `AfterDocumentPrint` | `theDocument, int hDC, double* ModelToDC, int* Rect` |
| 1610678276 | `AfterDocumentSave` | `theDocument` |
| 1610678277 | `AfterEnvironmentActivate` | `theEnvironment` |
| **1610678278** | **`AfterNewDocumentOpen`** ✅ tested | `theDocument` |
| 1610678279 | `AfterNewWindow` | `theWindow` |
| 1610678280 | `AfterWindowActivate` | `theWindow` |
| 1610678281 | `BeforeCommandRun` | `int theCommandID` |
| 1610678282 | `BeforeDocumentClose` | `theDocument` |
| 1610678283 | `BeforeDocumentPrint` | `theDocument, int hDC, double* ModelToDC, int* Rect` |
| 1610678284 | `BeforeEnvironmentDeactivate` | `theEnvironment` |
| 1610678285 | `BeforeWindowDeactivate` | `theWindow` |
| 1610678286 | `BeforeQuit` | — |
| 1610678287 | `BeforeDocumentSave` | `theDocument` |

⚠️ `AfterDocumentPrint`/`BeforeDocumentPrint` take `double* ModelToDC` and `int* Rect` —
**raw pointers, not SAFEARRAY**. They are the only members of this interface with pointer
parameters and will need their own marshaling care. Not tested.

## The 45 event coclasses

44 in `SolidEdgeFramework` plus 1 in `SEPreviewLib`. Full list:
`node scripts/apiq.mjs events`. The likely-useful ones:

`ApplicationEvents` ✅ · `DocumentEvents` · `FileUIEvents` · `BeforeFileSaveAsEvents` ·
`Command` · `Mouse` · `AddInEvents` · `CommandBarButtonEvents` · `ViewEvents` ·
`ModelRecomputeEvents` · `AssemblyRecomputeEvents` · `AssemblyChangeEvents` ·
`AssemblyConfigurationChangeEvents` · `SketchRecomputeEvents` · `DrawingViewEvents` ·
`PartsListEvents` · `ShortcutMenuEvents` · `NewFileUIEvents` · `AddInEdgeBarEvents` ·
`AddInSaveAsTranslatorEvents` · `FeatureSelectedFromPFEvents` · `AnimationEvents` ·
`DisplayEvents` · `GLDisplayEvents` · `RenderEvents` · `BendTableEvents` ·
`DraftBendTableEvents` · `ConnectorTableEvents` · `BlockTableEvents` ·
`FamilyOfPartsEvents` · `DividePartEvents` · `PhysicalPropertiesChangeEvents`

Each coclass exposes two event interfaces (`DISE…` and `ISE…`); `tlbimp` generates the
`_Event` + `_SinkHelper` + `_EventProvider` trio for each, plus one delegate per event.

⚠️ **Only `ApplicationEvents.AfterNewDocumentOpen` was tested.** The other 15 events on
that interface and the other 44 coclasses are typelib reading.

---

# Sheet metal, weldment, frames — what little is confirmed

**SheetMetal** ✅ only the document shell was touched:

```
Documents.Add("SolidEdge.SheetMetalDocument")  ->  SheetMetalDocument
```

declared in the **`SolidEdgePart`** typelib (not a sheet-metal-specific library — looking
for it under "SolidEdgeSheetMetal" finds nothing). A new document has
`ModelingMode == 1` (synchronous), `Models.Count == 0`, `RefPlanes.Count == 3`.

📘 Entry points read from the typelib, **not tested**: `Models` (24681),
`FlatPatternModels` (24707), `FlatPatternEdgebarFeatures` (24719), `BendTable` (24734),
`SimplifiedModels` (24703), `PropertyTableDefinitions` (24690), `AttachedPropertyTables`
(24689), `AdjustableDefinition` (24721) / `IsAdjustablePart` (24722).

**No sheet-metal feature was created** — no flange, tab, bend, dimple, louver, jog, hem,
contour or unbend. Treat that area as undiscovered.

**Weldment** 📘 shows up mostly as constant sets: `WeldmentCommandConstants` (**134**
values), `WeldmentGlobalConstants` (7), `WeldmentSectionTypeConstants` (4),
`WeldmentLinkStatusConstants` (3), `WeldmentDrawingViewTypeConstants` (3),
`AssemblyWeldmentOccurrencesOptionsConstants` (3), plus `DimWeldBead*` (4 + 4).
`SolidEdge.WeldmentDocument` has its own ProgID and `_IWeldmentDocumentAuto` lives in
`SolidEdgePart`.

**Frames** 📘 `StructuralFrames` / `StructuralFrame` (54 members) live in
**`SolidEdgeAssembly`**, with `StructuralFrameEndConditionConstants` (12),
`StructuralFrameExtendTrimPositionConstants` (2), `FrameShapeConstants` (2).

Explore with `node scripts/apiq.mjs members StructuralFrame` and
`node scripts/apiq.mjs enum WeldmentCommandConstants`.
