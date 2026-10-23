# Variables — driving geometry from the variable table

Measured live on **SE 2023 (223.00.13.05)**, 2026-10-23, with the throwaway console
`tools/MoldBaseProbe` (AutoEDM repo) attached through the ROT, working only in documents it
created in `%TEMP%`. Every number below was read back from the live object; the volumes were
compared with the hand-computed value to the cubic millimetre.

## The calls

`Document.Variables` (`PartDocument.Variables`, `AssemblyDocument.Variables`; typelib
SolidEdgeFramework):

| Call | Notes |
|---|---|
| `Add(pName, pFormula, [opt] UnitsType)` → `variable` | `UnitsType` is `UnitTypeConstants`: `igUnitDistance = 1`, `igUnitAngle = 2`, `igUnitScalar = 58`. |
| `Edit(pName, pFormula)` | Works on user variables **and on dimension variables**, by display name *or* system name. |
| `Item(name)` | By the user name (`"Comprimento"`). |
| `Query(pFindCriterium, [opt] NamedBy, [opt] VarType, [opt] CaseInsensitive)` → `VariableList` | `Query("*", 2, 3, false)` lists everything: `NamedBy` 0 user / 1 system / **2 both**; `VarType` 1 dimensions / 2 variables / **3 both**. |
| `CopyToClipboard(name)` / `AddFromClipboard(name, [opt] UnitsType)` | The "Paste link" route between documents. **Untested** — see below. |

`variable` has `Name`, `Formula`, `Value`, `UnitsType`, `DisplayName`, `SystemName`
(`V333`…), `VariableType` (`seVariableType_UserDefined = 1560616706`), `Delete()`. A
`Dimension` is in the same table and has `Formula`, `Value`, `Constraint`, `DisplayName`,
`SystemName` — but **no** `VariableType` (the `dynamic` binder throws on it).

## Traps, each one measured

- **`Add` without `UnitsType` creates a DISTANCE.** `Add("Ra", "1.6")` → `Value 0.0016`,
  `UnitsType 1`. For a plain number pass `igUnitScalar = 58` (`Value 2530`, raw).
- **The `Formula` of a constant reads back EMPTY.** `Add("Comprimento", "296", 1)` →
  `Formula ''`, `Value 0.296`. Only expressions survive in `Formula` (and come back
  re-spaced: `"Comprimento/2 - 20"` → `'Comprimento /2-20'`). **Read `Value`, never parse
  `Formula`** — code that parsed it never found the number (AutoEDM's `RaVariableStore`,
  fixed the same day).
- `Value` is in **database units** (metres for distance, raw for scalar); `"246 mm"` as the
  formula also works. Writing `Value = 0.4` sets 400 mm.
- **A typed cross-document formula KILLS the collection.** `Add("F1", "C:\…\Mestre.par!Comprimento")`
  (with or without quotes, or just `Mestre.par!Comprimento`) → `RPC_E_CONNECTION_TERMINATED`
  (0x80010006), and every later call on that `Variables` object fails the same way. SE survived.
- **A text variable kills it too.** `Add("Tipo", "\"A\"")` in an assembly →
  `RPC_E_CONNECTION_TERMINATED`. Keep text in file properties.
- `CopyToClipboard` → `CLIPBRD_E_CANT_OPEN` from an out-of-process console while the Windows
  session was **locked** (the console itself could not open the clipboard either), so the
  official link route is still unproven. Do not design around inter-document links until it
  is measured in-process with the desktop unlocked.
- **Dimension names are LOCALIZED, system names are not.** The extrusion depth shows as
  `Protrusão_Extrudada_1_Profundidade_Finita` on pt-BR; its `SystemName` is
  `ExtrudedProtrusion_1_FiniteDepth`. `Edit(systemName, …)` works — use it.

## Recipe: a plate fully driven by variables (ORDERED part)

Every step confirmed; after editing all nine variables the body volume matched the expected
value exactly (4 826 960 mm³).

