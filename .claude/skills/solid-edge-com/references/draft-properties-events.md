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

# Drawing views and dimensions from code (SE 2023 v223, 2026-10-19)

Measured building a fully dimensioned turned-part drawing (`AutoEDM.Drawing.TurnedDrawingBuilder`).
Every bullet cost a live round; the API raises **no error** for most of these mistakes —
the dimension is created, attached to something, and shows a wrong number.

**Views**
- `Documents.Add("SolidEdge.DraftDocument", templatePath)`, `ModelLinks.Add(partPath)`,
  `DrawingViews.AddPartView(link, igFrontView=4, scale, x, y, sePartDesignedView=0)`.
- Arbitrary orientation: `view.SetViewOrientation(viewVecX,Y,Z, localXX,Y,Z)` then `Update()`.
  Up in the view = viewVec × localX. Puts any part axis horizontal.
- `AddByFold(view, igFoldLeft=4, x, y)` for a projected view; iso = `AddPartView(..., 9, ...)`.
- `view.ModelToView(x,y,z, [out] vx, [out] vy)` and `DVLine2d.GetStartPoint/GetEndPoint`,
  `DVArc2d.GetCenterPoint/.Radius` share ONE space: **view-local, model metres, no scale**.
  Sheet = `view.GetOrigin` + `ScaleFactor` × local. Find 2D edges geometrically with these.
- The typed hop `DVLine2d.Reference/GetReferenceKey` → 3D edge throws `DISP_E_TYPEMISMATCH`
  with interop 219 against SE 223 — don't build on it; match geometry instead.

**Attaching dimensions — pass `dvElement.Reference`, never the DV element**
- With the raw `DVLine2d` SE reads its local coordinates as SHEET coordinates: the dim lands
  in the sheet's bottom-left corner with a nonsense value. `((dynamic)dvLine).Reference` fixes
  it; pick points are SHEET metres.
- Reading an existing dim: `Dimension.GetRelatedCount/GetRelated(i, out obj, out x,y,z, out kp)`
  needs the TYPED `SolidEdgeFrameworkSupport.Dimension` (IDispatch out-object → TYPEMISMATCH);
  the related object is a `Reference` (`.Object` = DVLine2d) or a `CenterLine`.

| Dimension | Call | Born wrong → fix |
|---|---|---|
| Coordinate origin | `AddCoordinateOriginEx(ref, px,py,0, false, tx,ty,0)` | 3rd point = origin TEXT; place it PERPENDICULAR to the run (above the view for a horizontal run) or the whole chain measures vertically. `MeasurementAxis*` setters throw on coordinate dims |
| Coordinate | `AddCoordinateEx(originDim, ref, px,py,0, false, tx,ty,0)` | follows the origin |
| Symmetric Ø | `AddSymmetricalDiameter(centerLine, cx,cy,0,false, ref, px,py,0, kp)` | measures along the axis; flip `MeasurementAxisDirection` |
| Centre line | `sheet.CenterLines.Add(x1,y1,0, x2,y2,0)` | `AddBetweenTwoLines(ref, ref)` → E_FAIL / 0x80040223 |
| Radius | `AddRadius(arcRef)` | raw DVArc2d → 0x80040223 |
| Chamfer | `AddChamfer(neighbourRef, …, false, chamferRef, mid…, true, 0)` | reversed order measures the neighbour; `TrackDistance` is born **−123456** (text off-sheet) — set ~0.006 |
| Angle between lines | `AddAngleBetweenObjects(ref1,…, ref2,…)` | born supplementary (180.5° for 0.5°); flip `MeasureLineDirection` |

- **Verify every dim**: `Value` is metres (radians for angles). Compare with the model,
  apply the known flip, delete (`Delete()`) what still does not match.
- `Dimension.Value` units: linear m, angle rad. `PrimaryUpper/LowerTolerance` are STRINGS
  in display units ("-0,01"). `DisplayType`: 2 = tolerance, **14 = fit + tolerance**
  ("Ø27 g6 (-0,01/-0,02)"); 9 stores the numbers but the PAN style does not show them.
- Style: `dim.Style.Name = "PAN (mm)"` switches to a named template style
  (`dim.Style = styleObj` fails to convert). ISO (mm) rounds angles to whole degrees.
- Text: `SetTextOffsets(d, 0)` slides a Ø label along its dimension line, `d` sheet metres
  from the axis.

**Placement (copying a hand-made layout, 2026-10-19).** Read the human's dims with
`GetKeyPoint(i, out x,y,z, out type, out handle)` — that is the layout spec.
- Symmetric Ø: `DisplayHalf = true` gives the full line with both arrows (7 key points:
  k1 = attach on the feature, k4/k5 = dim-line ends, k6 = text). `TrackDistance` is the
  SIGNED distance from **k1** (SE snaps the pick to the generatrix END, not where you
  clicked): positive = line to the left, negative = to the right. Verify the side by k4.
- Radius with leader: `Leader = true`, `TerminatorPosition = false`, `BreakPosition = 3`;
  then move the knee with `SetKeyPoint(3, x, y, 0)` — SE recomputes `TrackDistance`/
  `BreakDistance`. `TrackDistance` is NOT the leader slope (the human had 0.61 on leaders of
  different slopes). Concave arcs (groove) report k0 where the leader touches; use it.
