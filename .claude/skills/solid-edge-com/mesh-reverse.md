# Mesh and reverse engineering (what SE gives you, and what you have to build)

Measured on SE 2026 (v226.00.08.04) against a real imported mesh body, 2026-09-18.
Everything here was confirmed live or read from the typelib dump — nothing is from memory.

## The asymmetry that decides the whole design

The typelib says Solid Edge will **read** a mesh and **rebuild** a body from one, but will
not **understand** it for you:

| Exists in COM | Does NOT exist in COM |
|---|---|
| `MeshSurface.GetTriangleData` / `GetTrianglePoints` / `GetTriangleNormals` | any plane / cylinder / cone / sphere **fit** over a mesh region |
| `Body.GetFacetData`, `Body.FacetCount[Tolerance]` | any mesh **segmentation** |
| `Model.IsFacetBody` / `IsMixedFacetBody` | any mesh **region selection** |
| `Model.HealAndOptimizeWithMeshOptions(…, bFillHoles, FillHoleType)` | the commands on SE's own **Reverse Engineering** ribbon tab |
| `Models.AddBodyByMeshFacets`, `DoRemesh`, `ConvertToMeshes` | |
| `Sketches.CreateSectionSketches(…, bRecognizeLines/Arcs/Circles/Ellipses)` | |
| `BSplineSurfaces.Add`, `StitchSurfaces`, booleans | |

`Application.StartCommand` only **opens** the interactive Reverse Engineering command and
then waits for the mouse, so it is useless from a script. **Conclusion: the fitting is yours
to write.** Plan for that from the start instead of hunting for the API that would avoid it.

## Reading the triangles

```
Body.GetFacetData(Tolerance: Double, [out] FacetCount: Int32, [out] Points: Array,
                  [opt][out] Normals, [opt][out] TextureCoords,
                  [opt][out] StyleIDs, [opt][out] FaceIDs, [opt] bHonourPrefs)
```

- Works on a **facet body and on a plain B-rep solid** (the solid is tessellated on the spot),
  so one code path covers imported meshes and normal parts.
- Ask for **`Normals` and `FaceIDs` too** — they are free and `FaceIDs` is per-facet face
  ownership, which on a tessellated B-rep is segmentation handed to you. Fall back to the
  three-argument form if the optional outs fail, and **report which route ran**: silently
  losing `FaceIDs` would look like the segmentation code failing later.
- `Points` is a **triangle soup**: 3 vertices × 3 doubles per facet, no index buffer, vertices
  repeated per triangle. Measured: 4.168 facets → 37.512 doubles = 12.504 points, exactly 3×.
- **Metres**, like the rest of the geometry API. Convert once at the boundary.
- All `[out]`s need `ParameterModifier` by-ref marking, as everywhere else in late binding.

**`Body.Faces` is INACCESSIBLE on a facet body.** The read simply fails. There are no `igMesh`
faces to enumerate, so there is no pre-existing region to grab — this is the fact that forces
segmentation to start from the raw soup. Do not design around `Faces` and discover this later.

## `CreateSectionSketches` — the lever, and the trap it hides behind

```
Sketches.CreateSectionSketches(psaObjects: SAFEARRAY(IDispatch)*, RefPlane: IDispatch,
  [out] SketchesGenerated: SAFEARRAY(IDispatch)*, [out] SketchCount: int*,
  [out] enumErrorCode: SectionSketchesErrorCode*, [opt] NumOfPlanes: int,
  [opt] enumReferenceSide, [opt] dPlaneOffset: double,
  [opt] bRecognizeLines, bRecognizeArcs, bRecognizeCircles, bRecognizeEllipses: int)
```

If this works over a mesh it returns sketches with lines, arcs and circles **already
recognised**, which means a prismatic part can be rebuilt as editable features without you
writing any 2D primitive recognition. That makes it worth getting right.

**It failed with `DISP_E_TYPEMISMATCH` because of the caller, not the mesh.** `psaObjects` is
`SAFEARRAY(IDispatch)` and the first attempt passed `object[]`, which marshals as
`SAFEARRAY(VARIANT)`. This is the **same trap already documented for `CopySurfaces.Add` and the
extrusion calls** — it recurs because `object[]` is the natural thing to write. Use a typed
array (`new SolidEdgeGeometry.Body[] { … }`, `new SolidEdgePart.Sketch[0]` for the out).

