---
name: solid-edge-com
description: >
  Automate Solid Edge (2023/2026) from C#/.NET through COM — discover the API from the
  machine's own type libraries instead of guessing signatures from memory. Use this skill
  for ANY Solid Edge automation task: reading faces, colors, geometry or occurrence
  transforms; creating parts and features (extrude, holes, threaded holes, surfaces,
  booleans, surface-to-solid); reading facet/mesh bodies and reverse-engineering a scanned
  mesh back into surfaces; placing parts and creating assembly relations that really
  govern them; editing in the context of an assembly; generating 2D drafts from a model
  (sheets, view orientation, dimensions attached to model edges, tolerances and fits,
  GD&T feature control frames and datums, balloons, title-block text, PDF export);
  file properties; events; building
  ribbon add-ins (including .NET 10 via comhost); and debugging COM errors such as
  RPC_E_CALL_REJECTED, RPC_E_DISCONNECTED, DISP_E_TYPEMISMATCH, E_INVALIDARG,
  E_NOINTERFACE, RuntimeBinder failures, out-parameter marshaling, hangs on connect, and
  calls that silently hit the wrong Solid Edge instance. Trigger it whenever "Solid Edge",
  SolidEdgeFramework/SolidEdgePart/SolidEdgeGeometry/SolidEdgeAssembly, EDM electrode
  automation, or COM automation of a CAD system come up — even if the user never mentions
  the SDK, and even if the request looks like a plain C# question.
---

# Solid Edge COM automation

**The one rule this skill exists to enforce: never guess a signature from memory.**
A wrong guess costs a round-trip through a licensed Solid Edge, and the failure modes are
often *silent wrong results* rather than exceptions. Introspect first, then call.

## What is actually on the machine

The Siemens SDK docs need a WebKey login and the `SESDK.chm` is usually absent. That does
**not** mean the API is undiscoverable — the opposite:

- **Type libraries are installed.** 33 `.tlb` in the SE `Program` folder, plus **11 more
  embedded** in `.dll`/`.exe`/`.ocx`/`.dcx` (so a `*.tlb`-only sweep misses `PropAuto.dll`,
  `SEScreenCapture.dll`, `autosketch.dll`, `basic.dcx`, and `SECatiaV5Trans.exe`, which has
  no `.tlb` at all). Readable with `LoadTypeLibEx` + `REGKIND_NONE` — no license, no
  running SE, nothing registered.
- **Managed interop assemblies ship WITH the install.** 13 × `interop.SolidEdge*Lib.dll`
  in `Program`, versioned to the exact build (e.g. `226.0.0.106`). You do **not** need
  `tlbimp` (often absent) and you should **not** use the community NuGet `Interop.SolidEdge`,
  which lags the installed build by years.
- **Early binding therefore works**, including on **.NET 10** — see
  `references/inventory-and-binding.md`. Late binding with `dynamic` remains the default
  for portability, but "I can't reference the types" is no longer true.

Measured against **SE 2026, build 226.00.08.004 x64**. Where a fact is version-specific it
says so.

## Golden rules

Each line is a failure that has actually happened. Follow the pointer for the long form.

**Units and indexing**
- **Lengths are METERS.** 20 mm = `0.020`.
- **Angles have NO single rule** — `Occurrence.GetTransform` returns radians (π/2 measured
  exactly); `HoleData.ThreadTaperAngle` is radians; `BottomAngle`/`CountersinkAngle`/chamfer
  angles are degrees (`118`, not `2.06`) — **in the same object**. Check the *property*.
- **Collections are 1-based.** And `igQueryAll` is **1**, not 0.

**Process and threading**
- **x64 only**; a 32-bit build will not connect. **STA thread required** (`[STAThread]`).
- **Install an OLE message filter**, retrying on **both** `SERVERCALL_RETRYLATER` *and*
  `SERVERCALL_REJECTED`, with a bounded timeout.
