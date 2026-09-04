# API discovery: typelib dump, live-object SPY, action recorder

## Contents
- Full typelib dump ("offline SDK") and its cumulative mode
- Reflecting `Interop.SolidEdge` when the dump comes up short
- Live object dump (home-grown "Solid Edge Spy")
- Manual-action recorder
- The install's DATA tables (magic strings without a run)
- Early binding (optional, for production)

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
  also feeds the persistent, cumulative type-library dump** (see the cumulative-mode note above). A single "Inspect
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

## The install's DATA tables (free answers, no SE run)

The typelib tells you the *shape* of a call; it never tells you the **magic strings** an
`[in] BSTR` will accept (`HoleData.Standard`/`SubType`/`Size`, `ThreadDataByDescription`,
material and style names, pipe classes…). Those come from data files under
`C:\Program Files\Siemens\Solid Edge <ver>\Preferences\`, which are readable on any machine
with SE installed — **no license, no running SE, no round-trip through the human**:

- `HOLES.TXT`, `ISOHOLES.TXT`, `PipeThreads.txt`, `IsoPipeThreads.txt` — plain `;`-separated
  text; each row's "thread type" column is literally the string the API expects.
- `Holes\<Standard>.xlsx` (`ISO Metric`, `ANSI Metric`, `DIN Metric`, …) — the modern hole
  database. **The file name is the `Standard` string**; the sheet names are the hole types
  (`Simple`, `Threaded`, `Counterbore`, `Countersink`) and the `Sub Type` / `Size` columns are
  the other two strings. An .xlsx is a zip: unzip and read `xl/worksheets/sheetN.xml` against
  `xl/sharedStrings.xml` (`xl/workbook.xml` maps sheet names → `rId` → file). No Excel needed.
- `Materials\`, `Gagetable.xls`, `SE-LimitsAndFitsTable*.txt`, `propseed.txt` — same idea for
  materials, gauges, fits and file properties.

Cross-check the location at runtime rather than hardcoding it: many of these paths are
`Application.GetGlobalParameter` values (e.g. `seApplicationGlobalHoleSizeFile=61` → HOLES.TXT,
`seApplicationGlobalHolesDatabaseFolder=498` → the Holes folder), and a site can point them at
a customized copy. Reading the table also tells you what SE *cannot* do: HOLES.TXT has no pitch
column, so anything needing a pitch (a physical thread helix) cannot be driven from it.

## Early binding (optional, for production)

The type libraries ship with the install at
`C:\Program Files\Siemens\Solid Edge 2023\Program\` (`framewrk.tlb`, `part.tlb`,
`assembly.tlb`, `geometry.tlb`, `constant.tlb`, …). Reference them (or the
`Interop.SolidEdge` / `SolidEdge.Community` NuGet packages) behind an
`#if EARLY_BINDING` guard for IntelliSense + compile-time signatures, keeping
`dynamic` as the default portable path. Note: the API Programmer's Guide `.chm` is
**not** guaranteed to be installed (only user-guide `.chm`s under `Program\ResDLLs\`),
so the runtime typelib dump remains the source of truth.
