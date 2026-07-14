# Guia — Automação do Solid Edge por COM (a "Pedra de Roseta")

> Como dirigir o **Solid Edge 2023/2026** por **COM Automation** a partir de C#/.NET
> **sem** o SDK instalado e **sem** depender do pacote Community (que cobre pouco).
> Este guia consolida os aprendizados **validados em execução real** (Solid Edge 2023
> `223.00.13.05`) durante o projeto AutoEDM. Cada afirmação aqui ou foi confirmada num
> run numerado (os "Logs NNN") ou lida do **dump da type library** / da **reflexão do
> interop** — nada é adivinhado de memória.

**Público:** quem vai escrever integração COM com o Solid Edge do zero. Leia este guia
primeiro; depois use o catálogo de API em [`docs/api/`](./api/README.md) para assinaturas
exatas e a [memória SE-COM](./MEMORIA_SOLID_EDGE_COM.md) para o status (✅/❌/🟡) de cada
capacidade.

---

## 0. A regra de ouro: **introspecção, nunca adivinhação**

A doc do SDK da Siemens exige login e as type libraries não são baixáveis — mas o Solid
Edge **registra as typelibs localmente**. Então descubra a API em tempo de execução:

1. **Dump completo da typelib, uma vez** ("SDK offline"). De qualquer objeto IDispatch vivo:
   `ITypeInfo` → `GetContainingTypeLib` → `ITypeLib` → enumere toda coclass/interface/enum
   com **nomes de parâmetro, direção (`[in]/[out]/[opt]`) e valores de enum**. Semeie com um
   objeto de cada módulo (dedup por GUID de lib). Implementação de referência:
   [`ComDiagnostics.DumpTypeLibraries`](../src/AutoEDM.Core/Com/ComDiagnostics.cs) +
   [`ModelingProbe.DumpSdk`](../src/AutoEDM.Core/Experiments/ModelingProbe.cs). Saída em
   `src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_<versão>.txt` — **faça grep, nunca carregue
   inteiro.**

   | Módulo | Typelib | Objeto semente |
   |---|---|---|
   | Aplicação | SolidEdgeFramework | `Application` |
   | Montagem | SolidEdgeAssembly | o `AssemblyDocument` ativo |
   | Peça/features | SolidEdgePart | um `PartDocument`, seus `.Models`, `.Constructions`, `.RefPlanes` |
   | **Geometria** (Body/Face/Edge — onde vive `GetRange`) | SolidEdgeGeometry | uma `Face`/`Edge` viva |

2. **Quando o dump não pega uma lib** (o walk tipado às vezes trava antes da geometria/features),
   **reflita o `Interop.SolidEdge.dll`** — a projeção .NET tem **todas** as assinaturas e valores
   de enum, sem precisar rodar o SE. É como foi gerado [`SolidEdgeGeometry.md`](./api/SolidEdgeGeometry.md)
   e [`SolidEdgeAssembly.md`](./api/SolidEdgeAssembly.md) (ver [`tools/reflect_api_docs.ps1`](../tools/reflect_api_docs.ps1)).

3. **Para uma assinatura só**, sem o dump inteiro: `ComDiagnostics.LogSignatures(obj, "Método", …)`
   loga nome + params + `cParams` via ITypeInfo.

> **Ordem de confiança:** dump da typelib da máquina do usuário > reflexão do interop > docs
> autorais (`SolidEdge_API_COM_Referencia_Completa.md`) > memória. Quando divergem, **o dump vence**.

---

## 1. Restrições não-negociáveis (preveja os erros antes deles)

- **Unidades em METROS/RADIANOS** na API de geometria/modelagem. 20 mm = `0.020`; converta
  ranges ×1000 para mm.
- **Coleções são 1-based** (`.Item(1)`, `.Count`).
- **Só x64** — SE 2023/2026 são 64-bit; build 32-bit não conecta.
- **Thread STA obrigatória** — rode a automação em `[STAThread]` (a thread de UI do WinForms é
  STA; uma worker comum não é — crie uma STA ou marshale). Ver [`MainForm.RunOnSta`](../src/AutoEDM/UI/MainForm.cs).