- **The message filter does NOT protect you from hangs.** `GetActiveObject` has been
  measured hanging ~28 s with **zero** filter retries, because a hang is not a rejection.
  Give connect its own timeout on its own thread.
- **To attach, only `GetActiveObject` works.** `Activator.CreateInstance(ProgID)` **starts a
  SECOND, INVISIBLE instance** (`Visible == False`, no window) and everything then "works"
  in a session the user cannot see. Never use it as a connect fallback; snapshot
  `Process.GetProcessesByName("Edge")` before and after and abort if a PID appeared.
  → `references/connecting.md`

**Marshaling**
- **Out-params: it depends on WHICH late binding.** `dynamic` with `ref`/`out` works and
  needs **no** `ParameterModifier`. `Type.InvokeMember` **without** a `ParameterModifier`
  returns **plausible all-zero values** — silently. Prefer `dynamic`.
- **`[in,out]` needs the array to arrive ALLOCATED.** `Face.GetRange`, `Vertex.GetPointData`:
  `ref null` → `E_INVALIDARG`. (`Occurrence.GetTransform` is `[out]` pure — not the same case.)
- **SAFEARRAY params need a TYPED element array**, never `object[]`. With `dynamic` the
  failure is `E_INVALIDARG` "Could not convert argument N" — the call **never reaches SE**,
  so it says nothing about whether SE can do the operation. You can build the typed array
  without a compile-time reference (`Array.CreateInstance` + resolve the interop from the
  install folder).
  → `references/com-rules.md`

**Silent wrong results**
- **A feature `Add` that doesn't throw may still have failed.** Check
  `feature.Status == igFeatureOK (1216476310)` **as UInt32** after every `Add`. SE does
  **not** roll a failed feature back — `Delete()` it before retrying.
- **Then MEASURE the geometry.** Status OK is not proof: read the face back
  (`Body.Faces[igQueryCylinder]` + `Geometry.Radius` / `GetRange`).
- **A successful `Add` can hand back something you didn't ask for** — flat enum namespaces
  accept any legal-somewhere value, and SE merges the user's saved dialog defaults unless
  you pass `IgnoreSavedDefaultValues`. Note it is a **parameter**, not a property.
- **`.Type` on a Face/Edge is TOPOLOGY, never shape.** `Face.Type` is always `igFace`;
  the shape is `Face.Geometry.Type`, and `Face.Geometry` also carries the exact `Radius`.
- **An enum that INDEXES is rarely the enum a property RETURNS**
  (`FeatureTopologyQueryTypeConstants` vs `GNTTypePropertyConstants`).
- **There is no single integer type for comparing enums.** `igFeatureOK` needs `uint`
  (it exceeds `int.MaxValue`); `igCylinder` is **negative** as int32. Check the value first.
- **The same enum NAME can hold DIFFERENT values in different typelibs.**
  `ApplicationGlobalConstants` has 497 members in `constant.tlb`/`framewrk.tlb` and 24 in
  `DesMgr.tlb`/`RevMgr.tlb`, shifted — `seApplicationGlobalOfflineMode` is 25 in one and 5
  in the other. Wrong numbering reads or writes the **wrong global parameter, with no error**.
  → `references/com-rules.md`

**Names are not always in English**
- **Many `BSTR` keys are LOCALIZED.** On a pt-BR install
  `Properties.Item("Title")` → `DISP_E_BADINDEX` (it is `"Título"`) and
  `FaceStyles.Item("Blue")` → `0x80040B50` (it is `"Azul"`). ProgIDs and property-**set**
  names are not translated; property names, face-style names and draft sheet names are
  (and "2D Model" stayed English — not even the translation is consistent). SE's **data
  files** are localized too (the hole database sheet is "Rosqueado", not "Threaded").
  Enumerate and match a list of aliases, or key off a number instead of a name.
  → `references/localization.md`

