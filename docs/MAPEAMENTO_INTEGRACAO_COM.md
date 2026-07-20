# Mapeamento de Integração COM — Núcleo AutoEDM

Documento técnico para validação e continuação da automação de extração de eletrodos no **Solid Edge 2023/2026**. Baseado no código C# existente, no dump da typelib (`SE_API_dump_223.00.13.05.txt`) e na documentação oficial da Siemens.

> **⚠️ PARCIALMENTE SUPERADO (2026-07-08).** As seções sobre **edição in-place** (`Occurrence.Activate`)
> e **Inter-Part Copy** (`CopySurfaces.Add`) refletem o entendimento de antes dos logs 18–27. A
> fonte de verdade **atual** é [`docs/MEMORIA_SOLID_EDGE_COM.md`](./MEMORIA_SOLID_EDGE_COM.md)
> (§3 in-place, §4 Inter-Part — hoje ⛔ BLOQUEADO). O resto deste doc (correções de marshaling,
> mapa por estágio) continua válido.
>
> **Função deste doc:** mapa das funções COM por estágio do pipeline. Toda assinatura aqui foi confirmada no dump ou na documentação oficial.
>
> **Catálogo de API gerado:** para consulta completa de tipos, métodos e constantes, veja [`docs/api/README.md`](./api/README.md).

---

## 1. Resumo Executivo

### O que já funciona (validado no SE 2023)

| Componente | Status | Arquivo |
|---|---|---|
| Conexão COM (instância ativa) | ✅ | `Com/SolidEdgeConnector.cs` |
| OLE Message Filter | ✅ | `Com/OleMessageFilter.cs` |
| Dump da typelib | ✅ | `Com/ComDiagnostics.cs` |
| Leitura de cor das faces (`Face.Style.DiffuseRGB`) | ✅ | `Selection/FaceStyleColorReader.cs` |
| Seleção por cor/Ra | ✅ | `Selection/FaceSelector.cs` |
| Planejamento não-destrutivo de eletrodos | ✅ | `Electrode/ElectrodeBuilder.cs` |
| Catálogo de blanks e offset por Ra | ✅ | `Electrode/IBlankLibrary.cs`, `IOffsetPolicy.cs` |

### Bloqueio técnico atual

| Problema | Sintoma | Arquivo |
|---|---|---|
| `Face.GetRange` / `GetExactRange` falham | `DISP_E_TYPEMISMATCH` (`0x80020005`) | `Selection/FaceGeometry.cs` |
| `Vertex.GetPointData` falha no fallback | `DISP_E_TYPEMISMATCH` | `Selection/FaceGeometry.cs` |
| `EditInPlaceScope` tenta métodos inexistentes | `Occurrence.Edit()` e `Deactivate()` não existem | `Assembly/EditInPlaceScope.cs` |
| `Models.AddBoxByTwoPoints` falha | `DISP_E_PARAMNOTOPTIONAL` (`0x8002000F`) | `Experiments/ModelingProbe.cs` |
| `InterPartCopier` e stubs de geometria | `NotImplementedException` | `Electrode/InterPartCopier.cs`, `Electrode/ElectrodeBuilder.cs` |

### Próximo passo crítico

Corrigir `FaceGeometry.TryGetRangeMm`. Sem bounding box, a segmentação de regiões (`RegionSplitter`) não funciona e o catálogo de blanks é selecionado às cegas.

---

## 2. Correções Imediatas no Código Existente

### 2.1. `FaceGeometry.TryGetRangeMm` — `GetRange` com `[out]` SAFEARRAY

#### Assinatura confirmada (dump)

```text
[TKIND_DISPATCH Face]
    GetRange(MinRangePoint, MaxRangePoint) [2 params]
    GetExactRange(MinRangePoint, MaxRangePoint) [2 params]
```

Ambos os parâmetros são `[out] SAFEARRAY(double)` (array de 3 doubles).

#### Por que falha hoje

