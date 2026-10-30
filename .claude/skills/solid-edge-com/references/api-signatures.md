# Confirmed API facts and signatures (SE 2023, v223.00.13.05)

Verify every numeric value against the dump for your own version before trusting it.

## Contents
- Confirmed facts: connecting, face color, face traversal, bbox, occurrence transforms, in-place editing
- Surfaces: `Constructions` vs `Model`
- What the live Model actually exposes (163 members)
- SAFEARRAY marshaling
- Reference planes, topology-query enums, robust bounding box, assembly mates
- Undo, "preview", and what silently empties the user's undo stack (end of file)

## Confirmed facts (SE 2023, v223.00.13.05) — verify against the dump for other versions

- **Connect**: `SolidEdge.Application` ProgID; assembly `Document.Type == 3` (`.asm`).
- **Face color** lives in THREE different places — SE's "paint" command defaults to the
  **feature**, not the face, so you must handle all three:
  1. **Per-face** paint: `face.Style.Diffuse{Red,Green,Blue}` (0..1, ×255) or
     `Style.GetDiffuse(out r,g,b)`. **`face.Style` is `null`** if the paint is not a
     per-face override.
  2. **Feature** paint (the common default!): NOT visible on the face — `face.Style` is
     null and `Face.GetRGBAVals` returns the **body** color, not the feature paint. Read it
     from the feature: `PartDocument.Models.Item(m).Features.Item(i).GetStyle()` gives a
     Style with the same `Diffuse{Red,Green,Blue}`. The feature's faces are
     `feature.Faces[queryType]` — **indexed like `Body.Faces[1]`**, not a bare collection.
     Build a `Face.ID → color` map from all features (`Face.ID` matches across Body/Feature),
     then look each body face up by `Face.ID`. Validated end-to-end on SE 2023.
  3. **Body/effective** color: `Face.GetRGBAVals(out R,out G,out B,out A)` — four
     `[out] double` (0..1). With `dynamic` + `out` they arrive fine; through
     `Type.InvokeMember` you MUST mark them by-ref with a `ParameterModifier` or they come
     back `0,0,0,0` (same out-param trap as `GetRange` — mechanism measured and corrected in
     `com-rules.md`). This is the base color, NOT feature paint.
  Priority to resolve a face's color: per-face Style → feature `GetStyle` → `GetRGBAVals`.
  (`Occurrence.GetFaceStyle2(vbHonourPrefs: bool)` takes a bool, not a face — not usable here.)
- **Face traversal**: `PartDocument.Models → Model.Body → Body.Faces[queryType] → Face`;
  **query type 1 = igQueryAll** on SE 2023.
- **Face bounding box**: `Face.GetRange(MinRangePoint, MaxRangePoint)` (also
  `GetExactRange`) — dispid 227 / 132. Both corners are **`[in,out]`** SAFEARRAY of 3
  doubles (meters) — **not `[out]` pure**, so the array must arrive ALLOCATED (`ref null`
  gives `E_INVALIDARG`). With `dynamic` + `ref` they arrive correctly and no
  `ParameterModifier` is needed; through `Type.InvokeMember` without one they come back
  all zeros, silently. See `com-rules.md`.
  The two are **not** interchangeable: on B-spline geometry `GetRange` comes back padded
  and `GetExactRange` tight — see "`GetRange` is a PADDED box on B-spline edges" below
  before comparing a range against any fine tolerance.
- **Box feature** needs a RefPlane and 13 args:
  `AddBoxByTwoPoints(x1,y1,z1, x2,y2,z2, dAngle, dDepth, pPlane, ExtentSide,
  vbKeyPointExtent, pKeyPointObj, pKeyPointFlags)`. Pass `RefPlanes.Item(1)` as pPlane.
  `ExtentSide`: `igLeft=1, igRight=2, igSymmetric=3`. If the dynamic binder throws a
  VARIANT conversion error, call via `InvokeMember` (IDispatch coerces) with
  `Type.Missing` for optional object params.