- **Late binding (`dynamic`)** para compilar sem as typelibs. Faça cast para `(object)` antes de
  chamar seus próprios métodos/extensões sobre um resultado `dynamic` (força binding estático).
- **Instale um OLE message filter** — o SE é um servidor ocupado; sem filtro, chamadas estouram
  `RPC_E_CALL_REJECTED (0x80010001)` ou, enquanto o SE recalcula/mostra modal,
  `RPC_E_SERVERCALL_RETRYLATER (0x8001010A)`. O filtro tem que re-tentar nos **dois**. Ver
  [`OleMessageFilter`](../src/AutoEDM.Core/Com/OleMessageFilter.cs).
- **Conecte na instância viva** via Running Object Table
  (`ComInterop.GetActiveObject("SolidEdge.Application")`) — `Marshal.GetActiveObject` sumiu no
  .NET moderno, então P/Invoke `ole32`/`oleaut32`. Ver [`ComInterop`](../src/AutoEDM.Core/Com/ComInterop.cs).

---

## 2. A armadilha nº 1: **parâmetros `[out]` em late binding**

Muitos métodos essenciais devolvem valores por **parâmetro de saída**, não pelo retorno:
`Face.GetRange`, `Face.GetRGBAVals`, `Occurrence.GetTransform`, `Vertex.GetPointData`,
`Style.GetDiffuse`. Em **late binding**, `InvokeMember` **não popula** os `[out]` a menos que
você marque os argumentos como by-ref com um `ParameterModifier` — senão eles voltam
**zerados** (e você lê "origem, sem rotação" / "cor preta" sem nenhum erro).

Olhe a assinatura no catálogo e o `[out]` fica óbvio (ex. em [`SolidEdgeGeometry.md`](./api/SolidEdgeGeometry.md)):

```
GetRange([out] MinRangePoint: Array, [out] MaxRangePoint: Array) -> void
GetRGBAVals([out] RVal: Double, [out] GVal: Double, [out] BVal: Double, [out] AVal: Double) -> void
```

Receita correta (implementação de referência: [`FaceGeometry.TryGetRangeMm`](../src/AutoEDM.Core/Selection/FaceGeometry.cs)):

```csharp
object[] args = { new double[0], new double[0] };      // placeholders [out]
var mod = new ParameterModifier(2); mod[0] = true; mod[1] = true;   // marca by-ref
comFace.GetType().InvokeMember("GetRange", BindingFlags.InvokeMethod, null, comFace,
    args, new[] { mod }, CultureInfo.InvariantCulture, null);
// agora args[0]/args[1] têm os SAFEARRAY(double) de 3 componentes (metros)
```

> Um `GetTransform` lido todo-zero (sem `ParameterModifier`) causou **~23 mm de deslocamento**
> do eletrodo até ser corrigido. Se um getter "não popula", **suspeite do `[out]` antes de tudo**.

---

## 3. Cor e geometria de face

- **A cor da face vive em TRÊS lugares** — o comando "pintar" do SE por padrão pinta a
  **feature**, não a face, então trate os três (prioridade nesta ordem). Ver
  [`FaceStyleColorReader`](../src/AutoEDM.Core/Selection/FaceStyleColorReader.cs) e
  [`FaceSelector.BuildFeatureColorMap`](../src/AutoEDM.Core/Selection/FaceSelector.cs):
  1. **Por face:** `face.Style.Diffuse{Red,Green,Blue}` (0..1, ×255). `face.Style` é **`null`**
     se a pintura não for override por face.
  2. **Por feature (o padrão!):** não aparece na face — `PartDocument.Models.Item(m).Features.Item(i).GetStyle()`
     dá o Style. As faces da feature são `feature.Faces[queryType]` (**indexado como `Body.Faces[1]`**,
     não coleção nua). Monte um mapa `Face.ID → cor` das features e resolva cada face por `Face.ID`.
  3. **Corpo/efetiva:** `Face.GetRGBAVals(out R,G,B,A)` (0..1, **by-ref!**) — cor-base, NÃO a pintura da feature.
