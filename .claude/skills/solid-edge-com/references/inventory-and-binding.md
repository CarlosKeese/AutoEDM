# The API inventory, and early binding with the shipped interops

Measured against **SE 2026 build 226.00.08.004 x64**, 2026-10-17.

---

## What a full inventory of this install looks like

Reading every type library (loose `.tlb` **and** the embedded ones) with
`LoadTypeLibEx` + `REGKIND_NONE`:

| | |
|---|---|
| type libraries read | **45** (1 failed: `FEAAnalysisAdaptor.tlb`, `TYPE_E_CANTLOADLIBRARY`) |
| types | **3 737** |
| interfaces + dispinterfaces | **2 147** |
| coclasses | **193** |
| enums / values | **1 371 / 12 049** |
| members | **51 918** |
| parameters | **76 348** |
| `[out]` or `[in,out]` parameters | **29 772** |
| `[optional]` parameters | **6 488** |
| coclasses with an event interface | **45** |

The loose `.tlb` files are **33**; the other 11 type libraries are **embedded as resources**
in `.dll`/`.exe`/`.ocx`/`.dcx`. A `*.tlb`-only sweep misses `PropAuto.dll` (49 members),
`SEScreenCapture.dll` (25), `autosketch.dll` (21 types), `basic.dcx` (24 members),
`CADDBSr.exe` (13 types), `DiskVol.dll`, `SEInstallData.dll`, and **`SECatiaV5Trans.exe`,
which has no `.tlb` at all**. Find them by walking `HKCR\TypeLib` and keeping the entries
whose `win64`/`win32` path points into the SE install.

Core libraries and their LIBIDs:

| file | typelib | types | members |
|---|---|---|---|
| `Part.tlb` | `SolidEdgePart` `{8a7efa42-f000-11d1-bdfc-080036b4d502}` | 1091 | 22 542 |
| `constant.tlb` | `SolidEdgeConstants` `{c467a6f5-27ed-11d2-be30-080036b4d502}` | 754 | 0 |
| `framewrk.tlb` | `SolidEdgeFramework` `{8a7efa3a-f000-11d1-bdfc-080036b4d502}` | 465 | 4 876 |
| `assembly.tlb` | `SolidEdgeAssembly` `{3e2b3bd4-f0b9-11d1-bdfd-080036b4d502}` | 363 | 6 142 |
| `fwksupp.tlb` | `SolidEdgeFrameworkSupport` `{943ac5c6-f4db-11d1-be00-080036b4d502}` | 348 | 9 106 |
| `draft.tlb` | `SolidEdgeDraft` `{3e2b3bdc-f0b9-11d1-bdfd-080036b4d502}` | 276 | 6 174 |
| `geometry.tlb` | `SolidEdgeGeometry` `{3e2b3be1-f0b9-11d1-bdfd-080036b4d502}` | 74 | 944 |

**The typelib version is `1.0` for every library** — it does **not** track the SE version.
You cannot detect the SE release from `TLIBATTR`. Use the registry (below) or
`Application.Version`.

## Finding the install — the registry key MOVED

```
HKLM\SOFTWARE\Unigraphics Solutions\Solid Edge\Version *\CurrentVersion   <- DOES NOT EXIST on 2026
HKLM\SOFTWARE\Siemens\Solid Edge\Version 226\CurrentVersion               <- it is here
```

Code that only looks under `Unigraphics Solutions` **fails silently** on SE 2026. Try both
vendors, newest version first:

```csharp
static string? SolidEdgeProgramFolder()
{
    foreach (var vendor in new[] { "Siemens", "Unigraphics Solutions" })
    {
        using var se = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\{vendor}\Solid Edge");
        if (se is null) continue;
        foreach (var v in se.GetSubKeyNames()
                     .Where(n => n.StartsWith("Version", StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(n => n, StringComparer.OrdinalIgnoreCase))
        {
            using var cv = se.OpenSubKey($@"{v}\CurrentVersion");
            var p = cv?.GetValue("InstallPath") as string ?? cv?.GetValue("PathName") as string;
            if (!string.IsNullOrWhiteSpace(p) && Directory.Exists(p)) return p;
        }
    }
    return null;
}
```

Useful values under `CurrentVersion`: `InstallDir`, `InstallPath` / `PathName` (the
`Program` folder), `PreferencesPath`, `Version` (`226.00`), `Build`
(`226.00.08.004 x64`), `InstalledLanguage` (1046 = pt-BR — see `localization.md`).

