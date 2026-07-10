# Revisão de Código do AutoEDM — Kimi (2026-07-10)

> Varredura somente leitura em `src/AutoEDM.Core`, `src/AutoEDM.AddIn`, `src/AutoEDM` e
> `src/AutoEDM.Register`. Fontes: dump `SE_API_dump_223.00.13.05.txt`, catálogo
> `docs/api/`, `docs/MEMORIA_SOLID_EDGE_COM.md` e logs `045–047`.
>
> Esta revisão **não altera código** — descreve os achados para que o Claude aplique as
> correções.

---

## 1. Resumo executivo — problemas críticos

| # | Problema | Arquivo:linha | Severidade |
|---|----------|---------------|------------|
| 1 | **`EditInPlaceScope` baseia-se em premissa falsa**: `Occurrence.Activate = true` **não** entra em edição in-place. Todo o Inter-Part Copy via `CopySurfaces.Add` falha por causa disso. | `Assembly/EditInPlaceScope.cs:12-14,33-47` | **Crítico** |
| 2 | **Bug de unidade no posicionamento in-context**: `TryGetOrigin` retorna **metros**, mas `CreateInContextPart` divide por `1000.0` antes de `PutOrigin`. | `ElectrodeBuilder.cs:694`, `AssemblyContext.cs:85-102` | **Alto** |
| 3 | **Aridade errada em `RefPlanes.AddParallelByDistance`**: passa 7 argumentos; a assinatura real tem 6. | `Electrode/BlankModeler.cs:102-104` | **Alto** |
| 4 | **`FaceOffsets.Add` / `OffsetSurfaces.Add` recebem `object[]` onde a API quer `IDispatch` (FaceSet)**. | `Electrode/ModelingHelpers.cs:46-64,69-82` | **Alto** |
| 5 | **`CreateBlankAndHolder` usa `AddBoxByTwoPoints`**, primitivo direto que já se mostrou instável no late binding. | `Electrode/ElectrodeBuilder.cs:890-906` | **Alto** |

---

## 2. Diagnóstico detalhado

### 2.1 `src/AutoEDM.Core/Assembly/EditInPlaceScope.cs`

**Linhas 12–14 / 33–47**

```csharp
// comentário afirma que Activate=true entra em edição in-place
_occurrence.Activate = true;
```

- **Problema:** erro conceitual. O dump mostra `put Activate(p0: bool)` como propriedade de
  ativação da ocorrência, **não** como comando de edição in-place. Os sinais autoritários são
  `AssemblyDocument.ModelingInAssembly` e `InPlaceActivated`, ambos lidos como `False` no Log 029.
- **Impacto:** `CopySurfaces.Add` dá `E_FAIL` porque nunca estamos no modo de modelagem-em-montagem.
- **Recomendação:** documentar explicitamente que a API COM pública não expõe entrada em in-place;
  o escopo deve ser considerado **não operacional** para Inter-Part Copy até descobrirmos o gesto
  correto (comando nativo, `CreateTopologyReference`, ou fluxo semi-automático).

---

### 2.2 `src/AutoEDM.Core/Electrode/ElectrodeBuilder.cs`

**Linha 694**

```csharp
occurrence.PutOrigin(ox / 1000.0, oy / 1000.0, oz / 1000.0);
```

- **Problema:** `AssemblyContext.TryGetOrigin` devolve **metros** (os 6 slots de `GetTransform`
  são metros/radianos). Dividir por 1000 transforma 20 mm em 0,02 mm — eletrodo fica 1000× mais
  perto da origem do que deveria.
- **Recomendação:** remover a divisão por 1000 neste ponto.

**Linhas 821–861 (`ApplyOffset`)**

```csharp
object[] faces = ModelingHelpers.GetFeatureFaces(copyFeature);
ModelingHelpers.AddFaceOffset(constructions, faces, offsetM);
ModelingHelpers.AddOffsetSurface(constructions, faces, offsetM);
```

- **Problema:** `faces` é `object[]`. `FaceOffsets.Add` espera `FacesToOffset: IDispatch`
  (`SolidEdgePart.md:6222`) e `OffsetSurfaces.Add` espera `FaceSet: IDispatch`
  (`SolidEdgePart.md:9486`). Um array de faces não é `IDispatch`.
- **Recomendação:** construir um `FaceSet` COM ou usar `AddEx` com `SAFEARRAY(IDispatch)*`.
  Confirmar no SE real como montar o `FaceSet`.

**Linhas 890–906 (`CreateBlankAndHolder`)**

