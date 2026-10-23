# CHANGELOG — solid-edge-com

## 2026-10-23 (4) — learning a company standard from its template assembly

`references/variables.md`: the three read-only passes (links by Revision Manager, poses by `GetMatrix`,
planar faces + cylinders per part, mapped through each occurrence pose); what measuring templates instead
of finished molds corrected; ring profile `End(1 | 8192)` reconfirmed; absolute-position circles for
off-centre patterns; third circle at a centre line's middle.

## 2026-10-23 (3) — boss/side-groove/mid-hole recipes; library parts at template poses

`references/variables.md`: protrusion side picked by ADDED volume (wrong side adds 0); symmetric cut
`AddFiniteMulti(…, 3 = igSymmetric, …)`; side-plane sketch drawn through `Convert3DCoordinate`; circle at a
line's middle with `AddKeypoint(c, 0, line, 2)`; snapshot variables for features that don't follow
formulas; library parts placed with poses read from a template assembly; plate thickness from planar
faces, not from the body box. All measured live on the gaveta/Babyplast mold bases.

## 2026-10-23 (2) — Revision Manager link reading; coaxial counterbore check

`references/api-signatures.md`: read an assembly's links without SE through `RevisionManager.Application`
(`LinkedDocuments` is a parameterized PROPERTY — as a method every file "fails"); used to map which mold
base 253 molds reference. `references/errors.md`: a coaxial counterbore makes a right cut fail a naive
removed-volume check, and deleting it disconnects the document.

## 2026-10-23 — Variables drive geometry; nested assembly editing (SE 2023 v223)

New `references/variables.md`, measured with the throwaway probe `tools/MoldBaseProbe`: a whole plate
(outline, thickness, 4× and 2× hole patterns, pocket) driven by variables in ordered mode, checked by
exact volume after editing. Traps: empty `Formula` for constants, `Add` without units = distance,
cross-file and text formulas kill the collection, localized dimension names (use `SystemName`),
`Profiles.Add(face)` → InvalidCast, `AddFiniteExtrudedProtrusion` returns the Model, `RefPlane.Name`
write → E_FAIL, `OccurrenceDocument.Save()` → modal box that hangs. Nested editing and `Documents.Open`
of a loaded component returning the same, editable object. New golden rule under "Variables".
`references/errors.md`: five rows (incl. `Tuple.Create` with a `dynamic` argument). Which documents the user opened, and `Dirty` after `Documents.Open`.

## 2026-10-23 — in-context `Body.Faces` E_FAIL → false failure → broken feature

`references/errors.md`: on a part in in-place edit, `Body.Faces` can throw `E_FAIL` while features
still go in fine. A face-count success check rejects the good cut, and deleting its ordered profile
on "failure" breaks the feature and cascades `E_FAIL` across the part. Use `Status` as the
fallback and never delete a parent sketch of an existing feature.
## 2026-10-19 — stitch wants bodies; IDispatch arrays for mixed types

`references/edm-electrode.md`: `StitchSurfaces.Add` takes the surfaces' Body objects.
`SAFEARRAY(IDispatch)` can be built from an IID_IDispatch interface. A stitch consumes its inputs
(dead proxies break `dynamic` calls). Uniting works with `Attach(Body[])`. Added the
rim/hole/seam classification of open edges.

## 2026-10-19 — destructive trap: SaveCopyAs/Open around an in-context part

`references/errors.md`: running `SaveCopyAs` on a part in in-place edit, then opening and closing
the copy, unloaded the body of the original from memory and lost the unsaved work.

## 2026-10-19 — burn-surface copy without in-place (SE 2023 v223)

`references/edm-electrode.md`: the copy is solved via `CopyConstructions.Add` from the cavity
file. Face IDs are preserved, `AddByMatrix` places the copy, `CopySurfaces` runs intra-part
and the parent can be deleted. Colors survive in ordered only. Measured with a throwaway probe.

## 2026-10-19 — PathFinder order (SE 2023 v223)

New section in `references/assembly-relations.md`: `Occurrences.ReorderOccurrence`'s
`AfterTarget` flag does the opposite of its name (`true` = before), measured on a throwaway
assembly, with the sorting recipe and its timing.

## 2026-10-19 — drawing views and dimensions (SE 2023 v223)

