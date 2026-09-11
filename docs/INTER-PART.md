# Cópia entre peças (Inter-Part) — o que já falhou, o que nunca foi tentado

> Levantamento de 2026-09-11, feito antes da rodada 2 da sonda
> (`src/AutoEDM.Core/Experiments/InterPartProbe.cs`). Existe para que ninguém —
> inclusive nós — volte a gastar rodada em beco já explorado.

O problema: levar as faces de queima da peça-cavidade para cada peça-eletrodo,
dentro da montagem. É o único passo do fluxo do eletrodo que continua manual.

---

## 1. O que está ESTABELECIDO como fato

Não re-teste nada desta seção.

| Fato | Evidência |
|---|---|
| **Não existe verbo COM para entrar em edição in-place.** `ModelingInAssembly` e `InPlaceActivated` são **só getters** — não há setter em lugar nenhum da API | varredura exaustiva de `SolidEdgeAssembly.md`, `SolidEdgeFramework.md`, `SolidEdgePart.md` e do dump vivo |
| `Occurrence.Activate = true` **não** entra em in-place — só carrega a ocorrência | `logs/AutoEDM 029.log:81` imprimiu `ModelingInAssembly=False, InPlaceActivated=False` imediatamente antes do E_FAIL |
| As opções globais **já foram ligadas** e o E_FAIL persistiu | `logs/AutoEDM 023.log:31` leu `Allow(253)=False, CopyCmd(254)=False`; `024.log:31-32`, depois do fix do `[out]` by-ref, leu **`True,True`** — e `024.log:42` repetiu o E_FAIL |
| **`CopySurfaces.Add` funciona INTRA-peça — 67 vezes**, com o mesmo array tipado | `SurfaceBlockBuilder.cs:1599`; sucesso mais recente em `AutoEDM_20260911_074426.log:123` |
| Logo, o E_FAIL **não é** de tipo, de marshaling nem de nº de faces. É exclusivamente do **cruzamento de peças** | consequência dos dois anteriores |
| **Funciona nas mãos do desenhista**, com in-place aberto à mão | `AutoEDM_20260717_083451.log:17` — com o Carlos em contexto, `Constructions.Count=5` já na largada e `ActiveDocument` = a **PEÇA** (`Type=1`) |

### O teste discriminante que nunca foi usado como guarda

| Situação | `ActiveDocument` |
|---|---|
| Carlos em contexto, à mão (funciona) | **peça**, `Type=1` — `AutoEDM_20260717_083451.log:17` |
| Tentativa automática (E_FAIL) | **montagem**, `Type=3` — `logs/AutoEDM 029.log:80` |

---

## 2. Inventário das tentativas (logs 016–029, 13 execuções)

Toda a série aconteceu em 3 dias (06–08/jul/2026) e parou no log 029. A pasta
`logs/` está no `.gitignore` — use `grep` via shell, o Grep padrão não a enxerga.

| Chamada | Argumentos | Erro | Onde |
|---|---|---|---|
| `CopySurfaces.Add` (4 args) | `object[]` | `DISP_E_TYPEMISMATCH` | `016.log:37`, `017.log:38` |
| `CopySurfaces.Add` (5 args) | placeholder `[out]` | nº de parâmetros não coincide (a coleção tem `cParams=4`) | `016.log:39`, `017.log:40` |
| `CopySurfaces.Add(124, Face[124])` | **tipado**, peça standalone | **`E_FAIL`** ← 1º E_FAIL: o tipo passou a ser aceito | `018.log:37` |
| idem | peça do template in-place | `E_FAIL` | `020.log:71`, `021.log:110` |
| idem | peça de `AddByTemplate(2 args)` | `E_FAIL` | `022.log:40`, `023.log:41` |
| idem | com globais 253/254 = True | `E_FAIL` | `024.log:42`, `025.log:42`, `026.log:42` |
| `CopySurfaces.Add(1, Face[1])` | uma face só | `E_FAIL` | `027.log:81`, `028.log:81`, `029.log:82` |
| `CopySurfaces.Add(1, reference)` | 1 `Reference` | `DISP_E_TYPEMISMATCH` | `026.log:48` |
| `InterpartConstructions.Add(occ)` | binder dinâmico | `InvalidCastException` | `024.log:43` |
| `InterpartConstructions.Add(occ)` | `InvokeMember` | `E_NOINTERFACE` | `025.log:43` |
| `InterpartConstructions.Add(doc)` / `(body)` | — | `E_NOINTERFACE` | `025.log:45`, `:47` |
| `InterpartConstructions.Add(Face[])` | tipado | `DISP_E_TYPEMISMATCH` | `027.log:87`, `028.log:87`, `029.log:88` |
| `InterpartConstructions.Add(reference)` | — | **`E_UNEXPECTED`** (erro distinto de todos) | `026.log:44` |
| `InterpartConstructions.Add2(elet, occ/doc/ref)` | — | `E_FAIL` | `024.log:45`, `025.log:49`, `:51`, `026.log:46` |
| `InterpartConstructions.Add2(elet, Face[])` | tipado | `DISP_E_TYPEMISMATCH` | `027.log:85`, `028.log:85`, `029.log:86` |
| **`AssemblyDocument.CreateReference(occ, face)`** | — | **✅ OK — 124 referências criadas** | `026.log:43`, `:50` |

