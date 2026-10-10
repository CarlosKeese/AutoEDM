# Validated modeling recipes

## Contents
- Solid modeling that works (sketch + extrude)
- Positioned-part workaround (replaces inter-part copy)
- Synchronous vs ordered
- `ProfileSets` sketches
- Fixation holes
- Annular grooves (O-ring / snap-ring housings)
- Threaded holes / physical threads

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

**Who owns the sketch decides whether you delete it** (2026-09-08, from a part the user
could not clean up):

- **Ordered feature, ordered sketch** — the sketch is the feature's own child. *Leave it.*
  Deleting it kills the feature you just built, and the user deleting the feature takes the
  sketch with it. This is the clean case: put sketch-driven cut/revolve features in an
  **ordered** document and there is nothing to clean up.
- **Synchronous feature, ordered sketch** — nobody owns it. You *must* delete it, and it is
  exactly the case where `Delete()` can refuse.
- **Prefer the first.** Rather than building a cleanup mechanism for the second, require the
  ordered environment for the command (and grey the button out elsewhere).

**`ProfileSet.Delete()` can no-op without throwing — verify by count.** A locked sketch does
not raise; the call returns and the sketch is still there. A bare `try { ps.Delete(); } catch {}`
therefore reports success on precisely the failure you care about, and a *silent* catch reports
nothing at all — the first sign is the user's dirty part, with no line in the log. Read
`doc.ProfileSets.Count` before and after and log loudly when it did not drop.

**Cleaning up after an ordered feature: hide the `Profile`, not the `ProfileSet`.** A sketch
consumed by an ordered feature stays in the tree — correctly, it belongs to the feature — but on
screen it is a knot of black curves sitting on the geometry, and **`ProfileSet` has no `Visible`
property at all** (checked in the typelib dump, SE 223), so the user cannot switch it off from
the UI either. What does have `Visible` is the **`Profile`** inside it, and that is what draws.
So after the feature succeeds: set `profile.Visible = false` first — it is the only step that
fixes what the user actually sees — and only then try `ProfileSet.Delete()`.

**`SetAxisOfRevolution` leaves a `RefAxis` behind, and it draws too.** It is a document object in
its own right, not just the construction line in your sketch, and it carries its own `Visible`.
Hiding the profile alone still leaves that stroke on screen. Hide both.

**Verify a cleanup deletion two ways, not one.** After deleting a sketch a feature consumed,
check (a) that the collection actually shrank — `Delete()` on a consumed ordered sketch tends to
refuse *without throwing* — and (b) that the **feature is still there**, by body face count.
Without (b), a delete that took the feature with it would be logged as `CREATED ✓` over a part
with no groove in it.

**A `ProfileSet` takes exactly ONE `Profile` — the second `Profiles.Add` returns `E_FAIL`.**
Confirmed on a real part (2026-09-08) by breaking it: a routine that probed three base planes
was "optimised" to reuse a single probe set, one profile per plane. Plane 1 read fine, planes 2
and 3 threw `E_FAIL`, no plane matched, and the feature silently stopped being created — the
only symptom was two warning lines and zero geometry. The plural collection name promises
nothing; it holds one.

So **probe sketches are sketches, and you cannot pool them**. Counting them still matters — one
throwaway `ProfileSet` per base plane, times every item in a batch, is a lot of sketches — but
the answer is to **delete each one right after it answers, and verify**, not to share a set.

**Edges of a 3D sketch DIE after every feature you add (live, 2026-09-24).** Lines of a 3D sketch
are reachable only as topology — `Constructions.Sketch3DFeatures[i].Edges[igQueryAll=1]` →
`Edge.GetEndPoints` (the interop 219 has no 3D-sketch types at all). `RefPlanes.AddNormalToCurve` on
such an edge worked for the FIRST hole; every later call with edges read before it returned
`E_FAIL` — the model recompute invalidated them. Re-read the edge (match by endpoints) right before
each use, or avoid it: for axis-parallel channels a plane `AddParallelByDistance` of the base plane
perpendicular to the channel, at the entry, needs no edge at all (and also serves an entry that is
not on the line).

