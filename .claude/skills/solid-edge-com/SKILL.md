---
name: solid-edge-com
description: >
  Automate Solid Edge (2023/2026) from C#/.NET through COM when the SDK and type
  libraries are NOT installed — discover the API at runtime by introspection instead
  of guessing signatures from memory. Use this skill for ANY Solid Edge automation
  task: reading faces, colors, geometry or occurrence transforms; creating parts and
  features (extrude, holes, threaded holes, surfaces, booleans, surface-to-solid);
  reading facet/mesh bodies and reverse-engineering a scanned mesh back into surfaces;
  editing in the context of an assembly; building ribbon add-ins; or debugging COM
  errors such as RPC_E_CALL_REJECTED, RPC_E_DISCONNECTED, DISP_E_TYPEMISMATCH,
  E_NOINTERFACE, RuntimeBinder failures and out-parameter marshaling. Trigger it
  whenever "Solid Edge", SolidEdgeFramework/SolidEdgePart/SolidEdgeGeometry/
  SolidEdgeAssembly, EDM electrode automation, or COM automation of a CAD system come
  up — even if the user never mentions the SDK, and even if the request looks like a
  plain C# question.
---

# Solid Edge COM automation (no SDK on the machine)

The Siemens SE SDK docs require a login and the type libraries are not downloadable.
But SE registers its COM type libraries locally, so the API is fully discoverable at
runtime.

**The one rule this skill exists to enforce: never guess a signature from memory.**
Every wrong guess costs a full round-trip through a human who has to run the code
inside a licensed SE. Introspect first, then call.

## Working assumptions

This skill was written against a real project (AutoEDM: a C# add-in + external GUI
that builds EDM electrodes). Names like `ComDiagnostics.DumpTypeLibraries`,
`ModelingProbe`, `SurfaceBlockBuilder` and paths under `%LOCALAPPDATA%\AutoEDM\logs`
refer to that project's helpers. **Treat them as a reference implementation, not a
dependency** — if the current project has no equivalent, build the same three tools
(typelib dumper, live-object dumper, action recorder), because everything downstream
assumes they exist. `references/discovery.md` describes what each one must do.

## Non-negotiable constraints (predict these errors before they happen)

- **Lengths are METERS** in the geometry/modeling API. 20 mm = `0.020`. Convert
  ranges ×1000 for mm. **Angles have no single rule** — `Occurrence.GetTransform`
  returns radians, but `HoleData.BottomAngle`/`CountersinkAngle`/chamfer angles are
  **degrees** (`118`, not `2.06`), while `HoleData.ThreadTaperAngle` is radians in
  the same object. Check the property, never the object.
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
  (`OleMessageFilter.Register()`.) Add a bounded retry/timeout so a modal error
  dialog in SE doesn't hang the automation forever.
- **Connect to the running instance** via the Running Object Table
  (`ComInterop.GetActiveObject("SolidEdge.Application")`) — `Marshal.GetActiveObject`
  is gone in modern .NET, so P/Invoke `ole32`/`oleaut32`.

These earn their own explanation because they cause silent wrong results rather than
exceptions:

- **A feature `Add` that returns without throwing may still have failed.** SE reports
  feature failure through `.Status`, not through an HRESULT. Check
  `feature.Status == igFeatureOK (1216476310)` after every `Add`, comparing as uint32.
  **SE does not roll a failed feature back** — it stays in PathFinder, red. A retry
  loop that only tests the return value for null leaves the user a trail of junk to
  clean by hand, so `Delete()` any returned feature whose `.Status` is failed before
  the next attempt.
- **`[out]` parameters come back empty in late binding unless you mark them by-ref**
  with a `ParameterModifier`. `Face.GetRange` and `Occurrence.GetTransform` both
  return plausible all-zero values otherwise — an all-zero transform reads as "at the
  origin", which once caused a ~23 mm mis-placement that threw no error.
- **SAFEARRAY params need a TYPED element array** (`SolidEdgeGeometry.Face[]`,
  `SolidEdgePart.Profile[]`, `SolidEdgeGeometry.Body[]`), never `object[]` — `object[]`
  marshals as `SAFEARRAY(VARIANT)` and the call fails with `DISP_E_TYPEMISMATCH`.
  **This one recurs** — it has now bitten `CopySurfaces.Add`, the extrusion calls and
  `Sketches.CreateSectionSketches` (2026-09-18), because `object[]` is the natural thing
  to write. Whenever the dump says `SAFEARRAY(IDispatch)*`, reach for the typed array
  before running anything. And note what the error does *not* mean: the call never
  reached the command, so **a marshaling failure says nothing about whether SE can do
  the operation** — do not record it as "SE can't do X".
- **Some ordered features are a silent no-op on a synchronous part** — they return a
  non-null object, create nothing, and set no error. Branch on `ModelingMode`.