- **Travessia de face:** `PartDocument.Models → Model.Body → Body.Faces[queryType] → Face`;
  **`queryType` é um enum** (`FeatureTopologyQueryTypeConstants`): `igQueryAll=1`, `igQueryPlane=6`,
  `igQueryCylinder=10`, … (valores em [`SolidEdgeGeometry.md`](./api/SolidEdgeGeometry.md)). `Body.Faces` E
  `Feature.Faces` são **indexados por esse enum**: use `X.Faces[1]` e então `.Count`/`.Item(k)`.
- **Bounding box, ordem robusta:** `Face.GetRange`/`GetExactRange` ([out], by-ref) primeiro; se o
  binder não popular, itere `Face.Vertices` + `Vertex.GetPointData(out ponto)` e monte a AABB. Se o
  objeto **não for uma Face** (uma superfície/aresta de um `SelectSet`), `GetRange` lança
  `DISP_E_UNKNOWNNAME (0x80020006)` e `.Vertices` não existe — proteja a leitura e pule não-faces.

---

## 4. Transform de ocorrência (posicionar peças na montagem)

- **Ler:** `Occurrence.GetTransform(out x,y,z, out ax,ay,az)` (metros + radianos) — os 6 são
  `[out]`, **marque by-ref** ou voltam `0,0,0,0,0,0`. Ver [`AssemblyContext`](../src/AutoEDM.Core/Assembly/AssemblyContext.cs).
- **Escrever:** `Occurrence.PutOrigin(x,y,z)` (só translação) ou `Occurrence.PutTransform(x,y,z, ax,ay,az)`
  (translação **+ rotação**) — ambos em metros, ambos **sem** edição in-place.
- **Alinhar uma peça nova a uma ocorrência girada:** leia o transform da fonte (by-ref!), posicione
  com `PutTransform` usando os ângulos da fonte, e **rotacione qualquer offset local** (ex.: o centro XY
  de uma feature) pelo mesmo ângulo antes de somar a translação. Rotação pura em Z não muda Z; avise/pule
  inclinação em X/Y.

---

## 5. Modelagem sólida que FUNCIONA (receita validada)

Modele **no próprio documento da peça** (`app.Documents.Add("SolidEdge.PartDocument")` ou
`occurrence.OccurrenceDocument`) — é standalone e **não precisa** de edição in-place. As primitivas
(`AddBoxByTwoPoints`/`AddBoxByCenter`) são chatas (`DISP_E_TYPEMISMATCH`); use **sketch + extrusão**.
Implementação de referência: [`BlankModeler`](../src/AutoEDM.Core/Electrode/BlankModeler.cs).

```
ProfileSet  = partDoc.ProfileSets.Add()
profile     = ProfileSet.Profiles.Add(partDoc.RefPlanes.Item(1))     // Item(1) = plano XY
Lines2d.AddBy2Points(x1,y1, x2,y2) ×4      (retângulo, METROS)  — OU:
profile.Circles2d.AddByCenterRadius(cx, cy, raioMetros)         (cilindro)
profile.End(1)                              // 1 = perfil FECHADO (obrigatório antes de extrudar)
Models.AddFiniteExtrudedProtrusion(1, Profile[] TIPADO, side, distânciaMetros)
ProfileSet.Delete()                         // esboço feito por código fica travado — apague
```

- **`ProfileArray` precisa ser `SolidEdgePart.Profile[]` TIPADO** (→ `SAFEARRAY(IDispatch)`); `object[]`
  falha (mesma lição do `Face[]`). Referencie o interop só para o array; mantenha o resto late-bound.
