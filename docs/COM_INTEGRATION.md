# Integração COM com o Solid Edge

> **Público-alvo:** desenvolvedores que vão manter ou estender o AutoEDM.  
> **Escopo:** como o núcleo C# se comunica com o Solid Edge 2023/2026 via COM, quais bibliotecas são usadas, onde elas moram no Windows e como a API se organiza.  
> **Fonte da verdade:** `src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_223.00.13.05.txt` — dump gerado em tempo de execução a partir das type libraries registradas no Solid Edge instalado.
> 
> **Catálogo de API gerado:** consulte [`docs/api/README.md`](./api/README.md) para a documentação completa de tipos, métodos, parâmetros e constantes extraídos automaticamente do dump.

---

## 1. Arquitetura geral

O Solid Edge expõe sua funcionalidade como um servidor COM. O AutoEDM atua como cliente COM:

```
AutoEDM.exe (x64, STA, .NET Framework 4.7.2)
        │
        │  late binding (dynamic) + InvokeMember
        ▼
SolidEdge.Application (ProgID)
        │
        ├─ Documents
        │   ├─ .Open("molde.asm")
        │   ├─ .Add("SolidEdge.PartDocument")
        │   └─ ActiveDocument
        │
        ├─ Occurrences  (montagem)
        │   ├─ AddByFilename(...)
        │   ├─ AddWithTransform(...)
        │   ├─ Item(1)
        │   └─ Count
        │
        └─ Models / Constructions (peça)
            ├─ AddBoxByTwoPoints(...)
            ├─ CopySurfaces.Add(...)
            ├─ OffsetSurfaces.Add(...)
            └─ StitchSurfaces.Add(...)
```

O projeto usa **late binding** (`dynamic`) para compilar em máquinas que não tenham o Solid Edge instalido. Quando o binder do C# falha na coerção de VARIANT (caso comum em `[out]` arrays e enums), recorremos a `System.Type.InvokeMember`.

---

## 2. Registro Windows e ProgID

### 2.1. Onde o servidor é registrado

Durante a instalação do Solid Edge, o instalador registra o servidor COM em:

```
HKEY_CLASSES_ROOT\SolidEdge.Application
HKEY_CLASSES_ROOT\CLSID\{...}        ← aponta para Edge.exe
```

Você pode inspecionar no PowerShell:

```powershell
Get-ChildItem "HKCR:\SolidEdge.Application"
(Get-ItemProperty "HKCR:\SolidEdge.Application\CLSID")."(Default)"
```

No ambiente de desenvolvimento do AutoEDM:

```
C:\Program Files\Siemens\Solid Edge 2023\Program\Edge.exe
```

### 2.2. Como conectar

```csharp
// (A) Instância já em execução
object app = Marshal.GetActiveObject("SolidEdge.Application");

// (B) Iniciar nova instância
Type t = Type.GetTypeFromProgID("SolidEdge.Application");
object app = Activator.CreateInstance(t);
```

O AutoEDM usa `ComInterop.GetActiveObject` (P/Invoke) porque `Marshal.GetActiveObject` foi removido no .NET moderno. Veja `src/AutoEDM.Core/Com/ComInterop.cs`.

---

## 3. Type libraries e namespaces COM

O Solid Edge divide sua API em várias type libraries (TLB). As principais usadas pelo AutoEDM são:

| TLB (arquivo) | Namespace aproximado | Conteúdo principal |
|---|---|---|
| `SolidEdgeFramework.tlb` | `SolidEdgeFramework` | Application, Documents, SelecSet, estilos, OLE helpers |
| `SolidEdgeGeometry.tlb` | `SolidEdgeGeometry` | Body, Face, Edge, Vertex, ranges, vetores |
| `SolidEdgePart.tlb` | `SolidEdgePart` | PartDocument, Models, Constructions, features |
| `SolidEdgeAssembly.tlb` | `SolidEdgeAssembly` | AssemblyDocument, Occurrences, Occurrence |
| `SolidEdgeDraft.tlb` | `SolidEdgeDraft` | DraftDocument, views, sheets (não usado no MVP) |

Local típico no disco:

```
C:\Program Files\Siemens\Solid Edge 2023\Program\SolidEdgeFramework.tlb
C:\Program Files\Siemens\Solid Edge 2023\Program\SolidEdgeGeometry.tlb
...
```