## The interops ship WITH the install

```
C:\Program Files\Siemens\Solid Edge 2026\Program\interop.SolidEdge*Lib.dll
```

13 assemblies, all version **226.0.0.106**, `anycpu`, .NET Framework metadata
(`v4.0.30319`), strong-named (token `f67e9f7675c793b1`):

`FrameworkLib` · `FrameworkSupportLib` · `PartLib` · `AssemblyLib` · `DraftLib` ·
`ConstantLib` · `GeometryLib` · `RevisionManagerLib` · `InstallDataLib` · `PropAutoLib` ·
`ScreenCaptureLib` · `StructureEditorLib` · `ViewAndMarkupLib`

Three consequences:

1. **You do not need `tlbimp`** — and it is frequently absent (not present anywhere under
   `Program Files` on the measured machine, and there is no
   `Microsoft SDKs\Windows\*\bin\NETFX * Tools`).
2. **Do not use the community NuGet `Interop.SolidEdge`.** It lags the installed build —
   on this machine a production add-in referenced `Interop.SolidEdge 219.0.0.0` and
   `SolidEdge.Community.AddIn 219.0.0.0` (**SE 2019** packages) against an SE **2026**
   install. That is also the package responsible for the known parameter-direction
   divergence (see below).
3. These are the right input for static analysis tools, which read PE/CLI metadata — a
   `.tlb` is not PE/CLI, so no tool reads it directly.

### Namespaces differ from the community package

| shipped interop | community NuGet |
|---|---|
| `SolidEdge.Part.Interop.Profile` | `SolidEdgePart.Profile` |
| `SolidEdge.Geometry.Interop.Face` | `SolidEdgeGeometry.Face` |
| `SolidEdge.Framework.Interop.Application` | `SolidEdgeFramework.Application` |

Code written against one does not compile against the other. Check which is referenced
before copying a snippet.

### Static vs live: they agree

Cross-checking the typelibs against the shipped interops, **46 294 members compared**:

- **0** arity divergences
- **0** members present in the typelib and missing from the interop
- type counts identical except for `tlbimp` scaffolding

`FrameworkLib` has 935 types against 465 in `framewrk.tlb`; all 472 extra are scaffolding:
**230 event-handler delegates**, 66 `_Event` interfaces, 66 `_SinkHelper`, 66
`_EventProvider`, 44 `…Class` coclass implementations. Only `UINT_PTR` and `LONG_PTR`
(typedefs) exist in the typelib and not the interop.

Those **230 delegates are not noise** — they are the practical way to handle events in C#
(see `draft-properties-events.md`).

Expect these `tlbimp` projections when comparing signatures:

| rule | effect |
|---|---|
| each coclass gains a `…Class` type | +1 type per coclass |
| events generate delegates + `_Event` + `_SinkHelper` + `_EventProvider` | the Framework explosion |
| `IUnknown`/`IDispatch` members are dropped | ~3 000 members gone (expected) |
| `_NewEnum` becomes `GetEnumerator` | ~890 members |
| `[retval]` becomes the return value | arity −1 |
| **`[lcid]` disappears** (the CLR supplies it) | arity −1 more on `_I…Auto` |

### Parameter direction IS preserved in the shipped interops

Measured with reflection (`ParameterAttributes.In`/`Out`):

| member | dispid | typelib | what C# sees |
|---|---|---|---|
| `Face.GetRange` | 227 | `[in,out] SAFEARRAY(double)*` | `ref System.Array` (In+Out) ✅ |
| `Occurrence.GetTransform` | 22 | `[out] double*` ×6 | `out double` ✅ |
| `SurfaceByBoundaries.Add` | 20 | `[in,out] SAFEARRAY(IDispatch)*` | `ref System.Array` (In+Out) ✅ |
| `Relations3d.AddPlanar` | 5 | `[in] SAFEARRAY(double)*` | `ref System.Array` (In only) |
| `Vertex.GetPointData` | 124 | `[in,out] SAFEARRAY(double)*` | `ref System.Array` (In+Out) ✅ |
| `Application.GetGlobalParameter` | 83 | `[in,out] VARIANT*` | `ref object` (In+Out) ✅ |

