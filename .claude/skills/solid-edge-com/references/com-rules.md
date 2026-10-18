# COM marshaling and enum rules — the long form

Measured against **SE 2026 build 226.00.08.004 x64**, .NET 10.0.9, late binding with
`dynamic` unless stated, 2026-10-17.

---

## Out-parameters: it depends on WHICH late binding

This corrects an earlier version of this skill, which said out-params "come back empty in
late binding unless you mark them by-ref with a `ParameterModifier`". That is true for
*one* of the two mechanisms.

To make the test decisive the occurrence was **moved first**
(`PutOrigin(0.050, 0.030, 0.010)`) — on a part sitting at the origin, a correct `0,0,0` and
a marshaling-failure `0,0,0` are indistinguishable.

### `Occurrence.GetTransform` — `[out] double*` × 6, dispid 22

| mechanism | result |
|---|---|
| `dynamic` + `out double` | **`0.05, 0.03, 0.01`** ✅ |
| `Type.InvokeMember` **without** `ParameterModifier` | **`0, 0, 0, 0, 0, 0`** ❌ |
| `Type.InvokeMember` **with** `ParameterModifier` | **`0.05, 0.03, 0.01`** ✅ |

### `Face.GetRange` — `[in,out] SAFEARRAY(double)*` × 2, dispid 227

Cylinder R20 × 10 mm:

| mechanism | result |
|---|---|
| `dynamic` + `ref object` holding `double[3]` | `{-0.02,-0.02,0}` / `{0.02,0.02,0.01}` ✅ |
| `InvokeMember` **without** modifier | `{0,0,0}` / `{0,0,0}` ❌ |
| `InvokeMember` **with** modifier | `{-0.02,-0.02,0}` / `{0.02,0.02,0.01}` ✅ |
| `dynamic` + `ref null` | **fails** `E_INVALIDARG` "Could not convert argument 1" |

**The rule:** `ParameterModifier` is needed **only** on the `Type.InvokeMember` path — and
there it is mandatory, because without it the call *succeeds* and returns plausible zeros.
With `dynamic` + `ref`/`out` the C# binder marshals by-ref correctly on its own.

**Prefer `dynamic`.** It is shorter and it removes a whole class of silent failure.

```csharp
// works, no ParameterModifier anywhere
object min = new double[3], max = new double[3];
face.GetRange(ref min, ref max);
var (lo, hi) = ((double[])min, (double[])max);

occ.GetTransform(out double x, out double y, out double z,
                 out double ax, out double ay, out double az);
```

When you *must* use `InvokeMember` (e.g. to pass 21 positional args):

```csharp
var mods = new ParameterModifier(2); mods[0] = true; mods[1] = true;
face.GetType().InvokeMember("GetRange", BindingFlags.InvokeMethod,
    null, face, args, [mods], null, null);
```

### `[in,out]` is not `[out]`

`Face.GetRange`, `Face.GetExactRange`, `Vertex.GetPointData`, `Application.GetGlobalParameter`
are **`[in,out]`**: the array or VARIANT must arrive **allocated**. `ref null` fails.
`Occurrence.GetTransform` and `Profile.Convert2DCoordinate` are `[out]` pure. The inventory
tells you which: `node scripts/apiq.mjs sig "Face.GetRange"`.

There are **1 406** `[in,out]` parameters in the SE typelibs (878 in `Part.tlb` alone), so
this is not an edge case.

## SAFEARRAY parameters need a TYPED element array

```csharp
// WRONG — object[] marshals as SAFEARRAY(VARIANT)
doc.Models.AddFiniteExtrudedProtrusion(1, new object[] { prof }, igRight, 0.010);
//   -> E_INVALIDARG (0x80070057) "Could not convert argument 2 for call to
//      AddFiniteExtrudedProtrusion."
```

**The HRESULT is `E_INVALIDARG`, not `DISP_E_TYPEMISMATCH`.** With `dynamic` on .NET 10 the
C# binder fails the conversion *before* the call leaves your process, so grepping a log for
`DISP_E_TYPEMISMATCH` finds nothing. The consequence the older note drew is still exactly
right and worth repeating: **the call never reached the command, so a marshaling failure
says nothing about whether SE can do the operation.** Do not record it as "SE can't do X".