New section in `references/draft-properties-events.md`, measured while building the AutoEDM
"Desenho 2D" button: arbitrary view orientation, the view-local space shared by
`ModelToView` and the DV elements, and the rule that dimensions attach to
`dvElement.Reference` (the raw DV element is read in sheet space). Per-type table of the
dimension calls that are born wrong and the property flip that fixes each, plus tolerance
display types, named-style switch and the title-block refresh. Second pass: placement by
key points copied from a hand-made drawing, and GD&T (FCF symbol codes from
`DraftFCF.txt`, AddByTerminator + AddVertex placement, datum symbols, hidden edges).

The SKILL.md description and routing table now name 2D drafting (views, dimensions,
tolerances, GD&T, balloons, title block, PDF export), so drawing work triggers the skill.

## 2026-10-17 — reconnaissance pass against SE 2026 build 226.00.08.004 x64

Everything below came out of a dedicated recon project (`E:\Code\SE-API-Recon`): a full
static inventory of the installed type libraries, a cross-check against the interops the
install ships, and ~250 recorded COM calls against a live Solid Edge. Each change says
whether it is new, a correction, or a downgrade in confidence.

### First: the two copies had diverged

The skill existed in two places and **neither was a superset of the other**:

| | `AutoEDM\.claude\skills\solid-edge-com` | synced plugin cache |
|---|---|---|
| date | 2026-10-12 | 2026-10-01 |
| had `mesh-reverse.md` (21 KB) | **yes** | **no** — though its own index linked to it |
| `api-signatures` / `modeling-recipes` / `errors` / `addin-ribbon` | larger | smaller |
| "Assembly relations — 45 relations" section | **missing** | **present** |
| "Modeling traps confirmed 2026-09-30" section | **missing** | **present** |

So the plugin that actually loaded in sessions was 11 days stale **and** had a dead link,
while the newer local copy had silently **lost** the two assembly/modeling sections.

Also, the local folder's layout was broken: the files sat flat at the root while `SKILL.md`
pointed at `references/…`. Only the packaged `.skill` (which does have `references/`) was
internally consistent.

**This release merges both**, keeps the `references/` layout everywhere, and restores the
lost sections.

### Structure

`SKILL.md` was ~19 KB of mixed rules and long explanations. It is now a lean entry point —
when it applies, what is on the machine, one-line golden rules with pointers, the discovery
loop, the index — and the prose moved into references. New files:

- **`references/connecting.md`** — attaching, the message filter, hangs, and the
  wrong-instance trap.
- **`references/com-rules.md`** — the long form of the marshaling and enum rules, with the
  measurements behind them.
- **`references/localization.md`** — the API speaks the install's language.
- **`references/inventory-and-binding.md`** — the installed inventory, the shipped
  interops, early binding on .NET 10.
- **`references/assembly-relations.md`** — the restored 45-relation section, re-tested and
  re-graded.
- **`references/draft-properties-events.md`** — draft sections, file properties, events,
  and the little that is confirmed about sheet metal / weldment / frames.

And a new **`scripts/`**: `ApiDump` (reads every typelib with `REGKIND_NONE` — no license,
no running SE), `apiq.mjs` (queries the 45 MB inventory), `make-index.mjs`, `LiveProbe`
(asks the running SE what it really exposes), plus **~2.4 MB of pre-built greppable
indexes** — 21 400 members, 7 800 enum constants, 3 700 types.

### NEW — things the skill did not know

- **Names in the API are LOCALIZED.** On a pt-BR install
  `Properties.Item("Title")` → `DISP_E_BADINDEX` (it is `"Título"`) and
  `FaceStyles.Item("Blue")` → `0x80040B50` (it is `"Azul"`). Property-**set** names and
  ProgIDs are not translated; property names, face-style names and draft sheet names are —
  and "2D Model" stayed English, so not even the translation is consistent. The skill
  already knew SE's **data files** were localized; this extends it to the **API surface**.
  → `localization.md`
- **`Activator.CreateInstance(ProgID)` does not attach — it starts a second, invisible
  instance.** Measured: PIDs `[29076]` → `[29076, 19632]`, `Visible == False`, no window,
  and 61 test calls ran to completion in the phantom session while the user's did not move.
  Never use it as a connect fallback. → `connecting.md`
- **A busy Solid Edge blocks ROT binding, and the message filter cannot help.**
  `GetActiveObject` was measured hanging ~28 s and, in another session, past 90 s, with
  **zero** filter retries — a hang is not a rejection, so the filter is never consulted.
  In the 90 s case SE was alive, responsive, with no modal dialog, and burning ~79 % of a
  core. `Marshal.GetActiveObject` from .NET Framework hung identically, so it is not the
  P/Invoke. Give connect its own timeout on its own thread. → `connecting.md`