### 3.1. Gerar interop assemblies (early binding)

Se quiser trocar late binding por early binding, gere os interops com `TlbImp.exe`:

```bat
"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\TlbImp.exe" "C:\Program Files\Siemens\Solid Edge 2023\Program\SolidEdgeFramework.tlb" /out:Interop.SolidEdgeFramework.dll
"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\TlbImp.exe" "C:\Program Files\Siemens\Solid Edge 2023\Program\SolidEdgeGeometry.tlb" /out:Interop.SolidEdgeGeometry.dll
"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\TlbImp.exe" "C:\Program Files\Siemens\Solid Edge 2023\Program\SolidEdgePart.tlb" /out:Interop.SolidEdgePart.dll
"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\TlbImp.exe" "C:\Program Files\Siemens\Solid Edge 2023\Program\SolidEdgeAssembly.tlb" /out:Interop.SolidEdgeAssembly.dll
```

Depois referencie as DLLs no `.csproj` (exemplo já documentado em `AutoEDM.csproj`).

---

## 4. Configuração do projeto

Requisitos no `.csproj`:

```xml
<TargetFramework>net472</TargetFramework>
<PlatformTarget>x64</PlatformTarget>
<OutputType>WinExe</OutputType>
<LangVersion>7.3</LangVersion>
```

### 4.1. Por que x64?

O Solid Edge 2023/2026 é **64-bit only**. Um processo x86 não consegue se comunicar corretamente com o servidor COM x64. O executável do AutoEDM **deve** ser compilado como `x64` (ou `AnyCPU` sem preferir 32-bit).

### 4.3. Thread STA

A automação do Solid Edge exige Single-Threaded Apartment:

```csharp
[STAThread]
static void Main(string[] args) { ... }
```

A UI WinForms já roda em STA. O entry point do console também está anotado com `[STAThread]`.

### 4.4. OLE Message Filter

O SE frequentemente responde `RPC_E_SERVERCALL_RETRYLATER` quando está ocupado (diálogo, recompute, etc.). O AutoEDM registra um `IOleMessageFilter` para retry automático:

```csharp
OleMessageFilter.Register();
```

Veja `src/AutoEDM.Core/Com/OleMessageFilter.cs`.

### 4.5. Pacotes NuGet

**Nenhum pacote NuGet é obrigatório** para late binding. As referências diretas são:

```xml
<Reference Include="Microsoft.CSharp" />   <!-- dynamic -->
<Reference Include="System.Drawing" />     <!-- Color -->
<Reference Include="System.Windows.Forms" /><!-- UI -->
```

Se migrar para .NET 6+, adicione `System.Drawing.Common` via NuGet.

---

## 5. Objetos e métodos principais

A lista abaixo reflete as assinaturas vistas no dump da type library do SE 2023 (`223.00.13.05`). Todos os métodos são **1-based** nas coleções e usam **metros/radianos** para geometria.

### 5.1. Application

```text
Application
  .Documents        -> Documents
  .ActiveDocument   -> Document
  .Visible          -> bool
  .DisplayAlerts    -> bool
  .Version          -> string
  .StartCommand(id) -> void
  .Quit()           -> void
```

### 5.2. Documents

```text
Documents
  .Open(path)                    -> Document
  .Add("SolidEdge.PartDocument") -> Document
  .Add(progId, templatePath)     -> Document
```

### 5.3. AssemblyDocument & Occurrences

```text
AssemblyDocument
  .Occurrences       -> Occurrences
  .OccurrenceFileName -> string
  .SaveAs(path)      -> void

Occurrences
  .AddByFilename(OccurrenceFileName, UseSimplifiedPart)        -> Occurrence
  .AddWithTransform(OccurrenceFileName, ox, oy, oz, ax, ay, az) -> Occurrence
  .AddWithMatrix(OccurrenceFileName, Matrix)                    -> Occurrence
  .Item(index)        -> Occurrence
  .Count              -> int

Occurrence
  .Name               -> string
  .OccurrenceDocument -> PartDocument/AssemblyDocument
  .Activate           -> bool   // propriedade! true = edit in-place
  .PutOrigin(x,y,z)   -> void
  .GetTransform(...)  -> void   // out x,y,z, ax,ay,az (metros/radianos)
  .PutTransform(...)  -> void
```

