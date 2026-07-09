# Kimi — Briefing de Pesquisa e Análise do AutoEDM (Solid Edge COM)

Este arquivo define o **papel do Kimi** no projeto AutoEDM e reúne os **fatos
validados** que fundamentam suas análises. Leia o [`README.md`](../../../README.md) do
projeto para a direção atual e as verdades do domínio; este documento é o seu ponto de
partida como **pesquisador/analista**.

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

### Assinaturas / comportamentos confirmados

- **Cor da face** = `Face.Style` → `Diffuse{Red,Green,Blue}` (0..1; ×255). Leitor:
  `FaceStyleColorReader`.
- **Query de faces** = tipo `1` (`igQueryAll`), fixado em `FaceSelector`.
- **Bounding box de face** = `Face.GetRange(MinPt, MaxPt)` com os 2 args `[out]
  SAFEARRAY(double)`; em late binding **passe `new double[0]` + `ParameterModifier(2)`
  by-ref + `CultureInfo.InvariantCulture`** (senão `DISP_E_TYPEMISMATCH`). Lê 124/124.
- **Edição in-place** = `Occurrence.Activate` é **propriedade booleana** (`= true`/`false`),
  não método. ⚠️ **Correção (2026-07-08):** `Activate=true` provavelmente só **carrega/ativa
  a ocorrência**, NÃO garante entrar em edição in-place. Os sinais reais são
  `AssemblyDocument.ModelingInAssembly` / `InPlaceActivated` (dump), que o código nunca
  leu. `ActiveDocument` continuar sendo o `.asm` **não** prova falha. Ver
  [`docs/MEMORIA_SOLID_EDGE_COM.md`](../../../docs/MEMORIA_SOLID_EDGE_COM.md) §3.
- **Inter-Part Copy** = `Constructions.CopySurfaces.Add(NumberOfFaces: int, FaceArray:
  SAFEARRAY(IDispatch)*, [opt]InternalBoundary, [opt]ExternalBoundary) -> CopySurface*`.
  O `FaceArray` precisa ser **`Face[]` tipado** (marshala como `SAFEARRAY(IDispatch)`);
  `object[]` vira `SAFEARRAY(VARIANT)` e é rejeitado.
- **Criar peça EM CONTEXTO (in-place)** = `Occurrences.AddByTemplate(template)` (o
  `AddByFilename` só insere arquivo já existente → peça standalone → `CopySurfaces.Add`
  dava `E_FAIL`). Template padrão via `Application.GetDefaultTemplatePath(1)`
  (`1 = igPartDocument`).
- **Posição da ocorrência** = `Occurrence.GetTransform(out x,y,z, out ax,ay,az)` (metros,
  radianos); `PutOrigin`/`PutTransform` para posicionar.

### Erro → causa → o que investigar

| Erro no log | Causa provável | Próxima hipótese |
|---|---|---|
| `RuntimeBinder: não contém 'ToList'/'Occurrences'` | `dynamic` sem membro / objeto errado | conferir se o objeto é o esperado (peça vs. montagem) |
| `RPC_E_CALL_REJECTED` / `RETRYLATER` | diálogo modal / servidor ocupado | OleMessageFilter (já tratado) |
| `[out]` volta vazio | by-ref não marcado | `ParameterModifier` / semear array `[out]` |
| `DISP_E_TYPEMISMATCH` | VARIANT errado (ex.: `object[]` onde quer `IDispatch[]`) | array tipado / placeholder `[out]` correto |
| `E_FAIL` no `CopySurfaces.Add` | peça não está em contexto in-place | confirmar criação via `AddByTemplate` + `Activate` |
| `cParams=0` | é coleção/propriedade | usar `.Add(...)` na coleção |

---

## 5. Como consultar o dump (exemplos)

```bash
# assinatura de um método
grep -n "AddByTemplate" src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_*.txt
grep -n "CopySurfaces" src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_*.txt

# valores de um enum
grep -n "igPartDocument" src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_*.txt
```

> Nota: o dump tipado atual **grava parcial** (o walk de tipos ainda crasha no fim, na
> lib Assembly). Se a interface que você procura não estiver no dump, **sinalize** que
> falta um dump completo — não conclua que o método não existe.

---

## 6. Estado atual e onde ajudar

- **Ferramenta 1 (relatório de coordenadas)** — construída, aguardando teste do Carlos.
- **Fronteira**: criar eletrodo em contexto via `AddByTemplate` → `CopySurfaces.Add`.
  Se o próximo run ainda der `E_FAIL`, a hipótese seguinte é `InterpartConstructions.Add`
  / `CreateTopologyReference` (assinaturas a extrair do dump).

**Contribuição mais útil agora:** analisar o log do próximo run do copy-test (achar o
HRESULT e correlacionar com o passo), e comparar `CopySurfaces` vs.
`InterpartConstructions` vs. `CreateTopologyReference` para a cópia entre peças em
contexto — com base **no dump**, apontando as assinaturas reais.
