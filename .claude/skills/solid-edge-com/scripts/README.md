# scripts/ — discover the API instead of guessing

Three things live here: a **static inventory builder**, a **live introspection tool**, and
**pre-built greppable indexes** of one real installation.

Start with the indexes — they answer most questions with no build and no running SE.

---

## 1. `index/` — grep this first

Generated from a full read of **SE 2026 build 226.00.08.004 x64** (pt-BR install). Plain
text, one line per item, meant for `grep` and **never** to be read whole.

| file | lines | what |
|---|---|---|
| `index/members.txt` | ~21 400 | `Type.Member <tab> kind <tab> Nparams <tab> dDISPID <tab> return <tab> typelib [<tab>OUT]` |
| `index/enums.txt` | ~7 800 | `VALUE <tab> NAME <tab> ENUM <tab> TYPELIBS` |
| `index/types.txt` | ~3 700 | `kind <tab> NAME <tab> typelib <tab> Nmembers <tab> Nvars <tab> guid` |

```bash
# does this member exist, and what is its shape?
grep -P '^HoleData\.' index/members.txt | grep -i diameter
#   HoleData.HoleDiameter   prop rw   0p   d23   double   SolidEdgePart

# which members have out-params (the marshaling hazard)?
grep -P 'OUT$' index/members.txt | grep -P '^Face\.'

# reverse lookup: what is this magic number in my log?
grep -P '^ +1216476310\t' index/enums.txt
#   1216476310  igFeatureOK  FeatureStatusConstants  SolidEdgeConstants+SolidEdgePart

# which typelib owns a type?
grep -P '\tFaceStyle\t' index/types.txt
```

Two deliberate compactions, so this fits in a skill bundle:

- the dual `_I…Auto` interfaces are **excluded** (same API as the public interface of the
  same name, only with `HRESULT` + `[retval]` + `[lcid]` spelled out);
- `propget` + `propput` collapse into one line (`prop r` / `w` / `rw`).

Enum constants that are identical across typelibs collapse into one line with the libs
joined by `+`. **When the same NAME appears on separate lines with different values, that is
a real conflict** — `ApplicationGlobalConstants` and `DocumentStatus` do this, and the
typelib decides which value you want. See `references/com-rules.md`.

**These indexes describe ONE build.** If the machine has a different SE version, regenerate
(below) rather than trusting them.

## 2. `ApiDump` — rebuild the inventory for the machine in front of you

C#, `net10.0-windows`, x64. Reads every type library via `ITypeLib`/`ITypeInfo` with
**`REGKIND_NONE`** — nothing is registered, nothing is executed, **no SE license and no
running SE needed**.

```bash
cd ApiDump && dotnet build -c Release

# all the loose .tlb in the install:
./bin/Release/net10.0-windows/ApiDump.exe \
    --dir "C:/Program Files/Siemens/Solid Edge 2026/Program" \
    --out api.json --typelibs typelibs.json

# plus the EMBEDDED ones — a *.tlb sweep alone misses these:
#   --file ".../PropAuto.dll" --file ".../SEScreenCapture.dll"
#   --file ".../autosketch.dll" --file ".../basic.dcx" --file ".../CADDBSr.exe"
#   --file ".../SEInstallData.dll" --file ".../DiskVol.dll"
#   --file ".../SE3Dtrans.exe" --file ".../SEAcisTrans.exe"
#   --file ".../SECatiaV5Trans.exe"   <- has NO .tlb at all
#   --file ".../SEPreview.ocx" --file ".../SeThumbnail.dll"
```

`api.json` is the full record: per-parameter direction (`[in]`/`[out]`/`[in,out]`/`[opt]`/
`[retval]`/`[lcid]`), DISPIDs, vtable offsets, default values, enum values, event
interfaces. It runs ~45 MB — **query it, never load it.**

Then regenerate the indexes:

```bash
node make-index.mjs --api api.json --out index
```

## 3. `apiq.mjs` — query `api.json`

Needs `api.json` (from ApiDump). Point it with `--db` or `APIQ_DB`.

```bash
node apiq.mjs sig    "Face.GetRange"     # full signature + flags per parameter
node apiq.mjs members HoleData           # every member of a type
node apiq.mjs find   "GetRange"          # members by name, across all types
node apiq.mjs type   "^Face$"            # types by name
node apiq.mjs enum   "FeatureStatus"     # an enum with its values
node apiq.mjs const  "igQuery"           # constants by name
node apiq.mjs value  1216476310          # REVERSE lookup by value
node apiq.mjs events                     # coclasses and their event interfaces
node apiq.mjs outparams "^Face$"         # members with [out]/[in,out]
node apiq.mjs stats                      # per-typelib totals
```

`sig` is the one to reach for before writing any call — it prints direction and
optionality per parameter, which is what predicts marshaling errors:

```
Face.GetRange([in,out] SAFEARRAY(double)* MinRangePoint,
              [in,out] SAFEARRAY(double)* MaxRangePoint) -> void   [dispid 227]
  params : 2 (0 optional)
     1. MinRangePoint   SAFEARRAY(double)*   [in,out]
     2. MaxRangePoint   SAFEARRAY(double)*   [in,out]
```

## 4. `LiveProbe` — ask the RUNNING Solid Edge

Static metadata finds types the live object may not implement. This is step 4 of the
discovery loop: connect, walk a path, and dump the members the **live object** declares
(`IDispatch` → `ITypeInfo`).

```bash
cd LiveProbe && dotnet build -c Release

LiveProbe.exe                                     # identify the app, count documents
LiveProbe.exe --dump "ActiveDocument" --filter Style
LiveProbe.exe --new part --dump "Models"           # creates its OWN doc, closes unsaved
```

It never opens, saves or modifies a user document. Without `--new` it only reads.

What it brings along, and why those pieces matter:

- **`ComInfra.cs`** — `GetActiveObject` by P/Invoke (`Marshal.GetActiveObject` is gone in
  modern .NET), **with a timeout on its own thread** because that call has been measured
  hanging; `ComLive.Identify`/`Members` for live type info; and a correct
  **`IOleMessageFilter`** that retries on *both* rejection reasons.
- **`Recorder.cs`** — records every call with the signature used, the live type returned,
  out-param values, elapsed time and a **named HRESULT**. A `catch` that swallows the error
  destroys exactly the data you are collecting.

**`LiveProbe` deliberately has no ProgID fallback.** `Activator.CreateInstance(ProgID)`
does **not** attach to the open session — it starts a second, invisible instance, and then
everything "works" where the user cannot see it. See `references/connecting.md`.

---

## Requirements

- **.NET 10 SDK** (or adjust `TargetFramework`), building **x64** — SE 2026 is 64-bit only
  and a 32-bit build will not connect.
- **Node 20+** for the `.mjs` tools.
- `ApiDump`, `apiq` and `make-index` need **no Solid Edge license and no running SE**.
  `LiveProbe` needs SE open.
