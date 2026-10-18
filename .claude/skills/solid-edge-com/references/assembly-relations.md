# Assembly relations that actually govern

Two sources, kept separate on purpose:

- **Field evidence (2026-10-01):** a full mold-base assembly — 45 relations, 18
  occurrences, three planars per plate. All reported `Status = 1` and governed nothing.
- **Controlled re-test (2026-10-17):** SE 2026 build 226.00.08.004 x64, one planar
  relation between two blocks, every value read back.

Where they disagree the disagreement is stated, because the conditions differ and the
*condition* is the useful part.

---

## Signature, from the typelib

```
Relations3d.AddPlanar([in] IDispatch Plane1, [in] IDispatch Plane2,
                      [in] VARIANT_BOOL NormalsAligned,
                      [in] SAFEARRAY(double)* ConstrainingPoint1,
                      [in] SAFEARRAY(double)* ConstrainingPoint2) -> PlanarRelation3d*
                      dispid 5, vtbl offset 72
Relations3d.AddAxial([in] IDispatch Axis1, [in] IDispatch Axis2,
                     [in] VARIANT_BOOL NormalsAligned) -> AxialRelation3d*     dispid 6
Relations3d.AddGround([in] Occurrence* Occurrence) -> IDispatch                dispid 4
AssemblyDocument.CreateReference([in] Occurrence* Occurrence,
                                 [in] VARIANT Entity) -> IDispatch             dispid 38
```

**`AddPlanar` has 5 parameters and ZERO optional ones** — confirmed in the inventory. There
is no "call it without the points" form. In C# the two `[in] SAFEARRAY(double)*` arrive as
`ref System.Array`, so pass a variable, never a literal.

`CreateReference` returns a `Reference` from the **`SolidEdgeFramework`** typelib, not
`SolidEdgeAssembly`.

## Trap 1 — `AddByFilename` GROUNDS every occurrence ✅ confirmed twice

```
Occurrences.AddByFilename([in] BSTR OccurrenceFileName,
                          [in opt] VARIANT UseSimplifiedPart) -> Occurrence*   dispid 4
```

Measured directly:

| step | `Relations3d.Count` |
|---|---|
| empty assembly | **0** |
| after 1st insert | **1** |
| after 2nd insert | **2** |

Each is a **`GroundRelation3d`**, and the inserted occurrence reports
`Status = 2` (`seOccurrenceStatusFixed`). In the field case, 18 inserts produced 18
automatic grounds.

**Consequence:** a relation created between two occurrences that are *both* grounded comes
back `DetailedStatus = 4` (`BetweenFixed`) — it reports success and is **inert**. Free the
side that is supposed to move before relating.

```csharp
// walk BACKWARDS: Delete() reindexes the collection
static bool RemoveGround(dynamic doc, string occurrenceName)
{
    for (int i = (int)doc.Relations3d.Count; i >= 1; i--)
    {
        dynamic r = doc.Relations3d.Item(i);
        string owner;
        try { owner = (string)r.Occurrence.Name; } catch { continue; }
        if (owner != occurrenceName) continue;
        r.Delete();
        return true;
    }
    return false;
}
```

The `try/catch` is not decoration: there are relations whose `Occurrence`/`Occurrence1`
reads `E_FAIL`, and `Delete()` on the wrong item is destructive. Occurrence names look like
`file.par:1` and are **not** translated — they come from the filename.

Keep one part grounded as the datum; delete the rest.

## Trap 2 — the `NormalsAligned` flag ⚠️ status downgraded

The field note said the flag **reads backwards**: face-to-face CONTACT (outward normals
opposed) = `true`; FLUSH (coplanar, same direction) = `false`; and that with the
"intuitive" value the solver flipped a plate and a guide pillar 180°.

`api-signatures.md` still carries an older, contradictory line
(`NormalsAligned True=Align / False=Mate`). **Those two cannot both be right.**

The 2026-10-17 re-test **could not settle it**: the two faces used were *perpendicular*
faces of a block, so the 90° result was geometrically correct and there was no
contact-vs-coplanar expectation to compare against. **Unverified.**

To settle it, test with **two parallel faces** and a known expected outcome, and read
`GetTransform` before and after. Until then: treat the field observation as the working
hypothesis (it came from a real assembly where a plate visibly flipped), and **verify the
flag empirically in your own case** rather than trusting either line.

## Settling Trap 2 without settling it: the position-preserving oracle

*(AutoEDM, 2026-10-18 — design confirmed by construction and unit tests; the live run in SE is
still pending, so what is claimed here is the method, not a measurement.)*

