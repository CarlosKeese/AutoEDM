# Confirmed API facts and signatures (SE 2023, v223.00.13.05)

Verify every numeric value against the dump for your own version before trusting it.

## Contents
- Confirmed facts: connecting, face color, face traversal, bbox, occurrence transforms, in-place editing
- Surfaces: `Constructions` vs `Model`
- What the live Model actually exposes (163 members)
- SAFEARRAY marshaling
- Reference planes, topology-query enums, robust bounding box, assembly mates

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
     `[out] double` (0..1); mark them **by-ref with a `ParameterModifier`** or they return
     `0,0,0,0` (same out-param trap as `GetRange`). This is the base color, NOT feature paint.
  Priority to resolve a face's color: per-face Style → feature `GetStyle` → `GetRGBAVals`.
  (`Occurrence.GetFaceStyle2(vbHonourPrefs: bool)` takes a bool, not a face — not usable here.)
- **Face traversal**: `PartDocument.Models → Model.Body → Body.Faces[queryType] → Face`;
  **query type 1 = igQueryAll** on SE 2023.
- **Face bounding box**: `Face.GetRange(MinRangePoint, MaxRangePoint)` (also
  `GetExactRange`) — both corners are `[out]` SAFEARRAY of 3 doubles (meters). In late
  binding, mark the args by-ref with a `ParameterModifier` or they come back empty.
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

**What the live SE-2023 Model (163 members) ACTUALLY exposes** for surface→solid / boolean
(DIAG-confirmed, with the `.Add` signatures the probe read live):

| Op | Real call |
|---|---|
| Thicken a surface's faces into a solid | `partDoc.Models.AddThickenFeature(Side FeaturePropertyConstants, offset double, nFaces int, Faces SAFEARRAY(IDispatch)) → Model` |
| Boolean unite bodies (**Ordered-only in practice — see caveat**) | `Model.Unions.Add(nTargets, TargetArray SAFEARRAY(IDispatch), nTools, ToolsArray SAFEARRAY(IDispatch), SETargetDesignBodyOption, SETargetConstructionBodyOption)` (igCreateSingleDesignBodyOnNonManifold=2, igCreateSingleConstructionGeneralBody=1; both enums also have a `0` = igCreateMultiple…OnNonManifoldOption — safer default, doesn't fail the op on a non-manifold result). **`Unions` IS in the stale PIA** (unlike `FaceOffsets.AddEx`, below) — `(SolidEdgePart.Unions)model.Unions` compiles, and tlbimp generated its SAFEARRAY params as `ref Array` (not a plain array) — pass `ref targets, ref tools`. **Array element type is NOT one-size-fits-all — cast each side to what it actually IS, not to a generic `Body`:** a solid Model's target casts fine to `SolidEdgeGeometry.Body[]` (`model.Body`), but a **`CopySurface` tool does NOT implement `SolidEdgeGeometry.Body`** — casting it throws `E_NOINTERFACE` on IID `{09FCA073-DFBF-11D0-A275-080036C5ED02}` (confirmed live, 2026-07-17). A `CopySurface` is already a valid `IDispatch`; type its array element as `SolidEdgePart.CopySurface[]` instead (`new SolidEdgePart.CopySurface[] { (SolidEdgePart.CopySurface)surf }`) — `Unions.Add`'s `SAFEARRAY(IDispatch)` doesn't care that target/tool are different concrete interop types, it just needs each element to genuinely implement *some* real COM interface, not `object`. **CAVEAT (2026-07-20, 2 more real tests, correctly-typed target AND tool):** for a raw/un-stitched surface merging into a solid, `Unions.Add` reliably threw `E_FAIL` when the document was actually in SYNCHRONOUS mode — for that specific surface→solid "attach" use case, use `Model.Attach` or `Model.BooleanFeatures.Add` (next rows), NOT `Unions.Add`. `Unions.Add` may still be the right call for solid+solid merges (e.g. two design bodies) — not re-tested for that case. |
| **Attach a surface directly onto a solid ("anexar"), SYNCHRONOUS, no tree feature created** | `Model.Attach(NumOfObjects: int, psaObjects: SAFEARRAY(IDispatch)*, bAdd: bool, fpcSide: FeaturePropertyConstants) -> void`. Confirmed live 2026-07-20 (AutoEDM). Returns `void` — no new feature registers anywhere; the only trace is the SOURCE `CopySurface`/`StitchSurface` migrating into `Model.Features` (previously only in `Constructions.CopySurfaces`). `fpcSide` is **required** (no `[opt]` in the dump) — try `igRight=2` then `igLeft=1`. Tool array must be typed (`SolidEdgePart.CopySurface[]`/`StitchSurface[]`), not `object[]`. **Must run in Sync mode** — same silent-no-op-outside-native-mode risk as `AddThickenFeature` if the doc is already Ordered from a previous step. |
| Boolean unite, SYNCHRONOUS-capable alternative to `Unions` | `Model.BooleanFeatures.Add(NumberOfTools: int, Tools: VARIANT, Function: BooleanFeatureConstants, [opt]PlaneSide: VARIANT) -> BooleanFeature`. No explicit Target param (implicit = the Model's own body). `Function=3` = `seBooleanUnite` (`1`=Intersect, `2`=Subtract, `4`=PlaneFront — `docs/api/constants.md`). Less field-tested than `Attach` (found in the same 2026-07-20 sondagem, not yet the primary path in AutoEDM). **Fetch the collection via `Type.InvokeMember("BooleanFeatures", BindingFlags.GetProperty, ...)`, not dynamic property access** — `model.BooleanFeatures` threw `E_NOINTERFACE` even though the collection genuinely exists live (PIA-version mismatch, see error table). |
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
deriving them from a bounding box: `Cylinder.Radius` and `Circle.Radius` are plain properties (no
`[out]` marshaling), as are `Cylinder.GetCylinderData`/`Circle.GetCircleData` if you want the base
point and axis too. A bbox measures the *chord* of a partial cylindrical face and lies about the
diameter; the radius never does. (Bbox is still the right tool for a full circular edge's axis and
centre — see below.)

**A circular `Edge`'s `GetRange` hands you the whole axis for free.** `Edge.GetRange` /
`GetExactRange` have the same `[in,out] SAFEARRAY(double)` shape as `Face.GetRange` (so the same
by-ref `ParameterModifier` helper works on both). For a **full circular edge** the bounding box
is degenerate along the circle's normal, so one read gives all three things you'd otherwise hunt
for through the curve API: the **axis** is the direction whose box extent is ~0, the **centre**
is the box mid-point, and the **diameter** is either of the other two extents (their being equal
is also your circularity check). No curve-geometry calls, no guessing. Also on `Edge`:
`IsClosed`, `GetFaces([out] n, [in,out] faces)`, `Geometry`.

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

**Positioning a part by mates (alternative to `PutOrigin`).** When an exact origin isn't
enough, constrain the occurrence: `Ref = AssemblyDocument.CreateReference(Occurrence, Face)`
for each side, then `Relations3d.AddPlanar(Ref1, Ref2, NormalsAligned, cp1[3], cp2[3])`
(**`NormalsAligned` True=Align / False=Mate**) and `AddAxial(cylRef1, cylRef2, NormalsAligned)`
for holes/pins. `PutOrigin` stays the simplest path when the placement is a pure translation.