```csharp
ModelingHelpers.AddBoxByTwoPoints(models, x0, y0, z0, x1, y1, z1,
    blankHeight, plane, extentSide: 1);
```

- **Problema:** usa primitivo direto `AddBoxByTwoPoints`, que nos logs dá `DISP_E_TYPEMISMATCH`
  recorrente no late binding. O fluxo validado de criação de eletrodos usa `BlankModeler.CreateBox`
  (sketch + extrusão).
- **Recomendação:** substituir por `BlankModeler.CreateBox`.

**Linhas 761–764 (`EnableInterPartCopy`)**

```csharp
target.GetType().InvokeMember("SetGlobalParameter", ..., new object[] { 253, true });
```

- **Status:** ✅ Correto. O dump confirma `SetGlobalParameter(Parameter: ApplicationGlobalConstants, Value: VARIANT)`
  e as constantes `seApplicationGlobalAllowInterPart = 253`, `seApplicationGlobalInterPartCopyCommand = 254`.

**Linhas 780–793 (`ReadGlobal`)**

```csharp
var mod = new ParameterModifier(2); mod[1] = true;
app.GetType().InvokeMember("GetGlobalParameter", ..., args, new[] { mod }, ...InvariantCulture);
```

- **Status:** ✅ Correto. Reflete `[in,out] Value: VARIANT*`.

---

### 2.3 `src/AutoEDM.Core/Electrode/BlankModeler.cs`

**Linhas 102–104**

```csharp
topPlane = partDoc.RefPlanes.AddParallelByDistance(
    partDoc.RefPlanes.Item(1), holderHmm / 1000.0, 2,
    Type.Missing, Type.Missing, Type.Missing, Type.Missing);
```

- **Problema:** passa **7** argumentos. A assinatura real é
  `AddParallelByDistance(ParentPlane, Distance, NormalSide, [Pivot], [pivotorigin], [Local])` —
  **6** parâmetros. O sétimo `Type.Missing` provavelmente causa `DISP_E_BADPARAMCOUNT` ou
  `DISP_E_TYPEMISMATCH`.
- **Recomendação:** remover um `Type.Missing`; passar apenas 3 obrigatórios ou até 6.

**Linhas 64–67**

```csharp
var arr = new SolidEdgePart.Profile[] { (SolidEdgePart.Profile)profile };
((object)partDoc.Models).GetType().InvokeMember("AddFiniteExtrudedProtrusion",
    ..., new object[] { 1, arr, extrudeSide, h });
```

- **Status:** ✅ Correto. Array tipado `Profile[]` gera `SAFEARRAY(IDispatch)`. Validado nos logs 045–047.

**Linhas 117–124**

```csharp
object m6 = partDoc.HoleDataCollection.Add(33, fix.CenterTapDrillDiameter / 1000.0);
object hd4 = partDoc.HoleDataCollection.Add(33, fix.DowelDiameter / 1000.0);
```

- **Status:** ✅ Correto (após correção). `igRegularHole=33` funciona no síncrono.
- **Nota:** a versão anterior usava `igTappedHole=37`, que falha com `E_FAIL` e corrompe o proxy
  (`0x80010114`). O fallback para furo simples Ø5 é a solução correta até validarmos `Model.Threads.Add`.

**Linha 126**

```csharp
try { topPlane.Delete(); } catch { }
```

- **Problema:** apagar o plano de referência usado pelas features de furo pode invalidar essas
  features. `ProfileSet.Delete` é seguro em síncrono, mas a deleção do plano pai não foi validada.
- **Recomendação:** confirmar no SE se os furos sobrevivem; se não, manter o plano ou suprimi-lo.

---

### 2.4 `src/AutoEDM.Core/Electrode/ModelingHelpers.cs`

**Linhas 19–41 (`AddBoxByTwoPoints`)**

```csharp
internal static dynamic AddBoxByTwoPoints(dynamic models, ...)
{
    object[] args = { ..., 0 /* pKeyPointFlags */ };
    return InvokeMethod(models, "AddBoxByTwoPoints", args);
}
```

- **Problema:** primitivo direto, histórico de falhas; além disso, `pKeyPointFlags` é ponteiro
  (`KeyPointExtentConstants*`) e `int` boxed pode não marshalar corretamente.
- **Recomendação:** usar sketch+extrusão validado.

**Linhas 46–64 (`AddFaceOffset`)**

```csharp
object[] args = { faces, 0, 0, offsetMeters, Type.Missing, ... };
return InvokeMethod(constructions.FaceOffsets, "Add", args);
```