1. `ModelingMode = 2` on a part you just created (never on the user's).
2. `Variables.Add` each driver with `UnitsType = 1`.
3. **Centred rectangle** on `RefPlanes.Item(1)`: four `Lines2d.AddBy2Points`, corners tied
   with `Relations2d.AddKeypoint(l_i, 1 /*end*/, l_j, 0 /*start*/)`, `AddHorizontal` /
   `AddVertical` on the sides. Centre it with **a construction diagonal** whose ends are tied
   to two opposite corners and whose **middle** (`keypoint 2`) is `AddPointOn` both axes
   projected with `Profile.ProjectRefPlane(RefPlanes.Item(2))` and `(Item(3))`
   (`AddPointOn(diag, 2, axis, -1)`). `ProfileSet.IsUnderDefined` → `False`.
4. `Profile.Dimensions.Constraint = true`, then `AddLength(line)` on one horizontal and one
   vertical side (measures the line itself — no pick points to get wrong), `.Constraint = true`,
   and **`dim.Formula = "Comprimento"`** (or `Variables.Edit(dim.SystemName, "Comprimento")`).
5. `Profile.End(1)` → 0; `Models.AddFiniteExtrudedProtrusion(1, Profile[], 2, depth)`.
6. Link the depth: find the dimension variable by value with an **empty** `Formula`
   (`Query("*", 2, 1)`), then `Edit(SystemName, "Espessura")`.

**Hole pattern (4× symmetric):** the same centred rectangle, every side `ToggleConstruction`,
driven by `EntreX`/`EntreY`; a `Circles2d.AddByCenterRadius` at each corner with
`AddKeypoint(circle, 0 /*centre*/, line_i, 0 /*start*/)`; `AddEqual(c0, c_i)`;
`Dimensions.AddCircularDiameter(c0)` with `Formula = "DiamFuro"`.
`ExtrudedCutouts.AddThroughAllMulti(1, Profile[], 2)` cut through a plate extruded to side 2
(volume exact).

**Centred pocket on the top face:** `Profiles.Add(face)` with a `Face` from `Body.Faces[6]`
→ **`InvalidCastException`** in this context. Use
`RefPlanes.AddParallelByDistance(RefPlanes.Item(1), thickness, 2, …)`; its distance shows up
in the table (`ExtrudedCutout_2_Profile_Plane1_Distance` once consumed) — link it to
`Espessura` like any dimension. Then `ExtrudedCutouts.AddFiniteMulti(1, Profile[], 1, depth)`:
**side 1** cut into the plate (measured by removed volume), and link its depth variable.

Editing a variable updates the body **immediately** — `Recompute()` changed nothing.

**Link a NEW dimension by diffing the table, not by its value.** Snapshot the `SystemName`s of
`Query("*", 2, 1)` before the feature/plane call, then `Edit` the one that appeared with an empty
`Formula`. Searching by value picks the wrong dimension as soon as two of them are equal (a plate 46 mm
thick with a hole pattern 46 mm apart).

**Two more traps found while building on this (live, 2026-10-23):**
- `Models.AddFiniteExtrudedProtrusion` returns the **Model**, not the feature. Naming the return value
  names nothing; the feature is `Model.ExtrudedProtrusions.Item(Count)`. (`ExtrudedCutouts.Add*Multi`
  does return the cutout — `Name = "PM_Colunas"` stuck.)
- `RefPlane.Name = "…"` → `E_FAIL`, although the typelib says `rw`. To find a plane again later, keep
  the number of its **`SystemName`** (`RefPlane_7` → 7) in a scalar variable.
- Deciding the cut side by **removed volume** (expected `n·π·r²·depth` within 2 %) and retrying the
  other side with a fresh sketch is cheap and was right every time; deleting a cutout takes its sketch
  with it, so the retry must redraw.

**More plate features, measured live (SE 2023, 2026-10-23 — gaveta/Babyplast mold bases):**
- **Boss on an existing body** — `Model.ExtrudedProtrusions.AddFinite(Profile, 1, side, depth)`: pointed
  the wrong way the protrusion falls INSIDE the steel and the volume grows by **0** — so "added volume ==
  W·L·H" picks the side the same way removed volume does for cuts. From the bottom plane (RefPlane 1) the
  outward side was `1`.
- **Symmetric cut through the sketch plane** — `ExtrudedCutouts.AddFiniteMulti(1, Profile[], 3, depth)`:
  `3 = igSymmetric` is accepted as ProfilePlaneSide and cuts `depth/2` to each side. Used for side grooves
  along the whole plate: sketch on the reference plane that contains the plate's width and height axes
  and extrude `length + 2` symmetric.
- **Sketch on a side plane in REAL part coordinates**: pick the RefPlane whose normal (three
  `Convert2DCoordinate` calls) is the axis you need, then feed each 3D corner through
  `Profile.Convert3DCoordinate` — no guessing which sketch axis is X or Z. Two disjoint rectangles in one
  profile cut both grooves in one feature.
- **Point a circle at the MIDDLE of a sketch line**: `Relations2d.AddKeypoint(circle, 0, line, 2)`
  (`igCircleCenter = 0`, `igLineMiddle = 2`). Six holes = a construction rectangle's 4 corners + the middle
  of its two long sides; the cylinders were read back at the expected centres.
- Features that cannot follow the plate's dimensions through formulas (corner locks, side grooves drawn
  from absolute coordinates) keep the values they were built with in scalar variables; on update, a
  different snapshot means delete and redraw.

## In an assembly

- `Occurrence.OccurrenceDocument.Variables.Edit(...)` works without opening the part: the
  occurrence's `GetRangeBox` reflects the new thickness at once.
- That document is **`ReadOnly = True`**. **`OccurrenceDocument.Save()` raises a MODAL box**
  ("Não é possível salvar o arquivo. Talvez você não tenha acesso de gravação…") and the COM call
  blocks until someone clicks OK — on a locked session, forever. Set
  `Application.DisplayAlerts = false` around automation.