O código passa `new double[3]` com `ParameterModifier` by-ref. O binder COM do .NET interpreta `double[3]` como `SAFEARRAY` de entrada (`[in]`) e rejeita o tipo quando o método espera `[out]`.

#### Solução proposta (a validar no SE real)

Passar arrays vazios (`new double[0]`) ou `null` como `object[]`, permitindo que o método COM aloque e preencha os arrays. A documentação oficial da Siemens mostra:

```vb
Dim dblMin() As Double
Dim dblMax() As Double
Call objFace.GetRange(MinRangePoint:=dblMin, MaxRangePoint:=dblMax)
```

Em C# late binding, a forma mais próxima é:

```csharp
object[] args = { new double[0], new double[0] };
var mod = new ParameterModifier(2);
mod[0] = true;
mod[1] = true;

comFace.GetType().InvokeMember(
    "GetRange",
    BindingFlags.InvokeMethod,
    null,
    comFace,
    args,
    new[] { mod },
    CultureInfo.InvariantCulture,
    null);

// args[0] e args[1] devem conter double[] de 3 elementos
var min = (double[])args[0];
var max = (double[])args[1];
```

Se `new double[0]` falhar, tentar `null`:

```csharp
object[] args = { null, null };
```

Se ambos falharem, usar **early binding** temporário (`Interop.SolidEdge`) para confirmar que o método funciona, e depois replicar a marshalling correta no late binding.

#### Fallback por vértices

A assinatura correta de `Vertex.GetPointData` no dump:

```text
[TKIND_DISPATCH Vertex]
    GetPointData(Point) [1 params]
```

O parâmetro `Point` é `[out] SAFEARRAY(double)`. A correção é idêntica: passar `new double[0]` ou `null` com `ParameterModifier`.

```csharp
object[] args = { new double[0] };
var mod = new ParameterModifier(1);
mod[0] = true;
vtx.GetType().InvokeMember(
    "GetPointData",
    BindingFlags.InvokeMethod,
    null,
    vtx,
    args,
    new[] { mod },
    CultureInfo.InvariantCulture,
    null);
var p = (double[])args[0];
```

### 2.2. `EditInPlaceScope` — ativação in-place

#### Erro no código atual

O código chama `_occurrence.Edit()` e `_occurrence.Deactivate()`. O dump prova que **esses métodos não existem** em `Occurrence`:

```text
[TKIND_DISPATCH Occurrence]
    put Activate() [1 params]
    get Activate() [0 params]
```

#### Correção

`Activate` é uma **propriedade booleana**. Para ativar a ocorrência para edição in-place:

```csharp
_occurrence.Activate = true;   // entra em edição in-place
// ... fazer o trabalho ...
_occurrence.Activate = false;  // retorna à montagem
```

Ou via `InvokeMember`:

```csharp
occ.GetType().InvokeMember(
    "Activate",
    BindingFlags.SetProperty,
    null,
    occ,
    new object[] { true });
```

> **Atenção:** ativar in-place em uma ocorrência que já está ativa pode lançar exceção. Verificar `_occurrence.Activate` antes.

### 2.3. `Models.AddBoxByTwoPoints` — parâmetros opcionais

#### Assinatura confirmada (dispatch)

```text
AddBoxByTwoPoints(x1, y1, Z1, x2, y2, Z2, dAngle, dDepth, pPlane, ExtentSide, vbKeyPointExtent, pKeyPointObj, pKeyPointFlags) [13 params]
```

#### Por que falha hoje

`DISP_E_PARAMNOTOPTIONAL` indica que um parâmetro que parece opcional não está sendo tratado como opcional pelo binder. Os parâmetros `pPlane`, `ExtentSide`, `vbKeyPointExtent`, `pKeyPointObj`, `pKeyPointFlags` provavelmente são opcionais e devem receber `Type.Missing`.

#### Solução proposta

