---
name: solid-edge-com
description: >
  Automate Solid Edge (2023/2026) from C#/.NET through COM when the SDK and type
  libraries are NOT installed — discover the API at runtime by introspection instead
  of guessing signatures from memory. Use this skill for ANY Solid Edge automation
  task: reading faces, colors, geometry or occurrence transforms; creating parts and
  features (extrude, holes, threaded holes, surfaces, booleans, surface-to-solid);
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
  (`OleMessageFilter.Register()`.) Add a bounded retry/timeout so a modal error
  dialog in SE doesn't hang the automation forever.
- **Connect to the running instance** via the Running Object Table
  (`ComInterop.GetActiveObject("SolidEdge.Application")`) — `Marshal.GetActiveObject`
  is gone in modern .NET, so P/Invoke `ole32`/`oleaut32`.

Four more constraints earn their own explanation because they cause silent wrong
results rather than exceptions:

- **A feature `Add` that returns without throwing may still have failed.** SE reports
  feature failure through `.Status`, not through an HRESULT. Check
  `feature.Status == igFeatureOK (1216476310)` after every `Add`, comparing as uint32.
- **`[out]` parameters come back empty in late binding unless you mark them by-ref**
  with a `ParameterModifier`. `Face.GetRange` and `Occurrence.GetTransform` both
  return plausible all-zero values otherwise — an all-zero transform reads as "at the
  origin", which once caused a ~23 mm mis-placement that threw no error.
- **SAFEARRAY params need a TYPED element array** (`SolidEdgeGeometry.Face[]`,
  `SolidEdgePart.Profile[]`), never `object[]` — `object[]` marshals as
  `SAFEARRAY(VARIANT)` and the call fails with a binder error.
- **Some ordered features are a silent no-op on a synchronous part** — they return a
  non-null object, create nothing, and set no error. Branch on `ModelingMode`.
- **Worse than a no-op: an ordered method on a synchronous part can SUCCEED**, creating
  the sketch and the feature in the *other* environment — geometry the user then cannot
  delete. So never "try the other mode" as a fallback: read `ModelingMode` (sync=1,
  ordered=2) and call only that mode's family. If it refuses, say so and let the human
  decide to switch the document.
- **`.Type` on a Face/Edge is the TOPOLOGY kind, never the shape.** `Face.Type` is
  always `igFace`, `Edge.Type` always `igEdge`. Plane-vs-cylinder and circle-vs-line
  live one level down, on `.Geometry.Type` — and that geometry object also carries the
  exact `Radius`. Comparing `Face.Type` with `igPlane` matches nothing, forever.

## Where to look next

Read only the file you need; each is self-contained.

| Task at hand | Read |
|---|---|
| Setting up discovery, dumping the typelib, building the SPY or the action recorder, early binding | `references/discovery.md` |
| A specific HRESULT, binder error, or "it succeeded but nothing happened" | `references/errors.md` |
| A confirmed call signature, enum value, face color, face traversal, bbox, occurrence transform, in-place editing, surface collections | `references/api-signatures.md` |
| Building geometry: sketch+extrude, cylinders, holes, threaded holes, sync vs ordered, placing a part in an assembly | `references/modeling-recipes.md` |
| Putting a button inside SE: add-in registration, ribbon XML, RT_BITMAP icons, HKCU registration, deploy folder | `references/addin-ribbon.md` |
| The EDM electrode flow specifically: burn-surface copy, stitch, attach-to-block, GAP offset | `references/edm-electrode.md` |

## The discovery loop, in order

1. **Dump the type library once** (`ITypeInfo` → `GetContainingTypeLib` → `ITypeLib`),
   seeding one live object per module so all four typelibs get covered. Emit parameter
   **types and direction** (`[out]`, `[in,out]`, `[opt]`) plus enum **values** — types
   are what predict marshaling errors, and a missing `[opt]` marker is what predicts
   `DISP_E_PARAMNOTOPTIONAL`. Grep the output; never load it wholesale into context.
2. **Grep the dump before proposing any call.** The answer is usually already there.
3. **Confirm the member exists on the LIVE object** before building on it. Reflecting
   `Interop.SolidEdge` finds interop *types* that the running Model may not implement —
   this has produced confidently wrong code more than once. Reflection gives you
   signatures and enum values; the live IDispatch decides what is actually callable.
4. **When the API is unclear, have the human do it by hand and watch.** Dump the
   selected feature (SPY) or diff collection snapshots around the manual action
   (recorder). This reverse-engineers the exact call in one round instead of ten.
5. **Watch for the void-returning trap in step 4.** Not every visible modeling action
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
   The file name alone is not enough: a stale build and a fresh one look identical
   until you can see which folder it loaded from.
   The first lines then prove which binary actually ran, which kills the "is this even
   my new code?" ambiguity permanently. (For add-ins this matters doubly — see the
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