- **Worse than a no-op: an ordered method on a synchronous part can SUCCEED**, creating
  the sketch and the feature in the *other* environment — geometry the user then cannot
  delete. So never "try the other mode" as a fallback: read `ModelingMode` (sync=1,
  ordered=2) and call only that mode's family. If it refuses, say so and let the human
  decide to switch the document. A fallback that dirties the user's part is not a
  fallback.
- **Never set `ModelingMode` on the user's document — declare the environment instead.**
  This is the strongest form of the rule above, and it earned its place twice on the same
  project (2026-09-08). Every command should name the one environment its features belong
  to and *refuse* in the other; switching mid-operation breaks two ways at once:
  1. **Orphan sketches.** `ProfileSets` only ever makes an *ordered* sketch. Feed it to a
     *synchronous* feature and the sketch is stranded under PathFinder's "Ordered" node with
     no owner — the UI will not delete it, so the user cannot clean their own part. A single
     multi-item run left a pile of them.
  2. **Dead face proxies.** Changing the mode makes SE rebuild the body, so every
     `Face`/`Edge` read *before* the switch is a stale proxy afterwards. A command that
     collected the user's selection, switched to ordered, then painted/offset those faces
     failed with a bare `E_FAIL` that pointed nowhere near the real cause.
  The two exceptions: a document **your own code just created** (a throwaway probe part),
  and a switch the user explicitly asked for in that click.
- **Disable the command in the wrong environment, don't just fail in it.** The environment
  check belongs in the UI, not only at the bottom of the call stack — a greyed-out button
  teaches the constraint, an error dialog after the fact only reports it. In a ribbon add-in
  this is `RibbonControl.Enabled` (see `references/addin-ribbon.md`); keep the click-time
  check too, as the state can be one poll stale.
- **A successful `Add` can hand back an object you never asked for.** Two mechanisms,
  usually together. First, enums like `FeaturePropertyConstants` are one flat namespace
  shared by dozens of properties, so a value that is legal *somewhere* is accepted
  *anywhere* — pass it to the wrong property and SE substitutes something else instead
  of erroring. Second, SE merges the user's **saved dialog defaults** into your object
  unless you pass `IgnoreSavedDefaultValues = true`. So: **read the properties back and
  assert the ones that matter**, and where the call produces geometry, **measure it**
  (`Body.Faces[igQueryCylinder]` + `Face.GetRange`) instead of trusting
  `Status == igFeatureOK`. A hole reported OK came out Ø6.0 counterdrilled where Ø5.0
  tapped was asked for, and nothing in the API said so.
- **`.Type` on a Face or Edge is the TOPOLOGY kind, never the shape.** `Face.Type` is
  always `igFace`, `Edge.Type` always `igEdge`. Plane-vs-cylinder and circle-vs-line
  live one level down, on `.Geometry.Type` — and that geometry object also carries the
  exact `Radius`, which a bounding box cannot give you for a partial cylindrical face.
  Comparing `Face.Type` with `igPlane` matches nothing, forever.
- **More generally, an enum that INDEXES a collection is rarely the enum a property
  RETURNS.** `Body.Faces[igQueryCylinder=10]` does select the cylindrical faces, but
  `igQueryCylinder` will never appear as anyone's `.Type` — indexing uses
  `FeatureTopologyQueryTypeConstants`, reading uses `GNTTypePropertyConstants`. Before
  comparing any constant against a property, confirm in the dump that the property's
  own type is that enum.
- **Ask an object where it is; don't deduce it from how you built it.** A profile knows
  its own plane (`Convert2DCoordinate` at `(0,0)`, `(1,0)`, `(0,1)` gives origin plus
  two in-plane vectors, hence the normal), so sketch geometry can be placed from real 3D
  part coordinates and a `NormalSide` guess can be checked rather than assumed. The same
  move works elsewhere: a full circular edge's `GetRange` box is degenerate along the
  circle's normal, which hands you the axis and centre in one read (take the exact radius
  from `.Geometry`, not from the box). Most sign and axis bugs
  in sketch-driven modeling come from reasoning about construction history instead of
  querying the result.

## Where to look next

Read only the file you need; each is self-contained.

| Task at hand | Read |
|---|---|
| Setting up discovery, dumping the typelib, building the SPY or the action recorder, reading the install's data tables for a string argument, early binding | `references/discovery.md` |
| A specific HRESULT, binder error, or "it succeeded but nothing happened" | `references/errors.md` |
| A confirmed call signature, enum value, geometry-type enums, face color, face traversal, bbox, occurrence transform, profile plane frame, in-place editing, surface collections, cutouts, **ordered-tree groups, feature↔body face IDs, document properties (the revision trap), ribbon GuiVersion cache** | `references/api-signatures.md` |
| Building geometry: sketch+extrude, cylinders, holes, threaded holes, annular grooves and revolved cuts, sync vs ordered, placing a part in an assembly | `references/modeling-recipes.md` |
| Putting a button inside SE: add-in registration, ribbon XML, RT_BITMAP icons, HKCU registration, deploy folder, modeless dialogs, in-process hosting limits, picking a face or edge from the model | `references/addin-ribbon.md` |
| The EDM electrode flow specifically: burn-surface copy, stitch, attach-to-block, GAP offset | `references/edm-electrode.md` |
| Meshes and reverse engineering: reading facets, what SE will and will not fit for you, sectioning a mesh, writing a surface recogniser, reference-plane normals | `references/mesh-reverse.md` |