- `side` = direção da extrusão: `1 = igLeft (−normal)`, `2 = igRight (+normal)`, `3 = simétrico`.
- **Síncrono × ordenado (isto morde):** o template padrão (SE 2023) é **Síncrono**
  (`PartDocument.ModelingMode`: síncrono=1, ordenado=2). Métodos vêm em dois sabores: ordenado
  (`AddFinite…`) e **`AddSync…`**. Numa peça síncrona um método ordenado **cria a geometria mas a feature
  NÃO aparece no PathFinder** — use `AddSync` para virar feature de verdade. Ramifique no `ModelingMode`.
- **Esboços de `ProfileSets` são ORDENADOS — apague-os por código.** Depois que a feature síncrona consome
  o perfil, o esboço ordenado sobra **travado (o usuário não apaga na UI)**. Então `ProfileSet.Delete()`
  logo após — a feature síncrona sobrevive.
- **Plano base num offset em Z** (para levantar o bloco, ou desenhar no topo):
  `RefPlanes.AddParallelByDistance(Item(1), Zmetros, NormalSide, [Pivot], [PivotOrigin], [Local])` — `Distance`
  é **sempre positivo**; a direção é o `NormalSide` (`igLeft=1`/`igRight=2`). **Esconda** (`plane.Visible=false`),
  não apague (apagar plano de construção pode invalidar features ordenadas a jusante).
- **Confira `.Status`, não só try/catch:** o SE **não lança** COM error numa feature que falha — ele marca
  `feature.Status`. Depois de todo `Add`, cheque `Status == igFeatureOK (1216476310)` (`igFeatureFailed = 1216476311`).

---

## 6. Furos e roscas

- **Marque centros com `Profile.Holes2d.Add(x,y)`** — NÃO `Circles2d` (um círculo faz a feature de furo
  criar **zero furos, sem erro**). `HoleData = PartDocument.HoleDataCollection.Add(HoleType, ØMetros, …)` com
  `HoleType = igRegularHole (33)`; e numa peça síncrona
  `Model.Holes.AddSync(1, Profile[] TIPADO, side, ExtentType, profundidadeMetros, HoleData)` com
  `ExtentType = igFinite (13)` para furo cego. **Um `Holes2d.Add` por perfil** — dois centros num perfil só
  dão **um** furo. Ver [`BlankModeler.AddFixationHoles`](../src/AutoEDM.Core/Electrode/BlankModeler.cs).
- **A armadilha do `0x80010114` após regeneração (importante):** depois de um `Add` de feature o modelo
  **regenera** e o proxy da peça / coleções-filho (`Models`, `HoleDataCollection`, `Holes`) ficam
  brevemente desconectados. A **próxima** chamada COM estoura `0x80010114` ("o objeto não existe") — mesmo
  que o `Add` tenha dado certo. Pior: **a chamada que falha costuma ser uma op benigna que INICIA a próxima
  feature** (ex. o `ProfileSets.Add()` do próximo furo). Mitigação (Log 58):
  - crie os dependentes (ex. **todos** os `HoleData`) **antes** do primeiro `Add`;
  - **re-obtenha as coleções frescas** após cada `Add` (não cacheie `model.Holes`);
  - **re-tente 1× no `0x80010114`** após um respiro — e ponha **também o primeiro `Add` da próxima op**
    (o `ProfileSets.Add()`) **dentro** do try+retry, com cada item independente (uma falha não derruba os irmãos).
- **Rosca/tap:** `ThreadDataByDescription('M6')` **lança** em todo formato (`Erro ao chamar`) — preencha as
  props na mão (`Standard='ISO Metric', Size='M6', ThreadNominalDiameter, ThreadMinorDiameter, ThreadTapDrillDiameter`).
  `Model.Threads.Add(HoleData, NumCilindros, CylinderArray, CylinderEndArray)` na face cilíndrica do furo
  (`Body.Faces[igQueryCylinder=10]`) deu `E_FAIL`/fora-de-intervalo; um `HoleData` tapped com props + `AddSync`
  criou um furo para conferir na tela. **Prove rosca num probe DESCARTÁVEL** (um `E_FAIL` "envenena" o proxy).
  Na dúvida, entregue o M6 como furo-guia Ø5 simples (`igRegularHole`) e deixe o operador roscar.