- **The install's own interops make early binding viable**, including on **.NET 10**:
  13 × `interop.SolidEdge*Lib.dll` at the exact build version, so no `tlbimp` (usually
  absent) and no community NuGet. Referencing them compiles but does **not** load — resolve
  them at runtime from the install folder. → `inventory-and-binding.md`
- **The registry key moved vendors.** `HKLM\SOFTWARE\Unigraphics Solutions\Solid Edge\…`
  **does not exist** on 2026; it is `HKLM\SOFTWARE\Siemens\Solid Edge\Version 226\…`.
  Discovery code that only checks the old path fails silently.
- **The same enum NAME can hold DIFFERENT values in different typelibs.**
  `ApplicationGlobalConstants` has 497 members in `constant`/`framewrk` and 24 in
  `DesMgr`/`RevMgr`, shifted — `seApplicationGlobalOfflineMode` is 25 in one and 5 in the
  other, and these feed `GetGlobalParameter`. 20 constants collide this way; `DocumentStatus`
  too. → `com-rules.md`
- **There is no single integer type for enum comparisons.** `igFeatureOK` exceeds
  `int.MaxValue` (needs `uint`); `igCylinder` is **negative** as int32.
- **A new DraftDocument has SIX sheets, not one** — `Sheets` mixes 1 working + 4 background
  + 1 2D-model. Only `Sections` (dispid 4199) separates them, so "iterate the sheets" will
  process four format templates. → `draft-properties-events.md`
- **Events need a message pump** in a console STA, or they are never delivered — no error,
  no warning. Free inside an add-in. → `draft-properties-events.md`
- **A hidden `[lcid]` parameter** on the `_I…Auto` dual interfaces, injected by the CLR and
  invisible from C#. With `[retval]` it explains most "arity does not match" confusion.
- **Add-ins load lazily** — on a freshly started SE with no document open, **no** managed
  add-in was loaded at all, not even Siemens' own. An empty add-in log proves nothing until
  a document is open. → `addin-ribbon.md`
- **`ISolidEdgeAddIn` vs `ISEAddIn`** — you implement the first, SE hands you the second;
  confusing them fails silently. Plus the exact registry keys read off two real add-ins,
  the **.NET 10 comhost** route (`mscoree.dll` cannot load .NET 10), and `Cookie` being
  simply `CLSID.Data1`, so a correct Cookie proves only that SE *read* your registration.
  → `addin-ribbon.md`

### CORRECTED — claims that measurement contradicted

- **`ParameterModifier` is not always needed.** The old text said `[out]` params "come back
  empty in late binding unless you mark them by-ref with a `ParameterModifier`". Measured on
  both `Occurrence.GetTransform` (`[out] double*`) and `Face.GetRange` (`[in,out]
  SAFEARRAY`), with the part deliberately moved so a correct zero could not be mistaken for
  a failed one:
  `dynamic` + `ref`/`out` **works and needs no modifier**; `Type.InvokeMember` **without**
  one returns plausible **all-zero values, silently**. The hazard is real but belongs to
  `InvokeMember`. **Prefer `dynamic`.** → `com-rules.md`
- **`object[]` in a SAFEARRAY fails with `E_INVALIDARG`, not `DISP_E_TYPEMISMATCH`.** With
  `dynamic` on .NET 10 the C# binder fails the conversion before the call leaves the
  process ("Could not convert argument N"), so grepping a log for `DISP_E_TYPEMISMATCH`
  finds nothing. The conclusion the old note drew is unchanged and still important: the call
  never reached SE, so it says nothing about what SE can do.
- **The `SurfaceByBoundaries.Add` direction divergence is the NuGet's, not the API's.** The
  old note said reflecting `Interop.SolidEdge` imports an argument as `out` where the dump
  says `[in,out]`. On the interops that **ship with the install** that does not reproduce —
  direction comes through faithfully (`ref`, In+Out). The divergence belongs to the
  community package the note names. 46 294 members were cross-checked static-vs-shipped:
  **zero** arity divergences. (The NuGet itself could not be re-verified; it is not
  installed here.)