- **Problema:** `faces` é `object[]`. API espera `FacesToOffset: IDispatch`.
- **Recomendação:** montar `FaceSet` COM ou usar `AddEx` com `SAFEARRAY(IDispatch)*`.

**Linhas 69–82 (`AddOffsetSurface`)**

```csharp
object[] args = { 0, offsetMeters, faces, Type.Missing };
```

- **Problema:** `FaceSet: IDispatch` esperado, mas recebe `object[]`.
- **Recomendação:** mesmo que acima.

**Linhas 87–98 (`StitchSurfaces`)**

```csharp
object[] args = { surfaces.Length, surfaces, heal, toleranceMeters };
```

- **Status:** ⚠️ Assinatura parece correta (`Add(n, SurfaceArray, Heal, Tolerance)`), mas depende
  da cópia funcionar primeiro; não validado no SE real.

**Linhas 134–150 (`GetFeatureFaces`)**

```csharp
dynamic faces = feature.Faces(0);
```

- **Problema:** `0` não é um `FeatureTopologyQueryTypeConstants` válido. `igQueryAll = 1`.
- **Recomendação:** usar `feature.Faces[1]`.

**Linhas 154–169 (`InvokeMethod`)**

```csharp
catch (TargetInvocationException tie) when (tie.InnerException != null)
{
    throw tie.InnerException;
}
```

- **Problema:** estilo. Desencapsular a exceção externa perde o stack trace original.
- **Recomendação:** preservar `TargetInvocationException` ou capturar `COMException` com `ErrorCode`.

---

### 2.5 `src/AutoEDM.Core/Electrode/InterPartCopier.cs`

**Linhas 45–47**

```csharp
var faces = new SolidEdgeGeometry.Face[sourceFaces.Count];
faces[i] = (SolidEdgeGeometry.Face)sourceFaces[i].ComFace;
```

- **Status:** ✅ Tipo correto (`Face[]` tipado → `SAFEARRAY(IDispatch)`).
- **Ressalva:** o pacote `Interop.SolidEdge` é versão 219 (SE 2021). Casts para tipos do SE 2023
  podem falhar se os GUIDs/TLBs divergirem.

**Linhas 65–75**

```csharp
ipc.Add2(targetElectrodeDocument, faces);
ipc.Add(faces);
```

- **Problema:** `InterpartConstructions.Add2(PartTarget, AsmSource)` e `Add(AsmSource)` esperam
  `IDispatch` como fonte, mas `Face[]` não é um único `IDispatch`. Testes anteriores já falharam.
- **Recomendação:** implementar fallback com `AssemblyDocument.CreateReference` +
  `Occurrence.CreateTopologyReference`.

---

### 2.6 `src/AutoEDM.Core/Assembly/AssemblyContext.cs`

**Linhas 85–109 (`TryGetOrigin`)**

```csharp
/// <summary>Best-effort read of an occurrence origin (mm) ...</summary>
```

- **Problema:** o comentário diz retorno em mm, mas o código não multiplica por 1000 — e
  `GetTransform` realmente retorna metros. Isso induziu o bug de `ElectrodeBuilder.cs:694`.
- **Recomendação:** padronizar o comentário para "metros" e garantir que consumidores não dividam
  por 1000.

**Linhas 123–128 (`TryGetPlacement`)**

- **Ressalva:** também chama `InvokeMember("GetTransform", ...)` sem `ParameterModifier`. Funcionou
  nos logs, mas é frágil. Considerar usar `ParameterModifier(6)` by-ref para robustez.

---

### 2.7 `src/AutoEDM.Core/Selection/FaceStyleColorReader.cs`

**Linhas 109–120 (`TryGetDiffuse`)**

```csharp
object[] args = { 0.0, 0.0, 0.0 };
styleObj.InvokeMember("GetDiffuse", ..., args);
```

- **Problema:** `GetDiffuse` tem parâmetros `[out]`. Sem `ParameterModifier` by-ref, os valores
  podem não ser populados.
- **Recomendação:** adicionar `ParameterModifier(3)` com todos `true` e `InvariantCulture`,
  como feito em `ElectrodeBuilder.DumpFaceColorSources`.

---

### 2.8 `src/AutoEDM.Core/Experiments/BlankBoxProbe.cs`

- **Ressalva:** probe mantém testes com `HoleDataCollection.Add(37, ...)` (`igTappedHole`). Isso
  é aceitável em código de experimento, mas **não** deve ir para `BlankModeler.AddFixationHoles`
  até que `Model.Threads.Add` seja validado.