- **`AssemblyDocument.Save()` writes the edited part too** (file time changed, `Dirty` → False).
- Re-stacking plates after a thickness change is a plain `Occurrence.PutOrigin(x, y, z)` (metres)
  with `z` accumulated from each plate's `Espessura` variable.
- Assembly variables work for numbers (`Add("Serie", "3035", 58)`, `Add("Curso", "60", 1)`).
- **Nested (assembly → subassembly → part), all measured:** the subassembly's `OccurrenceDocument` is
  `ReadOnly`, yet `Variables.Edit` on a part reached through `sub.Occurrences.Item(i).OccurrenceDocument`
  **and** `PutOrigin` on the subassembly's own occurrences both work, and the TOP `Save()` writes the
  part and the subassembly. `Documents.Open(path)` of the already loaded subassembly (or part) returns
  **the same object**, now `ReadOnly = False`; `Save()` + `Close()` on it is clean and the top assembly
  keeps showing the change. That is the safe way to save an edited component.
- **Which documents did the USER open?** Components loaded only through an assembly are NOT in
  `Application.Documents`; `Documents.Open` adds them. So a component found in `app.Documents` before you
  open it is in a user window — if it is `Dirty`, refuse (saving would write the user's unsaved work) and
  check this for EVERY file before touching any, or a refusal halfway leaves the earlier ones edited in
  memory. Do not `Close()` what the user had open. Note `Dirty` is often **True right after your own
  `Documents.Open`** of a component the assembly changed (`PutOrigin`, a variable edit) — it is not proof
  of user edits for documents you opened yourself (measured 2026-10-23).
- `Occurrence.PutMatrix(m, true)` with a **90° rotation** written in the `TranslationLast` layout (row
  vectors, translation at 12–14) landed exactly (range box deviation 0 mm) — the layout question is
  settled for writes too, at least for identity-origin assemblies made by code.

## Worked application

AutoEDM's "Porta-molde" button (`src/AutoEDM.Core/Mold/MoldBase/`) builds a Polimold mold base as one
ordered part per plate driven by these recipes, inside `Porta Molde.asm`, and switches mold base by
rewriting the variables of the same parts (rebuilding only hole groups whose shape changed). Full round
trip 25.35 → 35.40 → 20.20 → 25.35 measured with exact volumes per plate.

**Library parts at the pose of a house template.** To put standard parts (a quick-change frame, rails,
handles, screws) where the company's template assembly has them: read each occurrence pose from the
template once (`GetMatrix`, TranslationLast — see `api-signatures.md`), store R and t, compose with the
rotation between the template frame and yours (`R' = M·R`, `t' = M·t`) and `PutMatrix` after
`Occurrences.AddByFilename(libraryPath)`. A whole sub-assembly (the frame) places the same way. Measured:
rails, handles and screws landed on the generated grooves and holes.

**Don't read a plate's thickness from the body range box of a real mold part** — cavity plates carry
cores/locks that stick out past the parting line (a 46 mm plate measured 52.1). List the planar faces
instead (`Body.Faces[6]`, keep those whose range is flat in one axis) and read the heights.

**Learning a company standard from its template assembly (read-only, measured 2026-10-23).** Three
passes, each opening the file only if the user does not have it open and closing with `Close(false)`:
1. *Links* — Revision Manager `LinkedDocuments` (see `api-signatures.md`): which parts are copied per
   mold (the template's own files) and which come from the shared library (keep those by reference).
2. *Poses* — `GetMatrix` of every top-level occurrence. A standard part is placed later with the same
   pose composed with your frame (`R' = M·R`, `t' = M·t`).
3. *Geometry per part* — planar faces flat in one axis (`Body.Faces[6]`: heights, steps, pocket floors,
   counterbore floors) and cylinders (`Body.Faces[10]`: Ø and centre from the face range). Positions read
   in part coordinates must go through that occurrence's pose before comparing parts — templates often
   place halves rotated 180° (`diag(-1, 1, -1)`), which turns an off-centre hole pattern to the other side.
What it taught on real templates: a "return pin Ø10 with a 16 × 5 head" seen in molds was the shared
frame's ejector rod; "guide pins Ø5 at 63" were M6 tapped holes (Ø5 = tap drill); a hole pattern 1 mm off
centre was deliberate anti-inversion. Measure the template, not the finished molds.

**More validated in the same work:**
- A ring (outer rectangle + inner rectangle in one profile, cut from one face) needs
  `Profile.End(1 | 8192)` — `igProfileAllowNested`; plain `End(1)` returns −113 (as in
  `modeling-recipes.md`, reconfirmed).
- Circles placed one by one at absolute sketch coordinates (no position dimension) are fine for an
  off-centre pattern; give them `AddEqual` and one diameter dimension so the Ø stays variable-driven.
- A line's middle also anchors a third circle on a centre line: `AddKeypoint(circle, 0, line, 2)`.