**Environment discipline**
- **Never set `ModelingMode` on the user's document — declare the environment instead.**
  Switching creates orphan sketches the user cannot delete, and kills every face proxy read
  before the switch. Exceptions: a document your own code just created, and a switch the
  user asked for in that click. A new `PartDocument` is **synchronous** (`ModelingMode == 1`).
- **ORDERED is not ASSOCIATIVE.** Build the sketch plane from the geometry the user picked,
  and tie elements to projected model edges — otherwise the feature cuts air at the user's
  next sync edit, with no error at creation time.
- **Disable the command in the wrong environment, don't just fail in it.** A greyed-out
  button teaches the constraint; an error dialog after the click only reports it.
- **A closed profile's ORIENTATION decides what `igLeft`/`igRight` means.** Circles come out
  counter-clockwise so hole cuts "just work" and hide the rule; a triangle drawn the other
  way removed 17 700 mm³ instead of 1 060. Measure the loop's signed area in sketch
  coordinates and reverse if negative.
  → `references/modeling-recipes.md`

## The discovery loop, in order

1. **Build the inventory once, from the machine.** Read every type library — the loose
   `.tlb` **and** the embedded ones — via `ITypeLib`/`ITypeInfo` with `REGKIND_NONE`, and
   emit parameter **types and direction** (`[in]`/`[out]`/`[in,out]`/`[opt]`/`[retval]`/
   `[lcid]`), DISPIDs, vtable offsets, defaults and enum **values**. Direction is what
   predicts marshaling errors; a missing `[opt]` is what predicts `DISP_E_PARAMNOTOPTIONAL`.
   `scripts/ApiDump` does this. The result is tens of megabytes — **never load it whole**.
2. **Query the inventory before proposing any call.** `scripts/apiq.mjs` answers by type,
   member, exact signature, enum, reverse value lookup, events and out-params:
   ```
   node scripts/apiq.mjs sig "Face.GetRange"
   node scripts/apiq.mjs value 1216476310
   node scripts/apiq.mjs members HoleData | grep -i diameter
   ```
   The answer is usually already there. A member you "remember" that the inventory doesn't
   list does not exist.