- **`dynamic` is not broken from outside the SE process.** `errors.md` claimed `dynamic`
  cannot bind from a probe exe/PowerShell, "so even `doc.Name` fails", and recommended
  `Type.InvokeMember` instead. An external .NET 10 console ran **~250** `dynamic` calls —
  `doc.Name`, `Models`, `Body`, `Faces[q]`, `Style`, `Properties`, `GetTransform` — with no
  binder problem. Downgraded to situational. This one mattered: the old advice pushed you
  onto `InvokeMember`, which is precisely the path with the silent-zeros out-param trap.
- **`RuntimeBinderException` first means the member does not exist.** Three such failures in
  one session were all wrong names: `HoleData.Diameter` → `HoleDiameter`;
  `FaceStyle.Name` → `StyleName`; and `IgnoreSavedDefaultValues` is a **parameter** of
  `HoleDataCollection.Add` (the 21st), **not a property**. HRESULT `0x80131500` is the
  late-binding equivalent of `DISP_E_MEMBERNOTFOUND` — query the inventory, do not try
  variations, and do not conclude "SE can't do this".
- **The add-in display name does not come from the registry.** With
  `Summary\409 = "SE-API-Recon Probe"` readable through HKCR, SE still wrote `"Sem Título"`
  into its own `AddIns` entry. Comparing three installed add-ins, none of the three registry
  candidates explains all three names. The evidence points at **`ISEAddIn.Description`**
  (dispid 3), set by the add-in inside `OnConnection`. Set it there.

### DOWNGRADED — field claims that a controlled re-test could not reproduce

Kept, with the conditions stated, because the condition is the useful part.

- **`ConstrainingPoint = (0,0,0)` did not break the relation.** The field note (45 relations
  on a mold base) said it leaves relations `Unsatisfied` and occurrences `OverDefined`. With
  **one** planar relation between two blocks, `(0,0,0)` gave `DetailedStatus = 1 (Solved)`
  and `Status = 4 (UnderDefined)` — same as real vertex points. Over-definition is a
  phenomenon of the *set*; a meaningless point only matters when several relations compete
  for the same degrees of freedom. Passing a real vertex is still good practice, but **not**
  for the reason "otherwise it fails". Reproducing the failure needs a multi-relation case,
  which was not done. → `assembly-relations.md`
- **`NormalsAligned` is now marked DISPUTED.** The field note says the flag reads backwards
  (`true` = contact); `api-signatures.md` still carried the opposite (`True = Align`). The
  re-test used *perpendicular* faces, so the 90° result was correct either way and settled
  nothing. Both readings are flagged in place, with a note on how to test it properly.
- **The message filter's retry path was never exercised.** It registered and revoked
  correctly across six runs, but all counters ended at zero — SE was idle. Keep it; do not
  treat "it worked" as evidence the retry logic is right.

### CONFIRMED with exact values

Auto-ground on `Occurrences.AddByFilename` (0→1→2 `GroundRelation3d`, occurrence
`Status = 2 Fixed`) · `igFeatureOK = 1216476310` compared as `uint` · `Face.Type = igFace
(167551075)` vs `Face.Geometry.Type = igCylinder (-114972029)`, both from
`GNTTypePropertyConstants` · `Face.Geometry.Radius` exact (Ø8 hole measured 8.000 mm) ·
lengths in metres · `GetTransform` angles in radians (π/2 exactly) · collections 1-based ·
`igQueryAll = 1` (not 0) · `Models.AddFiniteExtrudedProtrusion` needing a typed SAFEARRAY ·
a new `PartDocument` being synchronous · `Relations3d.AddPlanar` having 5 parameters and
**zero** optional ones · all of `Relation3dDetailedStatusConstants` and
`OccurrenceStatusConstants`.

### Known gaps, stated on purpose

No sheet-metal feature was created (only the document shell). Weldment, frames and meshes
are typelib-only — **`mesh-reverse.md` was deliberately left untouched**, because replacing
21 KB of field-tested reverse-engineering with a thinner static summary would trade verified
knowledge for weaker knowledge. Drawing views from a model, `AddAxial`, explicit `AddGround`
and `Holes.AddSync` were not tested. `GetGlobalParameter` was not executed (its enum
conflict is confirmed statically). A **.NET 10 comhost add-in was proven sound as a COM
object** — `CoCreateInstance` and `QueryInterface(ISolidEdgeAddIn)` both succeed, .NET 10
loads beside .NET Framework 4 — but SE never called `OnConnection`, and whether that is SE
requiring manual approval of a new add-in or a rejection of the comhost specifically was
**not determined**. Only one SE version and one UI language exist on the measured machine,
so 2023-vs-2026 comparisons and the English-install property indices are unverifiable here.
