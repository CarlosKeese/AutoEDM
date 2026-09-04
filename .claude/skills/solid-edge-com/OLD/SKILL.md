---
name: solid-edge-com
description: >
  Driving Solid Edge (2023/2026) via COM Automation from C#/.NET when the SDK/type
  libraries are NOT installed or downloadable — discovering the API at runtime by
  introspection instead of guessing. Use for any task that automates Solid Edge:
  reading faces/colors/geometry, creating parts/features (AddBox/AddCylinder/
  AddExtrudedProtrusion), Inter-Part Copy / CopySurfaces / StitchSurfaces /
  OffsetSurfaces / AddThickenFeature (surface→solid), editing
  in-context of an assembly, reading occurrence transforms, or debugging COM errors
  like RPC_E_CALL_REJECTED, RuntimeBinder, or out-parameter marshaling. Triggers on:
  "Solid Edge", "SolidEdge.Application", COM automation of CAD, SolidEdgePart/
  SolidEdgeGeometry/SolidEdgeAssembly, AddBoxByTwoPoints, GetRange on a face,
  face Style/Diffuse color, igQueryAll, EDM/electrode automation, sketch+extrude
  solid modeling (ProfileSets/AddFiniteExtrudedProtrusion), in-place editing /
  ModelingInAssembly, ribbon add-in buttons and icons (Win32 RT_BITMAP resource),
  hole features (HoleData / Holes.AddSync / Profile.Holes2d), threaded/tapped holes
  (Threads.Add is a separate feature), checking feature.Status (igFeatureOK) after Add,
  RefPlanes.AddParallelByDistance, assembly mates (CreateReference / Relations3d.AddPlanar),
  synchronous vs ordered modeling (ModelingMode), reflecting Interop.SolidEdge for
  signatures/enum values (but the live object's IDispatch is the TRUTH — reflection lists
  interop TYPES the live Model may NOT expose, e.g. ExtendSurfaces/IntersectSurfaces do NOT
  exist on the live Model; the real surface→solid path is Models.AddThickenFeature + Model.Unions),
  RPC_E_DISCONNECTED (0x80010108) after Holes.AddSync and re-acquiring ActiveDocument, SAFEARRAY
  marshaling with TYPED element arrays (Face[]/Edge[]/Body[], not object[]) and the ref-array-reuse
  trap, threaded holes via HoleData.ThreadDataByDescription (a WRITE-ONLY property, assign don't
  call) + ThreadSetting=igRegularThread to render, the add-in DEPLOY-folder trap (SE loads from
  %LOCALAPPDATA%\...\addin, re-run the register tool after EVERY rebuild), isolating COM-proxy-
  poisoning ops from the working deliverable, live COM object dumping (DumpObject / Solid Edge Spy
  style), named-ValueTuple loses names when returned through a dynamic call, attaching/uniting a
  surface onto a solid body (Model.Attach — synchronous, void, no tree feature — vs
  Model.BooleanFeatures.Add vs the Ordered-only Model.Unions/Subtracts/Intersects family),
  DISP_E_TYPEMISMATCH from a bare `null` IDispatch argument through Type.InvokeMember (needs
  System.Runtime.InteropServices.DispatchWrapper(null)), DISP_E_PARAMNOTOPTIONAL from Type.Missing
  on a param that isn't actually [opt] in the dump, E_NOINTERFACE reading a Model sub-collection via
  dynamic property access when the static PIA is older than the running SE (fetch via
  Type.InvokeMember(name, BindingFlags.GetProperty, ...) instead), a manual-action recorder ([REC])
  showing "nothing changed" for a real void-returning action (look for an existing item migrating
  between collections, not just new names).
---

# Solid Edge COM automation (no SDK on the machine)

The Siemens SE SDK docs require login and the type libraries aren't downloadable.
But SE registers its COM type libraries locally — so **discover the API at runtime
by introspection, never guess signatures from memory.** This skill encodes the
method and the errors it predicts.

## The one-time move: dump the whole type library ("SDK offline")

Before iterating method-by-method, dump the entire type library once. `ITypeInfo`
(from any live IDispatch object) → `GetContainingTypeLib` → `ITypeLib` → enumerate
every coclass/interface/enum with param names and enum **values**. Seed with one
object per module to cover the whole SDK (dedup by lib GUID):

| Module | Typelib | Seed object |
|---|---|---|
| Application/framework | SolidEdgeFramework | `Application` |
| Assembly | SolidEdgeAssembly | the active `AssemblyDocument` |
| Part/features | SolidEdgePart | a `PartDocument`, its `.Models`, `.Constructions`, `.RefPlanes` |
| **Geometry** (Body/Face/Edge — where `GetRange` lives) | SolidEdgeGeometry | a live `Face`/`Edge` (from any body, e.g. a test box) |

Reference impl in AutoEDM: `ComDiagnostics.DumpTypeLibraries(outPath, seeds)` +
`ModelingProbe.DumpSdk`. Output is a `.txt` — **grep it, never load it wholesale
into context.** This is what replaces the expensive one-method-per-run discovery loop.

**(2026-07-17) `DumpTypeLibraries` is now CUMULATIVE, not one-shot-overwrite.** If `outPath`
already exists, it reads the lib GUIDs already dumped (from a `[guid]` marker in each section's
header) and **appends only the new ones** — reruns from different seeds (or different sessions)
never lose libs already captured. This is also wired transparently into the live-object dump:
every `DumpObject`/"Inspect selection" click harvests the typelib of every object it touches into
`%LOCALAPPDATA%\AutoEDM\logs\SE_API_dump_<version>.txt` (see `HarvestTypeLibs`/`ResolveDumpPath`).
Practical effect: `SolidEdgeGeometry`/`SolidEdgeAssembly` — which the one-shot `ModelingProbe`
seeds never reliably reached — fill in naturally the moment the human selects any live Face,
Edge, or Occurrence and clicks "Inspect selection." No need to re-run a big seeded probe once
this is accumulating. `tools/generate_api_docs.py` auto-discovers the newest
`SE_API_dump_*.txt` between `%LOCALAPPDATA%\AutoEDM\logs` and the old `bin/Debug` location (or
takes an explicit path as `argv[1]`).