---

### 2.9 `src/AutoEDM.Core/AutoEDM.Core.csproj`

```xml
<PackageReference Include="Interop.SolidEdge" Version="219.0.0" />
```

- **Ressalva:** versão 219 = SE 2021 (ST11). O projeto roda no SE 2023 (`223.00.13.05`). Os
  GUIDs de `SolidEdgeGeometry.Face` / `SolidEdgePart.Profile` podem divergir, causando
  `InvalidCastException` nos casts tipados.
- **Recomendação:** monitorar; se necessário, atualizar para o pacote da versão 223 ou gerar
  interop localmente a partir da TLB do SE 2023.

---

## 3. APIs usadas corretamente (validadas)

| Método/Propriedade | Onde | Status |
|---|---|---|
| `ComInterop.GetActiveObject("SolidEdge.Application")` | `Com/SolidEdgeConnector.cs:181` | ✅ |
| `Application.GetDefaultTemplatePath(1)` | `Electrode/ElectrodeBuilder.cs:727` | ✅ |
| `Documents.Add("SolidEdge.PartDocument")` | `Com/SolidEdgeConnector.cs:140`, `ElectrodeBuilder.cs:330` | ✅ |
| `Occurrences.AddByTemplate(path, template)` | `Electrode/ElectrodeBuilder.cs:685` | ✅ |
| `Occurrences.AddByFilename(path)` | `Electrode/ElectrodeBuilder.cs:337` | ✅ |
| `Occurrence.GetTransform` | `Assembly/AssemblyContext.cs:94,125` | ✅ (com ressalva do `ParameterModifier`) |
| `Occurrence.PutOrigin` | `Electrode/ElectrodeBuilder.cs:338,694` | ✅ (com ressalva da unidade em 694) |
| `ProfileSets.Add → Profiles.Add → Lines2d.AddBy2Points → Profile.End(1) → Models.AddFiniteExtrudedProtrusion` | `Electrode/BlankModeler.cs:45-67` | ✅ |
| `HoleDataCollection.Add(33, diameter)` + `Holes.AddSync` | `Electrode/BlankModeler.cs:117-150` | ✅ |
| `Application.SetGlobalParameter(253/254, true)` | `Electrode/ElectrodeBuilder.cs:761-764` | ✅ |

---

## 4. APIs ainda não testadas / bloqueadas

| API/Caminho | Status | Hipótese / próximo passo |
|-------------|--------|--------------------------|
| `CopySurfaces.Add` | ⛔ Bloqueado | Requer edição in-place real; não há API COM pública para isso. |
| `InterpartConstructions.Add` / `Add2` | ❌ Falhou | Não aceita os tipos testados. Próxima hipótese: `Reference`/`TopologyReference`. |
| `CreateReference(Occurrence, Entity)` | 🟡 No dump | Pode ser o consumidor correto do `InterpartConstructions`. |
| `Face.GetReferenceKey` + `Occurrence.CreateTopologyReference` | 🟡 No dump | API dedicada a referência de topologia in-context. Alto potencial. |
| `Model.Threads.Add` | 🟡 Descoberta (Log 047) | Assinatura conhecida; pode renderizar rosca M6 separadamente. |
| `OffsetSurfaces.Add` + `StitchSurfaces.Add` | 🟡 Assinaturas conhecidas | Depende da cópia de faces funcionar primeiro. |

---

## 5. Conclusões e prioridades de correção

1. **Corrigir imediatamente (alto impacto, baixo custo):**
   - Remover divisão por 1000 em `ElectrodeBuilder.cs:694`.
   - Corrigir aridade de `AddParallelByDistance` em `BlankModeler.cs:102-104`.
   - Corrigir comentário de `AssemblyContext.TryGetOrigin` para "metros".

2. **Requer redesign (médio impacto):**
   - Substituir `AddBoxByTwoPoints` em `CreateBlankAndHolder` por `BlankModeler.CreateBox`.
   - Refazer `ModelingHelpers.AddFaceOffset` / `AddOffsetSurface` para usar `FaceSet` COM ou
     `AddEx` com array tipado.

3. **Bloqueio estratégico (sem solução rápida):**
   - Inter-Part Copy continua bloqueado. A alternativa mais promissora é
     `CreateTopologyReference`, ainda não testada no SE real.

4. **Oportunidade futura:**
   - Validar `Model.Threads.Add` para renderizar rosca M6 sem depender de `igTappedHole`.