The lesson beyond the fix: **a marshaling error is not an answer about the geometry.** The call
never entered the command, so it proves nothing about whether SE can section a mesh. Do not
record "SE can't do X" when the HRESULT says the arguments were wrong.

## Writing the recogniser: two mistakes that cost a red test each

**1. Segment by crease BEFORE fitting, never after.** The tempting order — grow planar regions
across the whole mesh, then look at the leftovers — breaks on any tessellated cylinder: each
strip between two segments is *exactly* planar, so a Ø20 hole comes back as 64 perfect little
planes and no cylinder at all. The correct order is: split into smooth components by dihedral
angle (the crease is what delimits a surface), then ask each whole component **which** surface
it is.

The exception that keeps the plane-growing code alive: a **fillet is tangent** to the face it
blends, so there is no crease between them and they land in the same component. A component
that is neither a plane nor a cylinder as a whole must therefore be peeled — grow the planes
out of it, then fit what remains.

**2. A curved surface FITS inside a planarity tolerance, in pieces.** A sphere measured at
0,05 mm decomposes into hundreds of "planes", and a report that just sums planar area will
call it prismatic. Detect the mosaic (many small patches carrying most of the "planar" area)
and **suspend the verdict** rather than printing the warning next to a confident conclusion —
the reader keeps the second sentence. Say which knob to turn (`distanciaPlanoMm`) and what
each outcome would mean.

**Fitting recipes that worked:**

- **Plane**: area-weighted mean normal + area-weighted centroid. Require *every* triangle's
  normal within the angle tolerance **and** every vertex within the distance tolerance; one
  outlier rejects the region. That last part is what stops a cylinder wall passing as a plane.
- **Cylinder axis**: the eigenvector of the **smallest** eigenvalue of `Σ area·n·nᵀ` — on a
  cylindrical surface every normal is perpendicular to the axis, so the axis is the direction
  the normals least occupy. Jacobi rotations on the 3×3 are enough; no library needed. Force a
  canonical sign (largest component positive) or two runs report opposite axes and diffs
  between runs become noise.
- **Cylinder radius**: project onto the plane ⊥ axis and fit a circle algebraically (Kåsa) —
  linear, no iteration, and it gives a residual you can threshold.
- **Always report the RMS of each fit, in mm.** Without it "this is a Ø12 cylinder" and "this
  vaguely resembles a cylinder" print identically.
- **Angular coverage** distinguishes a through hole from a corner radius: sort the fitted
  angles and measure the **largest empty gap**; sweep = 360° − gap. `max − min` is wrong, it
  breaks across the `atan2` discontinuity (an arc spanning ±180° reads as 340° instead of 20°).

## Welding a triangle soup

No index buffer means adjacency has to be rebuilt. Hash quantised coordinates into cells and
**scale the weld tolerance to the part** (`bbox diagonal × 1e-6`, floored): a mesh exported
through float does not repeat a shared vertex bit for bit, and an absolute epsilon that suits
a 10 mm insert silently fails to weld a 2 m part.

## Reference planes

**`RefPlane.Normal` is not readable** — on SE 2026 all three base planes answer
`DISP_E_UNKNOWNNAME (0x80020006)`. A tool that reports planes can give index and name but not
the axis, so anything that needs to know which plane is XY/XZ/YZ must **measure it or take it
from a validated mapping**, never read the normal. (On a fresh default part the observed
mapping was plane 1 = XY, plane 2 = XZ.)

## Beyond plane and cylinder: cone, torus, sphere (measured on a real 6-body mesh, 2026-10-10)

- **Three profile points always fit a circle.** A vertical wall plus an inclined face (two
  planes, 4 triangles) fitted as a "Ø53.57 cylinder, RMS 0". Reject cylinder/cone fits whose
  **tessellation sagitta** `R·(1 − cos(step/2))` exceeds ~0.1 mm — a real tessellated cylinder
  sits around 0.02 mm.