When the assembly is **already in the correct position** — an import from another CAD, or any
model you are adding relations to after the fact — you do not need to know what
`NormalsAligned` means. The geometry gives you an oracle:

> A correct relation is one that **solves AND moves nothing.**

So instead of choosing the flag, **measure it**:

```
snapshot the pose (GetMatrix) of every occurrence that can move
for flag in [true, false]:
    countBefore = Relations3d.Count
    rel = AddPlanar(...) / AddAxial(...)   // may throw
    if rel == null: delete anything above countBefore (Trap 4); try next flag
    if DetailedStatus == Solved and occ.Status != OverDefined and nothing moved:
        keep it — and RECORD which flag worked
    else:
        rel.Delete(); delete anything above countBefore; try next flag
discard the relation and restore the ground if both flags move the part
```

Three details that matter:

- **Measure rotation, not just origin.** A 180° flip about the centre of a symmetric plate
  barely moves the origin — and that flip is exactly the damage a wrong flag does. Compare the
  rotation matrix element-wise too, and scale the worst component difference by a part-sized
  lever arm before comparing against a linear tolerance.
- **Only the un-grounded occurrences can move,** so only those need re-reading each attempt.
  Re-reading every occurrence costs one COM round-trip per part per attempt.
- **Re-acquire the faces by `Face.ID`** (`Body.GetFaceByFaceID`, dispid 514) immediately before
  each `CreateReference`. The assembly regenerates when a relation is created, so a face proxy
  captured during an earlier scan is stale by the second relation — this is Trap 5 arriving on
  schedule rather than by accident. Caching the proxy makes only the first relation work.

This does not tell you what the flag *means*, and it is not a substitute for the parallel-face
experiment the trap asks for. What it does is make the ambiguity **harmless** in the one case
where relations are being inferred rather than authored — and, as a side effect, every run
reports which flag won, which is the data that would settle the trap.

## Trap 3 — `ConstrainingPoint1/2` ⚠️ CORRECTED

**Field claim:** passing `(0,0,0)` for every relation left plates `OverDefined`
(`Occurrence.Status = 32776`) and relations `DetailedStatus = 5` (`Unsatisfied`) even
though the geometry was right.

**Measured 2026-10-17, one planar relation between two blocks:**

| | `ConstrainingPoint = (0,0,0)` | real vertex points |
|---|---|---|
| `PlanarRelation3d.DetailedStatus` | **1 (Solved)** | **1 (Solved)** |
| `occ2.Status` | **4 (UnderDefined)** | **4 (UnderDefined)** |

With `(0,0,0)` the relation **solved normally**. The claim as written — "(0,0,0) makes the
relation fail" — **does not hold in general**.

**This does not refute the field observation; it conditions it.** Over-definition is a
phenomenon of the *set*: it appears when several relations compete for the same degrees of
freedom, and that is where a meaningless constraining point lets the solver pick the wrong
solution. One planar between two blocks has slack to spare (hence `UnderDefined`), so the
point never has to break a tie.

**What to carry forward:** passing a real vertex point is still good practice — it costs
almost nothing and removes a variable — but **not** for the reason "otherwise the relation
fails". In a heavily constrained assembly (three planars per plate, many plates) a bogus
point is a plausible cause of `OverDefined`/`Unsatisfied`; in a loose one it is harmless.
Reproducing the failure requires a multi-relation case, which has **not** been done.

### Getting a real point ✅

```
Vertex.GetPointData([in,out] SAFEARRAY(double)* Point) -> void      dispid 124
```

```csharp
dynamic v = face.Vertices.Item(1);
object p = new double[3];       // [in,out]: must arrive ALLOCATED; ref null -> E_INVALIDARG
v.GetPointData(ref p);
double[] pt = (double[])p;      // e.g. { -0.03, -0.02, 0 }
```

Measured on a 60 × 40 block: `{-0.03,-0.02,0}` and `{0.03,-0.02,0}` — consistent with the
dimensions, and **in PART coordinates**.

⚠️ To serve as a `ConstrainingPoint` in an assembly where the occurrence is moved or
rotated, this point must be **transformed by the occurrence**. In the test the occurrences
sat at the origin, so that step was **not exercised** — the measurement establishes where
the data comes from, not that the transform is unnecessary.

A face **vertex** is a safe point. A bounding-box centre is **not** — it can fall inside a
hole.

## Trap 4 — a failed relation call can still leave a relation behind

**Field observation (not re-tested):** `AddPlanar` with a `RefPlane`/`AsmRefPlane` returns
`E_INVALIDARG` **and still creates a hidden `PlanarRelation3d`** whose `Occurrence1` reads
`E_FAIL`. Two such orphans silently over-constrained a plate.