**Correction to an earlier version of this skill:** it said reflecting `Interop.SolidEdge`
imports a `SurfaceByBoundaries.Add` argument as `out` where the dump says `[in,out]`,
"which would drop your data on the way in". That does **not** reproduce on the interops
that ship with the install — direction comes through faithfully. The divergence belongs to
the **community NuGet package**, which the older note names, not to the API. Using the
shipped interops removes that whole class of problem. (The NuGet could not be re-verified
here; it is not installed.)

Also note `SAFEARRAY(double)*` surfaces as **`System.Array&`**, not `double[]&` — even in
early binding you pass `ref Array` (a variable, never a literal).

## Early binding on .NET 10 works

A `net10.0-windows` x64 project referencing the shipped interops compiled with **0 warnings
and 0 errors**, and all five referenced assemblies loaded at runtime (process .NET 10.0.9):

| assembly | ImageRuntimeVersion | types | interfaces | enums | delegates |
|---|---|---|---|---|---|
| `interop.SolidEdgeFrameworkLib` | v4.0.30319 | 935 | 411 | 116 | **230** |
| `interop.SolidEdgePartLib` | v4.0.30319 | 1091 | 915 | 176 | 0 |
| `interop.SolidEdgeGeometryLib` | v4.0.30319 | 74 | 68 | 5 | 0 |
| `interop.SolidEdgeAssemblyLib` | v4.0.30319 | 363 | 288 | 75 | 0 |
| `interop.SolidEdgeConstantLib` | v4.0.30319 | 754 | 0 | **753** | 0 |

.NET Framework metadata loads fine under .NET 10 because these are **pure interop**
assemblies — `[ComImport]` types with no implementation.

### The loading trap: referencing compiles, but does not load

```xml
<Reference Include="interop.SolidEdgePartLib">
  <HintPath>C:\Program Files\Siemens\Solid Edge 2026\Program\interop.SolidEdgePartLib.dll</HintPath>
  <EmbedInteropTypes>false</EmbedInteropTypes>
  <Private>false</Private>
</Reference>
```

At runtime you get
`FileNotFoundException: Could not load file or assembly 'interop.SolidEdgePartLib'` —
the assemblies live in `Program Files`, outside the probing path.

Do **not** fix this with `Private=true`. Copying freezes one build's interops inside your
app, and on a machine with a different SE version you would load the wrong ones. Resolve
at runtime from the detected install instead:

```csharp
var seProgram = SolidEdgeProgramFolder();
AssemblyLoadContext.Default.Resolving += (ctx, name) =>
{
    var candidate = Path.Combine(seProgram!, name.Name + ".dll");
    return File.Exists(candidate) ? ctx.LoadFromAssemblyPath(candidate) : null;
};
```

This also works for the trick in `com-rules.md` — building a typed SAFEARRAY without any
compile-time reference at all.

### When to use which

| | late binding (`dynamic`) | early binding (shipped interops) |
|---|---|---|
| compiles without SE present | ✅ | ❌ |
| survives a different SE version | ✅ | ⚠️ resolve at runtime |
| compiler catches wrong enum type | ❌ | ✅ (the `ApplicationGlobalConstants` trap) |
| typed event delegates | ⚠️ via reflection | ✅ |
| member typo caught | at runtime (`0x80131500`) | at compile time |

A good default: **late binding for the calls, early binding's types only where they buy
you something** — typed SAFEARRAY elements, event delegates, and the enums that collide.

## Querying the inventory instead of loading it

The full dump is ~45 MB of JSON. Never read it whole. `scripts/apiq.mjs`:

```
node scripts/apiq.mjs type   "^Face$"            types whose name matches
node scripts/apiq.mjs members HoleData           all members, with signatures
node scripts/apiq.mjs find   "GetRange"          members by name, any type
node scripts/apiq.mjs sig    "Face.GetRange"     full signature + per-param flags
node scripts/apiq.mjs enum   "FeatureStatus"     enum with values
node scripts/apiq.mjs const  "igQuery"           constants by name
node scripts/apiq.mjs value  1216476310          REVERSE lookup: which constant is this?
node scripts/apiq.mjs events                     coclasses and their event interfaces
node scripts/apiq.mjs outparams "^Face$"         members with [out]/[in,out]
node scripts/apiq.mjs stats                      per-typelib totals
```

`value` is the one people forget and need most: a magic number in a log becomes a name.