- **Cone**: axis = direction of **least variance** of the normals (every normal makes the same
  angle with it); **apex** = least-squares point satisfying `n·x = n·c` for every facet — every
  tangent plane of a cone, and every facet with two vertices on one generator, contains the
  apex. Half-angle from `ρ = h·tanβ`. Linear, no iteration. A hole chamfer was otherwise
  shredded into ~20 tilted "cylinders" of 3 triangles.
- **Torus / sphere**: every normal of a surface of revolution **intersects the axis** — linear in
  Plücker coordinates of the axis `(d, m)`: `d·(c × n) + n·m = 0`, smallest eigenvector of a 6×6.
  Two caveats, both measured: with only two meridian strips the system is degenerate (their
  normals also meet the line through the two profile centres), so fit the **whole connected
  group**; and on coarse tessellation (15° both ways) facet normals miss the axis by ~13°, so
  refine by minimising the profile-circle residual of the **vertices** (Nelder–Mead on axis tilt
  + position, started from the Plücker axis and from the nearest X/Y/Z). Major radius ≈ 0 ⇒
  sphere.
- **Seeding cylinders in a mixed region**: take the seed patch plus only the neighbours that
  suggest the **same axis** (`n_seed × n_neighbour`); a neighbour from another surface (the torus
  row above a wall facet) poisons the fit forever. Grow with a **refit after every accepted
  patch** — testing neighbours against the 3-patch fit (R2.51 for an R2.5) rejected the last two
  facets of the fillet, which then reported as "inclined faces of 7.5° and 22.5°".
- **Ruledness filter**: inside mixed regions, accept a cylinder/cone only if most vertices share a
  generator (same angle, different heights). Corner-blend fragments forced into cylinders fail
  it. Do **not** apply it to a whole crease-bounded surface: CAD exporters sometimes stagger the
  two rims of a cone.
- Snap arc **endpoints** to X/Y/Z within ~1°: a 90° fillet fitted as 90.4° becomes two 45.2°
  corner regions in reconstruction, and one of them fails.

## Mesh → solid: prismatic bands + boundary corrections

Section each Z band at mid-height and extrude with exact lines/arcs; then fix what a straight
extrusion cannot do, one feature per region. In the plane where the surface becomes a curve
(meridian of a cone/torus, section ⊥ to a horizontal cylinder axis, vertical section along the
steepest line of an inclined plane), the curve splits a small region in two, and **the mesh
decides** which side has material: generalised **winding number** (sum of solid angles of the
triangles / 4π — no edge cases, tolerant of small holes). Material ⇒ protrusion, empty ⇒ cutout.
Both are idempotent against the prismatic body, which can only be wrong inside those regions.

- Test points: a grid **inside** the region polygon, ≥ 0.04 mm from its border (above the mesh
  sagitta). The vertex average of a fillet's corner crescent (0.21 mm thick) falls inside the arc.
- Snap the cone mouths to recognised planes ⊥ axis: the fitted apex came out 0.0011 mm low and the
  chamfer cut stopped at 85.999, leaving a 0.001 mm skin over a sharp edge.
- Revolve recipe (SE 226): `axis = profile.SetAxisOfRevolution(line2d)` then
  `RevolvedCutouts/RevolvedProtrusions.AddFinite(profile, axis, 2, side, angleRad)`; partial
  revolve with the sketch at the middle of the arc and `side = 3` (symmetric) avoids guessing the
  rotation sense.
- **Draw closed extrusion profiles counter-clockwise in SKETCH coordinates** (read them back with
  `Convert3DCoordinate`). Clockwise, `ExtrudedCutouts.AddFinite(p, igLeft, …)` removes the
  OUTSIDE — a 1 060 mm³ wedge cut took 17 700 mm³.
- API-created features leave their sketches visible: set every `Profile.Visible = false` at the
  end (`doc.ProfileSets.Item(i).Profiles.Item(k)`).
- **Verify by geometric deviation, both ways**, not by volume: 24 chamfer cuts and 24 conical
  collars cancel exactly in volume. And deviation alone does not prove exact geometry either — a
  contour segment copied from the mesh (a radius turned into 26 lines) lies on the mesh by
  construction. "Exact" = deviation ≤ ~0.05 mm **and** no mesh-copied segment left uncorrected.