## The discovery loop, in order

1. **Dump the type library once** (`ITypeInfo` → `GetContainingTypeLib` → `ITypeLib`),
   seeding one live object per module so all four typelibs get covered. Emit parameter
   **types and direction** (`[out]`, `[in,out]`, `[opt]`) plus enum **values** — types
   are what predict marshaling errors, and a missing `[opt]` marker is what predicts
   `DISP_E_PARAMNOTOPTIONAL`. Grep the output; never load it wholesale into context.
2. **Grep the dump before proposing any call.** The answer is usually already there.
3. **Read the install's DATA tables for any string argument.** The typelib gives the
   *shape* of a call and never the magic strings an `[in] BSTR` accepts — hole
   standards, thread descriptions, materials, fits. Those live in files under
   `Preferences\` in the SE install, readable **without a license and without running
   SE**, so they cost no human round-trip at all. Locate them via
   `Application.GetGlobalParameter` rather than hardcoding. Reading the table also
   reveals what SE *cannot* do: the legacy `HOLES.TXT` has no pitch column, so nothing
   needing a pitch can be driven from it.
4. **Confirm the member exists on the LIVE object** before building on it. Reflecting
   `Interop.SolidEdge` finds interop *types* that the running Model may not implement,
   and can disagree with the dump on **parameter direction** — it imports one
   `SurfaceByBoundaries.Add` argument as `out` where the live dump says `[in,out]`,
   which would drop your data on the way in. The interop package is older than the SE
   build; where they differ, the dump wins. Reflection gives you signatures and enum
   values; the live IDispatch decides what is actually callable.
5. **When the API is unclear, have the human do it by hand and watch.** Dump the
   selected feature (SPY) or diff collection snapshots around the manual action
   (recorder). This reverse-engineers the exact call in one round instead of ten.
6. **Watch for the void-returning trap in step 5.** Not every visible modeling action
   creates a tree feature — some apply geometry with no record, so the recorder reports
   "nothing changed" on a real, successful action. The signature of one of these is an
   *existing* item migrating between collections rather than a new name appearing.
   Don't dismiss that as noise, and don't conclude "no feature means no call" — probe
   the owning object's member list directly.

## Working loop with a human running SE

The dev machine usually has no SE license, so a human runs the built app inside SE and
sends back a log. That round-trip is the scarce resource; spend it well.

1. **Non-destructive first.** A new probe only reads, introspects and writes a log. No
   geometry, no save, until the API is confirmed.
2. **One question per run.** Each log should answer one specific unknown — a signature,
   an enum value, whether a getter populates.
3. **Numbered logs** (`App NNN.log`) so runs are comparable.
4. **Close the GUI before rebuilding** — a running WinExe locks the output exe.
5. **Stamp the loaded build in the log — with the full PATH.** On startup, log
   `assembly.Location` plus `File.GetLastWriteTime(...)` for the add-in and core DLLs.
   The file name alone is not enough: a stale build and a fresh one look identical until
   you can see which folder it loaded from. The first lines then prove which binary
   actually ran, which kills the "is this even my new code?" ambiguity permanently. (For add-ins this matters doubly — see the
   deploy-folder trap in `references/addin-ribbon.md`.)
6. **Isolate risky operations from the working deliverable.** A feature that fails
   mid-op can poison the document proxy so that every *subsequent* call fails — an
   experiment can silently break a step that already worked. Put unproven operations
   behind their own command so the proven flow never shares a document mutation with
   the experiment.
7. **When a tool "finds nothing", make it dump the structure** — occurrences, bodies,
   face counts — and read the target value from *every* plausible source. A silent
   `continue` on a failed read hides the real cause; one diagnostic run beats three
   rounds of guessing.

## Reporting findings back

When a run confirms or refutes something, record it in the matching reference file
with the date and how it was confirmed (live dump, reflection, human observation).
Two habits keep this file trustworthy:

- **State which source confirmed a fact**, because they disagree. Authored docs and
  reflection are cross-checks; the live dump wins.
- **Keep corrected mistakes, marked as corrected.** Several entries here describe a
  mechanism that was chased for days and turned out wrong. The wrong path is worth a
  paragraph precisely because the next person will otherwise reason their way into it
  again.