**A escalada mostra onde parou de progredir:** `DISP_E_TYPEMISMATCH` (com
`object[]`) → **`E_FAIL`** (com `Face[]` tipado, log 018) → e daí em diante onze
rodadas repetindo o mesmo `E_FAIL` com roupas diferentes.

### Preparação de contexto, de passagem

- `Occurrences.AddByTemplate(template)` com **1 arg** insere o próprio arquivo de
  template como ocorrência (`020.log:66`). Com **2 args** (`novoPath, template`)
  funciona (`022.log:35`).
- `target.OccurrenceDocument.Occurrences` → `RuntimeBinderException`: isso pega a
  **peça**, não a montagem (`015.log:32`).

---

## 3. O que NUNCA foi executado

Esta é a parte que importa.

| Eixo | Situação |
|---|---|
| **`Face.GetReferenceKey` → `Occurrence.CreateTopologyReference`** | `MEMORIA_SOLID_EDGE_COM.md:236-238` chama de **"a alternativa mais promissora ao Inter-Part Copy"**. `grep` no `src/`: **zero ocorrências**. Escrito, priorizado, abandonado sem uma execução |
| **`CreateReference2(Object: IDispatch, Entity)`** | Irmão do único método que funcionou. A diferença (`Occurrence*` tipado vs `IDispatch` genérico) é exatamente o que costuma resolver `E_NOINTERFACE`. Nunca chamado |
| **Reaproveitar as 124 `Reference` que `CreateReference` criou** | Foram alimentadas só em `IPC.Add`/`Add2`/`CopySurfaces`. Nunca em `CopyConstructions`, `AddCopiedPart` ou `CreateTopologyReference` |
| **Faces de `Occurrence.Body`** | As 124 faces sempre saíram de `PartDocument.Body` — espaço da **peça** (`FaceSelector.cs:38,334,380`). `Occurrence.Body` (espaço da **montagem**) existe no dump e nunca foi usado. Para uma API "inter-part dentro da montagem", é a variante mais barata que sobrou |
| **`Constructions.CopyConstructions.Add(FileName, …, CopyColors)`** | A coleção existe (`AutoEDM 008.log:40`) e **nunca foi chamada**. É a única rota que lê de **arquivo** — dispensa in-place por construção — e a única com `CopyColors` |
| **`Models.AddCopiedPart` / `AddCopiedPartEx`** | Assinaturas capturadas ao vivo em `AutoEDM 008.log:31,37`. Nunca chamadas. Recomendadas em `recomendacoes_arquitetura.md:43-49` |
| **`Models.AddBodyByTag(tag)`** | Se a tag do Parasolid valer na **sessão** e não só no documento, atravessa documentos sem arquivo nenhum. Teste de 5 linhas |
| **`PartDocument.InPlaceActivated`** | O projeto só leu a flag da **montagem**. A peça tem a sua própria, nos dumps desde `AutoEDM 005.log:35` |
| **Família `*IPADoc*` / `InterpartLinks`** | `GetContainerDocumentAndOccurrenceOfIPADoc`, `GetContainerDocumentAndMatrixOfIPADoc`, `HasInterpartLinks`, `BreakAllInterpartLinks`, `FreezeAllInterpartLinks` — nos dumps desde o log 005, nunca citados em documento nenhum. `BreakAllInterpartLinks` é literalmente o "quebrar" que o Carlos faz à mão |
| **Comando nativo via `StartCommand`** | Citado na própria mensagem de exceção do `InterPartCopier.cs:79` como "alternativa pendente". `StartCommand` aparece **uma vez** em todo o `src/` — como texto dessa mensagem |
| **`EditAssembly` / `EditAssemblyWithOptions`** | Eliminados **pelo nome**, sem um run (`MEMORIA:183-186`) |

---

## 4. `CopyConstructions.Add` — a assinatura completa

`docs/api/SolidEdgePart.md:22320` (interface `_ICopyConstructionsAuto`).
Confirmada **ao vivo** em `AutoEDM_20260708_122737.log:158` (`cParams=0` na
coleção — é propriedade, chama-se `.Add` no retorno).

| # | Param | Tipo | Nota |
|---|---|---|---|
| 1 | `FileName` | `BSTR` | **obrigatório** — é isto que dispensa o in-place |
| 2-4 | `X/Y/ZScale` | `VARIANT` | 1.0 |
| 5 | `MirrorPlane` | `RefPlane*` | |
| 6 | `FamilyOfPartsMember` | `BSTR` | |
| 7 | `CoordinateSystem` | `CoordinateSystem*` | posiciona a cópia — ver abaixo |
| 8 | `IncludeDesignBody` | `bool` | `true` traz o corpo da cavidade |
| 9-11 | `IncludeConstruction{Solid,Surface,Curve}Bodies` | `bool` | |
| 12-13 | `NumberOfIncludeBodies` / `IncludeBodies` | `int` / `SAFEARRAY(IDispatch)` | corpos a dedo |
| 14 | **`CopyColors`** | `bool` | **sem isto o plano morre** |
| 15 | `CopyConstruction` | `[out]` | volta como valor de retorno no `IDispatch::Invoke` |