## Second real body: what a cast-like part adds (2026-10-10)

The same 6-mesh part had a body with legs: an L-angle section (2 mm walls, R1 tips, an inner R10
face tangent to both tips — an S curve), an R5 fillet from every leg wall into the underside of
the top plate, and free-form corner blends. The straight-extrusion solid lost 5.8 % of the
volume. Every fix below was found by measuring, and each one is general:

- **Never section a mesh exactly at a vertex height.** An edge whose endpoint lies ON the cutting
  plane counts as not crossed, its triangle yields one point, the segment vanishes and the loop
  closes through the wrong neighbour — the R10 face, one R1 tip and a wall disappeared from every
  leg because the band mid-height (Z 22) was a tessellation row. Nudge the cut by ~1e-4 mm off
  any vertex z before cutting.
- **Two tangent arcs are real, not noise.** Before "straightening" a curved strip between two
  arcs, check tangency: centre distance = R1 + R2 (external) or |R1 − R2| (internal). The R10 had
  RMS 0.0006 mm through four vertices and centres 11.05 mm apart for R1 + R10 = 11.04.
- **Inside a mixed region, grow plane patches with a TIGHTER tolerance than for whole planes**
  (we use 1/4 of the angle and 1/5 of the distance). A fillet is tangent to its wall, so with the
  loose tolerance a wall facet grows straight into the fillet and no longer fits its cylinder.
- **A narrow strip that is one plane is a plane.** A "chain of narrow patches → free-form" rule
  meant for cones/tori also swallowed a 4-triangle planar web; test the union as a plane first.
- **Seeds compete.** Seeding cylinders from the smallest patch up lets three neighbouring facets
  (any three profile points fit a circle) form a big false cylinder before the true small one
  grows to them. After growing, let every cylinder with few patches hand back each patch that
  fits a larger neighbouring cylinder; dissolve it if fewer than 3 remain.
- **Coaxial fragments are the same cylinder.** Where the tessellation rotates (top of the R1
  tips) a cylinder splits into "R0.905…R0.965" pieces. Merge pieces whose axis is parallel, axis
  line within ~0.15 mm and radius within ~0.15 of a larger neighbour: triangles that fit go to it,
  the rest become free-form.
- **Recognise a fillet as a HYPOTHESIS between two known faces, not as a free fit.** Fillet
  tessellation in real STL is often not ruled, so region growing never finds it, and a free fit
  mixes its cylindrical and toroidal parts. But a fillet touches two faces you already know:
  plane–plane ⇒ cylinder with axis `n₁ × n₂`; plane–cylinder ⊥ plane ⇒ torus on the cylinder's
  axis. Its centre is at `corner + s·r·(n₁ + n₂)` (s = +1 concave, −1 convex, normals outward), so
  the profile has ONE free parameter. Do a consensus search over r (how many vertices fall on the
  circle), refine r with only the agreeing points, require the inliers to cover ≥ 60 % of the
  expected arc (a wrong circle touches only near one tangency), keep only the largest connected
  piece (walls of two different legs can be coplanar — one "fillet" spanned 120 mm across the part
  before this), and snap r to a round value (multiple of 0.05 within 0.015).
- **Know when to stop.** A corner blend where an R5 wall fillet runs into a tip and dies against
  another face: meridian sections at 11 angles gave no circular profile (best tangent circle
  residual up to 0.09 mm, "radius" wandering 2.7–4.7). That is free-form; report it as such
  instead of forcing a torus. The deviation map (only Z 35–40 at the four legs) is what makes that
  call defensible.

Result on that body: −1 387 mm³ → −58 mm³ (0.24 %), deviation only inside the free-form blends.

## Batch conversion into one assembly

Convert several mesh bodies at once, each into its own new part; to put them together, save
each part, **close it**, then `Occurrences.AddByFilename(path)` into a new assembly and
`PutMatrix(identity, Replace: true)` (fallback `PutOrigin(0,0,0)`). Each solid was built in its
mesh's own coordinates, so the identity placement reproduces the original arrangement with no
positioning at all — checked by reading the occurrences back (origin 0,0,0, rotation 0). Never
overwrite: a taken name gets " (2)", " (3)". Six bodies + assembly took 100 s.