```csharp
// RIGHT — SAFEARRAY(IDispatch), with no compile-time reference to the interop
static Array TypedArray(string interopTypeFullName, params object[] items)
{
    var t = Type.GetType(interopTypeFullName, false) ?? ResolveFromInstall(interopTypeFullName)
            ?? throw new InvalidOperationException(interopTypeFullName);
    var arr = Array.CreateInstance(t, items.Length);
    for (var i = 0; i < items.Length; i++) arr.SetValue(items[i], i);
    return arr;
}
// doc.Models.AddFiniteExtrudedProtrusion(
//     1, TypedArray("SolidEdge.Part.Interop.Profile", prof), igRight, 0.010);
```

`ResolveFromInstall` loads `interop.SolidEdge*Lib.dll` from the SE `Program` folder — see
`inventory-and-binding.md`.

**Mind the namespace.** The interops that ship with the install use
`SolidEdge.<Domain>.Interop` (`SolidEdge.Part.Interop.Profile`,
`SolidEdge.Geometry.Interop.Face`). The community NuGet uses `SolidEdgePart`,
`SolidEdgeGeometry`. Code written for one does not compile against the other.

This failure recurs because `object[]` is the natural thing to write. It has bitten
`CopySurfaces.Add`, the extrusion calls and `Sketches.CreateSectionSketches`. **Whenever the
inventory says `SAFEARRAY(IDispatch)*`, reach for the typed array before running anything.**

## Omitting optional parameters in the middle of a list

`Type.Missing` is the way:

```csharp
// HoleDataCollection.Add(HoleType, HoleDiameter, …, TreatmentType is the 8th, …)
doc.HoleDataCollection.Add(igRegularHole, 0.008,
    Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing,
    igRegularThread);
```

For the 21st parameter (`IgnoreSavedDefaultValues`) you need all 21 positions, which is
easier through `InvokeMember` with an `object[21]`.

There are **6 488** `[optional]` parameters across the API. A parameter the inventory does
**not** mark `[opt]` is mandatory — passing nothing gets you
`DISP_E_PARAMNOTOPTIONAL (0x8002000F)`.

## A member that does not exist gives `RuntimeBinderException`, not `DISP_E_MEMBERNOTFOUND`

```
'System.__ComObject' does not contain a definition for 'Diameter'      -> HRESULT 0x80131500
```

This is the C# binder, not SE. It means **the member does not exist on that object**.
It happened three times in one session, and all three times the *name* was wrong:

| written | actual | dispid |
|---|---|---|
| `HoleData.Diameter` | **`HoleData.HoleDiameter`** | 23 |
| `HoleData.IgnoreSavedDefaultValues` | **not a property** — the 21st *parameter* of `HoleDataCollection.Add` | — |
| `FaceStyle.Name` | **`FaceStyle.StyleName`** | 1 |

The right response is to query the inventory, not to try variations:

```
node scripts/apiq.mjs members HoleData | grep -i diameter
node scripts/apiq.mjs find "^StyleName$"
```

The wrong response is concluding "SE does not support this".

Sibling error: **`0x80040B50`** is SE's own "item not found in collection" (bad name or
index) — see `localization.md`, because the usual cause is an English name on a translated
install.

## Enum comparisons have no single integer type

| enum | example | compare as |
|---|---|---|
| `FeatureStatusConstants` | `igFeatureOK = 1216476310` | **`uint`** — exceeds `int.MaxValue` |
| `GNTTypePropertyConstants` | `igCylinder = -114972029` | **`int`** — it is negative |

`Convert.ToUInt32(feature.Status)` works directly on the `dynamic`. Converting
`Face.Geometry.Type` to `uint` breaks the comparison silently. **Check the value in the
inventory before choosing the type.**

Confirmed values:

```
FeatureStatusConstants : 1216476310 OK · ...11 Failed · ...12 Warned
                         ...13 Suppressed · ...14 RolledBack
GNTTypePropertyConstants: igFace = 167551075 · igCylinder = -114972029
FeatureTopologyQueryTypeConstants: 1 All · 2 Roundable · 3 Straight · 4 Ellipse
                         5 Spline · 6 Plane · 7 Cone · 8 Torus · 9 Sphere · 10 Cylinder
ProfileValidationType  : 1 igProfileClosed · 4 Single · 8 NoSelfIntersect
                         16 RefAxisRequired · 32 NoRefAxisIntersect
                         8192 AllowNested · 524288 AllowPointsAsProfiles
FeaturePropertyConstants: 1 igLeft · 2 igRight · 3 igSymmetric · 4 igInside · 5 igOutside
                         33 igRegularHole · 34 Counterbore · 35 Countersink · 38 Tapered
                         164 igRegularThread · 166 igTaperedPipeThread
ModelingModeConstants  : 1 Synchronous · 2 Ordered
```

## The same enum NAME can hold DIFFERENT values in different typelibs

Of 785 distinct enum names, **561 appear in more than one typelib**. In 559 of those the
values are identical. In **two** they are not — and both are on a well-travelled path.

### `ApplicationGlobalConstants`

| typelib | members | `seApplicationGlobalCheckInOnClose` | `…OfflineMode` |
|---|---|---|---|
| `constant.tlb`, `framewrk.tlb` | 497 | **20** | **25** |
| `DesMgr.tlb`, `RevMgr.tlb` | 24 | **0** | **5** |

**20 constants share a name and differ in value**, shifted as a block. These feed
`Application.GetGlobalParameter` / `SetGlobalParameter`. Taking the Revision Manager
numbering and passing it to the Framework `Application` **reads or writes the wrong global
parameter with no error at all**.

Use the `framewrk.tlb`/`constant.tlb` numbering for `Application`. The `DesMgr`/`RevMgr`
numbering is valid only inside those libraries (both document-management, and the registry
itself labels the Revision Manager *Deprecated*).

With early binding against the shipped interops the compiler protects you, because they are
distinct types (`SolidEdge.Framework.Interop.ApplicationGlobalConstants` ≠
`SolidEdge.RevisionManager.Interop.ApplicationGlobalConstants`) — as long as the `using` is
right. In late binding with a literal number there is no net.

### `DocumentStatus`

7 constants in `constant.tlb`, 6 in `DesMgr.tlb` and `RevMgr.tlb`. Same caution.

`constant.tlb` is a **constants-only** library: 754 types, 745 enums, zero interfaces, zero
methods. It is the canonical source of values.

## A hidden `[lcid]` parameter on the dual interfaces

The `_I…Auto` dual interfaces carry an `[in, lcid] int` parameter that **the CLR supplies
itself**, so it is invisible from C#:

```
typelib : _IDrawingViewAuto.Name([in opt] VARIANT Recurse,
                                 [in,lcid] int Lcid,
                                 [out,retval] BSTR* Name) -> HRESULT      dispid 24679
C#      : one parameter
public  : DrawingView.Name([in opt] VARIANT Recurse) -> BSTR              dispid 24679
```

Through `dynamic`/IDispatch you use the public form and never see the LCID. Only a caller
invoking the raw dual vtable has to supply it. Mentioned because when you compare a typelib
signature against a C# signature and the arity is off by one or two, `[retval]` and
`[lcid]` are usually the explanation — not a wrong signature.

## Feature status, and why status is not enough

```csharp
uint st = Convert.ToUInt32(feature.Status);
if (st != 1216476310u)
{
    feature.Delete();   // SE does NOT roll a failed feature back; it stays red in PathFinder
    throw new InvalidOperationException($"feature failed, Status = {st}");
}
```

Then **measure**, because a successful `Add` can hand back something you did not ask for:

```csharp
double r = Convert.ToDouble(model.Body.Faces[10 /*igQueryCylinder*/].Item(1).Geometry.Radius);
// asked Ø8 mm -> measured 0.004  ✓
```

Two mechanisms make this necessary, usually together: flat enum namespaces accept any
value that is legal *somewhere*, and SE merges the user's **saved dialog defaults** into
your object unless you pass `IgnoreSavedDefaultValues`. A hole reported OK once came out
Ø6.0 counterdrilled where Ø5.0 tapped was requested, and nothing in the API said so.