- **Occurrence placement**: `Occurrence.GetTransform(out x,y,z, out ax,ay,az)` (meters +
  radians) — the 6 args are `[out]`, so **mark them by-ref with a `ParameterModifier`** or
  they all come back **0,0,0,0,0,0** (same out-param trap as `GetRange`; a silent all-zero
  transform read as "at the origin, no rotation" caused a ~23 mm mis-placement until fixed).
  Set with `Occurrence.PutOrigin(x,y,z)` (translation only) or **`Occurrence.PutTransform(x,y,z,
  ax,ay,az)`** (translation **+ rotation**; dump line 6707, mirror of GetTransform) — both meters,
  both work with no in-place edit. To align a new part to another, possibly **rotated**,
  occurrence (e.g. an electrode to a rotated cavity): read the source transform (by-ref!), place
  the new occurrence with `PutTransform` using the source's angles, and **rotate any source-local
  offset** (e.g. a feature's XY center) by the same angle before adding the source translation.
  For a pure Z-rotation Z is unchanged; warn/skip on X/Y tilt.
- **Occurrence FULL POSE — prefer this over the 3 angles when any X/Y tilt is possible**:
  `Occurrence.GetMatrix([in,out] Matrix: SAFEARRAY(double))` / `Occurrence.PutMatrix(Matrix:
  SAFEARRAY(double), Replace: bool)` — confirmed in the SE 2023 dump on both `Occurrence` and
  `Part` (same block as Get/PutTransform). 16 doubles, meters. The array is `[in,out]`: **pre-
  allocate `new double[16]` and mark it by-ref with a `ParameterModifier`**, and accept a NEW
  array coming back in the arg slot (some calls replace rather than fill — reading 16 zeros as
  a valid matrix is the trap here).
  **Why the matrix and not the angles**: `GetTransform` gives 3 Euler angles but the typelib
  never says in which ORDER they compose (Rz·Ry·Rx? intrinsic? extrinsic?). Guessing wrong only
  shows up when two axes rotate at once — precisely the tilted case you were trying to fix, and
  it fails *silently* (you get the transpose: a part lying the wrong way, no error). `GetMatrix`
  hands you the rotation already composed; `PutMatrix(..., Replace: true)` writes it back with no
  conversion to angles anywhere. `Replace: false` would COMPOSE with the current placement and
  apply the rotation twice.
  **The layout is NOT documented** (the signature only says `SAFEARRAY(double)`): translation
  may sit at indices 12,13,14 (row-vector, `p' = p·M`) or 3,7,11 (column-vector, `p' = M·p`),
  and picking wrong transposes the rotation. **Detect it at runtime**: read the origin with
  `GetTransform` (already a validated path) and pick whichever slot triple matches. Then transform
  local points by the matrix instead of adding coordinates axis-by-axis — adding a local Z depth
  to an assembly Z only works while the two Z axes are parallel, and when they are not it can
  still return a plausible-looking number (AutoEDM hit exactly this on job MD-14972, cavity at
  Y = −90°: 3 of 5 electrodes "looked right" by arithmetic coincidence).
- **In-place editing is NOT `Occurrence.Activate = true`.** That boolean only LOADS /
  activates the occurrence (large-assembly memory management); it does **not** enter
  part-edit-in-place. The authoritative signals are `AssemblyDocument.ModelingInAssembly`
  and `.InPlaceActivated` (bool getters) — read them, never assume. `EditAssembly()` edits
  the assembly, not a part. There is no reliable COM verb to enter part in-place edit.
- **Inter-Part Copy via COM is effectively blocked** (confirmed over ~11 runs). Because you
  can't get truly in-place, `Constructions.CopySurfaces.Add` behaves as **intra-part**
  (copies the part's own faces) and returns `E_FAIL` when fed another part's faces;
  `InterpartConstructions.Add/Add2` and `CreateReference`-based copies fail too
  (`E_FAIL`/`E_NOINTERFACE`/`DISP_E_TYPEMISMATCH`). **Don't sink runs into it.** Use a
  workaround that needs no in-place edit (see Modeling) or let the human do the copy.

## Modeling the electrode — real call signatures (SE 2023, from the dump)

The surface-copy / offset operations are **collection properties on
`PartDocument.Constructions`**, not direct methods — that's why introspection shows
them with `cParams=0`. You call `.Add(...)` on the returned collection; the **last
arg is the `[out]` result object**:

| Step | Call |
|---|---|
| Copy burn faces (associative) | `Constructions.CopySurfaces.Add(NumberOfFaces, FaceArray, InternalBoundary, ExternalBoundary, out CopySurface)` |
| Spark-gap offset (electrode SHRINKS → inward side / negative sense) | `Constructions.OffsetSurfaces.Add(Side, offsetDistance, FaceSet, Boundary, out OffsetSurface)` |
| In-context associative link | `InterpartConstructions.Add(AsmSource, out X)` / `Add2(PartTarget, AsmSource, out X)` |
| Bring a whole part, **keeping colors** | `CopyConstructions.Add(FileName, X/Y/ZScale, MirrorPlane, FamilyOfPartsMember, CoordinateSystem, IncludeDesignBody, …, CopyColors, out CopyConstruction)` — set `CopyColors=true` so face Ra colors survive |
| Linked Part Copy as DESIGN body (ordered) | `Models.AddCopiedPart(FileName, …)` → `Models.Item(1).CopiedParts.Item(1)`; `LinkMaterial = true`. **No color option** (typelib nor IDispatch: `CopyColors`/`LinkColors`… → `DISP_E_UNKNOWNNAME`), although the UI dialog has "Copy colors". Geometry follows the source file; **colors never do** — not even `CopyConstruction` with `CopyColors` re-copies a changed color (measured 2026-10-27, `tools/PartCopyProbe --cores`). `Face.ID` of the copy = source `Face.ID`, stable across updates → paint by ID from a temporary `CopyConstructions.Add(…, CopyColors=true)`, then delete it (`ProcessDrawingBuilder.CopySourceColors`). `Models.AddByConstruction(cc)` turns a construction into the design body but **drops the colors** |

> ⚠️ **These inter-part signatures exist in the typelib but do NOT work via COM** — see
> "Inter-Part Copy is effectively blocked" above. They're documented here for reference;
> in practice, model on the part itself (`modeling-recipes.md`) or have the human do the associative copy.

**Surface construction features DO work *intra-part* (SE 2023, confirmed Logs 57/58).** The
inter-part block is only about copying *another* part's faces. Within a **single part** (e.g.
after a human copies the burn faces into the electrode part), the `Constructions` surface
collections work — real signatures from the dump (last `[opt]` args omitted-OK):

| Op | Call |
|---|---|
| Copy this part's own faces → a surface | `Constructions.CopySurfaces.Add(NumberOfFaces, FaceArray SAFEARRAY(IDispatch), [opt]InternalBoundary, [opt]ExternalBoundary) → CopySurface` |
| Stitch surfaces into a (closed→solid) body | `Constructions.StitchSurfaces.Add(NumberOfSurfaces, SurfaceArray SAFEARRAY(IDispatch), [opt]Heal, [opt]Tolerance) → StitchSurface` (also `AddByMultiple`) |
| Offset a surface (spark gap) | `Constructions.OffsetSurfaces.Add(Side FeaturePropertyConstants, offsetDistance double, FaceSet IDispatch, [opt]Boundary) → OffsetSurface` |
| Thicken a surface into a solid | `Models.AddThickenFeature(…)` |

**⚠️ Reflection-of-interop LIES about what the live Model exposes — the live SPY/DIAG is the
truth (hard-won, 2026-07-15).** An earlier version of this skill claimed "`ExtendSurfaces,
IntersectSurfaces, TrimSurfaces` live on the Model" — because reflecting `Interop.SolidEdge`
finds those interop **types**. **But the live `partDoc.Models.Item(1)` IDispatch does NOT expose
them** — `model.ExtendSurfaces` throws `'System.__ComObject' does not contain a definition for
'ExtendSurfaces'`, and a live DIAG dump of the Model's real 163 members has **no** ExtendSurfaces /
IntersectSurfaces / TrimSurfaces. The interop assembly ships the *type* (some other object or a
newer SE build uses it), but **that type being reflectable ≠ this Model instance implementing it.**
Rule: **reflect the interop for signatures/enum values, but confirm the member EXISTS by dumping
the live object** (`ComDiagnostics.LogMembers`/`DumpObject`) before you build on it.

**…and check the typelib dump for WHICH object owns it before declaring it missing (2026-09-21).**
`ExtendSurfaces` does exist live — on **`ConstructionModel`**, i.e. `partDoc.Constructions.Item(i)`
(`docs/api/SolidEdgePart.md`, section `ConstructionModel`). `partDoc.Constructions.ExtendSurfaces`
and `Model.ExtendSurfaces` both give `DISP_E_UNKNOWNNAME`; the old probe only looked at those two
and concluded "doesn't exist". The collection is shared (every `Item(i)` lists the same features).
But **`ExtendSurfaces.AddEx` in a SYNCHRONOUS part failed for every input** — types 1/2/3, 0,5/1/2 mm,
8 rim edges or 1, edges from the `Body` or from the `StitchSurface` feature: `E_FAIL` (the first
call of each process gives `E_NOINTERFACE` instead). `Add1(..., TargetSurface=face)` →
`E_INVALIDARG`. Sync `Extend Surface` features made in the UI read back `Distance`/`Target` as
`E_NOINTERFACE` (sync features have no parameters). Automating the extend is still open; the
AutoEDM button only warns when the rim stops below the block.

**What the live SE-2023 Model (163 members) ACTUALLY exposes** for surface→solid / boolean
(DIAG-confirmed, with the `.Add` signatures the probe read live):

| Op | Real call |
|---|---|
| Thicken a surface's faces into a solid | `partDoc.Models.AddThickenFeature(Side FeaturePropertyConstants, offset double, nFaces int, Faces SAFEARRAY(IDispatch)) → Model` |
| Boolean unite bodies (**Ordered-only in practice — see caveat**) | `Model.Unions.Add(nTargets, TargetArray SAFEARRAY(IDispatch), nTools, ToolsArray SAFEARRAY(IDispatch), SETargetDesignBodyOption, SETargetConstructionBodyOption)` (igCreateSingleDesignBodyOnNonManifold=2, igCreateSingleConstructionGeneralBody=1; both enums also have a `0` = igCreateMultiple…OnNonManifoldOption — safer default, doesn't fail the op on a non-manifold result). **`Unions` IS in the stale PIA** (unlike `FaceOffsets.AddEx`, below) — `(SolidEdgePart.Unions)model.Unions` compiles, and tlbimp generated its SAFEARRAY params as `ref Array` (not a plain array) — pass `ref targets, ref tools`. **Array element type is NOT one-size-fits-all — cast each side to what it actually IS, not to a generic `Body`:** a solid Model's target casts fine to `SolidEdgeGeometry.Body[]` (`model.Body`), but a **`CopySurface` tool does NOT implement `SolidEdgeGeometry.Body`** — casting it throws `E_NOINTERFACE` on IID `{09FCA073-DFBF-11D0-A275-080036C5ED02}` (confirmed live, 2026-07-17). A `CopySurface` is already a valid `IDispatch`; type its array element as `SolidEdgePart.CopySurface[]` instead (`new SolidEdgePart.CopySurface[] { (SolidEdgePart.CopySurface)surf }`) — `Unions.Add`'s `SAFEARRAY(IDispatch)` doesn't care that target/tool are different concrete interop types, it just needs each element to genuinely implement *some* real COM interface, not `object`. **CAVEAT (2026-07-20, 2 more real tests, correctly-typed target AND tool):** for a raw/un-stitched surface merging into a solid, `Unions.Add` reliably threw `E_FAIL` when the document was actually in SYNCHRONOUS mode — for that specific surface→solid "attach" use case, use `Model.Attach` or `Model.BooleanFeatures.Add` (next rows), NOT `Unions.Add`. `Unions.Add` may still be the right call for solid+solid merges (e.g. two design bodies) — not re-tested for that case. |
| **Attach a surface directly onto a solid ("anexar"), SYNCHRONOUS, no tree feature created** | `Model.Attach(NumOfObjects: int, psaObjects: SAFEARRAY(IDispatch)*, bAdd: bool, fpcSide: FeaturePropertyConstants) -> void`. Confirmed live 2026-07-20 (AutoEDM). Returns `void` — no new feature registers anywhere; the only trace is the SOURCE `CopySurface`/`StitchSurface` migrating into `Model.Features` (previously only in `Constructions.CopySurfaces`). `fpcSide` is **required** (no `[opt]` in the dump) — try `igRight=2` then `igLeft=1`. **Pass the surface's BODY, not the feature (2026-09-21, 2 parts):** `new SolidEdgeGeometry.Body[] { body }` where `body = surf.Faces[1].Item(1).Body` (Copy/Stitch/SurfaceByBoundary features expose no `.Body`) — with `StitchSurface[]` it was `E_FAIL` on both sides, with the `Body[]` of the SAME surface it attached first try (fpcSide=2; 20→37 and 14→27 faces). The feature array (`CopySurface[]`) worked once in older logs and is kept as fallback. Pass the array by-ref (`ParameterModifier`). A rim lying EXACTLY on the block face (same Z) is enough — no need to penetrate. **Attach the surface the user prepared, not a copy of its faces:** a `CopySurface` of a `StitchSurface`'s faces lies on top of the original and `Attach` gives `E_FAIL`. **Must run in Sync mode** — same silent-no-op-outside-native-mode risk as `AddThickenFeature` if the doc is already Ordered from a previous step. |
| Boolean unite, SYNCHRONOUS-capable alternative to `Unions` | `Model.BooleanFeatures.Add(NumberOfTools: int, Tools: VARIANT, Function: BooleanFeatureConstants, [opt]PlaneSide: VARIANT) -> BooleanFeature`. **With a construction SURFACE as tool it gave `E_NOINTERFACE` in EVERY AutoEDM run (2026-09-10 → 09-21)** — it is a body-vs-body boolean; for surface→solid use `Attach` with `Body[]`. No explicit Target param (implicit = the Model's own body). `Function=3` = `seBooleanUnite` (`1`=Intersect, `2`=Subtract, `4`=PlaneFront — `docs/api/constants.md`). Less field-tested than `Attach` (found in the same 2026-07-20 sondagem, not yet the primary path in AutoEDM). **Fetch the collection via `Type.InvokeMember("BooleanFeatures", BindingFlags.GetProperty, ...)`, not dynamic property access** — `model.BooleanFeatures` threw `E_NOINTERFACE` even though the collection genuinely exists live (PIA-version mismatch, see error table). |
| Boolean subtract (also a hole workaround) | `Model.Subtracts.Add(nTargets, TargetArray, nTools, ToolsArray, DirectionArray SAFEARRAY(SESubtractDirection), targetOpt, constrOpt)` |
| Boolean intersect | `Model.Intersects.Add(nTargets, TargetArray, nTools, ToolsArray, targetOpt, constrOpt)` |
| Redefine/replace solid faces with a surface | `Model.RedefineFaces.Add(nFaces, [in,out] FacesArray, nEdges, [in,out] NonLaminarEdgesArray, [in,out] TangencyTypeArray, FaceMerge SurfaceByBoundaryPatchTopology, ReplaceFacesOnSolidBody bool) → RedefineFace` |
| Also present on the Model | `ReplaceFaces, Thickens, TrimExtendCollection, FaceOffsets, BlankSurfaces, Threads, Holes, Rounds, Chamfers, Attach, Detach, BooleanFeatures` (no ExtendSurfaces/IntersectSurfaces) |

**The stale PIA can have the COCLASS but be missing individual LIVE methods — check
per-method, not just per-type** (found 2026-07-17: `SolidEdgePart.FaceOffsets` exists in
this project's referenced `Interop.SolidEdge.dll` — a real, castable type — but that type
only has the old 8-param `Add`; **`AddEx` (13-param, multi-face) is simply absent**, because
the referenced interop was generated from an older SE SDK than what's installed. Casting to
`(SolidEdgePart.FaceOffsets)x` compiles fine and `.Add(...)` works — it's only when you reach
for `.AddEx` that you'd get a compile error "does not contain a definition." The fix is the
same one used everywhere else in this skill for members the PIA doesn't know about: call it
via `obj.GetType().InvokeMember("AddEx", BindingFlags.InvokeMethod, null, obj, args)` on the
raw `object`/`__ComObject`, passing the typed SAFEARRAY as a **plain array element** in
`args` (no `ref`, no `ParameterModifier` — those are only needed for true `[out]`/`[in,out]`
params; a pure `[in]` SAFEARRAY(IDispatch) marshals fine as a normal arg through
`IDispatch::Invoke`, same as `AddFiniteExtrudedProtrusion`'s profile array elsewhere in this
skill).

**Quick way to check whether a method exists in the referenced interop DLL before writing
code that assumes it does** (avoids a build-time surprise or a wrongly-guessed `InvokeMember`
call for something that was reachable directly all along):
```powershell
$asm = [System.Reflection.Assembly]::LoadFrom("path\to\Interop.SolidEdge.dll")
$t = $asm.GetType("SolidEdgePart.FaceOffsets")
$t.GetMethods() | Select-Object Name   # AddEx present? Add only?
$t.GetMethod("Add").GetParameters() | % { "$($_.ParameterType) $($_.Name)" }  # ref Array or plain Array?
```
This also answers the "does this SAFEARRAY param need `ref`" question directly (tlbimp
sometimes emits `ref Array` for params the IDL just calls `SAFEARRAY(...)*` with no
`[out]`/`[in,out]` tag) — cheaper than guessing and hitting a runtime marshaling error.

Stitch/offset/copy are on **`Constructions`** (also live-confirmed): `CopySurfaces.Add(nFaces,
FaceArray, [opt]InternalBoundary, [opt]ExternalBoundary) → CopySurface`; `StitchSurfaces.Add(
nSurfaces, SurfaceArray, [opt]Heal, [opt]Tolerance) → StitchSurface`; `OffsetSurfaces.Add(Side
FeaturePropertyConstants, offset double, FaceSet **IDispatch (single object, not array)**,
[opt]Boundary) → OffsetSurface`.

**Patch an open boundary loop ("Limite" / Surface Patch)** — `Constructions.SurfaceByBoundaries.Add(
NumberOfEdges int, [in,out] EdgesArray SAFEARRAY(IDispatch), [opt]NumberOfExcludeEdges,
[opt]ExcludeEdgesArray, [opt]Tangent) → SurfaceByBoundary` (dump of SE 2023,
`_ISurfaceByBoundariesAuto`; `AddEx`/`Add3` add guide wires, FaceMerge, fill preference). One call
per **closed** loop of edges — so the caller has to chain the laminar edges into loops first
(`Edge.GetEndPoints([out] StartPoint, [out] EndPoint)`, two by-ref SAFEARRAY(double) in meters,
same out-param trap as `GetRange`).

> ⚠ **The static PIA disagrees with the typelib on this one — and the PIA is the wrong one.**
> `Interop.SolidEdge` 219 imports it as `Add(int, out Array&, object, object, object)` — an
> **`out`** array, which would NOT carry your edges into the call — while the live SE 2023 dump
> marks the param `[in,out]`. Call it **late-bound via `InvokeMember` with a `ParameterModifier`
> marking that arg by-ref** (works for `[in,out]` and `[out]` alike) instead of through the PIA.
> A reminder that the interop package (219) is older than the SE build (223): where they differ,
> the dump wins.

**Surfaces of different kinds can't share one typed array.** `CopySurface`, `StitchSurface` and
`SurfaceByBoundary` are unrelated interop interfaces (no common base), so a stitch of "the copied
surface + its boundary patches" has **no** valid `SAFEARRAY(IDispatch)` element type. Workaround
that keeps the array homogeneous: harvest every piece's **faces** (`x.Faces[igQueryAll]`) and
build ONE `CopySurfaces.Add(typed Face[])` out of them, then stitch/unite that single surface.

- **SAFEARRAY marshaling needs a TYPED-element array, not `object[]`.** `List<object>.ToArray()`
  (→ `object[]`) marshals as `SAFEARRAY(VARIANT)` and the call fails with
  **"não foi possível converter argumento N da chamada em Add"** (a binder error, not
  DISP_E_TYPEMISMATCH). Build a typed array whose element type is the interop COM interface:
  `SolidEdgeGeometry.Face[]` / `Edge[]` / `Body[]` (or `SolidEdgePart.Profile[]`). That marshals
  as `SAFEARRAY(IDispatch)`. Cast element-by-element, but **tolerantly**: casting a live
  `__ComObject` to the coclass can throw `E_NOINTERFACE (0x80004002)` when the object **isn't
  actually that type** (e.g. a `SelectSet` item that's a body/edge, not a Face) — skip those and
  log the count, don't let one bad element abort the array.
- **The `ref`-array-reuse trap.** Reusing the SAME `System.Array` variable across two calls with
  `ref` corrupts the second call (seen as **"converter argumento 0"** — the error even points at
  the wrong arg). **Rebuild a fresh typed array for each call**, and pass `[in]` array params
  **by value** (no `ref`) — only genuine `[in,out]`/`[out]` params need `ref`. (`AddThickenFeature`'s
  `Faces` is `[in]`; `RedefineFaces` marks its arrays `[in,out]`.)
- **A surface→solid pipeline with what actually exists:** create/verify a `CopySurface`
  (`CopySurfaces.Add(typed Face[])`) → **`AddThickenFeature`** the burn faces toward the block
  (overshoot INTO the block so they overlap) → **`Unions.Add(blockBody, thickenedBody)`**. The
  earlier "ExtendSurfaces.Add1 → IntersectSurfaces.AddByAutoTrim → Unions" pipeline **does not
  run** (first two calls don't exist on the live Model). `RedefineFaces`/`ReplaceFaces` (Replace
  Face) is the alternative — but the surface must cover the solid's whole face cross-section.
- **`AddThickenFeature` side is unknown up front** — the surface normal decides which way it
  thickens; try both `Side` values and keep the one whose new body reaches toward the block
  (measure the new body's max Z), deleting the wrong-way body. A thicken that fails mid-op can
  **poison the proxy** (next `Holes.AddSync` → `E_FAIL`) — so isolate it (see the isolation rule at the end of `errors.md`).
- Full `Constructions` set (SE 2023, 43 members) worth knowing: `BlueSurfs, BSplineSurfaces,
  CopyConstructions, CopySurfaces, ExtrudedSurfaces, InterpartConstructions, LoftedSurfaces,
  MidSurfaces, OffsetSurfaces, PartingSurfaces, RevolvedSurfaces, RuledSurfaces, StitchSurfaces,
  SurfaceByBoundaries, SweptSurfaces, UnitedBodies, WrapSketchs`.
- A user **`SelectSet`** of individual faces gives you the faces (enough for a footprint bbox and
  for `CopySurfaces.Add`) — you no longer need the human to do "Surface→Copy" by hand; auto-create
  the CopySurface from the selected faces with the typed `Face[]` array.

**`Model.Holes` — the full adder set (SE 2023 dump).** Ordered: `AddFinite(Profile, side,
depth, Data)`, `AddFromTo`, `AddThroughNext`, `AddThroughAll`. Synchronous: `AddSync(nProfiles,
ProfilesArray, side, ExtentType, depth, Data)`. Multi-body: `AddMultiBody`/`AddSyncMultiBody`.
**Every one has an `…Ex` twin with one extra trailing input, `bPhysicalThread: bool`** —
`AddFiniteEx(Profile, side, depth, Data, bPhysicalThread)`, `AddSyncEx(nProfiles, Profiles,
side, ExtentType, depth, Data, bPhysicalThread)`, etc. That flag, not `HoleData.ThreadSetting`,
is what cuts a real helix; see the threaded-holes section of `modeling-recipes.md`. The
resulting `Hole` also exposes `CreatePhysicalThread` (get **and** put) and
`CreatePhysicalThreadAndReturnStatus(bool, [out] PhysicalThreadErrorCode)`. `Model.Threads` has
the same `Add`/`AddEx(…, bPhysicalThread, …)` pair.

**Application-global options.** `Application.GetGlobalParameter(Parameter
ApplicationGlobalConstants, [in,out] Value VARIANT)` / `SetGlobalParameter(Parameter, Value)`.
The Value is `[in,out]`, so mark it by-ref with a `ParameterModifier` or it reads back empty
(same trap as `GetRange`). Useful members: `seApplicationGlobalEnableThreadedDisplay=30`,
`seApplicationGlobalHoleSizeFile=61` (HOLES.TXT path), `seApplicationGlobalThreadDisplayMode=406`,
`seApplicationGlobalHolesDatabaseFolder=498`. `PartDocument`/`SheetMetalDocument` have their own
`Get/SetGlobalParameter` over `PartGlobalConstants`/`SheetMetalGlobalConstants`.

**Reference plane for a hole at a given depth.**
`RefPlanes.AddParallelByDistance(ParentPlane, Distance, NormalSide, [Pivot], [PivotOrigin],
[Local])`. `Distance` is **always positive** (meters); direction is the `NormalSide`
**side enum `igLeft=1 / igRight=2`** (a `FeaturePropertyConstants` side — not a dedicated
"normal-side" enum). `Local=False` makes a **global named** plane. If the plane comes out on
the wrong side, flip `igLeft`↔`igRight` — that one param is the whole fix.

**TWO different geometry enums — do NOT mix them up** (an earlier version of this skill said
they share values; the dump says otherwise, and the mistake silently breaks face-type checks):

| Use | Enum | Values |
|---|---|---|
| **Indexing** `Body.Faces[…]` / `Feature.Faces[…]` / `Edges[…]` | `FeatureTopologyQueryTypeConstants` | small ints — `igQueryAll=1`, `igQueryPlane=6`, `igCone=7`, `igTorus=8`, `igSphere=9`, `igQueryCylinder=10` |
| **Reading** any object's `.Type` | `GNTTypePropertyConstants` | big magic numbers — topology `igBody=167551091`, `igFace=167551075`, `igEdge=167551093`, `igVertex=167551101`; surfaces `igPlane=-1909484335`, `igCylinder=-114972029`, `igCone=-114972031`, `igSphere=-114972027`, `igTorus=-114972025`, `igBSplineSurface=1465959633`; curves `igCircle=167551105`, `igEllipse=167551107`, `igLine=167551109`, `igBSplineCurve=167551103` |

So `Body.Faces[10]` gets the cylinders, but a cylindrical face's *surface* type reads
`-114972029`, **not** `10`. Comparing against the query constants silently matches nothing.

**`Face.Type` is NOT the surface type — it is always `igFace` (and `Edge.Type` always `igEdge`).**
Confirmed in the field 2026-09-04: a check of `Face.Type == igPlane` rejected *every* face in
existence, and the error message read "type 167551075" — which is `igFace`, i.e. the object
answering "I am a face". `.Type` on a topology object reports the TOPOLOGY kind; the shape lives
one level down:

```csharp
face.Geometry.Type   // igPlane / igCylinder / igCone / igTorus / igBSplineSurface
edge.Geometry.Type   // igCircle / igLine / igEllipse / igBSplineCurve
```

**And once you're holding the geometry object, take the exact numbers from it** instead of
deriving them from a bounding box: `Cylinder.Radius`, `Circle.Radius`, `Torus.MinorRadius` and
`Torus.MajorRadius` are plain properties (no `[out]` marshaling), as are
`Cylinder.GetCylinderData`/`Circle.GetCircleData` if you want the base point and axis too. A bbox
measures the *chord* of a partial cylindrical face and lies about the diameter; the radius never
does. (Bbox is still the right tool for a full circular edge's axis and centre — see below.)

> **Prefer the property over the method whenever both offer the same number.**
> `GetCylinderData([out] BasePoint, [out] AxisVector, [out] Radius)` and
> `GetTorusData([out] …, [out] MajorRadius, [out] MinorRadius)` hand the radius back through an
> `[out]` **scalar**, and `[out]` scalars are exactly what this runtime's late binding does not
> populate reliably (same reason `GetFacetData`'s `FacetCount` has to be ignored in favour of the
> array length — see `SectionAreaCalculator`). A property has no out-marshaling to get wrong.
> Validated on SE 2023, 2026-09-11: a radius sweep over a real cavity read 4/4 curved faces through
> the properties with zero failures.

**`Face.GetParamRange` answers "is this face a full hole/pin, or just a corner?" in one read.**
Signature confirmed on the *live* object (not only the dump):
`GetParamRange([in,out] MinParam: SAFEARRAY(double)*, [in,out] MaxParam: SAFEARRAY(double)*)`,
`cParams=2` — the **same shape** as `Face.GetRange`, so the same by-ref `ParameterModifier` helper
drives it.

> ⚠ **Same shape, different units — do NOT run it through the metres→mm conversion.** The two
> arrays are *surface parameters*, not lengths: on a cylinder one component is an **angle in
> radians**. Feeding that to `Units.MToMm` yields a number 1000× too big without throwing, which is
> precisely the plausible-and-wrong class of bug `Units` exists to prevent. Reuse the raw by-ref
> executor (`FaceGeometry.TryTwoArrayOut`), not the mm one (`TryTwoPointOutMm`, which is right for
> `GetRange`/`GetExactRange`/`Edge.GetEndPoints` because those *are* metres).

Recipe: a cylindrical face whose parameter span is ≈`2π` wraps all the way round, so it is a
**hole or a pin**, not a milled corner. Which of the two components carries the angle is *not*
documented — test both and log which one opened to 2π; the other is the cylinder's axial extent in
metres, which would only reach 2π on a 6,28 m part. This is what lets a machinability check stop
reporting "Ø8 × 25 mm, too deep to mill → EDM" for what is an ordinary drilled hole.

**`Face.ID` is stable within a session, NOT across a rebuild.** It is still the right key to match
a body face against a feature face *in one pass* (above), but it is not a durable handle: on
2026-09-11 the same two R0,277 mm faces came back as IDs 253/218 before an edit and 636/550 after.
Anything that must survive an edit wants `Face.GetReferenceKey([out] ReferenceKey, [opt][out] KeySize)`.

**The live object exposes more than `docs/api/` lists.** Introspecting a real `Face` on SE 2023
printed **42** members, including `GetFacetDataEx`, `GetRenderLineData` and `FeatureIDsAndNames` —
none of which appear in `docs/api/SolidEdgeGeometry.md`. The generated reference is a floor, not a
ceiling: when something you need seems absent, dump the live member list before concluding it
doesn't exist.

**A circular `Edge`'s `GetRange` hands you the whole axis for free.** `Edge.GetRange` /
`GetExactRange` have the same `[in,out] SAFEARRAY(double)` shape as `Face.GetRange` (so the same
by-ref `ParameterModifier` helper works on both). For a **full circular edge** the bounding box
is degenerate along the circle's normal, so one read gives all three things you'd otherwise hunt
for through the curve API: the **axis** is the direction whose box extent is ~0, the **centre**
is the box mid-point, and the **diameter** is either of the other two extents (their being equal
is also your circularity check). No curve-geometry calls, no guessing. Also on `Edge`:
`IsClosed`, `GetFaces([out] n, [in,out] faces)`, `Geometry`.

**`Edge` has NO `Faces` property** — only `GetFaces`. Counting faces per edge (1 = open/laminar,
2 = stitched) is `InvokeMember("GetFaces", args = { 0, new SolidEdgeGeometry.Face[0] })` with both
args by-ref; the result is `args[0]`. An `object[]` placeholder (or `null`) gives
`DISP_E_TYPEMISMATCH` because the out array is `SAFEARRAY(IDispatch)`. `edge.Faces` failed on every
edge for months, so "0 open edges" meant "couldn't read", not "closed" (confirmed live 2026-09-21).

**Face color (`FaceStyle`) — confirmed live 2026-09-21:** the name property is **`StyleName`**
(there is no `Name`: `DISP_E_UNKNOWNNAME`). `Document.FaceStyles.Item("name")` finds a style by
name; a missing name throws HRESULT `0x80040B50`. `FaceStyles.Add(name, "")` + `Diffuse{Red,Green,
Blue}` (0..1) + `Body.SetFacesStyle(n, Face[] by-ref, style)` paints, and `Face.Style.StyleName`
reads it back. Send **one call per body**: faces from different bodies in one call gave `E_FAIL`.
The paint **survives a later `FaceOffsets.AddEx`** (ordered): the offset face keeps the style.

**Ask the profile where its plane actually is — never deduce it from which RefPlane you used.**
`Profile.Convert2DCoordinate(x2d, y2d, [out] x3d, [out] y3d, [out] z3d)` and
`Profile.Convert3DCoordinate(x3d, y3d, z3d, [out] x2d, [out] y2d)` (all `[out]`, so by-ref
`ParameterModifier` as usual). Three calls to `Convert2DCoordinate` — `(0,0)`, `(1,0)`, `(0,1)` —
give the plane's **origin** and its two in-plane unit vectors in 3D; their cross product is the
**normal**. With that frame you can:
- decide whether a plane *contains* a given axis (normal ⟂ axis **and** the axis point's signed
  distance ≈ 0) — and pick or build the sketch plane by testing instead of guessing;
- **verify** an `AddParallelByDistance` landed on the right side, so a wrong `NormalSide` fixes
  itself instead of producing geometry in the wrong place;
- draw a profile from **real 3D part coordinates** (`Convert3DCoordinate` each point) and never
  reason about which local sketch axis is which. This is the single biggest source of
  sign/axis errors in sketch-driven modeling, and it just goes away.
(Units: these take/return METRES like everything else.)

**Cutouts.** `ExtrudedCutouts`: `AddFinite(Profile, ProfileSide, ProfilePlaneSide, Depth)`,
`AddThroughAll`, `AddFromTo`, `AddFiniteMulti(nProfiles, ProfileArray, …)` — and note there is
**no `AddSync*` variant at all** on `ExtrudedCutouts` (unlike `Holes`), so on a synchronous part
you are calling an ordered method; check the body actually changed.
`RevolvedCutouts` **does** have sync: `AddFiniteSync(Profile, RefAxis, ProfileSide,
[ProfilePlaneSide], [AngleofRevolution])` plus the ordered `AddFinite(...)` of the same shape.
`AngleofRevolution` is in **radians** (2π = full turn).

**The `RefAxis` for a revolve comes from the profile, not from a collection.** `RefAxes` has
`Item`/`Count` but **no `Add`**. The way to get one:
```
line    = profile.Lines2d.AddBy2Points(x1,y1,x2,y2)   // along the axis
profile.ToggleConstruction(line)                       // keep it out of the closed contour
refAxis = profile.SetAxisOfRevolution(line)            // -> RefAxis
profile.End(1)
```
(`ToggleConstruction(Element)` and `IsConstructionElement(Element)` are both on `Profile`.)

**Face bounding box, robust order.** `Face.GetRange`/`GetExactRange` ([out] SAFEARRAY,
by-ref) first; if the binder won't populate them, fall back to iterating
`Face.Vertices` and reading `Vertex.GetPointData(out point)` (also [out], meters) to
build the AABB from boundary points. If the object **isn't actually a `Face`** (e.g. a
surface body or edge picked up from a user `SelectSet`), `GetRange`/`GetExactRange` throw
`DISP_E_UNKNOWNNAME (0x80020006)` and `.Vertices` is absent — so guard the read and skip
non-faces (or descend into `item.Faces[igQueryAll]` when the item is a body/surface).

**A per-face bbox loop can silently UNDERSHOOT — even on a genuine Face** (found
2026-07-17, AutoEDM). A curved/blend face threw `DISP_E_UNKNOWNNAME` on **both**
`GetRange` and `GetExactRange`, **and had no `Vertices` property at all** (all 3 fallbacks
failed on that one face). Code that skips failed faces when accumulating a bounding box
(`if (TryGetRangeMm(f, ...)) { merge } ` with a silent `continue` otherwise) then computes a
bbox that's **too small if the skipped face happened to be the extreme one** (e.g. the apex
of a domed/rounded cap) — no exception, no obviously-wrong number, just a plausible-looking
box that's short by however much that face stuck out. **Fix: don't trust the per-face loop
alone — also read the parent BODY's own `GetRange`/`GetExactRange`** (same by-ref SAFEARRAY
shape, works on `Body`/`CopySurface` items too, confirmed live) and **expand** (never shrink)
the per-face box with it. The body-level range doesn't depend on any single face succeeding,
so it catches what individual faces silently drop.

**`GetRange` is a PADDED box on B-spline edges — `GetExactRange` is the tight one** (found
2026-09-16, AutoEDM). They are not interchangeable, and the difference only shows up on
spline geometry. Measured on a BlueSurf lofted between two horizontal splines, same edge,
same call shape:

```
Edge.GetRange       ->  Z -0.0050 .. 0.0050   (Δ = 0.01000 mm)
Edge.GetExactRange  ->  Z  0.0000 .. 0.0000   (Δ = 0.00000 mm)
```

The rim edge is *exactly* planar; `GetRange` inflates it by ~±0.005 mm per side (the pad
tracked the body diagonal, ~0.04% of it, not a fixed epsilon). On **lines and arcs the two
agree**, so code that has only ever seen analytic geometry looks correct for years and then
fails the first time a profile is a spline.

**Consequence: pick the method by what the number is FOR.**
- *Deciding whether something is planar / horizontal / degenerate* — anything compared against
  a tight tolerance: use **`GetExactRange`**. A micron-level test against a padded box
  rejects geometry that is perfectly flat, with no error and a plausible-looking number.
- *Grouping by proximity, sizing stock, reserving clearance* — use `GetRange`; a box that
  only ever errs **larger** is the safe one there, and 10 µm of slack is free.

So keep **two** helpers rather than reordering one. In AutoEDM this is `TryGetRangeMm`
(loose-first, unchanged for the electrode/machinability paths) alongside
`TryGetExactRangeMm` (exact-first, used by the WEDM planarity test). Reordering the shared
one would have silently changed every already-validated feature.

The same warning applies to the circular-edge trick above: the axis is the direction whose
extent is ~0, and "~0" is only really ~0 with `GetExactRange`.

**`Constructions.DerivedCurves.Add` kills the source surface AND its edges** (found 2026-09-16,
AutoEDM). `Add(nEdges, Edge[] typed, CurveType)` works — `igDCComposite=1`, and the resulting
`DerivedCurve` exposes `Name`/`Visible`/`Edges[igQueryAll]` so a later pass can read it back.
But it regenerates the construction tree, and **everything you were holding from the surface it
came from dies with it**: in one run only the *first* Add of the session succeeded, and every
later one returned `E_INVALIDARG (0x80070057)` — even a single-edge call. Re-reading the cached
surface object after that first Add returned edges that matched **nothing**, neither by
`Edge.ID` (SE renumbers them) nor by geometry, so a surface whose edges were read *after* the
first Add produced nonsense contours: one-edge "loops" that never close.

This is the same family as the `RPC_E_DISCONNECTED`/`0x80010114` traps above, but it does not
announce itself — no disconnect error, just an invalid-argument on the *next* call and silently
wrong geometry reads. The pattern that fixes it generalizes to any feature-creating loop:

1. **Read everything first, with the model untouched**, and keep only identities, never COM
   proxies: for a feature, its index in its collection plus its body bounding box and face count;
   for an edge, its `ID` *and* a geometric key (endpoints + exact bbox, rounded).
2. **Then create**, and before *each* `Add` re-acquire the whole chain — document from
   `Application.ActiveDocument` (the Application never disconnects), the feature by scanning its
   collection for the matching fingerprint, the edges by matching identity — falling back from ID
   to geometry, because only the geometry survives a renumber.
3. Log how many edges matched by ID vs by geometry. That number is what tells you the model
   regenerated underneath you, and it is invisible otherwise.

Confirmed on the same part that used to fail: with the read/create split in place, **7 derived
curves in one run, every `Add` accepted, zero `E_INVALIDARG`** — where before it was one curve
and then nothing. Worth applying preemptively to any loop that creates one feature per iteration
off a shared parent.

**A corner radius comes back as `igEllipse`, not `igCircle`** (found 2026-09-16, AutoEDM).
`Edge.Geometry.Type` on the fillet edges of a profile read **167551107 = `igEllipse`**
(`GNTTypePropertyConstants`: `igVertex`=167551101, `igBSplineCurve`=167551103,
`igCircle`=167551105, `igEllipse`=167551107, `igLine`=167551109). An ellipse whose
`MinorMajorRatio` is 1 **is** a circular arc — so a `switch` that only handles `igCircle`
silently degrades every radius to a stroked polyline, while SE's own "Save As IGES" writes
the arcs perfectly. That contrast (our export vs. `SaveAs`) is the symptom to recognize.

The `Ellipse` interface is **not** `Circle`: no `Radius` property. It has `MinorMajorRatio`,
`GetCenterPoint`, `GetAxisVector`, `GetMajorAxis` and `GetEllipseData([out] CenterPoint,
[out] AxisVector, [out] MajorAxis, [out] MinorMajorRatio)`.

**And `MinorMajorRatio` is not a usable test either** — SE reported **0.9999** for the corner
radii of a profile, so `ratio == 1` (even at 1e-6) still stroked every one of them to a
polyline. Treat the declared ratio as commentary, not evidence.

What works is measuring the geometry: take the two endpoints **plus interior samples** along
the edge (`GetPointAtParam` at ¼, ½, ¾ of `GetParamExtents`) and require all of them at the
same distance from the centre, within the tolerance the output actually needs. Don't trust
the length of the `GetMajorAxis` vector either (unverified whether it is a unit vector or the
semi-axis). Endpoints alone are never proof — a real ellipse cut symmetrically has endpoints
equidistant from the centre — and three points are exactly a circle, so sample more than
three or the interior point cannot disagree. Failing the check, fall back to the polyline
instead of emitting a wrong arc, and **log the measured radius spread in mm**: that number
says whether the tolerance is too tight or the curve is genuinely an ellipse, which the
ratio never did.

**Positioning a part by mates (alternative to `PutOrigin`).** When an exact origin isn't
enough, constrain the occurrence: `Ref = AssemblyDocument.CreateReference(Occurrence, Face)`
for each side, then `Relations3d.AddPlanar(Ref1, Ref2, NormalsAligned, cp1[3], cp2[3])`
(**`NormalsAligned`: this line is DISPUTED — see `assembly-relations.md`.** Field evidence
from a 45-relation mold base says the flag reads BACKWARDS, i.e. face-to-face CONTACT =
`true` and FLUSH = `false`, and that the "intuitive" value flipped a plate 180°. A 2026-10-17
re-test could not settle it, because it used perpendicular faces. Verify empirically in your
case before trusting either reading.) And `AddAxial(cylRef1, cylRef2, NormalsAligned)`
for holes/pins. `PutOrigin` stays the simplest path when the placement is a pure translation.

## Ordered-tree GROUPS, feature faces, and the revision property (measured 2026-09-18)

**A "Grupo" in the ordered PathFinder is a first-class COM object, and it is itself a
collection.** It shows up as an item of `PartDocument.DesignEdgebarFeatures`:

| member | value (live, SE 223.00.13.05) |
|---|---|
| `Name` / `DisplayName` / `EdgebarName` | what the user typed — `"Rev.2"` |
| `SystemName` | `"Group_1"` — **this** is how you know an item IS a group, without depending on the name |
| `Count` + `Item(i)` | the features inside it |
| `Faces(FeatureTopologyQueryTypeConstants)`, `ExactRange` | the group's own faces/box |
| `Ungroup()` | **destructive** — never call it from a read-only tool |

Creating a group also creates a variable named `<GroupName>_Suppress` (SystemName
`Group_1_Suppress`) in the variable table.

Do **not** infer group membership from position in `DesignEdgebarFeatures`: the children do
follow the group in the flat list, but a feature created *after* the group is indistinguishable
from one inside it. Ask the group.

**Painting "what changed" on a rendered mesh:** a feature's faces come from
`feature.Faces[1]` (`igQueryAll`, indexed like `Body.Faces[1]`), and **`Face.ID` matches
between the feature's face and the body's face** — build an `ID → feature` map, then tag each
body face while you mesh it. Measured on a real mold plate: 4 of 432 faces for one feature.
Feature types that replace/delete faces may return none — log it instead of failing.

**⚠ The standard revision property is a SAVE COUNTER, not a revision.** Matching a document
property by *part* of its name ("contains rev") picks up **"Número da Revisão"** (Windows
`PIDSI_REVNUMBER`, group Arquivo/Geral), which Solid Edge increments on save — real values in
one assembly were 153, 24, 9, 12. That turned 43 untouched parts into "new parts" and made the
report's revision 153. The field a user actually fills is **"Revisão"** (`"Revision"` in an
en-US install), in the **Resumo** group, next to Título/Categoria. Match the **whole** property
name, case- and accent-insensitively, and keep the accepted names in config — the label is
localized and the COM name may differ from what the properties dialog shows.

**Ribbon layout is cached per add-in: bump `AddInEx.GuiVersion` when the XML changes.** A new
`<button>` added without bumping it appears in the *wrong group* (SE reuses the cached layout
and appends the unknown id) — it is not an XML error, and re-registering alone does not fix it.

## Rounds / Chamfers / in-context part (confirmed live 2026-10-21, AutoEDM O'ring, SE 223, ORDERED)
- **`Model.Rounds.Add(NumberOfEdgeSets, EdgeSetArray, RadiusArray)` works with ONE SET PER EDGE**:
  `Add(edges.Count, object[] edges, double[] radii /*same length, meters*/)` — accepted on the 1st try
  with an untyped `object[]` of Edge proxies via `InvokeMember` (EdgeSetArray is VARIANT, not SAFEARRAY(IDispatch)).
  Status OK, 2 bottom edges of an annular groove rounded R0.25.
- **`Chamfers.AddEqualSetback` right after that Round → `E_FAIL` with every array form** (count N / 1,
  object[] / typed `Edge[]`). Cause under test: the edge proxies were read BEFORE the Round, which
  regenerates the body. Re-read edges from the feature's faces after every feature you add.
- **Part edited in place (in context of an assembly): `Application.ActiveDocument` IS THE PART**
  (`Type == 1`, `PartDocument.InPlaceActivated == True`). But scanning `Application.Documents` for an
  assembly with `InPlaceActivated == True` found NONE — don't rely on the assembly's flag; use
  `PartDocument.GetInContextAssemblyNameForInterpartLinks` (out BSTR) to name it (result not yet seen live).

## Padronizar a vista salva (miniatura do arquivo) — confirmado 2026-10-22, SE 2023 (223.00.13.05) pt-BR, ao vivo

Uso: regravar a miniatura de `.par`/`.asm` de biblioteca (projeto Catalogos, `SolidEdge/PadronizadorVistas.cs`).
Sequência que funcionou em peça e montagem (≈1,3–2 s por arquivo, incluindo Open/Save/Close):

```csharp
dynamic doc = app.Documents.Open(path);           // 1 argumento basta (os outros 5 são [opt])
dynamic view = app.ActiveWindow.View;
doc.CoordinateSystems.Visible = false;            // é ISTO o "triedro" que aparece na miniatura
for (i = 1..doc.RefPlanes.Count) doc.RefPlanes.Item(i).Visible = false;       // peça; montagem: AsmRefPlanes
doc.PMI.Show = false; doc.PMI.ShowDimensions = false; doc.PMI.ShowAnnotations = false; // peça; na montagem PMI → 0x80004003
view.RenderModeType = 11;                         // seRenderModeSmoothBoundary (o padrão lido era 10, SmoothVHL)
view.ApplyNamedView("Iso");  view.Fit();  view.Update();
doc.Save();  doc.Close(false);                    // o Save regrava a miniatura (conferido pelo IShellItemImageFactory)
```

- **`ApplyNamedView` aceita os nomes em INGLÊS mesmo no SE em português**: `"Iso"`, `"iso"`, `"Dimetric"`,
  `"Trimetric"` funcionam; `"Isométrica"`, `"Isometric"`, `"Dimétrica"`, `"Trimétrica"` → `E_INVALIDARG 0x80070057`.
  (Exceção à regra geral de BSTR localizados de `localization.md`.)
- Montagem: `doc.RefPlanes` não existe (`0x80131500` RuntimeBinder); use `AsmRefPlanes`.

## Reading assembly links WITHOUT Solid Edge — Revision Manager (measured 2026-10-23, SE 2023)

To find which sub-assemblies/parts hundreds of assemblies reference (here: which mold base each of 253
molds uses), do not open them in SE. `RevisionManager.Application` (registered with SE; runs as its own
`DesMgr.exe`) reads the link table of a file:

```powershell
$rm  = New-Object -ComObject RevisionManager.Application
$doc = $rm.Open($path)                                   # IRMgrApp.Open -> IDocAuto
$ls  = [System.__ComObject].InvokeMember("LinkedDocuments",
         [System.Reflection.BindingFlags]::GetProperty, $null, $doc, @(0))   # seLinkTypeAll = 0
# $ls.Count / $ls.Item(i).FullName  (also via InvokeMember)
```

- **`LinkedDocuments` is a PARAMETERIZED PROPERTY**, not a method. Calling it with `InvokeMethod` (and/or
  type `1` = seLinkTypeNormal) returns an object where `Count` is "not found" — every file then looks like a
  failure. `GetProperty` with `@(0)` works for every path, including the network ones with `[brackets]`
  (the brackets were a red herring).
- 522 assemblies of a 253-folder mold tree scanned in a few minutes; 3 failures were real missing files
  (`STG_E_FILENOTFOUND`). Paths come back as UNC (`\\server\share\...`) even when opened by drive letter.
- One `DesMgr.exe` per `New-Object`; kill the ones you started when done.

---

## Physical properties and occurrence moves (SE 2023 v223, 2026-10-29)

- `Model.ComputePhysicalPropertiesWithSpecifiedDensity(density kg/m³, accuracy, ref Volume,
  ref Area, ref Mass, ref CoG, ref CoV, ref GlobalMoments, ref PrincipalMoments, ref PrincipalAxes,
  ref RadiiOfGyration, ref RelativeAccuracy, ref Status)` with `dynamic`: scalars must be typed
  `double`/`int` (`object` → "Could not convert argument 10"), and the arrays must arrive
  **allocated and typed** — `double[3]` ×2, `double[6]`, `double[3]`, `double[9]`, `double[3]`
  (`object` → "Could not convert argument 7"). Mass in kg; `part.Save()` keeps it.
- `Occurrence.Move(dx, dy, dz)` from PowerShell 5.1 late binding → **E_FAIL**; `PutTransform` with
  the angles from `GetTransform` (radians) does both rotation and translation. Since rotation is
  about the occurrence origin, rotate first, re-read `GetRangeBox`, then translate the origin by the
  bbox-centre delta.

## Scale body — `Model.ScaleBodyFeatures.Add` (confirmed live 2026-10-28, SE 2023 v223.00.13.05)

Live `ITypeInfo` of the collection (TKIND_DISPATCH; the object does **not** answer QI for the dual
`_IScaleBodyFeaturesAuto`, so early binding through the interop throws `TargetException`/`InvalidCast`):

```
Add(VT_I4 NumberOfBodies, [in] SAFEARRAY(VT_DISPATCH)* BodyArray,
    VT_R8 dXValue, VT_R8 dYValue, VT_R8 dZValue, [in,optional] VT_DISPATCH RefKeyPoint)
```

- **The optional `RefKeyPoint` is `VT_DISPATCH`, not VARIANT.** Omitting it sends "param not found"
  (`VT_ERROR`), which SE cannot coerce: `InvokeMember` → `DISP_E_TYPEMISMATCH (0x80020005)`; `dynamic`
  → `E_INVALIDARG "Could not convert argument 0"` — the binder counts arguments **from the end**, so
  "argument 0" was the omitted last one. Pass `new DispatchWrapper(null)` explicitly.
- `BodyArray`: typed `SolidEdge.Geometry.Interop.Body[]` (interop loaded from the running `Edge.exe`
  folder), passed **by reference** (`ParameterModifier[1] = true`).
- Factors are plain multipliers (1.006 = +0.6%), about the part origin when the key point is null.
- Measured on an ordered part whose body is a part copy: 1.01/1.02/1.03 → exact range ratios
  1.01000/1.02000/1.03000, `Status` = `igFeatureOK`, `get_NonUniformScale` reads them back,
  `Name` is writable and shows in the PathFinder (`EdgebarName`). `Delete()` removes it cleanly.

```csharp
object[] args = [1, bodies /* Body[] */, fx, fy, fz, new DispatchWrapper(null)];
var byRef = new ParameterModifier(6); byRef[1] = true;
object feature = coll.GetType().InvokeMember("Add", BindingFlags.InvokeMethod, null, coll, args,
    [byRef], CultureInfo.InvariantCulture, null);
```

## Multi-body in an ordered part — separate design bodies (confirmed live 2026-10-28, SE 2023)

Goal: add solids to a part that already has a body WITHOUT uniting them (gate/runner bodies).

- **`Models.AddBody(igPartType=1, name)` then a BASE extrusion `Models.AddFiniteExtrudedProtrusion`**
  → a new `Model` (Models.Count +1), the existing body untouched (volume identical). `AddBody` alone
  shows nothing; **without it the base extrusion creates NOTHING** (no body, no union, no error).
  Re-using an `AddBody` name while that body still exists fails `0x807B003F`; after its extrusion is
  deleted the name is free again.
- **`Model.Name` is shared by all bodies of the part** (every body reads "Design Model"; renaming
  the new one renamed the part's own body). Never identify or rename bodies through it.
- **`Model.ExtrudedProtrusions` is the WHOLE PART's list, not the body's** — every Model returns
  the same items. `Item(1)` of the new body is the oldest extrusion of the part. To get the new
  feature, diff the feature names before/after the `Add`; identify your bodies by the
  **feature's `Name`** (unique, writable). Deleting the extrusion deletes its body.
- Collect delete targets first: deleting an extrusion shrinks `Models` mid-loop (`DISP_E_BADINDEX`).
- **`RefPlanes.AddParallelByDistance(...)` (3 args) makes a LOCAL plane**: `Name` reads null and
  cannot be used to find it; it is deleted together with the `ProfileSet` that uses it. A plane made
  on the wrong side and then `Delete()`d may silently stay — pick `NormalSide` right the first time
  (`Sign(baseNormal[axis]) == Sign(offset)` → igRight) and keep the `GetRootPoint` check as a guard.
- `RefPlane.GetNormal` / `GetRootPoint`: one `[in,out]` `double[3]` (allocated, by-ref modifier).
- Draw the profile with `Profile.Convert3DCoordinate` from part coordinates; choose the extrusion
  side from the profile frame (`Convert2DCoordinate` (0,0),(1,0),(0,1) → normal) — measured exact
  (gate 67.4 mm³, runner 377.0 mm³, bounding boxes where designed).

## Undo, "preview", and what silently EMPTIES the user's undo stack (measured live 2026-10-29, SE 2023 v223.00.13.05)

Measured by AutoEDM `tools/UndoProbe` on throwaway parts. The API has **no** equivalent of the
native commands' dynamic feature preview (`Mouse.DynamicsMode` only rubber-bands a line, circle or
rectangle). A "preview" therefore means building the feature for real and taking it back. Watch the
stack with `PartDocument.UndoTransactionCount()` (method, 0 args).

| Operation (part document) | Undo stack |
|---|---|
| Feature `Add` in ORDERED (extruded protrusion) | +1 per feature |
| Feature `Add` in SYNCHRONOUS | +3 per feature |
| Rename a feature / paint a face style | +1 to +2 / +1 |
| `ProfileSets.Add`, `Profiles.Add`, `RefPlanes.AddParallelByDistance` | 0 (not undo steps) |
| `ProfileSet.Delete()` of a sketch a feature CONSUMED | no harm |
| **`ProfileSet.Delete()` of a sketch NO feature consumed** (a probe sketch) | **→ 0**, at once or at the next `RefPlanes.Add…` |
| **`ISECommandEx.BeginUndoTransaction` … `ISECommandEx2.EndUndoTransaction`** | **→ 0**, and stays 0 |

**The probe-sketch trap.** Minimal repro in ORDERED: `ps = ProfileSets.Add(); ps.Profiles.Add(RefPlanes.Item(1)); ps.Delete();`
then `RefPlanes.AddParallelByDistance(face, …)` → stack 6 → 6 → **0**. Each call alone is harmless.
After that, neither `PartDocument.Undo` nor the UI's Ctrl+Z (`StartCommand(57643)` = `PartEditUndo`)
can go back past it — the user loses the whole history. The AutoEDM O'ring did exactly this on every
groove: it opened a sketch on a trial parallel plane to learn where the plane landed, deleted it when
the plane was on the wrong side, then created the other side. **Locate planes from geometry instead:**
`RefPlane.GetRootPoint` / `GetNormal` (one `[in,out]` `double[3]`, meters, allocated + by-ref
modifier). On a new part, `RefPlanes.Item(1..3)` read normals (0,0,1), (1,0,0), (0,−1,0) at the
origin; a plane parallel to the top face (Z 30) at 5 mm reads Z 35 on side 2 and Z 25 on side 1. The
sign of `GetNormal` may differ from the sketch frame's U×V — compare distances in absolute value or
test both sides. Only open the sketch on the plane that passed. (AutoEDM: `ProfilePlaneFrame.OfRefPlane`.)

**Transactions — do not use.** `ISECommandEx2` (with `EndUndoTransaction`) is missing from
Interop.SolidEdge 219; declared by hand it works: IID `{7c4aeeeb-93b0-48f4-b81d-07713327a293}`,
`InterfaceIsIUnknown`, 9 placeholder slots for `ISECommand` (memid 0x6000xxxx order: Mouse, Window,
Done get/put, OnEditOwnerChange get/put, OnEnvironmentChange get/put, Start), then
`BeginUndoTransaction(BSTR)`, `BeginTransparentUndoTransaction(BSTR)`, `EndUndoTransaction()`.
The `Command` comes from `Application.CreateCommand(seNoDeactivate = 2)` + `Start()`. Both begin
variants, with or without `Done = true` after the end, left the stack at 0 and the features
un-undoable.

**What does work, for a Cancel/Preview button:**

- `PartDocument.Undo(object NumTransactions)` with `n = UndoTransactionCount() after − before`:
  back to the identical feature list and volume. 2 protrusions: 40–90 ms. O'ring groove (cut +
  round + chamfer, ordered) = 7 steps, `Undo(7)` ≈ 50 ms, three cycles in a row.
  `AssemblyDocument` has **no** `Undo` method (only `UndoSteps`).
- Deleting the new features by name: 25–110 ms (≈ 450 ms on a 64-feature part), but it adds steps —
  a later Ctrl+Z brings the "cancelled" preview back. Fallback only.
- Cost of one O'ring preview: cut 0.5–0.7 s on a light part, 1.1–1.4 s with 64 features.
- **Every face proxy read before a cut or an undo is dead afterwards** (`E_FAIL`). Re-read the
  target each cycle (10–15 ms light, ≈ 740 ms with 64 features). **`Face.ID` and `Edge.ID` survive
  cut + undo** (same IDs found twice in a row): store IDs, re-find by walking `Body.Faces[1]` (26–63 ms).
- **Part edited IN PLACE inside an assembly (live 2026-10-30):** after the O'ring cut the part's
  `UndoTransactionCount()` read 0 (before 0 or 1, after 0) — there is nothing to `Undo(n)`. Detect
  `steps <= 0` and fall back to deleting the new features (diff the `DesignEdgebarFeatures` names before
  and after, delete newest first; refuse if the list changed meanwhile). On the same part opened alone,
  a 9-hole cooling preview was 30 steps and `Undo(30)` took 153 ms.

**Ordered-mode reference planes made by the API:** they are not undo steps, `Delete()` on them throws
`E_INVALIDARG` ("O valor não recai no intervalo esperado"), and they disappear by themselves when the
next feature is computed (RefPlanes 9 → 5 after one protrusion). Discard an unused one with
`Visible = false`.

**New part made ORDERED before its first feature:** `Models.Item(1).Body` → `E_FAIL` after an API
protrusion (volume and faces unreadable). On a throwaway part, build the body in synchronous and then
switch to ordered.