```csharp
object[] args = new object[13];
args[0] = x1; args[1] = y1; args[2] = z1;
args[3] = x2; args[4] = y2; args[5] = z2;
args[6] = dAngle;
args[7] = dDepth;
args[8] = Type.Missing;  // pPlane
args[9] = Type.Missing;  // ExtentSide
args[10] = Type.Missing; // vbKeyPointExtent
args[11] = Type.Missing; // pKeyPointObj
args[12] = Type.Missing; // pKeyPointFlags

models.GetType().InvokeMember(
    "AddBoxByTwoPoints",
    BindingFlags.InvokeMethod,
    null,
    models,
    args,
    null,
    CultureInfo.InvariantCulture,
    null);
```

Se `Type.Missing` ainda falhar, passar explicitamente `null` para os parâmetros de objeto opcionais.

---

## 3. Mapeamento de Funções COM por Estágio do Núcleo

### 3.1. Seleção e Leitura de Faces

| Objetivo | Objeto/Coleção | Método/Propriedade | Parâmetros | Notas |
|---|---|---|---|---|
| Acessar faces | `Body` | `Faces[FaceType]` | `FaceType` (opcional) | Use `igQueryAll` (=1) |
| Contar faces | `Faces` | `Count` | — | Coleção 1-based |
| Obter face | `Faces` | `Item(index)` | `index` (1-based) | — |
| Cor da face | `Face` | `Style.DiffuseRed/Green/Blue` | — | Já validado |
| BBox da face | `Face` | `GetRange(MinRangePoint, MaxRangePoint)` | 2 `[out]` arrays de double | Ver correção acima |
| BBox exata | `Face` | `GetExactRange(MinRangePoint, MaxRangePoint)` | 2 `[out]` arrays de double | Ver correção acima |
| Vértices | `Face` | `Vertices` | — | Coleção 1-based |
| Ponto do vértice | `Vertex` | `GetPointData(Point)` | 1 `[out]` array de double | Ver correção acima |
| Arestas | `Face` | `Edges` | — | Coleção 1-based |
| ID da aresta | `Edge` | `ID` / `Tag` | — | Para conectividade |

### 3.2. Contexto de Montagem

| Objetivo | Objeto/Coleção | Método/Propriedade | Parâmetros | Notas |
|---|---|---|---|---|
| Ocorrências | `AssemblyDocument` | `Occurrences` | — | Coleção 1-based |
| Obter ocorrência | `Occurrences` | `Item(index)` | `index` (1-based) | — |
| Documento da ocorrência | `Occurrence` | `OccurrenceDocument` | — | Retorna `PartDocument` |
| Nome do arquivo | `Occurrence` | `OccurrenceFileName` / `PartFileName` | — | — |
| Ativar in-place | `Occurrence` | `Activate = true` | boolean | Propriedade, não método |
| Desativar in-place | `Occurrence` | `Activate = false` | boolean | — |
| Transformação | `Occurrence` | `GetMatrix(Matrix)` / `PutMatrix(Matrix, Replace)` | `[out] Matrix`, `Matrix, bool` | Matriz 4×4 como array 16 elementos |
| Origem | `Occurrence` | `GetOrigin(x,y,z)` / `PutOrigin(x,y,z)` | 3 doubles | — |

### 3.3. Inter-Part Copy (cópia associativa de faces)

#### Assinatura confirmada (dump)

```text
[TKIND_DISPATCH Constructions]
    get CopySurfaces() [0 params]

[TKIND_DISPATCH CopySurfaces]
    Add(NumberOfFaces, FaceArray, InternalBoundary, ExternalBoundary) [4 params]

[TKIND_INTERFACE _ICopySurfacesAuto]
    Add(NumberOfFaces, FaceArray, InternalBoundary, ExternalBoundary, CopySurface) [5 params]
```

#### Sequência COM para copiar faces de queima