**Fixation holes (validated recipe).** Mark centers with **`Profile.Holes2d.Add(x,y)`** —
NOT `Circles2d` (a plain circle makes the hole feature create **zero holes, with no error**).
Then `HoleData = PartDocument.HoleDataCollection.Add(HoleType, DiameterMeters, …)` with
`HoleType = igRegularHole (33)`; and on a synchronous part
`Model.Holes.AddSync(1, Profile[] TYPED, ProfilePlaneSide, ExtentType, depthMeters, HoleData)`
with `ExtentType = igFinite (13)` for a **blind** hole (ordered part → `AddFinite(Profile,
side, depth, HoleData)`). Then delete the ProfileSet. **One `Holes2d.Add` per profile** —
two centers in one profile yields only **one** hole.

**Annular grooves (O-ring housings, snap-ring grooves, reliefs) — one recipe covers all three
placements.** A groove in a flat face, a groove around a shaft and a groove inside a bore look
like three different features, but every one of them is *a rectangle revolved 360° about the
same axis*. Only where the rectangle sits changes (radial offset and axial position), so build
one modeling path and three ways to place four points — not three feature paths to maintain.

```
axisDir  = unit vector of the groove axis (from the circular edge's degenerate GetRange span)
plane    = a sketch plane CONTAINING that axis     (find/verify it with the profile frame, below)
frame    = ProfilePlaneFrame.Discover(profile)     (Convert2DCoordinate ×3)
radial   = normalize(cross(frame.Normal, axisDir)) (which sign does not matter — it revolves)
P(a, r)  = axisPoint + axisDir*(a - axisPoint·axisDir) + radial*r
           → Convert3DCoordinate → Lines2d.AddBy2Points ×4
axisLine = Lines2d along the axis → ToggleConstruction → SetAxisOfRevolution → RefAxis
profile.End(igProfileClosed|Single|NoSelfIntersect|RefAxisRequired|NoRefAxisIntersect = 61)
RevolvedCutouts.AddFiniteMulti[Sync](1, SolidEdgePart.Profile[], refAxis, Type.Missing, Type.Missing)
ProfileSet.Delete()                                  // ALWAYS — see the ordered-sketch rule above
```

- **`Profile.End(criteria)` RETURNS a status** — log it. Discarding it is how an invalid profile
  turns into a mute `E_FAIL` three calls later. Ask for the revolve criteria (61), not just
  `igProfileClosed`: the axis requirement is part of what makes SE bind the RefAxis to the
  profile. Caveat from the field: `ProfileValidationStatus` only declares `0` (valid) and `−1`
  (invalid), but a real `End(61)` returned **−104** on a profile that then cut perfectly. Treat
  the value as a diagnostic to log, **not** as a gate to abort on.
- **What actually cuts the groove (confirmed on a real part, ordered, 2026-09-04):**
  `RevolvedCutouts.AddFiniteMulti(1, SolidEdgePart.Profile[], refAxis, Type.Missing, Type.Missing)`
  — the MULTI form, with the **angle OMITTED**. Two things had to be right at once:
  the typed `Profile[]` (an `object[]` marshals as SAFEARRAY(VARIANT); SE wants
  SAFEARRAY(IDispatch)), and letting SE default the angle. **Passing 2π explicitly returns
  `E_FAIL`**, and so does the single-profile `AddFinite[Sync](profile, refAxis, ProfileSide, …)`
  in every shape tried (both sides, both modes, several parts). A closed profile has no side to
  pick, which is probably why the form that demands one is the one that refuses.
- **NEVER fall back to the other modeling mode's method.** Calling `AddFinite*` (ordered) on a
  synchronous part does not fail cleanly — SE creates the sketch and the feature in the *other*
  environment, and the user ends up with a feature they cannot delete. Read
  `PartDocument.ModelingMode` (sync=1, ordered=2) and use only that mode's family; if it refuses,
  say so and let the user decide to switch the document. A "fallback" that dirties the user's
  part is not a fallback.
- **A failed attempt is expensive.** Deleting the failed feature SE left in the tree can
  disconnect the document proxy (`RPC_E_DISCONNECTED`), after which every remaining attempt in
  the loop fails the same way. Order the attempts so the known-good shape is FIRST, and abort the
  loop on the first disconnect instead of logging noise.