## Choosing the extrusion direction, and forecasting before building (2026-10-11)

Not every 2.5D part is 2.5D in Z (a bent U channel is 2.5D in Y; parts rotated 30° in space are
2.5D along no world axis). Do not rewrite the section for arbitrary directions — **move the mesh
and the recognised surfaces into a frame whose local Z is the chosen direction**, plan there, and
send only the build back to world:
- World axis → **cyclic permutation** of coordinates (a proper rotation, exact arithmetic: a
  normal (0,1,0) becomes exactly (0,0,1), so every `== 0 / == 1` test in the section still works).
- Arbitrary direction → orthonormal basis, then snap local components below 1e-5 to 0 (the
  rotated fit carries 1e-7 noise and otherwise no wall is "vertical").
- Sketch in any frame: CS by matrix whose rows are the local axes + `Convert3DCoordinate` from
  world points; the derived RefPlane does not inherit the CS axes.

**Forecast the solid against the mesh by points BEFORE building** (60k points in the box: inside
the mesh vs inside bands + corrections in build order; count only disagreements farther than
0.3 mm from the surface). It is what chooses the direction (Z unless its forecast error is above
max(1 mm³, 0.1 % of volume); then X, Y and the normals of the 4 largest off-axis plane groups are
ALL evaluated — stopping at the first "acceptable" one picked a side wall of a rotated V) and
what tells the user "out of reach" (> 2 % of volume) before a 30-minute build. Ties (within
max(1 mm³, 0.1 %)) go to fewer bands, then fewer mesh-copied segments.
- Inside-the-mesh test: **winding**, not parity, on a column grid (ray along Z per 0.5 mm cell).
  Real STL has **overlapping shells**; the signed-volume formula then undercounts (2 367 vs
  2 702 mm³ measured by points) and a correct solid looks "13 % too big". SE treats overlap as
  material — so must the forecast, and the report must quote the measured volume.
- Make it fast: integer (Sunday) winding with a bounding box per polygon, not a sum of `atan2`.

## Band steps: the error the per-surface corrections do not cover (2026-10-12)

Per-surface corrections (corner between arc and tangents, segment between arc and chord, the two
half-rectangle triangles of a ramp) only fix the band error when the band edge falls inside them.
On a divider whose top is "45° ramp → R20 → flat" they left a material triangle above the tangent
line, a whole ramp stair-stepped (its 5 test points all landed on the side the band already got
right → "already agrees"), and blocks up to 3.4 mm above a finger-scoop curve made of 4 arcs.
- **Band step region**: for each band crossing a lying cylinder (axis ⟂ extrusion) or an
  inclined plane, the area between the arc/line and the band's REAL edge — a vertical line taken
  from the band's own loops on the section line — split where they cross. Arcs split into pieces
  monotonic in height first. The mesh decides add/cut as for any region.
- Accept a step only if it leaves ≥ 0.5 mm³ **not covered** by already-accepted regions of the
  same sign, measured on a dense grid — sparse test points said "covered" while the triangle the
  step exists for sat just outside the corner region. The threshold matters: on a part full of
  R0.5 rounds, 133 of 137 steps were hundredth-mm slivers.
- Thousands of candidates × exact solid-angle winding over 65k triangles = 91 s; the column grid
  answers "inside the mesh?" for them in microseconds.
- A plan that needs steps is not a "clean Z": evaluate the other directions and let the tie
  rule decide (otherwise a link went back to 27 stepped bands + 61 corrections instead of 11).

**Hairline gaps and wafers.** Exact values from recognition and raw mesh coordinates differ by
1e-5…4e-4 mm (the STL had the wall x = 20.8 at 20.8004; the arc end at 46.94215, the band level at
46.94214). Each mismatch left a 10 nm crack or a 0.4 µm wafer in the solid — invisible in volume,
2.3 mm in the solid → mesh deviation (the sample lands on the internal face). Cure at the source:
step edges within 0.001 mm of a band level go TO the level (extend the arc a hair); extents along
the extrusion snap to recognised planes (within 0.02 mm); a junction between a recognised wall and
a mesh-copied segment is projected ONTO the wall (within 0.05 mm) instead of keeping the mesh point.