1. **Criar o eletrodo em contexto** (in-place) na montagem. Este é o passo mais crítico e ainda em validação; sem ele, `CopySurfaces.Add` retorna `E_FAIL`.

   Opções de API identificadas no dump para criar peça in-place:
   - `AssemblyDocument.CreateReference(...)`
   - `AssemblyDocument.CreateReference2(...)`
   - `Occurrences.AddByTemplate(...)`
   - `electrodeDoc.Constructions.InterpartConstructions.Add(asmSource)`
   - `electrodeDoc.Constructions.InterpartConstructions.Add2(partTarget, asmSource)`

   A sequência exata deve ser validada no SE real. Consulte o catálogo em [`docs/api/SolidEdgePart.md`](./api/SolidEdgePart.md) para as assinaturas.

2. **Ativar in-place** o eletrodo destino na montagem:
   ```csharp
   occurrenceEletrodo.Activate = true;
   ```

3. **Acessar** a coleção `Constructions.CopySurfaces` do documento do eletrodo ativo:
   ```csharp
   dynamic copySurfaces = electrodeDoc.Constructions.CopySurfaces;
   ```

4. **Montar o array de faces** a copiar (`FaceArray`). Cada elemento deve ser um `Face` da ocorrência da cavidade. Em late binding, o array precisa ser tipado como `SolidEdgeGeometry.Face[]` para marshalar como `SAFEARRAY(IDispatch)`; `object[]` marshala como `SAFEARRAY(VARIANT)` e é rejeitado.

5. **Chamar `Add`**:
   ```csharp
   // array TIPADO de Face -> SAFEARRAY(IDispatch)
   var faceArray = sourceFaces.Select(f => (SolidEdgeGeometry.Face)f.ComFace).ToArray();
   int numFaces = faceArray.Length;
   object internalBoundary = Type.Missing; // ou null
   object externalBoundary = Type.Missing; // ou null

   dynamic copySurface = copySurfaces.Add(numFaces, faceArray, internalBoundary, externalBoundary);
   ```

6. **Desativar in-place**:
   ```csharp
   occurrenceEletrodo.Activate = false;
   ```

#### Importante

- `Constructions.CopySurfaces` é uma **coleção** (`cParams=0` no acesso); a operação é `.Add(...)`.
- **NÃO usar** `Models.AddCopiedPart` — esse método traz a peça inteira, não faces isoladas. O dump confirma que são APIs distintas.
- O resultado é um `CopySurface` (surface body) no eletrodo, visível no PathFinder com símbolo de link.

### 3.4. Offset das Faces de Queima

#### Assinatura confirmada (dump)

```text
[TKIND_DISPATCH Constructions]
    get OffsetSurfaces() [0 params]

[TKIND_DISPATCH OffsetSurfaces]
    Add(Side, offsetDistance, FaceSet, Boundary) [4 params]
```

#### Sequência COM

1. Obter a coleção:
   ```csharp
   dynamic offsetSurfaces = electrodeDoc.Constructions.OffsetSurfaces;
   ```

2. Criar `FaceSet` contendo as faces copiadas (ou a superfície resultante do CopySurfaces).

3. Chamar `Add`:
   ```csharp
   int side = ...;            // direção do offset (a validar: 0/1)
   double distance = offsetMm / 1000.0; // metros, positivo ou negativo
   object faceSet = ...;      // FaceSet ou array de faces
   object boundary = Type.Missing;

   dynamic offsetSurface = offsetSurfaces.Add(side, distance, faceSet, boundary);
   ```

> O sinal de `distance` e o valor de `Side` precisam ser validados no SE real para garantir que o offset seja para **dentro** da cavidade (encolhimento do eletrodo).

### 3.5. Stitch (costurar superfícies em sólido)

#### Assinatura confirmada (dump)

```text
[TKIND_DISPATCH Constructions]
    get StitchSurfaces() [0 params]

[TKIND_DISPATCH StitchSurfaces]
    Add(NumberOfSurfaces, SurfaceArray, Heal, Tolerance) [4 params]
```

#### Sequência COM

```csharp
 dynamic stitchSurfaces = electrodeDoc.Constructions.StitchSurfaces;
 object[] surfaces = { offsetSurface }; // ou múltiplas
 int numSurfaces = surfaces.Length;
 bool heal = true;
 double tolerance = 0.0001; // 0.1 mm em metros

 dynamic stitchFeature = stitchSurfaces.Add(numSurfaces, surfaces, heal, tolerance);
```