- **Fallback that reuses only proven calls**: build the groove as a BODY and subtract it —
  `Models.AddFiniteRevolvedProtrusion[Sync](1, Profile[], refAxis, ProfilePlaneSide, 2π)`
  (`ProfilePlaneSide` is NOT optional here) then
  `Model.BooleanFeatures.Add(1, SolidEdgePart.Model[], seBooleanSubtract=2, Type.Missing)` —
  the same boolean that unites the burn surface to the block. Delete the ring body if the
  boolean fails, or the part is left with a stray solid.

- **The groove must be ASSOCIATIVE to the edge, or the first sync edit destroys it** (Carlos,
  2026-09-21: "apesar do recurso ser ordenado, não existe vínculo do esboço com o modelo"). The
  recipe below — base plane / `AddParallelByDistance` at a measured distance, four lines at
  absolute coordinates — produces a groove that is correct *when created* and has **no reference
  to the part at all**. Move or resize the hole in synchronous and the ordered groove stays where
  it was. Being ordered only gave the sketch an owner; it gave it no parents.
  - **Plane from the edge (CONFIRMED LIVE 2026-09-21, first try, ordered part, face groove on a
    Ø20 cylinder top: plane origin landed ON the edge at (10,0,20), normal (0,1,0), axis inside it,
    cut `igFeatureOK`, faces 3→7).** A plane normal to a
    circle at a point of that circle *contains the circle's axis* — exactly the plane a revolve
    needs. `RefPlanes.AddNormalToCurve(Curve=edge, PlanePoint=igCurveStart(14),
    OrientationPlaneOrPivot=<base plane>, PivotOrigin=igPivotStart(3), [Local], [ParentCurve])`
    (signature from the typelib dump + `Interop.SolidEdge` reflection; the orientation plane only
    decides the sketch X direction — `RefPlanes.Item(1)` was accepted first; keep 2..3 as fallback).
    Whether the groove then FOLLOWS an edit is a separate check (below) — creation succeeding
    proves the plane is built from the edge, not that SE re-solves it on every edit. **Checked
    live the same day: MOVING the cylinder → the groove followed ✓; changing its Ø → the groove
    went eccentric ✗** (the sketch rides rigidly on the edge point, which leaves the axis). Verify with the profile frame that the
    axis lies in it, as always. Sketch coordinates are local to the plane, so the groove now
    moves **rigidly** with that point of the edge. Fall back to the loose plane only with a loud
    "NOT associative" warning.
  - **Still open: diameter changes.** Rigid-with-a-point is not enough when the hole's Ø changes —
    the drawn axis line stays at the old radius from the plane point. The fix is to
    `Profile.IncludeEdge(edge)` (projects the circle as its diameter segment) and constrain to it
    with `Relations2d` (`AddPointOn`, `AddKeypoint`, `AddPerpendicular`, `AddColinear` — all
    `(Object, Int32 keypointIndex, …)`, reflection 2026-09-21). Keypoint indices ARE in the interop
    (`SolidEdgeConstants.KeypointIndexConstants`, reflection): `igLineStart=0`, `igLineEnd=1`,
    `igLineMiddle=2`, `igCircleCenter=0`, `igArcStart=1`, `igArcEnd=2`. Confirmed LIVE that the
    rigid anchor alone FAILS on a Ø change (2026-09-21: groove went eccentric).
    Implemented next (NOT YET CONFIRMED LIVE): the included circle shows edge-on as a Ø-long
    segment whose MIDDLE is on the axis → construction line L drawn on it + `AddColinear(L, proj)`
    (axial), `AddPointOn(proj, igLineMiddle, axisLine)` (radial), and
    `Relations2d.AddSet(n, ref Line2d[] {4 rect lines, axis, L})` so the whole profile moves as a
    rigid body with the axis — concentric, same radii (a new Ø wants a new ring anyway).
  - **`Profile.IncludeEdge` TRAP (live, 2026-09-21):** the call succeeds and really puts the edge
    in the sketch, but its `[opt][out] Geometry2d` comes back **empty** (by-ref `ParameterModifier`
    and all). Code that trusted the out-param bailed out before `ToggleConstruction`, the included
    edge stayed as profile geometry, the revolve came back `Status` failed, and deleting that
    feature disconnected the document (`RPC_E_DISCONNECTED`) — every later call died with it.
    Rules: find what an include/project added by **diffing the profile's 2D collections**
    (`Lines2d`, `Arcs2d`, `Circles2d`, … by COM identity), never by the out-param; and **before
    the feature call, count the non-construction elements** (`IsConstructionElement`) — if it is
    not exactly the contour you drew, delete everything the anchoring added and cut without it.
    Anything you add to a sketch that is about to become a feature is a way to lose the feature.
  - **Rigid-set shortcut FAILED live (2026-09-21).** `AddColinear(L, proj)` + `AddPointOn(proj,
    igLineMiddle, axis)` + `Relations2d.AddSet(n, Line2d[])` were all accepted (no error, 3/3) and the
    cut was fine — but after a Ø change the sketch stayed on the OLD centre, and when the user
    constrained it by hand the rectangle fell apart ("Elementos desconectados" ×4, "vários perfis
    abertos"). Two lessons: (1) **lines drawn end-to-end by coordinates are NOT connected** — without
    `AddKeypoint(line_i, igLineEnd, line_j, igLineStart)` at every corner, the first solver move opens
    the profile; (2) `AddSet` does not act as a rigid body you can drag by one member. Next attempt
    (not yet confirmed): a FULLY DEFINED sketch — 4 corner keypoints, sides `AddParallel`/
    `AddPerpendicular` to the axis line, axis `AddPerpendicular` to the included edge + its middle on
    the axis, and driving dimensions (`Dimensions.Constraint = true`, then
    `AddDistanceBetweenObjects(obj1, x1,y1,0,false, obj2, x2,y2,0,false)`, 2D sketch metres) for inner/
    outer radius from the axis and axial position from the edge. Log `ProfileSet.IsUnderDefined`.
  - **That failed too (live, 2026-09-21), and it taught the real lesson.** 10/10 relations took and
    `End` returned 0 for the first time, but both `AddDistanceBetweenObjects` dimensions were BORN
    at 7.15 mm (expected 5.017 / 8.717) — the pick points made SE measure something else — and since
    they were driving, the solver bent the groove to the wrong Ø. Worse: the user then constrained
    the sketch BY HAND and it still left the axis on a Ø change. **So the revolve approach itself is
    the problem:** its sketch must lie in a plane containing the axis, and the only such plane you can
    tie to the edge (`AddNormalToCurve`) has its origin ON the edge, which moves (and can re-orient)
    when Ø changes. No constraint inside the sketch fixes a plane that moves wrongly.
  - **Pivot (implemented, NOT YET CONFIRMED LIVE): annulus + extruded cutout.** Sketch on a plane
    PERPENDICULAR to the axis tied to a planar face the edge touches (`Edge.GetFaces` → the planar
    face whose normal ∥ axis; sketch on the face itself, or `RefPlanes.AddParallelByDistance(face,
    d, side)`). There `IncludeEdge` yields a circle; two `Circles2d` + `Relations2d.AddConcentric`
    to it + `Dimensions.AddCircularDiameter` (measures the circle itself — no pick points) +
    `ExtrudedCutouts.AddFiniteMulti(1, Profile[], side, depth)`. Same rectangular section for face,
    shaft and bore grooves; concentricity is the one thing the concentric relation guarantees.
    **Prefer the modeling a human would draw over clever constraint tricks.**
    First live run: it never ran — `Edge.GetFaces(ref 0, ref object[0])` (the SharpCornerProbe
    pattern) threw `DISP_E_TYPEMISMATCH` on a circular edge, and the code silently fell back to the
    revolve, so the user saw the old failure and blamed the new approach. **When a new path has a
    fallback, log loudly that the fallback ran — and don't depend on an unproven marshal for the
    entry condition:** the planar face is now the one the user clicked (face groove) or found by
    bounding box among `Body.Faces[igQueryAll]` (normal ∥ axis, on the edge plane, covering the
    circle); `GetFaces` is last resort.
    Second live run: sketch PERFECT (circle included, 2/2 concentric, both Ø dims born right,
    `IsUnderDefined = False`) — yet `ExtrudedCutouts.AddFiniteMulti` came back `igFeatureFailed`,
    with side 1, and again with symmetric (3), so it was not the direction. `Profile.End(1|8)`
    had returned **−113**. **An annulus is a NESTED profile: pass `igProfileAllowNested = 8192`**
    (`ProfileValidationType`: Closed 1, Single 4, NoSelfIntersect 8, RefAxisRequired 16,
    NoRefAxisIntersect 32, AllowNested 8192, AllowPointsAsProfiles 524288). Without it the profile is
    rejected and the feature consuming it is born failed. (Fix = `End(1|8|8192)`, NOT YET CONFIRMED
    LIVE.) Also: a failed ordered feature left in the tree after your cleanup deletes its sketch
    reports "o perfil não existe mais" — delete the FEATURE (it takes the sketch) instead.
  - **CONFIRMED LIVE (2026-09-21, 3rd run): the annulus + extruded cutout works.** `End(8201) = 0`,
    sketch fully defined, face groove and shaft groove both `igFeatureOK`, and the user reported the
    grooves now follow the part. `ExtrudedCutouts.AddFiniteMulti(1, Profile[], 3 /*symmetric*/, d)`:
    **the symmetric distance is the TOTAL** (measured: 4.2 mm symmetric on a face → 2.1 mm in
    material; 3.7 mm on a mid-groove plane → 3.7 mm wide). Symmetric removes the side question
    entirely: sketch at mid-groove for shaft/bore, on the face with 2× depth for a face groove.
    `RefPlanes.AddParallelByDistance(planarFace, 0, 2)` works as a "coincident plane"; passing the
    Face straight to `Profiles.Add` gave an InvalidCast.
  - **Hiding the sketch of an extruded feature (CONFIRMED LIVE 2026-09-21):** `Profile.Visible =
    false` on the proxy you drew with was accepted and the circles STAYED on screen (for the revolve
    it had been enough). What worked: **`ExtrudedCutout.ShowDimensions = false`** on the feature. The
    other two routes gave nothing: `feature.Profile` came back null and `feature.GetProfiles(ref 0,
    ref object[0])` threw `DISP_E_TYPEMISMATCH` (same family as `Edge.GetFaces` — the `[in,out]
    SAFEARRAY(IDispatch)` wants a typed array, not `object[]`). A temp plane rejected mid-command
    could not even be deleted right away (RPC_E_DISCONNECTED after its probe sketch was dropped) —
    set `Visible = false` on it as the fallback, or it stays on screen.
  - **How to test associativity:** create the groove, then in the synchronous part move the hole
    face (and separately change its Ø) and check the groove followed. Creating it and measuring
    faces proves nothing about this.
- **Shaft or bore? Ask the FACE, not the body's bounding box** (live, 2026-09-21). "Cylinder
  radius vs. how far the body reaches from the axis" called a Ø22 shaft a BORE because a larger
  flange below it made the body reach r = 13; the groove was cut the wrong way and the ring floated.
  Use the face's own orientation: at the middle of `Face.GetParamRange` (raw, no m→mm), read
  `Face.GetPointAtParam` and `Face.GetNormal` — `(1, ref double[]{u,v}, ref double[0])`, same by-ref
  shape as the WEDM-validated `Edge.GetPointAtParam` — and take cos(normal, radial): > 0 = the normal
  points away from the axis = shaft; < 0 = bore (solid B-rep normals point out of material).
  CONFIRMED LIVE 2026-09-21 on the same stepped part (cos = 1.000 → shaft, Ø21 and Ø20.5); keep
  the bbox rule only as a logged fallback.
- **Finding the sketch plane** is the part people get wrong. Don't assume `RefPlanes.Item(2)` is
  XZ. Walk items 1–3, discover each one's frame, keep the one whose **normal is perpendicular to
  the axis** (that plane is parallel to the axis), then offset it by the axis point's signed
  distance with `AddParallelByDistance` and **re-discover the frame to confirm the axis now lies
  in it**. Verifying instead of assuming makes the `NormalSide` guess self-correcting.
- **Overshoot the open side** by ~0.05 mm (start the rectangle just outside the sealing surface).
  A cut that lands exactly tangent to the face can fail to open the groove.
- **`ProfileSide` is not deducible from the outside** — try `igRight=2` then `igLeft=1`, checking
  `.Status` each time and deleting the failed feature before the next attempt (see `errors.md`).
- **Prove it worked by counting faces**, not by a non-null return: snapshot
  `model.Body.Faces[igQueryAll].Count` before and after. A revolved cut that silently did nothing
  returns a feature object just like one that worked.

**Threaded/tapped holes (SE 2023) — `HoleType` is NOT where the thread lives.** Decoded by
SPY-ing a tapped M6 a human made in the Hole dialog (2026-09-03), after two rounds of the API
producing holes that were never threaded at all. The whole recipe, straight off that hole:

```
HoleType               = 33     igRegularHole      ← NOT igTappedHole
TreatmentType          = 37     igTappedHole       ← THIS is the thread switch
Standard               = "ISO Metric"              (= the .xlsx file name)
SubType                = "Standard Thread"
Size                   = "M6"
Fit                    = "Close (H12)"
ThreadDiameterOption   = 0      seTapDrillDiameter
ThreadTapDrillDiameter = 0.005  (5 mm — what actually gets drilled)
HoleDiameter           = 0.006  (6 mm — the NOMINAL is stored here)
ThreadMinorDiameter    = 0.004917    ThreadExternalDiameter = 0.004773
ThreadDescription      = "M6"        ThreadSetting = 164  igRegularThread
ThreadDepthMethod      = 13     igFinite      ThreadDepth = 0.014
CreatePhysicalThread   = False   ← even the hand-made hole has no cut helix
```

**`igTappedHole (37)` is a `FeaturePropertyConstants` value meant for `TreatmentType`, not for
`HoleType`.** `FeaturePropertyConstants` is one flat enum shared by dozens of properties, so a
value existing in it says nothing about which property accepts it — and **SE does not raise an
error when you pass a wrong one.** Passing `HoleDataCollection.Add(37, …)` gave back
`HoleType=36 (igCounterdrillHole)`, `TreatmentType=44 (igNone)` and the user's *saved hole-dialog
defaults*; the resulting "M6" was a Ø6.0 hole with a Ø6.9 counterdrill — untappable, and
cosmetically indistinguishable from a plain hole. **Read the properties back after the `Add`**
(`TreatmentType == 37` is the signal) and **measure the resulting cylinder**
(`Body.Faces[igQueryCylinder=10]` + `Face.GetRange`) — the wrong hole was Ø6.0 where Ø5.0 was
asked for, and only the measurement made that visible.

**Always pass `IgnoreSavedDefaultValues = true`** (last param of `Add`, param 25 of `AddEx`).
Otherwise SE merges whatever the user last set in the Hole dialog into your HoleData — that is
where the phantom counterbore/countersink/`BottomAngle=118` values came from.

**Shaping the hole: drill point, thread length ≠ hole length, entry chamfer.** A hole that is
correctly *threaded* can still be wrong as a machining feature — flat bottom, thread running the
full depth, sharp entry. Three separate settings, none of which come for free (and
`IgnoreSavedDefaultValues=true` resets them to flat/none, so pass them explicitly):

- **Drill point at the bottom**: `BottomAngle` (the **total** included angle — 118° is a standard
  twist drill) plus `VBottomDimType` = `igVBottomDimToFlat (145)` or `igVBottomDimToV (146)`.
  `ToFlat` means the depth you ask for is measured **to the shoulder** and the cone sits below it
  — which is how a toolmaker dimensions a blind hole; `ToV` measures to the tip.
- **Thread shorter than the hole**: `ThreadDepthMethod = igFinite (13)` + `ThreadDepth`. This is
  a *different number in a different place* from the hole's own depth, which is the feature's
  extent — the `FiniteDepth` argument of `AddSyncEx`/`AddFiniteEx`. Setting only one of them
  gives a thread that runs to the bottom of the hole.
- **Entry chamfer**: `AddEx` params 29–31 `StartChamferOn/Setback/Angle` (also 32–34 Neck,
  35–37 End), or on the HoleData itself `SetStartChamfer(ChamferOn, Setback, Angle)` /
  `GetStartChamfer([out] on, [out] setback, [out] angle)`. All three of `GetStartChamfer`'s args
  are `[out]`, so they need the by-ref `ParameterModifier` — same trap as `Face.GetRange`. The
  21-param `Add` has no chamfer params at all; there, `SetStartChamfer` is the only way.

**Units are MIXED inside `HoleData`** — lengths in meters as everywhere else, but
`BottomAngle`, `CountersinkAngle` and the chamfer angles are in **DEGREES** (the hand-made hole
read `BottomAngle = 118`, `CountersinkAngle = 90`). Only `ThreadTaperAngle` is radians
(`0.0436…` = 2.5°). Do not apply a blanket "SE angles are radians" rule to this object.

Two constructors, and `AddEx` is the one you want for a thread (live dump; authored docs
claiming 7 params are wrong):
- `Add(HoleType, HoleDiameter, [CounterboreDiameter], [CounterboreDepth], [CountersinkDiameter],
  [CountersinkAngle], [BottomAngle], **[TreatmentType]**, [TaperMethod], [Taper],
  [ThreadMinorDiameter], [ThreadDepthMethod], [ThreadDepth], [VBottomDimType], [TaperDimType],
  [CounterboreProfileLocationType], [TaperLValue], [TaperRValue], [ThreadExternalDiameter],
  [ThreadDescription], [IgnoreSavedDefaultValues])` — **21 params**.
- `AddEx(HoleType, [Standard], [SubType], [Size], [Fit], [HoleDiameter], …same middle…,
  [ThreadDescription], [IgnoreSavedDefaultValues], [ThreadDiameterOption],
  [ThreadTapDrillDiameter], [HeadClearance], [Start/Neck/EndChamferOn/Setback/Angle])` —
  **37 params**, the standard-driven one. With ~27 trailing `Type.Missing` args, call it through
  `InvokeMember` (IDispatch coerces) rather than the dynamic binder.

*Two thread tables ship with SE, and they are NOT interchangeable:*

| Table | Path | Columns | Reached by |
|---|---|---|---|
| **legacy** | `Preferences\HOLES.TXT` (plain text, `;`-separated) | nominal Ø; internal minor Ø; external minor Ø; **thread type**; thread family — **no pitch** | `HoleData.ThreadDataByDescription = "<thread type>"` |
| **current** | `Preferences\Holes\<Standard>.xlsx` (`ANSI Inch`, `ANSI Metric`, `DIN/GB/GOST/ISO/JIS/UNI Metric`) | sheet **Threaded**: Thread Type (1 standard / 2 straight pipe / 3 tapered pipe); **Sub Type**; ThreadFamily; **Size**; Nominal Ø; Tap Drill Ø; Internal Minor; External Minor; **Pitch**. sheet **Simple**: **Sub Type**; Size; **Fit**; Hole Diameter | `HoleData.Standard`/`SubType`/`Size`/`Fit`, or `AddEx` |

**Read these files instead of guessing the strings** — they sit in the SE install on any machine
that has SE, no license or SE run needed, and the paths are also readable at runtime via
`Application.GetGlobalParameter` (`seApplicationGlobalHoleSizeFile=61` → HOLES.TXT;
`seApplicationGlobalHolesDatabaseFolder=498` → the .xlsx folder), so a customized location is
discoverable too. (The .xlsx is a zip — read `xl/worksheets/sheetN.xml` against
`xl/sharedStrings.xml`, mapped by `xl/workbook.xml`; no Excel needed.)

Valid ISO-Metric strings, read off that workbook: threaded `SubType` ∈ {`Standard Thread`,
`Straight Pipe Thread`, `Tapered Pipe Thread`}; plain-hole `SubType` ∈ {`Dowel`, `Drill Size`,
`General Screw Clearance`}; `Fit` ∈ {`Exact`, `Nominal`, `Close (H12)`, `Normal (H13)`,
`Loose (H14)`, `Clearance`, `Press`, `Transitional`}. M6 row: Ø6, tap drill 5, internal minor
4.917, external minor 4.773, pitch 1. In HOLES.TXT the coarse M6's thread type is literally
`"M6"` — `"M6x1"`/`"M6 x 1"` do **not** exist there (the fine ones are `"M6 x 0.75"` and
`"M6 x 0.5"`). **`SubType` is never the size**: `SubType="M6"` (an earlier guess in this skill)
is not a thing. And `Standard`/`SubType`/`Size` alone do **not** make a hole threaded — with
`TreatmentType=igNone` SE resolves M6 against the *Simple* sheet instead and gives you the
Ø6.4 screw-clearance hole.

Remaining gotchas:
  1. **`HoleData.ThreadDataByDescription` is a WRITE-ONLY PROPERTY, not a method.** The dump
     shows a `put` with no `get`; reflection shows `set_…(String)`, `CanRead=False`. Calling it
     method-style — `hd.ThreadDataByDescription("M6")` — throws "erro ao chamar" for *every*
     format (mis-diagnosed for weeks). **Assign it:** `hd.ThreadDataByDescription = "M6"`. It
     fills the legacy thread numbers (and reads back `ThreadNominalDiameter > 0`, which looks
     like success) but it does **not** set `TreatmentType`, so the hole still comes out plain.
     Prefer the `AddEx` recipe above; use this only to top up a legacy-only install.
  2. **`ThreadSetting = igRegularThread (164)`** is the API's "Rosca" checkbox — annotation
     only (`igNone=44` = no thread). Necessary, not sufficient.
  3. **`ThreadSetting` never cuts the helix.** The cut is a separate flag,
     `Hole.CreatePhysicalThread` (**get AND put** — an earlier note here called it read-only,
     wrong), plus `CreatePhysicalThreadAndReturnStatus(bool, [out] PhysicalThreadErrorCode)`.
     Better than toggling it afterwards: **every `Holes` adder has an `…Ex` twin whose last
     input is `bPhysicalThread`** — `AddSyncEx(nProfiles, Profiles, side, ExtentType, depth,
     Data, bPhysicalThread)`, `AddFiniteEx(Profile, side, depth, Data, bPhysicalThread)`, also
     `AddFromToEx`/`AddThroughNextEx`/`AddThroughAllEx`, and `Threads.AddEx(...)`. Ask for the
     helix at creation.
  4. **Decode the error code — a failed physical thread is otherwise silent.**
     `PhysicalThreadErrorCode`: `0 NoError, 1 UnknownError, 2 ProfileCreationError,
     3 HelixCreationError, 4 BooleanOperationError, 5 InvalidThreadTypeError,
     6 InvalidPitchValueError, 7 DisabledByAdminError`. The `[out]` needs a by-ref
     `ParameterModifier` — without it you read back `0`, a false "no error". A real run returned
     **5 (InvalidThreadType)** — which turned out to mean exactly that: the HoleData was a
     counterdrill, not a thread.
  5. **A correct threaded hole can still LOOK unthreaded.** Thread display is an
     application-global: `seApplicationGlobalEnableThreadedDisplay=30` (and
     `seApplicationGlobalThreadDisplayMode=406`), via
     `Application.GetGlobalParameter/SetGlobalParameter(param, [in,out] Value)` — `[in,out]`, so
     by-ref `ParameterModifier` again. It read **False** on the real machine. Check it before
     blaming the recipe, and treat turning it on as the user's call — it changes their whole SE.
  6. **`ThreadDiameterOption` decides what gets drilled**: `seTapDrillDiameter=0`,
     `seInternalMinorDiameter=1`, `seNominalDiameter=2`, `seInsidePipeDiameter=3`. The same
     nominal M6 gives Ø5 / Ø4.917 / Ø6. `HoleDiameter` still stores the nominal.

Enums: `igRegularHole=33, igCounterboreHole=34, igCountersinkHole=35, igCounterdrillHole=36,
igTappedHole=37` (the last one as a **TreatmentType**; `TreatmentType` also takes
`igNone=44`, `igTaperedHole=38`); `igRegularThread=164, igStraightPipeThread=165,
igTaperedPipeThread=166`; `igFinite=13` for `ThreadDepthMethod`. The separate
`Model.Threads.Add(HoleData, nCyl, CylinderArray, CylinderEndArray, …)` feature (applied to
`Body.Faces[igQueryCylinder=10]`) **fails every way** on SE 2023 (E_FAIL / out-of-range) —
don't use it. Keep a **plain-Ø5 fallback in a fresh doc** if the tapped AddSync fails, so you
never lose the central hole.

**Physical (cut) thread: not needed, and never validated on SE 2023.** The hand-made reference
hole has `CreatePhysicalThread = False`, and the shop works from the cosmetic thread
(`TreatmentType` + `ThreadSetting`) plus the callout — a cut helix only bloats the body. Ship
the cosmetic recipe; reach for `bPhysicalThread` / `CreatePhysicalThreadAndReturnStatus` only
if someone actually needs the helix in the solid, and expect to debug it (a real attempt
returned `5 InvalidThreadType`, though that run’s HoleData was not tapped either).
