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