Se `StitchSurfaces.Add` não fechar o volume, pode ser necessário:
- `BoundedSurface` para tampar furos.
- `ExtrudedProtrusion` a partir de um perfil.
- `Models.AddExtrudedProtrusion` (35 params — ver dump).

### 3.6. Modelagem do Blank e Holder

Opções COM para criar o blank:

| Método | Quando usar | Assinatura (dispatch) |
|---|---|---|
| `Models.AddBoxByTwoPoints` | Blank retangular simples | 13 params |
| `Models.AddBoxByCenter` | Blank centrado em ponto | 12 params |
| `Models.AddCylinderByCenterAndRadius` | Blank cilíndrico | 10 params |
| `Models.AddExtrudedProtrusion` | Blank com perfil complexo | 35 params |

Após criar o blank, adicionar furos de fixação:
- `Models.AddHoleByFiniteExtent` / `AddHoleByThroughExtent` (confirmar nomes no dump).
- Ou criar sketch + `Models.AddFiniteExtrudedCutout`.

### 3.7. Re-pintura e Save

| Objetivo | Método/Propriedade | Notas |
|---|---|---|
| Aplicar cor | `Face.Style = styleObject` | Requer objeto `Style` do documento |
| Criar/obter estilo | `PartDocument.Styles.Add` / `Styles.Item(name)` | — |
| Salvar como .par | `Document.SaveAs(path)` | Entrega deve ser `.par` nativo |

### 3.8. Relatório de Coordenadas de Queima

| Dado | Fonte COM |
|---|---|
| Coordenadas X, Y, Z da base do eletrodo | `Occurrence.GetOrigin` ou `Occurrence.GetMatrix` |
| Ra do eletrodo | Metadados do plano (`RegionPlan.Ra`) |
| Nome do arquivo | `RegionPlan.Passes[i].ElectrodeFileName` |
| Material do blank | `ElectrodeParams.Material` |
| Offset do passe | `PassPlan.InwardOffsetMm` |

---

## 4. API Interna Planejada (C#)

Para tornar o núcleo reaproveitável por projetos futuros e por uma futura camada MCP, propõe-se uma API interna de alto nível sobre o COM bruto:

```csharp
namespace AutoEDM.Core
{
    public interface IElectrodeAutomation
    {
        /// <summary>Conecta ao Solid Edge (ativa ou nova).</summary>
        bool Connect();

        /// <summary>Monta o plano de eletrodos a partir da montagem ativa.</summary>
        ElectrodeBuildPlan PlanFromActiveAssembly(ElectrodeParams parameters);

        /// <summary>Cria um eletrodo (desbaste ou acabamento) para uma região.</summary>
        BuildResult BuildElectrode(
            RegionPlan region,
            PassPlan pass,
            string assemblyPath,
            string outputFolder);

        /// <summary>Gera relatório de coordenadas de queima.</summary>
        string GenerateBurnReport(ElectrodeBuildPlan plan, string outputPath);
    }
}
```

### Ferramentas de baixo nível (toolbox)

```csharp
namespace AutoEDM.Core.Tools
{
    public static class SolidEdgeGeometry
    {
        public static bool TryGetFaceRangeMm(dynamic face, out double[] min, out double[] max);
        public static bool TryGetVertexPointMm(dynamic vertex, out double[] point);
        public static BoundingBox ComputeBoundingBox(IEnumerable<dynamic> faces);
    }

    public static class SolidEdgeModeling
    {
        public static dynamic AddBoxByTwoPoints(dynamic models, double x1, double y1, double z1, double x2, double y2, double z2);
        public static dynamic CopyFacesInterPart(dynamic targetDoc, dynamic sourceFaces, dynamic targetOccurrence);
        public static dynamic OffsetSurface(dynamic constructions, dynamic faceSet, double distanceMm, int side);
        public static dynamic StitchSurfaces(dynamic constructions, object[] surfaces);
    }

    public static class AssemblyTools
    {
        public static void ActivateInPlace(dynamic occurrence);
        public static void DeactivateInPlace(dynamic occurrence);
        public static double[] GetOccurrenceOrigin(dynamic occurrence);
    }
}
```

