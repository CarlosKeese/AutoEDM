# Localization: the API speaks the install's language

Measured against **SE 2026 build 226.00.08.004 x64** with
`InstalledLanguage = 1046` (**pt-BR**), 2026-10-17.

---

## The problem in one sentence

**Several `BSTR` values that the API accepts as a LOOKUP KEY come back in the language of
the installation.** Code copied from English examples works on an English install and
fails on a translated one, with an error that never mentions language.

This is not a corner case — it hits file properties and face colors, two of the most
common things an automation touches.

## Case 1 — file property names

```csharp
doc.Properties.Item("SummaryInformation").Item("Title").Value = "x";
//                                              ^^^^^^^
//  -> DISP_E_BADINDEX (0x8002000B) "Índice inválido."
```

**Property-SET names are invariant English.** `doc.Properties` has 7 sets, all English:

```
SummaryInformation · ExtendedSummaryInformation · DocumentSummaryInformation
Custom · ProjectInformation · VersionHistory · MechanicalModeling
```

**The property names inside them are translated.** The 17 members of
`SummaryInformation`, enumerated by index:

| # | measured (pt-BR) | English |
|---|---|---|
| 1 | Título | Title |
| 2 | Assunto | Subject |
| 3 | Autor | Author |
| 4 | Palavras-chave | Keywords |
| 5 | Comentários | Comments |
| 6 | Modelo | Template |
| 7 | Último Autor | Last author |
| 8 | Número da Revisão | Revision number |
| 9 | Tempo total de edição | Total editing time |
| 10 | Data da Última Impressão | Last printed |
| 11 | Data de Criação | Create time |
| 12 | Data da última gravação | Last saved |
| 13 | Número de páginas | Page count |
| 14 | Número de palavras | Word count |
| 15 | Número de caracteres | Character count |
| 16 | Nome do aplicativo | Application name |
| 17 | Segurança | Security |