---

## 7. Superfícies

- **Inter-part copy é BLOQUEADA por COM** (confirmado em ~11 runs): sem edição in-place real,
  `Constructions.CopySurfaces.Add` se comporta como **intra-peça** e dá `E_FAIL` com faces de OUTRA peça;
  `InterpartConstructions`/`CreateReference` também recusam. **Não gaste runs nisso** — deixe o humano fazer a
  cópia associativa, ou modele na própria peça. Ver [`InterPartCopier`](../src/AutoEDM.Core/Electrode/InterPartCopier.cs).
- **Mas features de superfície FUNCIONAM *intra-peça*** (SE 2023, Logs 57/58). Dentro de UMA peça (ex.: depois
  que o humano copiou as faces de queima para dentro dela), as coleções de `Constructions` funcionam:

  | Op | Chamada |
  |---|---|
  | Copiar faces da própria peça → superfície | `Constructions.CopySurfaces.Add(NumFaces, FaceArray SAFEARRAY(IDispatch), [opt]InternalBoundary, [opt]ExternalBoundary) → CopySurface` |
  | Costurar superfícies (→ sólido) | `Constructions.StitchSurfaces.Add(NumSurfaces, SurfaceArray SAFEARRAY(IDispatch), [opt]Heal, [opt]Tolerance) → StitchSurface` |
  | Offset de superfície (spark gap) | `Constructions.OffsetSurfaces.Add(Side, offsetDistance, FaceSet, [opt]Boundary) → OffsetSurface` |
  | Engrossar superfície em sólido | `Models.AddThickenFeature(…)` |

  - **NÃO existe `ExtendSurfaces`** — para uma superfície alcançar/fechar contra um sólido, **engrosse**
    (`AddThickenFeature`), reconstrua com `SurfaceByBoundaries`, ou substitua a face do sólido.
  - `FaceArray`/`SurfaceArray` querem o **`SAFEARRAY(IDispatch)` TIPADO**. Um `SelectSet` de faces te dá as
    faces (pegada) mas **nenhum objeto de superfície** para costurar — crie um com `CopySurfaces.Add(faces)` primeiro.

---

## 8. Edição in-place NÃO é `Occurrence.Activate = true`

Esse booleano só **carrega/ativa** a ocorrência (gestão de memória de montagem grande); **não** entra em
edição de peça in-place. Os sinais autoritativos são `AssemblyDocument.ModelingInAssembly` e `.InPlaceActivated`
(getters bool) — **leia-os, nunca assuma**. Não há verbo COM confiável para entrar em edição in-place de peça.
Contorno validado (substitui o inter-part copy): construa a peça **standalone**, `SaveAs` → `Close`, depois
`Occurrences.AddByFilename(path)` → `PutOrigin/PutTransform`.

---

## 9. Erro → causa → correção (tabela de campo)

