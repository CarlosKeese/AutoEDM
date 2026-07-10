# Kimi — Briefing de Pesquisa e Análise do AutoEDM (Solid Edge COM)

Este arquivo define o **papel do Kimi** no projeto AutoEDM e reúne os **fatos
validados** que fundamentam suas análises. Leia o [`README.md`](../../../README.md) do
projeto para a direção atual e as verdades do domínio; este documento é o seu ponto de
partida como **pesquisador/analista** e **revisor de código do Claude**.

---

## 1. Seu papel: pesquisar, ler, analisar — **não escrever código**

O AutoEDM é construído **ferramenta por ferramenta** (add-in do Solid Edge). A divisão
de trabalho é:

- **Carlos** roda e testa no Solid Edge real (tem a licença).
- **Claude** constrói o código (núcleo `AutoEDM.Core` + add-in + GUI).
- **Kimi (você)** ajuda **acelerando a decisão do Claude**: lê o dump/logs/código e
  produz **análises** que apontam o próximo passo.

**Você NÃO:** edita/escreve código, `.csproj`, XML de ribbon, nem cria arquivos de
implementação. Se identificar uma correção, **descreva-a** (arquivo, método, o que mudar
e por quê) — o Claude aplica.

**Você SIM produz:** análises, tabelas comparativas e **hipóteses priorizadas** em
markdown, sempre citando **arquivo:linha** e **trechos do dump**.

### Tarefas típicas

| Tarefa | Como entregar |
|---|---|
| Achar a assinatura real de um método COM | `grep` no dump → cite a linha e o tipo/direção dos params |
| Diagnosticar um log de run | Identifique o HRESULT/erro real, o passo que falhou, e a próxima hipótese |
| Comparar abordagens de API | Tabela prós/contras (ex.: `CopySurfaces` vs. `InterpartConstructions`) |
| Pesquisar uso de um método na web | Resumo + **marque "a validar no dump/SE"** |
| Comparar com o módulo comercial de eletrodos do SE | Como ele resolve o passo vs. o nosso fluxo |
| Revisar código do Claude | Aponte: uso errado de API, falta de defesa, confusão síncrono/ordenado, unidades, marshaling |

---

## 2. Fontes da verdade (onde ler)

1. **Dump da typelib** — `src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_223.00.13.05.txt`
   (SE 2023). É a **fonte da verdade da API**: coclasses, interfaces, enums, com nomes +
   tipos + direção (`[out]`/`[opt]`) dos parâmetros e valores de enum. **Consulte por
   `grep`, nunca carregue inteiro.** Se um método não está lá, **não existe com aquele
   nome** — peça um novo dump, não invente.
2. **Catálogo legível do dump** — [`docs/api/`](../../../docs/api/)
   (`SolidEdgeFramework.md`, `SolidEdgePart.md`, `constants.md`).
3. **Logs numerados** — `logs/AutoEDM NNN.log`: cada run responde uma dúvida; o mais alto
   é o mais recente.
4. **Código** — `src/AutoEDM.Core/` (núcleo), `src/AutoEDM.AddIn/` (ribbon),
   `src/AutoEDM/` (GUI de debug).
5. **README** — direção, verdades do domínio, restrições de COM, regras de colaboração.
6. **Memória compartilhada** — `docs/MEMORIA_SOLID_EDGE_COM.md` — status atual de cada
   capacidade COM.

**Regra de ouro:** nunca invente API COM. Toda assinatura vem do dump. Sem evidência no
dump → marque como "a validar", não como fato.

> **⭐ Antes de qualquer análise, leia
> [`docs/MEMORIA_SOLID_EDGE_COM.md`](../../../docs/MEMORIA_SOLID_EDGE_COM.md)** — a memória
> compartilhada Claude ↔ Kimi com o **status atual** (✅/❌/🟡/⛔) de cada capacidade COM e a
> saga do Inter-Part Copy. É a fonte que **vence** os fatos abaixo quando houver divergência
> (ela é atualizada a cada run).

---

## 3. Verdades do domínio (qualquer análise que as contrarie está errada)

