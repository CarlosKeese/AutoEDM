# EDM electrode workflow (AutoEDM project)

Specific to the electrode workflow. The general discovery method lives in SKILL.md;
the call signatures in `references/api-signatures.md`.

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
     throws `DISP_E_TYPEMISMATCH` (the general SAFEARRAY-typing rule in `api-signatures.md`,
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
     the collection genuinely exists live (see the error table's PIA-mismatch row in `errors.md`).
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
   confirmed from the typelib dump (now capturing `SolidEdgeGeometry` live too — see `discovery.md`):
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
   SHRINKS" rule, per the project's Ra→offset table), `FaceOffsetBlendType=194`
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
was stripped from it, per the project's own decision log) → attach surface to block (non-feature
action, or fall back to União — see above) → switch to
**Ordered** modeling → add the GAP offset (`Model.FaceOffsets`, above) → exit in-context edit back
to the assembly. `Application.ActiveDocument` reports the electrode **part** (`Type=1`) the whole
time it's in-context — the Gravador's "Iniciar leitura"/"Gravar log" clicks must both land while
that part window still has focus (see the recorder-robustness note in the project's decision log
about `ConfirmDocParaGravacao` — it only warns when the doc ISN'T a part, not when it's the WRONG
part; a first attempt this same day accidentally snapshotted the master/cavity part instead of the
electrode because "Iniciar leitura" was clicked too early, before entering the electrode's
in-context edit).