3. **Read the install's DATA tables for any string argument.** The typelib gives the *shape*
   of a call and never the magic strings an `[in] BSTR` accepts — hole standards, thread
   descriptions, materials, fits. They live under `Preferences\` in the install, readable
   without a license and without running SE, and they are **in the install language**.
   Reading the table also reveals what SE *cannot* do.
4. **Confirm the member exists on the LIVE object before building on it.** Static metadata
   finds types the running object may not implement. Ask the object: `IDispatch` →
   `GetTypeInfo(0)` → `ITypeInfo` gives the real interface name and member list.
   On the shipped interops static and live agree closely (46 294 members cross-checked,
   zero arity divergence); on the **community NuGet** they do not — that package is the
   one that imports a `SurfaceByBoundaries.Add` argument as `out` where the typelib says
   `[in,out]`. Where sources differ, the live object wins.
5. **When the API is unclear, have the human do it by hand and watch.** Dump the selected
   feature (SPY) or diff collection snapshots around the manual action (recorder). One
   round instead of ten.
6. **Watch for the void-returning trap in step 5.** Not every visible action creates a tree
   feature; some apply geometry with no record, so the recorder reports "nothing changed"
   on a real success. The tell is an *existing* item migrating between collections rather
   than a new name appearing.

## Where to look next

Read only the file you need; each is self-contained.

| Task at hand | Read |
|---|---|
| Connecting, message filter, hangs, hitting the wrong instance, `Visible`, `DisplayAlerts` | `references/connecting.md` |
| The long form of the marshaling and enum rules above, with measured evidence | `references/com-rules.md` |
| A name lookup failed, or anything involving `BSTR` keys, UI labels or SE data files | `references/localization.md` |
| Building and querying the API inventory; early binding; the shipped interops; .NET 10 | `references/inventory-and-binding.md` |
| A specific HRESULT, binder error, or "it succeeded but nothing happened" | `references/errors.md` |
| Setting up discovery, the typelib dumper, the SPY, the action recorder | `references/discovery.md` |
| A confirmed call signature, enum value, geometry-type enums, face color, face traversal, bbox, occurrence transform, profile plane frame, in-place editing, surface collections, cutouts, ordered-tree groups, feature↔body face IDs, document properties, ribbon GuiVersion cache | `references/api-signatures.md` |
| Building geometry: sketch+extrude, cylinders, holes, threaded holes, annular grooves and revolved cuts, sync vs ordered | `references/modeling-recipes.md` |
| Placing parts, ground/contact/flush/axial relations, and why relations say "solved" and govern nothing; reordering occurrences in the PathFinder (`ReorderOccurrence` flag is inverted) | `references/assembly-relations.md` |
| Draft sheets and sections (a new draft has **6** sheets, not 1), drawing views in any orientation, dimensions from code (attach to `.Reference`, born-wrong flips, keypoint placement), tolerances, GD&T frames and datums, balloons, title block, PDF export, file properties, events and the message pump | `references/draft-properties-events.md` |
| Putting a button inside SE: add-in registration, the two interfaces people confuse, ribbon XML, RT_BITMAP icons, HKCU registration, deploy folder, .NET 10 comhost, modeless dialogs, picking a face or edge | `references/addin-ribbon.md` |
| The EDM electrode flow specifically: burn-surface copy, stitch, attach-to-block, GAP offset | `references/edm-electrode.md` |
| Meshes and reverse engineering: reading facets, what SE will and will not fit for you, sectioning a mesh, writing a surface recogniser, mesh → solid with boundary corrections, choosing the extrusion direction, band steps and hairline gaps, batch conversion, verifying by deviation | `references/mesh-reverse.md` |

## Working loop when a human runs SE for you

When the dev machine has no license, a human runs the built app inside SE and sends back a
log. That round-trip is the scarce resource; spend it well. (When SE *is* on the machine,
steps 1, 5 and 6 still apply — they are about not corrupting the user's work.)

1. **Non-destructive first.** A new probe only reads, introspects and writes a log. No
   geometry, no save, until the API is confirmed.
2. **One question per run.** Each log answers one unknown.
3. **Numbered logs** (`App NNN.log`) so runs are comparable.
4. **Close the GUI before rebuilding** — a running WinExe locks the output exe, and a
   loaded add-in locks its DLL (hence the separate deploy folder).
5. **Stamp the loaded build in the log — with the full PATH** plus
   `File.GetLastWriteTime`. The file name alone is not enough: a stale build and a fresh
   one look identical until you can see which folder it came from. This kills the "is this
   even my new code?" ambiguity permanently.
6. **Isolate risky operations from the working deliverable.** A feature that fails mid-op
   can poison the document proxy so every *subsequent* call fails.
7. **When a tool "finds nothing", make it dump the structure** — occurrences, bodies, face
   counts — and read the target from *every* plausible source. A silent `continue` on a
   failed read hides the real cause.
8. **Work only in documents you created, in a scratch folder.** Snapshot
   `Documents.Count` first; if the user has something open, say so and stop rather than
   risk it. Save and restore `Application.DisplayAlerts` around the run.

## Reporting findings back

When a run confirms or refutes something, record it in the matching reference file with
the date and how it was confirmed. Three habits keep these files trustworthy:

- **State which source confirmed a fact**, because they disagree. Static metadata and
  reflection are cross-checks; the live object wins.
- **Say when something was NOT reproduced, and under what conditions you tried.** A claim
  that held in a 45-relation mold base and not in a 1-relation test case is not wrong — it
  is *conditional*, and the condition is the useful part.
- **Keep corrected mistakes, marked as corrected.** Several entries here describe a
  mechanism that was chased for days and turned out wrong. The wrong path earns a
  paragraph precisely because the next reader would otherwise reason their way into it
  again.