Não existe `Add2`. Existe `AddEx`, igual + `ConfigName: BSTR` e
`DeleteVoids: bool` antes do `[out]`.

Os tipos dos `VARIANT` opcionais não são adivinhação: cada um tem uma
**propriedade homônima tipada** em `_ICopyConstructionAuto`
(`SolidEdgePart.md:22326-22410`) — `put Plane(RefPlane*)`,
`put CoordinateSystem(CoordinateSystem*)`, `put CopyColors(bool)`.

### Posicionar a cópia

`PartDocument.CoordinateSystems.AddByMatrix(Matrix: SAFEARRAY(double))`
(`SolidEdgePart.md:11421`) aceita os **mesmos 16 doubles em metros** do
`Occurrence.PutMatrix` que já está validado no projeto. Entrega a matriz
relativa cavidade→eletrodo direto, sem passar por ângulos de Euler — que é a
armadilha já documentada na skill (a typelib nunca diz a ordem de composição, e
errar transpõe a rotação em silêncio).

### Depois de extrair as superfícies: ESCONDER, nunca apagar

O `CopySurface` é filho do `CopyConstruction` do mesmo jeito que o esboço do
canal era filho do corte revolvido — é o bug do commit `a460627`.

| Ação | Efeito |
|---|---|
| `Visible = false` | ✅ esconde só o pai |
| `BreakLinks()` | ✅ corta o vínculo vivo com o `.par` da cavidade |
| `Suppress = true` | ❌ filhos ficam sem geometria |
| `Delete()` | ❌ arranca o pai — o bug do o'ring |

E a limpeza é **cosmética**: engula a exceção, logue, e não deixe decidir o
veredito da operação.

---

## 5. Dirigir o comando nativo

`Application.StartCommand(CommandID)` existe e é live-confirmado
(`AutoEDM_Logs_Consolidated_Analysis.md:876`).

O enum `SolidEdgeCommandConstants` tem **só 9 membros** e nenhum de cópia de
superfície (`constants.md:3596-3612`). O único plausível é
`sePartInsertPartCommand = 40254`.

**Mas o ID não precisa ser adivinhado:**

```
Environment.CommandCategories → CommandCategory.Item(i) → CommandInfo{Caption, Id}
```

Um dump enumera todo comando por nome e mata a incógnita. E
`Application.CommandEnabled(id, ambiente, [out] licenciado, [out] idDesconhecido)`
testa um ID **sem executá-lo** — o que também responde "o comando de inter-part
está habilitado sem in-place?".

**Custo honesto:** `StartCommand` dispara um comando **interativo** e retorna na
hora, sem como passar argumentos. Se o comando consumir a pré-seleção do
`SelectSet`, funciona; se pedir seleção própria, trava esperando o humano.
`AbortCommand(true)` é saída de emergência obrigatória em todo caminho de erro.

---

## 6. O custo do contorno atual

Hoje o AutoEDM cria a peça com o bloco e o desenhista subtrai a cavidade à mão
(`ElectrodeBuilder.cs:380-390`). Fica manual: salvar a montagem, entrar em
contexto, copiar as faces, quebrar o vínculo, fechar vãos com "Limite", estender
arestas até a base.

E tem um custo escondido, registrado em
`AutoEDM_Logs_Consolidated_Analysis.md:2164`: as superfícies copiadas à mão
**não carregam a cor original de queima** — "Ra por cor nesse ambiente sempre
dava não mapeada". Foi por isso que o GAP/Ra saiu do "Criar Base" e virou passo
separado com lista suspensa.

O `CopyColors=true` do `CopyConstructions.Add` se propõe a resolver exatamente
isso, e nunca foi testado.

---

## 7. Ordem de execução recomendada

A sonda (`InterPartProbe`, botão "Sonda inter-part") cobre 1 a 5 numa rodada.

1. **Estado** — `ActiveDocument.Type` + `InPlaceActivated` na peça **e** na montagem.
2. **`CopyConstructions` por arquivo**, em ordenado e em síncrono, com histograma
   de cores depois (é ele que prova o `CopyColors`).
3. **Faces de `Occurrence.Body`** contra o controle de `PartDocument.Body`. Erro
   **diferente** é a pista que interessa.
4. **`GetReferenceKey` → `CreateTopologyReference`.**
5. **`CreateReference2`** e **`AddBodyByTag`.**
6. **Dump dos comandos nativos** — vale mesmo que tudo acima falhe.
7. **O teste que só o usuário faz:** entrar em edição em contexto à mão, deixar
   aberta, rodar a sonda de novo. Comparar os dois logs responde o que 13
   execuções não responderam — se a cópia depende só desse estado.