Emit **parameter types and direction**, not just names — read each param's
`ELEMDESC` (`FUNCDESC.lprgelemdescParam`) for the `TYPEDESC` (resolve `VT_PTR` → `*`,
`VT_SAFEARRAY` → `SAFEARRAY(...)`, `VT_USERDEFINED` via `GetRefTypeInfo`) and
`wParamFlags` for `[out]`/`[in,out]`/`[opt]`, plus the return type. Types are what
actually predict marshaling errors (e.g. seeing `GetRange([out] p: SAFEARRAY(double)*)`
tells you immediately it needs by-ref out-param handling). Enum **values** matter too
(constants like `igLeftExtent`) — dump `VARDESC.VAR_CONST` values.

For a single method's signature (when you don't want the full dump), use the
lighter `ComDiagnostics.LogSignatures(obj, "MethodName", ...)` — reads
name + param names + `cParams` via ITypeInfo.

**When the runtime dump is incomplete, reflect `Interop.SolidEdge`.** The typed typelib
dump can crash mid-walk (later libs uncovered — e.g. hole/feature methods, some enums). The
`Interop.SolidEdge` / `SolidEdge.Community` NuGet ships the **full typed signatures and enum
values** — load the DLL and reflect, no SE run needed:
`asm.GetType("SolidEdgePart.Holes").GetMethods()` gave the exact `AddSync(...)`/`AddFinite(...)`
params; `[Enum]::GetValues(GetType("SolidEdgePart.FeaturePropertyConstants"))` gave
`igRegularHole=33`, `igFinite=13`, etc. A separately-authored **type catalog** (names +
descriptions of every class, e.g. from the Programmer's Guide) is a useful breadth map but
has **no signatures/enum values** — reflect the interop or grep the dump for those.

**Authored reference docs (AutoEDM project)** carry more than a bare catalog — treat them as a
**cross-check, still second to the live dump**: `docs/SolidEdge_API_COM_Referencia_Completa.md`
(class map incl. `Model.Threads/Rounds/Chamfers/Drafts…` treatment features) and
`docs/SolidEdge_API_COM_Assinaturas_Enums_Fluxos.md` (method **signatures** with
`[in]/[out]/[opt]`, **enum numeric values**, HRESULT table, and step-by-step flows for
extrude / in-context electrode / assembly relations / face-query). They sometimes disagree
with the real typelib (e.g. list `HoleDataCollection.Add` as 7 params when the SE 2023 dump
shows more) — when they do, **the dump wins**.

## Non-negotiable constraints (predict these errors before they happen)

- **Units are METERS** in the geometry/modeling API. 20 mm = `0.020`. Convert
  ranges ×1000 for mm.
- **Collections are 1-based** (`.Item(1)`, `.Count`).
- **x64 only** — SE 2023/2026 are 64-bit; a 32-bit build won't connect.
- **STA thread required** — run automation on `[STAThread]` (a WinForms UI thread
  is STA; a plain worker thread is not — marshal or create an STA thread).
- **Late binding (`dynamic`)** so the code compiles without the type libraries.
  Cast to `object` before calling your own/extension methods on a `dynamic` result
  to force static binding.
- **Install an OLE message filter** — SE is a busy server; without a filter, calls
  throw `RPC_E_CALL_REJECTED (0x80010001)` or, while SE recomputes/shows a modal,
  `RPC_E_SERVERCALL_RETRYLATER (0x8001010A)`. The filter must retry on **both**.
  (`OleMessageFilter.Register()`.) Consider a bounded retry/timeout so a modal error
  dialog in SE doesn't hang the automation forever.
- **Connect to the running instance** via the Running Object Table
  (`ComInterop.GetActiveObject("SolidEdge.Application")`) — `Marshal.GetActiveObject`
  is gone in modern .NET, so P/Invoke `ole32`/`oleaut32`.

## Error → cause → fix (seen in the field)

| Symptom | Cause | Fix |
|---|---|---|
| `RuntimeBinder: '...' não contém definição para 'ToList'` | LINQ on a `dynamic` COM result | Assign to a typed variable first, then LINQ; cast `(object)` to force static binding |
| `CS8183: cannot infer type of implicit discard` on `Method(dynObj, …, out _)` | With **dynamic** args the call is late-bound, so the compiler can't type the `out _` discard | Use an explicitly-typed throwaway: `SomeType ignore; Method(dynObj, …, out ignore);` — dynamic dispatch supports `out`, just not the implicit `_` discard |
| Call throws `RPC_E_CALL_REJECTED` | SE busy | Register the OLE message filter |
| `out`/`ref` params come back empty (e.g. `Face.GetRange`) | `InvokeMember(..., new object[]{null,null})` doesn't populate `[out] SAFEARRAY` in late binding | Pre-fill the args array, or pass `ParameterModifier` marking `[out]`, or check the real signature in the dump — the method/param shape may differ |
| Method call fails with wrong arg count | Guessed arity | Read `cParams` from the dump; SE methods often have many optional trailing params |
| `cParams=0` on a real method (e.g. `CopySurfaces`) | Operates on the current selection / all-VARIANT-optional | Set up a selection first, or introspect again with a live target |
| A `Faces` (or similar) member returns nothing / `.Count` throws | It's **indexed by a topology query-type enum**, not a bare collection — true for `Body.Faces` AND `Feature.Faces` | Access `X.Faces[1]` (`igQueryAll`), then `.Count`/`.Item(k)`; if unsure, try `[1]` then the bare property |
| Enum constant is wrong across versions | Enum values are version-specific | Read the value from the dump (e.g. face query type / `igQueryAll`), don't hardcode from another version |
| `DISP_E_TYPEMISMATCH` passing an **array of COM objects** (`Face[]`, `Profile[]`) | `object[]` marshals as `SAFEARRAY(VARIANT)`; the method wants `SAFEARRAY(IDispatch)` | Build a **typed interop array** (`SolidEdgeGeometry.Face[]`, `SolidEdgePart.Profile[]`) — reference `Interop.SolidEdge` just for the array; keep the rest late-bound |
| `E_FAIL (0x80004005)` on an inter-part / in-context modeling call | You are NOT actually in in-place edit (see in-place fact below) | Confirm `AssemblyDocument.ModelingInAssembly`; if false, the operation can't work — switch to a workaround that needs no in-place edit |
| `DISP_E_TYPEMISMATCH (0x80020005)` passing a bare C# `null` for an **IDispatch-typed (not VARIANT) parameter** through `Type.InvokeMember`'s `object[]` args (e.g. `FaceOffsets.AddEx`'s `ToReferenceEntity`/`ToKeyPoint`) | A raw `null` element in that `object[]` marshals to `VT_EMPTY`, not `VT_DISPATCH` with a null pointer — the callee's typed `IDispatch*` param rejects `VT_EMPTY` as the wrong VARTYPE. This is invisible from the signature dump (it just says `ToReferenceEntity: IDispatch`, no hint about `null` handling) | Wrap it: `new System.Runtime.InteropServices.DispatchWrapper(null)` instead of `null` — forces `VT_DISPATCH` with a null pointer, which the param actually expects for "no reference chosen" |
| `E_NOINTERFACE (0x80004002)` reading a **Model/Document sub-collection via dynamic property access** (`blockModel.SomeNewishCollection`) even though a live SPY/`ProbeSub` dump proves the collection genuinely exists on the object | `dynamic` member access on a COM object whose CLSID/IID is ALSO known to the referenced static PIA (`Interop.SolidEdge`) can resolve through that PIA's (older/narrower) interface definition instead of falling back to pure late-bound IDispatch — if the PIA's version of the interface predates that property, the QueryInterface-style resolution fails even though the live object's real IDispatch has it | Fetch the property via `Type.InvokeMember(name, BindingFlags.GetProperty, null, owner, null)` on the raw `object`, not `owner.PropertyName` — this is the SAME code path `ComDiagnostics`/`ProbeSub` use for its dumps, which is exactly why the SPY sees members that a dynamic access on the same object can't reach |
| `DISP_E_PARAMNOTOPTIONAL (0x8002000F)` calling a method found only via live SPY (not in the static PIA) with `Type.Missing` for a trailing arg that "looked optional" | The signature dump is authoritative about `[opt]` — a parameter with NO `[opt]` marker in the dump (e.g. `Model.Attach`'s `fpcSide: FeaturePropertyConstants`, unlike `Model.BooleanFeatures.Add`'s `[opt]PlaneSide`) is REQUIRED even if semantically it feels like it should default to something | Re-check the dumped signature text specifically for `[opt]` on that exact param before assuming `Type.Missing` is safe; if absent, supply a real value (try the small known enum set for that param family, e.g. `igRight=2`/`igLeft=1` for a "which side" `FeaturePropertyConstants`) |
| A feature `Add` **returns without throwing** but the geometry is wrong/absent | SE does **not** raise a COM error for a failed feature — it sets `.Status` | After every `Add`, check `feature.Status == igFeatureOK (1216476310 / 0x4877F5D6)`; `igFeatureFailed = 1216476311`. Compare as **uint32**. try/catch alone misses these |
| `E_INVALID_MODELING_MODE` / `CO_E_NOT_SUPPORTED (0x80004021)` | Called a method for the wrong modeling mode (ordered method on a sync part, etc.) | Branch on `PartDocument.ModelingMode` (sync=1/ordered=2); use the matching `AddSync*` vs `AddFinite*` |
| `0x80010114` ("object does not exist") on the call **right after a feature `Add`** (`AddSync`/`AddFinite`/extrude) — even when that Add **succeeded** | After a feature Add the model **regenerates** and the part proxy / child collections (`Models`, `HoleDataCollection`, `Holes`) briefly disconnect; the next COM call on a cached ref fails and can cascade past your per-item catch. (An `E_FAIL` mid-op poisons the proxy the same way — it's not only failures.) | Create dependent objects (e.g. all `HoleData`) **before** the first Add; **re-fetch collection refs fresh** after each Add (don't cache `model.Holes` across Adds); **retry once on `0x80010114`** after a short sleep. **The stale call is often a *benign* op that STARTS the next feature** — e.g. `ProfileSets.Add()` for the next hole — so wrap **that first call inside the try + retry too**, not just the feature `Add`, and make each item independent (one stale failure shouldn't abort its siblings). (Log 58: the M6 hole succeeded, then the Ø4's `ProfileSets.Add()` sat *outside* the retry, threw `0x80010114`, and killed both dowels.) Isolate risky/experimental features (threads) in a throwaway part and make secondary steps non-throwing so a failure can't take down the deliverable |
| `RPC_E_DISCONNECTED (0x80010108)` — "object was disconnected from its clients" — on the call **after** `Holes.AddSync` (even when the hole succeeded) | Worse than the stale `0x80010114`: `Holes.AddSync`'s regen **permanently disconnects the part document proxy** — retry can't reconnect a dead object, and `Application.DelayCompute=true` around the holes does **not** prevent it. (Protrusions via `AddFiniteExtrudedProtrusion` do NOT disconnect — only `Holes.AddSync`.) Since every RCW to the same document dies together, this also kills a `partDoc` you cached elsewhere (e.g. a dialog field) | **The `Application` object never disconnects — re-acquire the document fresh from `app.ActiveDocument` for EACH hole** (and each subsequent operation), rebuilding its plane/HoleData/profile from that fresh doc. Route the add-in's `Application` (not a cached doc) into any long-lived UI, and re-fetch `ActiveDocument` per action. Cleanup after a disconnect can't use dead handles either — snapshot collection **counts** (`Models.Count`, `Constructions.CopySurfaces.Count`) before building, then delete everything **beyond the baseline** via a freshly re-acquired doc. (Alternative if AddSync stays hostile: cut the hole as a `Subtracts` of a cylinder protrusion, which doesn't disconnect.) |
| `'System.ValueTuple<…>' does not contain a definition for 'created'` at **runtime** (compiles fine) | A method that returns a **named** `ValueTuple` was called with a `dynamic` argument → the whole call is late-bound, so its result is `dynamic` and the tuple **element names don't exist at runtime** (only `Item1/Item2/Item3` do) | Don't return a named tuple from a method you call with a `dynamic` arg. Return a **concrete class**, or take a typed result object / `out` params and populate it — member access then binds to real runtime members even under dynamic dispatch |

## Live object dump ("Solid Edge Spy", home-grown)

`ComDiagnostics` already does the `IDispatch → ITypeInfo → ITypeLib` walk that Solid Edge Spy
does. `DumpObject(label, comObj, maxDepth)` lists a live object's **property values**
(safe-getting each 0-arg getter) AND recurses into child COM objects — so you
**reverse-engineer the API from a real feature the human built by hand.** Workflow that beats
guessing: have the human create the feature the right way in SE (a hole, a surface copy, an
offset), **select it**, and dump the selection (`doc.SelectSet.Item(i)`). The dump reveals the
exact type, the collection it lives in, and every property (e.g. a `Hole` feature exposes
`HoleData` with `HoleType=33, HoleDiameter, Depth`; its `.Parent` Model exposes the whole
boolean/extend family). Wire it to a ribbon button ("Inspect selection") for a zero-friction
discovery loop.

**(2026-07-17 upgrade — read this before assuming the old "skips methods" behavior.)** The dump
used to silently drop anything that wasn't a 0-arg getter (every method, every indexed
property). It no longer does:
- Every member is classified via one `ITypeInfo` walk (`GetMemberSchema`, shared with
  `LogSignatures`): 0-arg getters print `name = value` as before; **methods print a full
  signature line** (`métodos: AddSync(...) -> ret; ...`); **indexed getters/setters print a
  signature too** instead of vanishing. This is what turns one SPY click into "here's the exact
  call to make," not just "here's a value."
- **Collections (anything with a 0-arg `Count` + an `Item`) auto-expand** instead of logging as
  opaque "COM; max-depth" — it prints `Count` and dumps the first 5 items (`maxItems`) at
  `depth+1`. Depth-1 now genuinely shows child-collection **contents**, not just names — bump
  `maxDepth` only if you need grandchildren.
- **Every object touched during a dump — including navigation members that don't recurse — now
  also feeds the persistent, cumulative type-library dump** (see below). A single "Inspect
  selection" click both answers "what does this look like right now" and permanently grows the
  offline SDK map.

- **Skip the navigation members when recursing** (`Application`, `Parent`, `Document`,
  `Documents`, `ActiveDocument`) — otherwise depth≥1 dumps the entire SE application object
  (~250 noise lines per selection). Skipping them keeps the VALUE dump to the object's own props
  + useful direct children (they still get harvested into the typelib map, just not recursed).
- **Dumping ONE feature reveals the WHOLE feature tree for free.** Because `.Parent`/model
  back-references chain through every feature, a depth-1 dump of a single selected feature logs
  each sibling feature's `Name`/`DisplayName`/`EdgebarName`/`Type` — so selecting the *last*
  feature the human made and dumping it gives you the entire manual recipe in creation order
  (each feature's real interop `Name` and numeric `Type`), not just the one you clicked.
- **Manual-action recorder** (better than per-feature selection when the user does many steps):
  snapshot the **item NAMES** of every collection under `Constructions` + `Models.Item(1)` +
  `DesignEdgebarFeatures` (enumerate them **generically** — walk `GetMemberNames`, keep members
  whose value has a `.Count`; a fixed list misses where the feature lands), let the human do the
  work, snapshot again, and diff **by name** (not count — stitching CONSUMES input surfaces, so
  counts drop; names catch adds AND removes). Log the doc identity (`Name/Type/Models.Count`)
  each snapshot — an empty diff usually means `ActiveDocument` wasn't the part being edited.

**The EDM "close the burn surface and join it to the block" manual recipe (SE 2023 PT, decoded
by SPY 2026-07-16).** The human's steps and the exact features they create (feature `Name` /
`Type`), to reproduce via COM:
1. **Close the X,Y side gaps** — "Conserto da Superfície" / edgebar "Vinculado" (`Surface Patch_1`,
   Type 145703112, sync). A boundary/patch surface over each open loop — likely
   `Constructions.SurfaceByBoundaries.Add(nEdges, ref Array edges, nExclude, exclude, Tangent)`.
2. **Stitch** — "Costurar" (`StitchedSurface_1`, Type −396962225, sync) —
   `Constructions.StitchSurfaces.Add(nSurf, ref Array surfaces, Heal, Tolerance)`.
3. **Extend the rim in Z** up to the block — "Estender Superfície" (`Extend Surface_1`,
   Type −1575914081, sync). (A real feature even though `Model.ExtendSurfaces` isn't reachable by
   late-binding — find its true collection.) **Not observed in the 2026-07-17 recording** (the
   human's block, built by the AutoEDM tool itself, ended up in the SAME solid body as the
   stitched surface with no separate union/extend feature in between — `Models.Count` went 0→1
   as a single "Design Model" body).

   **HISTORICAL — this whole sub-thread (2026-07-16/17) chased the WRONG mechanism; kept for the
   methodology lesson, see the CORRECTED recipe right after it.** `Carlos` said at the time there
   was an "attach surface to solid" command that "creates NO feature-tree entry at all", and that
   when it doesn't work the human falls back to "União" — so the conclusion drawn then was "don't
   try to reproduce 'anexar', there's nothing to call, go straight for `Unions.Add`". That
   conclusion was **wrong**: "anexar" DOES have a real COM call (`Model.Attach`, see below) — it
   was never found because nobody had sondado the Model's member list for it yet, only guessed
   from the ribbon UI wording. The follow-up test (**CONFIRMED live 2026-07-17**) then spent a
   whole round making `Unions.Add` work by stitching the surface to a synthetic rim patch first
   and forcing Ordered mode — a real, reproducible recipe (patch the open rim with
   `SurfaceByBoundaries.Add` → `StitchSurfaces.Add` the surface + patch → `Unions.Add` the
   stitched result, `E_FAIL` in sync / `E_INVALIDARG` in ordered without the patch) — but it was
   solving the wrong problem: the human's OWN manual process does not require any of that. General
   COM lesson that DOES still hold: **not every visible modeling action in the SE UI creates a
   tree feature** — some apply geometry directly with no record, so a feature-tree-based recorder
   (the Gravador) shows a false "nothing changed" for those actions even though the model visibly
   changed; but the fix for that isn't "give up and use a different API that DOES register a
   feature" — it's to sonda the owning object's member list directly (below) rather than assume a
   void-returning method doesn't exist just because a naive Union-based workaround does.

   **CORRECTED recipe (2026-07-20, AutoEDM `SurfaceBlockBuilder.TryUniteToBlock`, 8 real test
   rounds against a live SE 2023/2026, narrative in AutoEDM's `docs/AutoEDM_Logs_Consolidated_
   Analysis.md`).** The human's actual manual flow, and Carlos's own correction mid-investigation:
   "está tentando costurar superfícies para unir ao bloco, esse não é o recurso correto, preciso
   apenas do comando 'unir' mas no síncrono e não no ordenado" — Stitch is NOT the union mechanism
   (it's a separate, legitimate step ONLY for consolidating a multi-face raw surface into one
   cohesive body BEFORE attaching it — different from stitching a synthetic rim patch just to
   satisfy `Unions.Add`), and the real union runs in SYNCHRONOUS, not Ordered.
   - **The real "anexar" call is `Model.Attach(NumOfObjects: int, psaObjects: SAFEARRAY(IDispatch)*,
     bAdd: bool, fpcSide: FeaturePropertyConstants) -> void`** — found on the live Model's member
     list once someone actually looked (163 members, `ProbeModelApi` in AutoEDM) instead of
     assuming "no feature = no call". It **returns void and registers NO tree feature** — that's
     exactly why the Gravador diff kept showing "nothing changed" even on a successful manual run;
     the only observable trace is that the SOURCE `CopySurface` feature (still present, same
     `Type=igCopySurfaceObject`) starts appearing under `Model.Features` in addition to
     `Constructions.CopySurfaces` — i.e. it gets reparented into the body's feature list rather
     than consumed/replaced by a new one. **If a recorder diff shows an EXISTING item migrating
     between two collections (not a brand-new name), that's the signature of a void-returning
     "attach"-style call — don't dismiss it as noise.**
   - `fpcSide` is **NOT optional** (the dump shows no `[opt]` on it, unlike `BooleanFeatures.Add`'s
     `PlaneSide`) — `Type.Missing` throws `DISP_E_PARAMNOTOPTIONAL`; supply a real
     `FeaturePropertyConstants` value, tried `igRight=2` then `igLeft=1` (same left/right
     convention as extrude `ExtentSide`).
   - The tool array (`psaObjects`) must be a **typed** `SolidEdgePart.CopySurface[]` (or
     `StitchSurface[]` if you stitched first) — `object[]` marshals as `SAFEARRAY(VARIANT)` and
     throws `DISP_E_TYPEMISMATCH` (the general SAFEARRAY-typing rule elsewhere in this skill,
     confirmed again here).
   - **`Model.Attach` is SYNCHRONOUS-only** — same class of bug as `AddThickenFeature` (silent
     no-op outside its native mode) — if the document is already in Ordered mode (leftover from a
     PREVIOUS run that switched modes for the GAP step), force `ModelingMode = 1` **before**
     calling `Attach`, then switch to Ordered only afterward for `FaceOffsets`.
   - **Fallback candidate (found in the same sondagem, less tested):** `Model.BooleanFeatures.Add(
     NumberOfTools: int, Tools: VARIANT, Function: BooleanFeatureConstants, [opt]PlaneSide: VARIANT)
     -> BooleanFeature` — a SEPARATE boolean collection from `Unions`/`Subtracts`/`Intersects`
     (those are the Ordered-only family; confirmed `E_FAIL` in Sync across 2 real tests, don't use
     them for a sync surface→solid attach). Notably has **no explicit Target parameter** — the
     target is implicit (the Model's own body), matching the "select tool, not target+tool" feel
     of the ribbon UI. `Function=3` = `seBooleanUnite` (`docs/api/constants.md`). Must be fetched
     via `Type.InvokeMember("BooleanFeatures", BindingFlags.GetProperty, ...)`, NOT
     `blockModel.BooleanFeatures` dynamic access — the latter throws `E_NOINTERFACE` even though
     the collection genuinely exists live (see the error table's PIA-mismatch row above).
   - **Stitch still has a real, narrower use: self-consolidation, not rim-patching.** When the burn
     surface comes from several SEPARATE selected faces (not one pre-made `CopySurface`), a human's
     real process re-includes `StitchSurfaces.Add` over just those faces (`Heal=true`, no synthetic
     rim patch) to fold them into one coherent body before `Attach` — confirmed by a real `[REC]`
     recording showing `StitchedSurface_N` appear right before a successful Attach. This is
     optional (skip to the raw surface if it fails) and orthogonal to the abandoned rim-patch hack.
4. **Offset the burn surface by GAP** — "Afastar/Deslocar Face" (`Offset_N`, Type 1180468550,
   **ordered** — `ModelingMode` flips 1→2 right before this step). **CORRECTED 2026-07-17 (was
   wrongly attributed to `Model.FaceMoves` on 2026-07-16 — the Gravador's per-collection diff now
   names the collection unambiguously):** it's **`Model.FaceOffsets`**, confirmed live +
   confirmed from the typelib dump (now capturing `SolidEdgeGeometry` live too — see below):
   ```
   Model.FaceOffsets.Add(FacesToOffset: IDispatch, BlendRecreation: FeaturePropertyConstants,
       AlongOrReverseVector: FeaturePropertyConstants, offsetDistance: double,
       ToReferenceEntity: IDispatch, ToKeyPoint: IDispatch, DistanceFromKeyPoint: double,
       AlongOrReverseDirectionToKeyPoint: FeaturePropertyConstants) -> FaceOffset  [8 params]
   Model.FaceOffsets.AddEx(NumFaces: int, FacesToBeOffset: SAFEARRAY(IDispatch),
       FaceOffsetType: FaceOffsetConstants, NumOfLiveRules: int, LiveRules: SAFEARRAY(...),
       LiveRulesOnOff: SAFEARRAY(bool), BlendRecreation: FeaturePropertyConstants,
       AlongOrReverseVector: FeaturePropertyConstants, offsetDistance: double,
       ToReferenceEntity: IDispatch, ToKeyPoint: IDispatch, DistanceFromKeyPoint: double,
       AlongOrReverseDirectionToKeyPoint: FeaturePropertyConstants) -> FaceOffset  [13 params]
   ```
   Live example decoded (24 faces offset at once → use `AddEx`, not `Add`, for the whole burn
   surface): `FaceOffsetType=1` (`igFaceOffsetBySynchronousOffset`), `AlongOrReverseVector=20`
   (`igNormal` — offsets along the face normal), `AlongOrReverseDirectionToKeyPoint=44`
   (`igNone` — no keypoint target, so `ToReferenceEntity`/`ToKeyPoint` are null), `offsetDistance
   = -5E-05` (meters = **−0,05 mm, NEGATIVE = shrink inward** — matches the known "electrode
   SHRINKS" rule, [[electrode-anatomy]]/[[autoedm-decisions]] Ra→offset table), `FaceOffsetBlendType=194`
   (`igIgnoreBlends`). This is the concrete call to automate the GAP-offset step.

To find WHICH open edges bound each X,Y gap, note `edge.Faces.Count` is **not readable by late
binding** here (returns nothing) — get boundary edges another way (per-face `Loops`, or the
surface's laminar-edge query) rather than counting adjacent faces.

**Real workflow clarified (Carlos, 2026-07-17) — Inter-Part Copy DOES work, but only in-context.**
The human creates the electrode as a new part **in the assembly**, then edits it **in-context**
(in-place, `AssemblyDocument.ModelingInAssembly`-style) — that in-context mode is what makes
Inter-Part Copy possible (a bare `.par` opened standalone can't do it, matching the earlier
"Inter-Part Copy blocked" finding — it's blocked OUTSIDE in-context, not always). Sequence: copy
the burn surfaces via Inter-Part Copy → **"quebrar" (break)** to unlink them from the source part
→ treat/close/stitch the surfaces → generate the block (the human uses the AutoEDM "Criar Base"
button for this part, renamed 2026-07-17 from "Bloco sobre superfícies" — GAP/color-offset logic
was stripped from it, see `autoedm-decisions` memory) → attach surface to block (non-feature
action, or fall back to União — see above) → switch to
**Ordered** modeling → add the GAP offset (`Model.FaceOffsets`, above) → exit in-context edit back
to the assembly. `Application.ActiveDocument` reports the electrode **part** (`Type=1`) the whole
time it's in-context — the Gravador's "Iniciar leitura"/"Gravar log" clicks must both land while
that part window still has focus (see the recorder-robustness note in `autoedm-decisions` memory
about `ConfirmDocParaGravacao` — it only warns when the doc ISN'T a part, not when it's the WRONG
part; a first attempt this same day accidentally snapshotted the master/cavity part instead of the
electrode because "Iniciar leitura" was clicked too early, before entering the electrode's
in-context edit).

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
> in practice, model on the part itself (below) or have the human do the associative copy.

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
  **poison the proxy** (next `Holes.AddSync` → `E_FAIL`) — so isolate it (see below).
- Full `Constructions` set (SE 2023, 43 members) worth knowing: `BlueSurfs, BSplineSurfaces,
  CopyConstructions, CopySurfaces, ExtrudedSurfaces, InterpartConstructions, LoftedSurfaces,
  MidSurfaces, OffsetSurfaces, PartingSurfaces, RevolvedSurfaces, RuledSurfaces, StitchSurfaces,
  SurfaceByBoundaries, SweptSurfaces, UnitedBodies, WrapSketchs`.
- A user **`SelectSet`** of individual faces gives you the faces (enough for a footprint bbox and
  for `CopySurfaces.Add`) — you no longer need the human to do "Surface→Copy" by hand; auto-create
  the CopySurface from the selected faces with the typed `Face[]` array.

**Isolate proxy-poisoning / experimental ops from the working deliverable.** A single feature that
fails mid-op (a tapped `HoleData`, an `AddThickenFeature` that doesn't apply) can **poison the
document proxy** so that *every subsequent* `Holes.AddSync` returns `E_FAIL` — i.e. an experiment
can silently break a step that already worked (fixation holes). Put risky/unproven surface ops
behind a **separate command** (its own ribbon button) that runs only that op, so the proven flow
(block + band + holes) never shares a document mutation with the experiment. Iterate the hard part
in isolation.

**Solid modeling that WORKS (validated recipe).** Model on the **part's own document**
(`app.Documents.Add("SolidEdge.PartDocument")` or `occurrence.OccurrenceDocument`) — this
is standalone and needs **no in-place edit**. Box/`AddBoxByTwoPoints`/`AddBoxByCenter` are
finicky (`DISP_E_TYPEMISMATCH`); use **sketch + extrude**:

```
ProfileSet  = partDoc.ProfileSets.Add()
profile     = ProfileSet.Profiles.Add(partDoc.RefPlanes.Item(1))   // Item(1) = XY plane
Lines2d.AddBy2Points(x1,y1,x2,y2) ×4   (rectangle, METERS, centered on origin)
profile.End(1)                          // 1 = closed profile (required before extrude)
Models.AddFiniteExtrudedProtrusion(1, Profile[] TYPED, side, distMeters)
ProfileSet.Delete()                     // code-made sketch is locked for the user; delete it
```

- `ProfileArray` **must be a typed `SolidEdgePart.Profile[]`** (→ `SAFEARRAY(IDispatch)`);
  `object[]` fails (same lesson as `Face[]`).
- `side` = extrude direction: `1 = igLeft (−normal)`, `2 = igRight (+normal)`, `3 = symmetric`.
- The sketch created in code can't be deleted by the user; in **Synchronous** mode the
  solid doesn't depend on it, so `ProfileSet.Delete()` cleans it up.
- **Cylinder (round blank / fixation shaft)**: same recipe, but the profile is one
  `Profile.Circles2d.AddByCenterRadius(x, y, radiusMeters)` instead of the 4 `Lines2d`.
  Extruded **on the block's top plane** (a `RefPlanes.AddParallelByDistance` at the top Z),
  side `2 (+normal)`, it's a **protrusion that unions with the block** — how a grip shaft is
  added on top of a holder. `AddBoxByCenter`/`AddCylinderByCenterAndRadius` primitives stay
  finicky; sketch+extrude of a rectangle or circle is the reliable path for both.
- **Base plane at a Z offset** (to lift the block, or to sketch on the top): make an offset
  plane `RefPlanes.AddParallelByDistance(Item(1), Zmeters, NormalSide=2, …)` and sketch on it;
  the profile's local (0,0) sits on that plane. Hide it (`plane.Visible=false`) rather than
  delete (deleting a construction plane can invalidate downstream ordered features).

**Positioned-part workaround (replaces inter-part copy).** To place geometry in an
assembly without in-place edit: build the part **standalone** (recipe above),
`partDoc.SaveAs(path)` → `partDoc.Close()`, then
`AssemblyDocument.Occurrences.AddByFilename(path)` → `Occurrence.PutOrigin(x,y,z)`. All
validated. (`Occurrences.AddByTemplate(newPartPath, template)` creates a part in-context —
arg1 is the NEW part's path, arg2 the template — but still does **not** grant in-place edit.)

**Synchronous vs ordered features (this bites).** The default part template (SE 2023) is
**Synchronous** — `PartDocument.ModelingMode`: `seModelingModeSynchronous=1`,
`seModelingModeOrdered=2`. Feature methods come in two flavors: ordered (`AddFinite`,
`AddFiniteExtrudedProtrusion`, …) and **`AddSync*`**. On a synchronous part an ordered
method *creates the geometry* but the feature **does not appear in the PathFinder** — use
`AddSync`/`AddSyncEx` so it's a real synchronous feature. Branch on `ModelingMode`.

- **Some ordered features have NO sync variant and are a SILENT NO-OP on a sync part**
  (solved 2026-07-15). `Models.AddThickenFeature` (surface→solid thicken) has no
  `AddThickenFeatureSync`; called on a synchronous part it **returns a non-null Model but
  creates no body** (`Models.Count` unchanged, no exception, no `.Status` error — it just
  does nothing). This looks exactly like "ordered and synchronous are mixing" to the user.
  Extruded protrusions happen to auto-convert to sync features; thicken/boolean-heavy ones
  don't. **Fix: for such an op, switch `partDoc.ModelingMode = 2` (ordered) first**, run it,
  and leave it — a **synchronous body with ordered finishing features on top is the normal SE
  model** (the "Ordered" section under "Synchronous" in PathFinder). Detect success by a **new
  body OR the target body's face count increasing** (an ordered thicken may merge straight into
  the existing design body rather than spawn a separate one — then no `Unions.Add` is needed).
  Verify a feature really applied with **`ModelingMode` logged + before/after body face counts**,
  not just `Models.Count` and a non-null return.

**Sketches from `ProfileSets` are ORDERED — delete them from code.** `ProfileSets.Add()`
makes an **ordered** profile even inside a synchronous part; once a synchronous feature
consumes it, the leftover ordered sketch is **locked (the user can't delete it in the UI)**.
So always `ProfileSet.Delete()` right after building the feature — the synchronous feature
survives, and no orphan sketch remains. (Cleaner still: make the sketch synchronously via
`Sketches`; reserve ordered sketches for downstream ops like offset/chamfer/round.)

**Fixation holes (validated recipe).** Mark centers with **`Profile.Holes2d.Add(x,y)`** —
NOT `Circles2d` (a plain circle makes the hole feature create **zero holes, with no error**).
Then `HoleData = PartDocument.HoleDataCollection.Add(HoleType, DiameterMeters, …)` with
`HoleType = igRegularHole (33)`; and on a synchronous part
`Model.Holes.AddSync(1, Profile[] TYPED, ProfilePlaneSide, ExtentType, depthMeters, HoleData)`
with `ExtentType = igFinite (13)` for a **blind** hole (ordered part → `AddFinite(Profile,
side, depth, HoleData)`). Then delete the ProfileSet. **One `Holes2d.Add` per profile** —
two centers in one profile yields only **one** hole.

**Threaded/tapped holes — the recipe that actually RENDERS a metric thread (SE 2023, solved
2026-07-15).** Create the HoleData as tapped: `hd = HoleDataCollection.Add(igTappedHole=37,
tapDrillDiaMeters)`, then `Holes.AddSync(1, Profile[] TYPED, side, igFinite=13, depth, hd)`
(re-acquiring the doc per hole, per the RPC_E_DISCONNECTED fix — that's what stopped the old
E_FAIL/poison cascade). Three gotchas that each looked like "the thread doesn't work":
  1. **`HoleData.ThreadDataByDescription` is a WRITE-ONLY PROPERTY, not a method.** Reflection
     shows `set_ThreadDataByDescription(String)` with `CanRead=False, CanWrite=True`. Calling it
     method-style — `hd.ThreadDataByDescription("M6")` — throws "erro ao chamar" for *every*
     format (this was mis-diagnosed for weeks). **Assign it:** `hd.ThreadDataByDescription = "M6"`
     (also `"M6x1"`). That pulls the correct **metric** Standard/SubType/diameters from SE's
     thread table. Verify it took by reading back `hd.ThreadNominalDiameter > 0`.
  2. **Filling only the diameters by hand, WITHOUT the Standard, renders a TRAPEZOIDAL thread**
     (and a wrong Ø). If you must hand-fill (table refused the description), also set
     `hd.Standard = "ISO Metric"`, `hd.SubType`, `hd.Size = "M6"` — the diameters alone give the
     wrong thread FORM.
  3. **The thread is DATA-only until you enable rendering — set `hd.ThreadSetting =
     igRegularThread (164)`** (the API equivalent of the "Rosca" checkbox in the Hole dialog;
     `igNone=44` = no thread). With correct data but `ThreadSetting` unset, the hole has all the
     M6 numbers but shows as a plain drilled hole. This is the last mile people miss.
  4. **`ThreadSetting=igRegularThread` alone is COSMETIC ONLY — it does not cut the real
     helical geometry** (confirmed 2026-07-17: a real UI-made tapped hole had
     `ThreadSetting=164` yet `Hole.CreatePhysicalThread = False`, and Carlos visually
     confirmed that hole was NOT actually threaded in the solid). The `Model.Holes` feature
     (and `Model.Threads` feature) exposes its own separate bool property
     **`CreatePhysicalThread`** plus a method
     `CreatePhysicalThreadAndReturnStatus(CreatePhysicalThread: bool, [out]
     enumPhysicalThreadErrorCode: PhysicalThreadErrorCode*) -> void`. To get a solid with a
     real machined thread (what EDM/manufacturing needs, as opposed to a drawing-callout
     thread), call `hole.CreatePhysicalThreadAndReturnStatus(true, out status)` after the
     hole is created — this is the untested missing step for the M6 fixation hole. Not yet
     wired into `BlankModeler`/`BlankBoxProbe` — next recording session should validate it.
`HoleDataCollection.Add` has **21 params** (authored docs claiming 7 are wrong — the live dump
wins): `(HoleType, HoleDiameter, [CounterboreDiameter], [CounterboreDepth], [CountersinkDiameter],
[CountersinkAngle], [BottomAngle], [TreatmentType], [TaperMethod], [Taper], [ThreadMinorDiameter],
[ThreadDepthMethod], [ThreadDepth], [VBottomDimType], [TaperDimType],
[CounterboreProfileLocationType], [TaperLValue], [TaperRValue], [ThreadExternalDiameter],
[ThreadDescription], [IgnoreSavedDefaultValues])`. Enums: `igRegularHole=33, igCounterboreHole=34,
igCountersinkHole=35, igCounterdrillHole=36, igTappedHole=37; igRegularThread=164,
igStraightPipeThread=165, igNone=44`. The separate `Model.Threads.Add(HoleData, nCyl,
CylinderArray, CylinderEndArray, …)` feature (applied to `Body.Faces[igQueryCylinder=10]`) **fails
every way** on SE 2023 (E_FAIL / out-of-range) — don't use it; the tapped-HoleData path above is
the one that works. Still keep a **plain-Ø5 fallback in a fresh doc** if the tapped AddSync fails,
so you never lose the central hole.

**Reference plane for a hole at a given depth.**
`RefPlanes.AddParallelByDistance(ParentPlane, Distance, NormalSide, [Pivot], [PivotOrigin],
[Local])`. `Distance` is **always positive** (meters); direction is the `NormalSide`
**side enum `igLeft=1 / igRight=2`** (a `FeaturePropertyConstants` side — not a dedicated
"normal-side" enum). `Local=False` makes a **global named** plane. If the plane comes out on
the wrong side, flip `igLeft`↔`igRight` — that one param is the whole fix.

**Geometry-type / face-query enums** (`FeatureTopologyQueryTypeConstants`, same values as
`Face.Geometry.Type` / `GNTTypePropertyConstants`): `igQueryAll=1`, `igLine=3`, `igEllipse=4`,
`igQueryPlane=6`, `igCone=7`, `igTorus=8`, `igSphere=9`, `igQueryCylinder=10`. Use
`Body.Faces[igQueryCylinder]` to grab a hole's cylindrical wall (for `Threads.Add`), or
`igQueryPlane` to filter planar burn faces.

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

## Early binding (optional, for production)

The type libraries ship with the install at
`C:\Program Files\Siemens\Solid Edge 2023\Program\` (`framewrk.tlb`, `part.tlb`,
`assembly.tlb`, `geometry.tlb`, `constant.tlb`, …). Reference them (or the
`Interop.SolidEdge` / `SolidEdge.Community` NuGet packages) behind an
`#if EARLY_BINDING` guard for IntelliSense + compile-time signatures, keeping
`dynamic` as the default portable path. Note: the API Programmer's Guide `.chm` is
**not** guaranteed to be installed (only user-guide `.chm`s under `Program\ResDLLs\`),
so the runtime typelib dump remains the source of truth.

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

## Working loop (with a human running SE)

The dev machine usually has no SE license, so a human runs the built app in SE and
sends back a numbered log. To keep that loop cheap:

1. **Non-destructive first.** New probes only read + introspect + write a log. No
   geometry/save until the API is confirmed.
2. **One question per run.** Each log should answer a specific unknown (a signature,
   an enum value, whether a getter populates).
3. **Grep the dump before proposing a call** — the answer is usually already there.
4. **Numbered logs** (`AutoEDM NNN.log`) so runs are comparable.
5. **Close the GUI before rebuilding** — a running WinExe locks the output exe.
6. **Add-in: SE loads from the DEPLOY folder, NOT `bin\Debug` — so a plain rebuild changes
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
7. **When a tool "finds nothing", make it dump the structure** (occurrences, bodies, face
   counts) and the target value from **every plausible source** — e.g. face color from
   `face.Style`, `Face.GetRGBAVals`, and `Occurrence.GetFaceStyle2`. Silent `continue` on a
   failed read hides the real cause; one diagnostic run beats guessing.