| Sintoma | Causa | Correção |
|---|---|---|
| `RuntimeBinder: '...' não contém definição para 'ToList'` | LINQ sobre resultado COM `dynamic` | Atribua a variável tipada e então LINQ; cast `(object)` força binding estático |
| `CS8183: cannot infer type of implicit discard` em `Método(dynObj, …, out _)` | Args dynamic → chamada late-bound; o compilador não tipa o discard `out _` | Use variável tipada de descarte: `T ignore; Método(…, out ignore);` |
| `RPC_E_CALL_REJECTED (0x80010001)` / `RETRYLATER (0x8001010A)` | SE ocupado / modal | Registre o OLE message filter (re-tenta nos dois) |
| `[out]`/`ref` voltam vazios (`GetRange`, `GetTransform`, `GetRGBAVals`) | late binding não popula `[out] SAFEARRAY` sem by-ref | `ParameterModifier` marcando os `[out]` + `CultureInfo.InvariantCulture` |
| `DISP_E_TYPEMISMATCH` passando `Face[]`/`Profile[]` | `object[]` marshala como `SAFEARRAY(VARIANT)`; o método quer `SAFEARRAY(IDispatch)` | Array **tipado** do interop (`SolidEdgeGeometry.Face[]`, `SolidEdgePart.Profile[]`) |
| `DISP_E_UNKNOWNNAME (0x80020006)` em `GetRange` | o objeto não é uma `Face` (é superfície/aresta) | Proteja e pule; ou desça em `item.Faces[igQueryAll]` |
| Método com aridade errada | arity chutada | Leia `cParams` no dump; métodos SE têm muitos opcionais no fim |
| `X.Faces.Count` lança / vem vazio | `Faces` é **indexado por enum de query**, não coleção nua (Body E Feature) | `X.Faces[1]` (`igQueryAll`), então `.Count`/`.Item(k)` |
| `Add` de feature **não lança** mas a geometria está errada/ausente | o SE marca `.Status`, não lança | Cheque `feature.Status == igFeatureOK (1216476310)` como uint32 |
| `E_FAIL (0x80004005)` em op inter-part / in-context | você **não** está em edição in-place | Confirme `ModelingInAssembly`; se false, troque por um contorno sem in-place |
| `0x80010114` na chamada logo **após** um `Add` (mesmo bem-sucedido) — inclusive no `ProfileSets.Add()` da PRÓXIMA feature | regeneração desconecta o proxy/coleções | Crie dependentes antes; re-obtenha coleções frescas; **retry 1× no 0x80010114** com a 1ª chamada da próxima op **dentro** do try; itens independentes |
| Feature ordenada não aparece no PathFinder | método ordenado numa peça síncrona | Use `AddSync*`; ramifique no `ModelingMode` |

---

## 10. Add-in de ribbon (botão dentro do SE)

Para um botão **dentro** do SE (vs. EXE externo), use o NuGet **`SolidEdge.Community.AddIn`** (traz
`Interop.SolidEdge`, compila sem SE). Padrão verificado — ver [`ElectrodeAddIn`](../src/AutoEDM.AddIn/ElectrodeAddIn.cs)
e [`ElectrodeRibbon`](../src/AutoEDM.AddIn/ElectrodeRibbon.cs):

- Classe `[ComVisible(true)][Guid(...)][ProgId(...)] MeuAddIn : SolidEdgeCommunity.AddIn.SolidEdgeAddIn`.
- `OnConnection(...)` → `base.OnConnection(...)`, então `AddInEx.GuiVersion = N` (**incremente quando mudar a
  ribbon**, senão o SE não recarrega o layout).
- `OnCreateRibbon(controller, envCategory, firstTime)` → `controller.Add<MinhaRibbon>(envCategory, firstTime)`.
- `[ComRegisterFunction]`/`[ComUnregisterFunction]` com `RegistrationSettings` (Environments/Titles/Summaries).
- `MinhaRibbon : Ribbon`, ctor `LoadXml(assembly, "Namespace.Ribbon.xml")` (recurso embutido); override
  `OnControlClick(control)` e despache por `control.CommandId` (= `id` no XML).
- **Ícones** vêm de um recurso **Win32 `RT_BITMAP` embutido no DLL** (`<Win32Resource>…res</Win32Resource>`);
  `imageId` do XML = o ID do RT_BITMAP. Sem `rc.exe`, gere o `.res` na mão (ver [`tools/make_ribbon_res.ps1`](../tools/make_ribbon_res.ps1)).
- **In-process:** você recebe `Application` direto no `OnConnection` — sem ROT. **Não** libere o `Application` do SE no teardown.
- **Sem admin?** Registre por-usuário em `HKCU\Software\Classes` (o SE lê HKCU antes de HKLM). Ver
  [`AutoEDM.Register`](../src/AutoEDM.Register/Program.cs).