Note the capitalisation is inconsistent ("Número da **R**evisão" vs "Data da **ú**ltima
gravação"), so normalising case does not rescue a hardcoded name either.

### Write it so it survives any language

```csharp
static int IndexOfProperty(dynamic set, params string[] aliases)
{
    for (int i = 1; i <= (int)set.Count; i++)
    {
        string n;
        try { n = (string)set.Item(i).Name; } catch { continue; }
        foreach (var a in aliases)
            if (n.Equals(a, StringComparison.OrdinalIgnoreCase)) return i;
    }
    return -1;
}

dynamic si = doc.Properties.Item("SummaryInformation");    // set name: safe
int idx = IndexOfProperty(si, "Title", "Título");
si.Item(idx).Value = "…";
```

Writing by index works (`si.Item(1).Value = "…"` confirmed). Whether index 1 is `Title` on
an English install was **not verifiable** here — only one language is present on this
machine. Prefer the alias scan over a bare index if you ship to more than one language.

### For your own data, use the `Custom` set

```csharp
doc.Properties.Item("Custom").Add("ReconTag", "v1");   // -> PropertyEx
```

The only place where **you** own the name, and therefore the only one immune to
translation.

## Case 2 — face style (colour) names

Worse, because the error is opaque:

```csharp
doc.FaceStyles.Item("Blue");   // -> 0x80040B50   (no mention of name, language or collection)
doc.FaceStyles.Item("Azul");   // -> FaceStyle (SolidEdgeFramework)   ✓
```

`PartDocument.FaceStyles` (dispid 24711) holds **60** styles. The first six, by
`StyleName`:

```
1 Preto   2 Preto (claro)   3 Preto (fraco)   4 Azul   5 Normal   6 Plano Ref
```

Every tutorial on the internet does `FaceStyles.Item("Blue")`. All of them break here.

Two extra traps found while testing this:

- **`FaceStyle` has no `.Name`** — it is **`.StyleName`** (dispid 1). `.Name` gives
  `RuntimeBinderException 0x80131500`.
- **`Face.Style` returns `null` before you assign one.** A freshly created face does not
  report an inherited style through the getter; only after a `put` does the `get` answer.
  So "read the face's current colour" is not available on virgin geometry.

### Painting a face, confirmed end to end

```csharp
dynamic cyl = model.Body.Faces[10 /*igQueryCylinder*/].Item(1);
cyl.Style = doc.FaceStyles.Item("Azul");            // propput dispid 605
string back = (string)cyl.Style.StyleName;          // -> "Azul"
```

### Language-independent: pick by COLOUR, not by name

`FaceStyle` exposes the components directly, and numbers do not get translated:

```
propget FaceStyle.StyleName()           -> BSTR     dispid 1
propget FaceStyle.WireframeColorRed()   -> float    dispid 5
propget FaceStyle.WireframeColorGreen() -> float    dispid 6
propget FaceStyle.WireframeColorBlue()  -> float    dispid 7
```

```csharp
static dynamic? StyleByColor(dynamic doc, float r, float g, float b, float tol = 0.2f)
{
    dynamic? best = null; float bestD = float.MaxValue;
    for (int i = 1; i <= (int)doc.FaceStyles.Count; i++)
    {
        dynamic s = doc.FaceStyles.Item(i);
        try
        {
            float sr = Convert.ToSingle(s.WireframeColorRed),
                  sg = Convert.ToSingle(s.WireframeColorGreen),
                  sb = Convert.ToSingle(s.WireframeColorBlue);
            float d = MathF.Abs(sr-r) + MathF.Abs(sg-g) + MathF.Abs(sb-b);
            if (d < bestD && d <= tol * 3) { bestD = d; best = s; }
        }
        catch { }
    }
    return best;
}
```

More code, but it is the only robust way. "Paint the cylindrical faces blue" is a realistic
request, and on this machine the obvious implementation fails.

## Case 3 — draft sheet names

```
Sheets.Item(1).Name     -> "Folha1"      (not "Sheet1")
Sheets.Item(2..5).Name  -> "Folha-A4", "Folha-A3", "Folha-A2", "Folha-A1"
Sheets.Item(6).Name     -> "2D Model"    <- stayed ENGLISH
```

Note the inconsistency: the format templates were translated and "2D Model" was not. **You
cannot even assume that everything is translated.**

## Case 4 — SE's own data files (known earlier, same root cause)

The hole database (`Preferences\Holes\<standard>.xlsx`) on a Portuguese install has the
sheet **"Rosqueado"**, not "Threaded", headers "Subtipo / Tamanho / Diâmetro de Macho", and
sub-types "Rosca de Tubo Reta/Cônica". A reader that only knows the English names silently
finds nothing. Match names in every language you ship for, and pass the **raw** string back
to the API — that string is what `AddEx` matches against.

## Summary: which names are translated?

| kind of name | translated? | evidence |
|---|---|---|
| ProgID (`SolidEdge.PartDocument`) | **no** | worked |
| property-**set** name (`SummaryInformation`, `Custom`) | **no** | worked |
| property name inside a set | **YES** | "Título" |
| face style name | **YES** | "Azul" |
| draft sheet name | **YES** (partly) | "Folha1", but "2D Model" |
| occurrence name (`file.par:1`) | no — comes from the filename | `probe-holes-125705.par:1` |
| SE data-file sheets/headers | **YES** | "Rosqueado" |

**Safe default: never look a name up in English without a fallback.** Enumerate and match
against an alias list, or key off an index or a numeric value.

## What was NOT verified

Only one install, one language, on the measured machine. Not established:

- whether `SummaryInformation` indices are the same on an English install;
- whether the order of the 60 `FaceStyles` is the same;
- whether any API returns the **invariant** name alongside the translated one (searched
  those collections for members containing "Internal", "Invariant" or "Key" — none found).

Anyone depending on indices in a multi-language deployment should measure on an English
install before trusting them.