### Camada MCP futura

Se um MCP for criado no futuro, ele deve expor **tools de negócio**, não de geometria bruta:

| Tool | Descrição | Chama no núcleo |
|---|---|---|
| `plan_electrodes_from_active_assembly` | Lê montagem ativa e retorna plano | `IElectrodeAutomation.PlanFromActiveAssembly` |
| `build_electrode` | Gera `.par` de um passe | `IElectrodeAutomation.BuildElectrode` |
| `generate_burn_report` | Gera relatório de coordenadas | `IElectrodeAutomation.GenerateBurnReport` |
| `list_burn_regions` | Lista regiões de queima detectadas | `ElectrodeBuildPlan.Regions` |
| `get_blank_recommendation` | Retorna blank sugerido | `IBlankLibrary.SelectBlank` |

---

## 5. Plano de Validação Sequencial

### Fase 1 — Correção do bounding box (PRIORIDADE MÁXIMA)

1. Aplicar a correção de `FaceGeometry.TryGetRangeMm` (arrays vazios).
2. Aplicar a correção de fallback por vértices (`Vertex.GetPointData`).
3. Rodar `AutoEDM.exe plan <montagem>` e verificar se o blank é selecionado com dimensões coerentes.

### Fase 2 — Inter-Part Copy mínimo

1. Criar montagem de teste com 2 blocos (`cavidade_teste.par` + `eletrodo_destino.par`).
2. Corrigir `EditInPlaceScope` para usar `Activate = true/false`.
3. Implementar `InterPartCopier.CopyBurnFaces` copiando **1 face plana**.
4. Validar no PathFinder: aparece superfície copiada com símbolo de link.

### Fase 3 — Offset e Stitch

1. Aplicar `OffsetSurfaces.Add` na face copiada.
2. Validar direção (deve encolher para dentro).
3. Costurar superfícies em sólido com `StitchSurfaces.Add`.

### Fase 4 — Blank, holder e furos

1. Criar blank ao redor da pegada usando `AddBoxByTwoPoints`.
2. Adicionar holder e furos M6 + 2×Ø4.
3. Re-pintar faces de queima.

### Fase 5 — Save e relatório

1. Salvar eletrodo como `.par` nativo.
2. Gerar relatório de coordenadas de queima.

---

## 6. Conflitos Documentados a Evitar

| Tema | Recomendação incorreta (docs antigos) | Verdade confirmada |
|---|---|---|
| Inter-Part Copy | `Models.AddCopiedPart` traz peça inteira | Use `Constructions.CopySurfaces.Add` para copiar faces |
| Edição in-place | `Occurrence.Edit()` / `Deactivate()` | `Occurrence.Activate` é propriedade booleana |
| Bounding box | `GetRange` retorna 6 doubles separados | `GetRange` retorna 2 arrays `[out]` de 3 doubles cada |
| MCP | Criar tools genéricas de CAD (blocos, furos) | MCP deve ser camada fina sobre núcleo C# de eletrodos |

---

## 7. Referências

- Dump local: `src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_223.00.13.05.txt`
- Documentação oficial Siemens:
  - [Face.GetRange](https://support.industrysoftware.automation.siemens.com/trainings/se/107/api/SolidEdgeGeometry~Face~GetRange.html)
  - [Edge.GetRange](https://support.industrysoftware.automation.siemens.com/trainings/se/107/api/SolidEdgeGeometry~Edge~GetRange.html)
  - [Solid Edge Part Type Library](https://support.industrysoftware.automation.siemens.com/trainings/se/107/api/SolidEdgePart_P.html)
- Skill do projeto: `.claude/skills/solid-edge-com/SKILL.md`
- README do projeto: `README.md`
- Plano de testes: `docs/PLANO_TESTE_SE.md`