- Chamfer with leader: `TerminatorPosition = true`, `BreakPosition = 3`, `BreakDistance`
  = knee leg; `TrackDistance` = vertical leg, sign depends on the picked line — verify by k4.
  The text side flips with `BreakPosition`: on a leader going UP, 3 puts the text right of
  the knee and 1 puts it left (measured 2026-10-20; the leader going down already falls left with 3).
- A radius label's text always runs to the RIGHT of the knee, whatever `BreakPosition` or
  `TerminatorPosition` says — to get it out of the way, move the knee (lower), not the side.
- Angle: `SetKeyPoint(6, x, y)` moves the text and the arc follows.
- Circular Ø in an end view: `TrackAngle = π` makes it horizontal; check k6 is above.
- Balloon with leader: `Balloons.Add(textX, textY, 0)` → `Leader/Callout=1/BalloonType=0`
  → `BalloonText` (property text, `\r` between lines) → **`SetTerminator(ref, x,y,0,false)`
  last**. `AddByTerminator` or terminator-first loses the leader (only 1 key point).
- Custom iso: `SetViewOrientation` works on iso views too; the "lying" turned-part iso is
  view = (v + up − axis), localX = −(v + axis) in the side-view frame.
- Background `TextBox.Text = ...` resets `VerticalAlignment`/`Justification` (template had
  1/4) and the text jumps a row — save and restore them (font/size via `Edit.SetSelect(0,n,5)`).
- `SaveCopyAs("x.pdf")` exports the ACTIVE window; for an unsaved draft `SaveAs("x.pdf")`
  exports it and the document stays unsaved.
- Title block shows "Erro: Sem referência" until `draft.UpdatePropertyTextCacheAndDisplay()`.

**GD&T (feature control frames, datum symbols), 2026-10-19.**
- Text syntax is in SE's own `Template\Reports\DraftFCF.txt`: cells separated by `|`, symbol
  codes `%CY` cylindricity, `%FL` flatness, `%PP` perpendicularity, `%PR` parallelism,
  `%AN` angularity, `%PO` position, `%CO` concentricity, `%SY` symmetry, `%DI` Ø; measured
  live: `%CR` circular runout. E.g. `fcf.PrimaryFrame = "%PP|0,01|A"`. (`%VB` or `%PE`
  render as literal text — no error.)
- Placement that works: `FeatureControlFrames.AddByTerminator(target, x, y, 0, false)` →
  `PrimaryFrame` → **`AddVertex(boxX, boxY, 0)`**: the box moves to the vertex and the leader
  bends to the target. The box is CENTERED on the vertex (≈18 mm wide with 2 cells, ≈20 mm
  with 3, ≈6 mm tall at the house style). `Add(x,y)` + `SetTerminator` jumps the box onto the
  terminator; `SetKeyPoint(0, …)` on it moves only Y.
- `target` can be a **Dimension** (frame on a Ø dimension = controls the axis). The terminator
  SNAPS to the dim line's TOP end (outer end, 3 mm past k4) whatever point you pass — passing
  k5 still lands on top (measured 2026-10-20). Lay frames on Ø dimensions out ABOVE the stack.
- Datum: `DatumFrames.AddByTerminator(dimOrEdgeRef, x, y, 0, false)` then `.Datum = "A"`;
  on a Ø dimension's k5 (bottom end) = axis datum, on a face edge = plane datum.
  `AddVertex` on a datum frame does NOT move it to the vertex (it slides along its line).
- **Break line (dog-leg) on a frame** (measured 2026-10-20): `fcf.BreakLine = true`,
  `BreakLineDistance` = shelf length, `BreakLineDirection` false = box RIGHT of the leader end,
  true = box LEFT. Key points: k0 terminator, **k1 = leader end** (shelf start), k4 = shelf end
  at the box, k2 = vertex. Where `AddVertex(x, …)` puts k1 depends on the side and on the box's
  real width (box right: k1 ≈ x − w/2; box left: x is the box center). `SetVertex(1, …)` and
  `Move` are inconsistent across frames — for a VERTICAL leader create, read k1, and if it
  missed, `Delete` and recreate with x corrected by the error (AddVertex is a translation).
- Measured box widths at the house style: 3 cells ≈ 20 mm; `%PO|%DI0,01|A` (position, Ø zone)
  ≈ 27 mm. Position = `%PO`, Ø in a cell = `%DI` prefix of the value.
- A datum frame on a dimension cannot be moved off the dimension line: `Leader = true` +
  `AddVertex` slides it horizontally, `AddByTerminator` beyond the line end leaves it floating.
  Leave room next to the dimension line instead.
- Associative centerline: `CenterLines.AddByStartEnd(edgeRef1, x1, y1, 0, false, edgeRef2, x2,
  y2, 0, false)` on the two end edges of the part (DVLine `.Reference`) — follows the view, and
  the style extends it ~3.5 mm past each end. `CenterLines.Add(x1,y1,0,x2,y2,0)` is loose
  geometry. Deleting a centerline deletes the symmetric Ø dimensions hanging on it.
- Datum on the feature's own frame (ISO): `DatumFrames.AddByTerminator(fcf, x, boxBottomY, 0,
  false)` hangs the symbol right under the box at x (≈5 mm wide, 6 mm tall). Passing the
  frame's `Range` bottom instead lands on the leader end — use the box edge (vertex y − 3 mm).
- Hidden edges for bores: `drawingView.Defaults_ShowHiddenEdges = true` before `Update()`;
  the hidden DV lines are matched and dimensioned like visible ones.

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