**So: after any failed relation call, list `Relations3d` and delete what you did not mean
to create.** This costs one loop and prevents a class of bug that is invisible otherwise.

## Trap 5 — face proxies go stale ✅ practice validated

After the assembly regenerates (relations added, close/reopen),
`CreateReference(occ, oldFace)` → `E_FAIL` and `face.Vertices` → `E_FAIL`.

**Re-read the faces immediately before each relation:**

```csharp
dynamic f = occ.OccurrenceDocument.Models.Item(1).Body.Faces[igQueryPlane].Item(1);
var r = doc.CreateReference(occ, f);     // use it NOW
```

In the re-test, re-reading before every attempt worked every time. The stale-proxy failure
itself was **not provoked deliberately**, so the failure mode stays field-sourced — but
the practice that avoids it is confirmed.

## Verify every relation THREE ways

Status on the relation alone proves nothing.

```csharp
var ds = Convert.ToInt64(rel.DetailedStatus);
var st = Convert.ToInt64(occ.Status);
occ.GetTransform(out double x, out double y, out double z,
                 out double ax, out double ay, out double az);
```

`Relation3dDetailedStatusConstants` (confirmed in the inventory):

| value | name | meaning |
|---|---|---|
| 0 | Unknown | |
| 1 | Solved | |
| 2 | Suppressed | |
| 3 | BetweenSetMembers | |
| **4** | **BetweenFixed** | **inert — both sides grounded** |
| 5 | Unsatisfied | geometry or points wrong |
| 6 | MissingGeometry | reference died |

`OccurrenceStatusConstants` (confirmed):

| value | name |
|---|---|
| 1 | WellDefined |
| 2 | Fixed |
| 4 | UnderDefined |
| **32776** | **OverDefined** |
| 32784 | NotConsistent |
| 32800 | NotChanged |
| 32832 | NonAlgebraic |
| 32896 | Unknown |

And **`GetTransform` is the only way to see that the relation actually moved the part.**
Measured after a successful planar:

```
origin (m)   -0.05, 0.01, 0
angles       0, -0, 1.5707963267948966      <- exactly π/2
```

So **the angles are RADIANS**, confirmed numerically, and the solver moved *and* rotated
the occurrence — which is the proof that the relation governs rather than merely reporting
`Solved`. A part that "moved" or "rotated 180°" is the only visible symptom of a wrong flag.

## Other confirmed facts

- **`PlanarRelation3d.Offset` sign** (field, metres): coordinate of face 1 minus coordinate
  of face 2 along the face axis (face 1 at X −225, face 2 at X −200 → `Offset = −0.025`).
  The first guess (+0.025) moved the plate 50 mm the wrong way.
- **`AddAxial(cylRef1, cylRef2, true)`** worked for pin-in-hole and bushing-in-bore, paired
  with one planar (flush of head top with plate face). *(field; not re-tested)*
- `CreateReference`'s entity must come from that occurrence's document **in this session**.
- **`PutOrigin` works on a grounded occurrence.** `PutOrigin(0.050, 0.030, 0.010)`
  (dispid 14) succeeded on a grounded part and `GetTransform` returned exactly those
  values. Ground stops the *solver* from moving it, not you. (`Move(dx,dy,dz)` is dispid 12.)
- **`UpdateAll()` and close/reopen do NOT repair bad relations** — status only changes when
  the relation is recreated correctly. *(field; in the re-test `UpdateAll()` ran clean but
  there was no bad relation to repair, so not re-verified.)*
- Plates built centred on the origin with three planars each (one contact + two flushes)
  reached `Status = 1` on every occurrence once points and flags were right. Rotational
  redundancy of three planars is accepted by the solver; the over-definition was entirely
  the bad points. *(field)*

## Navigation path

```
AssemblyDocument
  ├─ Occurrences.AddByFilename(path) -> Occurrence        (auto-grounds!)
  │    ├─ Name / Status / GetTransform(out ×6) / PutOrigin / Move
  │    └─ OccurrenceDocument.Models.Item(1).Body.Faces[q]   <- read FRESH
  ├─ Relations3d
  │    ├─ Item(i).Occurrence.Name / .DetailedStatus / .Delete()
  │    └─ AddGround / AddPlanar / AddAxial
  ├─ CreateReference(occ, entity) -> Reference (SolidEdgeFramework)
  └─ UpdateAll()
```

`StructuralFrames` / `StructuralFrame` (54 members) also live in `SolidEdgeAssembly` —
not tested.