> **Atenção:** `Occurrence.Activate` é uma **propriedade booleana**, não um método. Atribuir `true` entra em edição in-place; `false` retorna à montagem. Veja `src/AutoEDM.Core/Assembly/EditInPlaceScope.cs`.

### 5.4. PartDocument, Models & Constructions

```text
PartDocument
  .Models        -> Models
  .Constructions -> Constructions
  .RefPlanes     -> RefPlanes
  .SelectSet     -> SelectSet
  .SaveAs(path)  -> void

Models
  .AddBoxByTwoPoints(x1,y1,z1, x2,y2,z2, dAngle, dDepth,
                     pPlane, ExtentSide, vbKeyPointExtent,
                     pKeyPointObj, pKeyPointFlags, [ppModel]) -> Model
  .AddCylinderByCenterAndRadius(...) -> Model
  .Item(index) -> Model
  .Count       -> int

Model
  .Body -> Body

Constructions
  .CopySurfaces  -> CopySurfaces
  .OffsetSurfaces -> OffsetSurfaces
  .StitchSurfaces -> StitchSurfaces
  .FaceOffsets    -> FaceOffsets
  .ExtrudedSurfaces, .RevolvedSurfaces, ...

CopySurfaces
  .Add(NumberOfFaces, FaceArray, InternalBoundary, ExternalBoundary) -> CopySurface

OffsetSurfaces
  .Add(Side, offsetDistance, FaceSet, Boundary) -> OffsetSurface

StitchSurfaces
  .Add(NumberOfSurfaces, SurfaceArray, Heal, Tolerance) -> StitchSurface

FaceOffsets
  .Add(FacesToOffset, BlendRecreation, AlongOrReverseVector,
       offsetDistance, ToReferenceEntity, ToKeyPoint,
       DistanceFromKeyPoint, AlongOrReverseDirectionToKeyPoint) -> FaceOffset
```

### 5.5. Geometria (Body, Face, Edge, Vertex)

```text
Body
  .Faces(queryType) -> Faces
  .Edges(queryType) -> Edges

Faces / Edges / Vertices
  .Count      -> int
  .Item(index) -> Face / Edge / Vertex

Face
  .Style              -> FaceStyle (cor = DiffuseRed/Green/Blue 0..1)
  .GetRange(MinRangePoint, MaxRangePoint) -> void [out]
  .GetExactRange(MinRangePoint, MaxRangePoint) -> void [out]
  .Area               -> double
  .Body               -> Body

Vertex
  .GetPointData(Point) -> void [out]
```

> **Dica de late binding:** para `[out] SAFEARRAY(double)`, passe arrays **vazios** (`new double[0]`) e marque-os como `ref` com `ParameterModifier`. Arrays pré-semeados (`new double[3]`) causaram `DISP_E_TYPEMISMATCH` no SE 2023.

---

## 6. Add-ins do Solid Edge

Um add-in do Solid Edge é uma DLL COM registrada com a categoria `CATID_SolidEdgeAddIn` (`{26B1D2D1-2B4E-11d2-9E4E-080036B4D502}`) e a chave `AutoConnect`.

Não faz parte do MVP do AutoEDM (que é automação externa), mas a estrutura COM é a mesma:

```text
HKEY_CLASSES_ROOT\CLSID\{SeuAddInCLSID}
  \Implemented Categories\{CATID_SolidEdgeAddIn}
  \SolidEdgeAddIn
      AutoConnect = dword:00000001
```

A DLL deve implementar `SolidEdgeFramework.ISolidEdgeAddIn` e responder aos eventos de conexão. Para o AutoEDM, isso só seria necessário se quisermos transformar o projeto em um add-in que rode dentro do processo do SE.

---

## 7. Descoberta da API via TypeLib

O projeto inclui `ComDiagnostics.DumpTypeLibraries`, que sobe pelos objetos COM até `ITypeLib` e gera:

```
src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_{versão}.txt
```

Esse dump lista coclasses, interfaces, enums e assinaturas de métodos. Antes de usar qualquer método COM desconhecido:

```bash
grep "CopySurfaces" src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_*.txt
grep "AddBoxByTwoPoints" src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_*.txt
```

**Não invente assinaturas.** Se não estiver no dump, ou não existe com aquele nome, ou precisa ser descoberto empiricamente.

---

## 8. Fluxo típico do AutoEDM

1. **Conectar:** `SolidEdgeConnector.Connect()`
2. **Abrir montagem:** `Documents.Open(asmPath)`
3. **Iterar ocorrências:** `AssemblyDocument.Occurrences`
4. **Selecionar faces de queima por cor:** `FaceSelector.SelectByRaColorMap()`
5. **Planejar:** `ElectrodeBuilder.PlanFromAssemblyDocument()`
6. **Criar peça em contexto:** `CreateInContextPart()`
   - cria `.par`
   - adiciona à montagem via `Occurrences.AddByFilename()`
   - ativa edição in-place (`Occurrence.Activate = true`)
   - copia faces via `Constructions.CopySurfaces.Add()`
7. **Offset interno:** `ApplyOffset()`
8. **Blank + holder:** `CreateBlankAndHolder()`
9. **Re-pintar e salvar .par:** `RecolorAndSave()`

---

## 9. Dicas e armadilhas

| Problema | Causa provável | Solução |
|---|---|---|
| `RPC_E_SERVERCALL_RETRYLATER` | SE ocupado | `OleMessageFilter.Register()` |
| `DISP_E_TYPEMISMATCH` em `[out]` | Argumento não marcado como `ref` | `ParameterModifier` + arrays vazios |
| `DISP_E_PARAMNOTOPTIONAL` | Parâmetro obrigatório omitido | Preencher todos os params da sobrecarga DISPATCH |
| Índice fora dos limites | Coleções 1-based | Começar em `Item(1)` |
| Unidades erradas | Valor em mm passado diretamente | Dividir por 1000 (metros) |
| `STG_E_LOCKVIOLATION` | Documento já aberto no SE | Fechar no SE antes de abrir via COM |
| `Style == null` | Face sem estilo próprio | Criar/assignar um `FaceStyle` antes de pintar |
| Cores 0..1 vs 0..255 | API usa `float` unitário | Multiplicar por 255 na leitura; dividir por 255 na escrita |

---

## 10. Catálogo de API e Constantes

A documentação gerada automaticamente a partir do dump da type library está disponível em [`docs/api/`](./api/):

| Arquivo | Conteúdo |
|---|---|
| [`api/README.md`](./api/README.md) | Índice do catálogo de API. |
| [`api/SolidEdgeFramework.md`](./api/SolidEdgeFramework.md) | Tipos da type library `SolidEdgeFramework`. |
| [`api/SolidEdgePart.md`](./api/SolidEdgePart.md) | Tipos da type library `SolidEdgePart`. |
| [`api/constants.md`](./api/constants.md) | Enums e constantes COM consolidadas. |

Para regenerar o catálogo após atualizar o Solid Edge:

```bash
python tools/generate_api_docs.py
```

---

## 11. Referências

- `src/AutoEDM.Core/Com/SolidEdgeConnector.cs` — conexão COM
- `src/AutoEDM.Core/Com/OleMessageFilter.cs` — retry RPC
- `src/AutoEDM.Core/Com/ComInterop.cs` — `GetActiveObject` via P/Invoke
- `src/AutoEDM.Core/Com/ComDiagnostics.cs` — introspecção de type libraries
- `src/AutoEDM.Core/Assembly/EditInPlaceScope.cs` — edição in-place
- `src/AutoEDM.Core/Selection/FaceGeometry.cs` — leitura de bounding box
- `src/AutoEDM.Core/Selection/FaceStyleColorReader.cs` — leitura de cor
- `src/AutoEDM.Core/Electrode/InterPartCopier.cs` — cópia associativa entre peças
- `src/AutoEDM.Core/Electrode/ElectrodeBuilder.cs` — orquestração do fluxo
- Dump local: `src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_223.00.13.05.txt`
- [Solid Edge Community GitHub](https://github.com/SolidEdgeCommunity)
- [Solid Edge Programmer's Guide](https://support.industrysoftware.automation.siemens.com/trainings/se/107/api/ProgrammersGuide.html)