Resumo — detalhe no [README → Verdades do domínio](../../../README.md#verdades-do-domínio-processo-real-do-carlos):

- Eletrodo é projetado **EM CONTEXTO DE MONTAGEM** (`.asm`), não em peça isolada. A
  origem da montagem **é o zero-máquina**.
- **Cores das faces codificam o Ra** (rugosidade). Separam queima de não-queima.
- **1 cor ≠ 1 região**: uma cor marca vários detalhes; cada detalhe = 1 eletrodo.
  Segmentação por **proximidade espacial**.
- Cópia das faces de queima = **Inter-Part Copy associativo** (`CopySurfaces.Add`), não
  trazer a peça inteira (`AddCopiedPart` é outra coisa — **erro nº 1 de pattern-matching**).
- Eletrodo **ENCOLHE**: offset das faces **para DENTRO**, `f(GAP, Ra)`. GAP só por Ra.
- Entrega: **`.par` nativo** (NX lê direto). Sem STEP/Parasolid/`.x_t` no fluxo do eletrodo.
- Raios pequenos demais: **só sinalizar**, nunca consertar.

---

## 4. Fatos SE-COM validados (para fundamentar análises)

Confirmados no SE 2023 (`223.00.13.05`) por runs numerados. **Sinalize dependência de
versão** ao usá-los.

### Restrições que já custaram runs

- **Unidades = METROS e RADIANOS** na API de geometria/modelagem. 20 mm = `0.020`.
- **Coleções 1-based** (`.Item(1)`, `.Count`).
- **x64** + **thread STA** obrigatórios. Late binding (`dynamic`) para compilar sem typelib.
- **Parâmetros `[out]`** não populam em late binding sem `ParameterModifier` by-ref
  (ou `InvokeMember`).
- **Erro de conversão de VARIANT no binder dinâmico** → via `InvokeMember` (o IDispatch
  coage) + `Type.Missing` para objeto opcional.
- **`cParams=0` num "método"** = é **propriedade-coleção**; a operação é `.Add(...)`.
- **Arrays de COM objetos** (`Face[]`, `Profile[]`) devem ser **tipados** para marshalar como
  `SAFEARRAY(IDispatch)`. `object[]` vira `SAFEARRAY(VARIANT)` e é rejeitado.

### Assinaturas / comportamentos confirmados

- **Cor da face** = `Face.Style` → `Diffuse{Red,Green,Blue}` (0..1; ×255). Leitor:
  `FaceStyleColorReader`.
- **Query de faces** = tipo `1` (`igQueryAll`), fixado em `FaceSelector`.
- **Bounding box de face** = `Face.GetRange(MinPt, MaxPt)` com os 2 args `[out]
  SAFEARRAY(double)`; em late binding **passe `new double[0]` + `ParameterModifier(2)`
  by-ref + `CultureInfo.InvariantCulture`** (senão `DISP_E_TYPEMISMATCH`). Lê 124/124.
- **Modo de modelagem** = `PartDocument.ModelingMode`: `1 = seModelingModeSynchronous`,
  `2 = seModelingModeOrdered`. Métodos `*Sync` são para síncrono; `AddFinite`/`Add` são
  para ordenado.
- **Edição in-place** = `Occurrence.Activate` é **propriedade booleana** (`= true`/`false`),
  não método. ⚠️ **`Activate=true` NÃO entra em edição in-place**. Os sinais autoritativos
  são `AssemblyDocument.ModelingInAssembly` / `InPlaceActivated` (dump), ambos **apenas
  leitura**. A API COM **não expõe** um método/programático para entrar no modo de edição
  in-place da UI. Ver `docs/MEMORIA_SOLID_EDGE_COM.md` §3.
- **Inter-Part Copy** = `Constructions.CopySurfaces.Add(NumberOfFaces: int, FaceArray:
  SAFEARRAY(IDispatch)*, [opt]InternalBoundary, [opt]ExternalBoundary) -> CopySurface*`.
  O `FaceArray` precisa ser **`Face[]` tipado**; `object[]` é rejeitado. Mesmo com tipo
  correto, `CopySurfaces.Add` dá **E_FAIL** fora do modo in-place.
- **InterpartConstructions** = `Add(AsmSource: IDispatch)` / `Add2(PartTarget, AsmSource)`.
  Não aceita `Occurrence`, `PartDocument`, `Body` ou `Face[]` como `AsmSource` testados até
  aqui (`E_NOINTERFACE`/`DISP_E_TYPEMISMATCH`). A fonte provável é um `Reference` ou
  `TopologyReference`.
- **Criar peça EM CONTEXTO (in-place)** = `Occurrences.AddByTemplate(OccurrenceFileName,
  TemplateFileName)` — arg1 é o caminho do NOVO part, arg2 é o template. `AddByFilename`
  só insere arquivo já existente → peça standalone.
- **TopologyReference** = `Face.GetReferenceKey(out key)` + `Occurrence.CreateTopologyReference(key)`.
  Assinaturas claras no dump, **nunca testado no SE real**.
- **Posição da ocorrência** = `Occurrence.GetTransform(out x,y,z, out ax,ay,az)` (metros,
  radianos); `PutOrigin`/`PutTransform` para posicionar.
- **Furo simples (síncrono)** = `HoleDataCollection.Add(33, diameter)` + `Holes.AddSync(1,
  Profile[], side, 13, depth, holeData)` — **validado**.
- **Furo roscado (`igTappedHole=37`)** = cria `HoleData` OK, mas `Holes.AddSync` falha com
  `E_FAIL` no síncrono (Log 046). Causa a confirmar: talvez exija ordenado ou parâmetros
  adicionais no `HoleData`.
- **Sketch + extrusão** = caminho validado para blocos:
  `ProfileSets.Add() → Profiles.Add(plane) → Lines2d.AddBy2Points → Profile.End(1) →
  Models.AddFiniteExtrudedProtrusion(1, Profile[], side, depth) → ProfileSet.Delete()`.
- **Primitivos diretos (`AddBoxByTwoPoints`, `AddCylinderByCenterAndRadius`)** =
  `DISP_E_TYPEMISMATCH` recorrente no late binding. **Evitar**; preferir sketch+extrude.

### Erro → causa → o que investigar

| Erro no log | Causa provável | Próxima hipótese |
|---|---|---|
| `RuntimeBinder: não contém 'ToList'/'Occurrences'` | `dynamic` sem membro / objeto errado | conferir se o objeto é o esperado (peça vs. montagem) |
| `RPC_E_CALL_REJECTED` / `RPC_E_SERVERCALL_RETRYLATER` | diálogo modal / servidor ocupado | OleMessageFilter (já tratado) |
| `[out]` volta vazio | by-ref não marcado | `ParameterModifier` / semear array `[out]` |
| `DISP_E_TYPEMISMATCH` (0x80020005) | VARIANT errado (`object[]` onde quer `IDispatch[]`) ou placeholder `[out]` errado | array tipado (`Face[]`/`Profile[]`) / `ParameterModifier` |
| `DISP_E_PARAMNOTOPTIONAL` (0x8002000F) | param obrigatório omitido (`Type.Missing` recusado) | passar `null` no objeto obrigatório; conferir aridade no dump |
| `E_FAIL` (0x80004005) no `CopySurfaces.Add` | operação inválida fora do modo in-place | `CreateTopologyReference` / `InterpartConstructions` com `Reference` |
| `E_NOINTERFACE` (0x80004002) no `IPC.Add` | tipo de `AsmSource` errado | usar `CreateReference`/`CreateTopologyReference` |
| `E_UNEXPECTED` (0x8000FFFF) no `IPC.Add(reference)` | objeto `Reference` não é o consumidor esperado | validar in-place real / tentar outro consumidor |
| `0x80010114` após falha de feature | objeto COM corrompido / documento em estado inválido | não acessar coleções após falha; recriar documento ou abortar operação |
| `cParams=0` num método real | é **propriedade-coleção** | usar `.Add(...)` na coleção |

---

## 5. Mapa de API por domínio (guia rápido para revisar código do Claude)

### Criar geometria

| Objetivo | Coleção/Objeto | Método | Modo |
|---|---|---|---|
| Criar peça | `Documents` | `Add("SolidEdge.PartDocument")` | — |
| Criar montagem | `Documents` | `Add("SolidEdge.AssemblyDocument")` | — |
| Criar peça em contexto | `Occurrences` | `AddByTemplate(newPartPath, templatePath)` | — |
| Sketch | `ProfileSets` → `Profiles` | `Add()` → `Add(plane)` | ambos |
| Fechar perfil | `Profile` | `End(1)` | ambos |
| Linha 2D | `Lines2d` | `AddBy2Points(x1,y1,x2,y2)` | ambos |
| Círculo 2D | `Circles2d` | `AddByCenterRadius(...)` | ambos |
| Centro de furo | `Holes2d` | `Add(x,y)` | ambos |
| Extrusão (adicionar) | `ExtrudedProtrusions` | `AddFiniteMulti(n, Profile[], side, depth)` | síncrono |
| Extrusão (adicionar) | `ExtrudedProtrusions` | `AddFinite(Profile, side, side, depth)` | ordenado |
| Corte (remover) | `ExtrudedCutouts` | `AddFiniteMulti` / `AddThroughAllMulti` | síncrono |
| Corte (remover) | `ExtrudedCutouts` | `AddFinite` / `AddThroughAll` | ordenado |
| Furo | `Holes` | `AddSync(n, Profile[], side, 13, depth, HoleData)` | síncrono |
| Furo | `Holes` | `AddFinite(Profile, side, depth, HoleData)` | ordenado |
| Revolução | `RevolvedProtrusions`/`Cutouts` | `AddFiniteSync` / `AddSync` | síncrono |
| Revolução | `RevolvedProtrusions`/`Cutouts` | `AddFinite` / `Add` | ordenado |

### Modificar geometria

| Objetivo | Coleção/Objeto | Método | Modo |
|---|---|---|---|
| Offset de faces | `FaceOffsets` | `Add` / `AddEx` | síncrono |
| Copiar faces | `Constructions.CopySurfaces` | `Add(n, FaceArray, ...)` | requer in-place |
| Offsetar superfícies | `Constructions.OffsetSurfaces` | `Add(Side, distance, FaceSet, Boundary)` | a validar |
| Costurar superfícies | `Constructions.StitchSurfaces` | `Add(n, SurfaceArray, Heal, Tolerance)` | a validar |
| Cópia inter-peça | `Constructions.InterpartConstructions` | `Add(Reference)` / `Add2(part, Reference)` | a validar |
| Deletar furos | `DeleteHoles` | `AddByFace(face)` / `Add(type, dia)` | a validar |
| Mover faces (síncrono) | `Model` | `SyncLinearMove`, `SyncPlanarMove` | síncrono |
| Rotacionar faces (síncrono) | `Model` | `SyncRotate` | síncrono |

### Montar

| Objetivo | Objeto | Método/Propriedade |
|---|---|---|
| Inserir peça existente | `Occurrences` | `AddByFilename(path)` |
| Criar peça in-context | `Occurrences` | `AddByTemplate(newPartPath, templatePath)` |
| Posicionar | `Occurrence` | `PutOrigin(x,y,z)` |
| Ler posição | `Occurrence` | `GetTransform(out x,y,z,out ax,ay,az)` |
| Referência a entidade | `AssemblyDocument` | `CreateReference(Occurrence, Entity)` |
| Referência topologia | `Occurrence` | `CreateTopologyReference(ReferenceKey)` |
| Relacionamento | `AssemblyDocument` | `AddMateRelation`, `AddPlanarRelation`, etc. |

---

## 6. Checklist de revisão de código do Claude

Ao revisar código do Claude, verifique:

1. **Unidades**: todo valor geométrico está em metros/radianos na chamada COM? (`mm/1000.0`)
2. **1-based**: indices começam em 1, não 0 (`Item(1)`).
3. **Array tipado**: `Face[]`, `Profile[]` em vez de `object[]` quando o dump pede
   `SAFEARRAY(IDispatch)`.
4. **Síncrono vs ordenado**: o método escolhido corresponde ao `ModelingMode`? `AddSync`
   em síncrono, `AddFinite` em ordenado.
5. **`[out]` via `InvokeMember`**: `ParameterModifier` by-ref + `InvariantCulture`.
6. **Opções opcionais**: `Type.Missing` aceito? Se `DISP_E_PARAMNOTOPTIONAL`, tente `null`.
7. **Objeto correto**: a variável `dynamic` é mesmo a peça/montagem/ocorrência esperada?
8. **Defesa pós-falha**: após um `E_FAIL` de feature, o código tenta acessar a mesma
   coleção/objeto? Isso pode gerar `0x80010114`.
9. **In-place**: código assume que `Occurrence.Activate = true` entra em edição in-place?
   **Está errado.**
10. **Inter-Part Copy**: código passa `Face[]` para `CopySurfaces.Add` fora de in-place?
    Vai falhar. Alternativa: `CreateTopologyReference`.
11. **Primitivos**: uso de `AddBoxByTwoPoints`/`AddCylinderByCenterAndRadius`? Preferir
    sketch+extrude validado.
12. **Furos roscados**: uso de `igTappedHole` no síncrono? Testar em ordenado ou usar
    fallback para furo simples.
13. **Não inventar API**: toda assinatura usada deve estar no dump ou em `docs/api/`.

---

## 7. Como consultar o dump (exemplos)

```bash
# assinatura de um método
grep -n "AddByTemplate" src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_*.txt
grep -n "CopySurfaces" src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_*.txt

# valores de um enum
grep -n "igPartDocument" src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_*.txt
grep -n "seModelingModeSynchronous" src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_*.txt
```

> Nota: o dump tipado atual **grava parcial** (o walk de tipos ainda crasha no fim, na
> lib Assembly). Se a interface que você procura não estiver no dump, **sinalize** que
> falta um dump completo — não conclua que o método não existe.

---

## 8. Estado atual e onde ajudar

- **Ferramenta 1 (relatório de coordenadas)** — ✅ construído.
- **Criação de eletrodos com bloco standalone** — ✅ funciona (sketch+extrusão +
  `AddByFilename` + `PutOrigin`), mas furos de fixação estão falhando (`igTappedHole` no
  síncrono).
- **Inter-Part Copy via COM** — ⛔ BLOQUEADO. `CopySurfaces.Add` falha por falta de modo
  in-place programático. A alternativa não testada é `CreateTopologyReference`.
- **Offset / Stitch das faces de queima** — 📖 depende da cópia funcionar primeiro.

**Contribuição mais útil agora:**
1. Revisar código do Claude contra o checklist da §6.
2. Diagnosticar logs mais recentes para padrões de erro (`E_FAIL`, `DISP_E_TYPEMISMATCH`,
   `0x80010114`).
3. Propor experimentos para validar `CreateTopologyReference` como substituto do
   Inter-Part Copy.