---

## 11. O loop de trabalho (com um humano rodando o SE)

A máquina de dev normalmente **não** tem licença do SE; um humano roda o app e devolve um **log numerado**.
Para o loop sair barato:

1. **Não-destrutivo primeiro.** Probes novos só leem + introspectam + logam. Nada de geometria/save antes de a API estar confirmada.
2. **Uma pergunta por run.** Cada log responde a UM desconhecido (uma assinatura, um valor de enum, se um getter popula).
3. **Grep no dump antes de propor uma chamada** — a resposta quase sempre já está lá.
4. **Logs numerados** (`AutoEDM NNN.log`) para runs comparáveis.
5. **Feche a GUI antes de rebuildar** (um WinExe rodando trava o `.exe`). **Add-in: feche o SE antes do rebuild**
   e re-rode o registrador por-usuário; suba `GuiVersion` só quando o layout da ribbon muda.
6. **Quando um tool "não acha nada", faça-o dumpar a estrutura** (ocorrências, corpos, contagem de faces) e o
   valor-alvo de **toda fonte plausível**. `continue` silencioso numa leitura falha esconde a causa.

---

## 12. Onde as coisas moram (mapa do repositório)

| O quê | Onde |
|---|---|
| **Este guia** (aprendizados) | `docs/GUIA_SOLID_EDGE_COM.md` |
| **Status ✅/❌ de cada capacidade** COM | [`docs/MEMORIA_SOLID_EDGE_COM.md`](./MEMORIA_SOLID_EDGE_COM.md) |
| **Catálogo de API** (objetos/métodos/params/enums) | [`docs/api/`](./api/README.md) — Framework, Part, **Geometry**, **Assembly**, constants |
| **Dump da typelib** (fonte de verdade da máquina) | `src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_<versão>.txt` |
| **Gerador de docs do dump** | [`tools/generate_api_docs.py`](../tools/generate_api_docs.py) |
| **Gerador por reflexão do interop** (Geometry/Assembly) | [`tools/reflect_api_docs.ps1`](../tools/reflect_api_docs.ps1) |
| Introspecção COM (dump/LogSignatures) | [`ComDiagnostics`](../src/AutoEDM.Core/Com/ComDiagnostics.cs) |
| Conexão + message filter | [`SolidEdgeConnector`](../src/AutoEDM.Core/Com/SolidEdgeConnector.cs), [`ComInterop`](../src/AutoEDM.Core/Com/ComInterop.cs), [`OleMessageFilter`](../src/AutoEDM.Core/Com/OleMessageFilter.cs) |
| Leitura de geometria/cor (out-params) | [`FaceGeometry`](../src/AutoEDM.Core/Selection/FaceGeometry.cs), [`FaceSelector`](../src/AutoEDM.Core/Selection/FaceSelector.cs) |
| Transform de ocorrência | [`AssemblyContext`](../src/AutoEDM.Core/Assembly/AssemblyContext.cs) |
| Modelagem (sketch+extrusão, furos, eixo, cilindro) | [`BlankModeler`](../src/AutoEDM.Core/Electrode/BlankModeler.cs), [`ModelingHelpers`](../src/AutoEDM.Core/Electrode/ModelingHelpers.cs) |
| Add-in (ribbon) | [`ElectrodeAddIn`](../src/AutoEDM.AddIn/ElectrodeAddIn.cs), [`ElectrodeRibbon`](../src/AutoEDM.AddIn/ElectrodeRibbon.cs) |

---

### Versão

Validado em **Solid Edge 2023 `223.00.13.05`** (`Interop.SolidEdge` v219). Para outra versão do SE,
regenere o dump e o catálogo — **valores de enum e assinaturas podem mudar**; o dump da máquina-alvo é
sempre a fonte de verdade.
