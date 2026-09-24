# PROJECT_STATE — AutoEDM

> Fonte de verdade do estado deste projeto. Atualize ao fechar cada sessão,
> e reflita a mesma coisa na linha do `HUB.md`.

- **Pasta:** `/AutoEDM`
- **Tipo de tarefa:** paralelizável (cada comando da ribbon é uma ferramenta independente)
- **Repositório:** `CarlosKeese/AutoEDM` — **público** desde 2026-09-09
- **Licença:** MIT
- **Status:** em andamento — primeira versão compilada publicada

---

## O que é

Biblioteca C# + add-in COM que automatiza a extração de eletrodos de cavidades
de molde no Solid Edge, substituindo o add-on pago *Electrode Design*. Serve
também como referência prática de integração COM com o SE.

## Como está montado

| Projeto | Alvo | Papel |
|---|---|---|
| `src/AutoEDM.Core` | `net472` + `net8.0-windows` | Núcleo reaproveitável (late binding) |
| `src/AutoEDM.AddIn` | `net472` | Add-in COM, ribbon "AutoEDM" |
| `src/AutoEDM` | `net472` | GUI WinForms de debug (conecta via ROT) |
| `src/AutoEDM.Register` | `net472` | **Registra o add-in em HKCU, sem admin** |
| `src/AutoEDM.Mcp` | `net8.0-windows` | **Servidor MCP** (stdio) — o Claude Code dirige a SE |
| `tests/AutoEDM.Core.Tests` | `net8.0-windows` | Testes da lógica pura |

O duplo alvo do `Core` existe porque o add-in precisa rodar em `net472` dentro
do Solid Edge, mas os testes precisam rodar fora do CAD. Desde 2026-09-17 esse
duplo alvo ganhou um segundo uso: o **servidor MCP** também consome o `Core`, pelo
alvo `net8.0-windows` — então o contrato de fio da ponte e o catálogo de
ferramentas são escritos UMA vez e os dois lados usam o mesmo tipo.

## Comandos para trabalhar aqui

```powershell
dotnet build AutoEDM.sln -c Release -p:Platform=x64
dotnet test  tests/AutoEDM.Core.Tests/AutoEDM.Core.Tests.csproj -c Release
pwsh tools\pack.ps1 -Version 2026.9.9                                 # gera dist\AutoEDM-2026.9.9.zip
src\AutoEDM.Register\bin\x64\Release\net472\AutoEDM.Register.exe      # registra
src\AutoEDM.Register\bin\x64\Release\net472\AutoEDM.Register.exe /u   # remove
```

Feche o Solid Edge antes de rodar o registrador de novo — com o SE aberto os
DLLs ficam travados.

A ponte MCP está registrada em `.mcp.json` na raiz do repo, apontando para
`src/AutoEDM.Mcp/bin/x64/Release/net8.0-windows/AutoEDM.Mcp.exe` — então `dotnet build`
em Release já deixa o servidor pronto. Ele NÃO é empacotado pelo `pack.ps1`: a ponte é
ferramenta de desenvolvimento, e o instalador do operador não precisa do binário. (A
condição original era "enquanto a ponte não tiver o 1º run validado no SE" — o 1º run
aconteceu em 2026-09-18, e a decisão de não empacotar ficou de pé por outro motivo.)

## Decisão travada: o add-in nunca troca o ambiente de modelagem

Cada botão **declara** se exige síncrono ou ordenado, na tabela `CommandSpec`
de `ElectrodeRibbon`. No ambiente errado o botão fica cinza e explica o que
trocar. Quem troca é o usuário. Isso veio de dois estragos concretos: esboços
órfãos presos no nó "Ordenado" do PathFinder (`ProfileSets.Add()` cria esboço
ordenado mesmo em peça síncrona) e o "Aplicar GAP" falhando em peça síncrona,
porque a troca de ambiente reconstrói o corpo e mata as faces já lidas.

**Exceção explícita (2026-09-21):** a ferramenta MCP `se_trocar_ambiente` troca a peça
a pedido do agente, com a escrita liberada. É um passo à parte, nunca dentro de outra
operação, e limpa o `SelectSet` antes, porque a troca mata os proxies. A decisão continua:
nenhum botão e nenhuma outra ferramenta trocam o ambiente.

| Comando | Exige |
|---|---|
| Criar Base, Unir superfícies | síncrono |
| Aplicar GAP, Alojamento de O'ring | ordenado |
| Montagem e diagnóstico | qualquer |

## Estado por ferramenta

| Ferramenta | Estado |
|---|---|
| Relatório de coordenadas de queima | ✅ construído |
| Spec-sheet de eletrodos | ✅ construído |
| Criar eletrodos c/ blank | ✅ validado no SE |
| Criar Base | ✅ validado no SE |
| Criar eletrodo (manual) | ✅ validado no SE; janela de seleção por clique **validada (2026-09-24)** — face à vista pelo raio do cursor, centro certo com faces de vários postiços, 11–20 ms por clique |
| Unir superfícies | ✅ validado no SE |
| Guarda de ambiente por comando | ✅ construído |
| Configuração externa (`config.json`) | ✅ construído, coberto por teste |
| Análise de usinabilidade (nível 1: raio + canto vivo) | ✅ construído, coberto por teste; raio validado no SE |
| Curvas das superfícies (WEDM) | ✅ validado no SE (2026-09-16) |
| Exportar perfis WEDM (IGES por Z) | ✅ validado no SE e **no Pitágoras** (2026-09-16) — a cadeia inteira, da peça ao programa da máquina |
| Lista de corte na serra | 🚧 construído, coberto por teste, **aguardando validação no SE** |
| Lista de modificações (folha de revisões) | 🚧 rodando no SE; escritor .xlsx próprio, miniaturas Z+/Z− com features numeradas |
| Ponte MCP (Claude Code → Solid Edge) | ✅ **validada no SE (2026-09-18)** — 1º run com o CAD aberto: `se_status`, `se_planos`, `se_arvore`, `se_log` e `se_modelar` sobre COM real |
| Sonda de malha (Eng. Reversa) | ✅ **rodada na malha real (2026-09-18)** — 4.168 facetas lidas; `Body.Faces` inacessível; seccionamento reprovado por marshaling |
| Reconhecimento de superfície sobre malha (`se_reconhecer_malha`) | 🚧 escrito e coberto por 13 testes, **aguardando o 1º run no SE** (exige trocar add-in + servidor MCP) |
| Modelagem por primitivas (`se_modelar`) | ✅ **validada no SE (2026-09-18)** — exemplo `carrinho`: 6 primitivas, 0 falhas, **corpo único** |
| Botões da ribbon via MCP (15 ferramentas) + `se_trocar_ambiente` | 🚧 escritos (2026-09-21), compilam, catálogo coberto por teste; **aguardando o 1º run no SE** (exige trocar add-in + servidor MCP) |
| Testes de unidade | ✅ 382 passando, 0 falhas |
| Alojamento de O'ring (ISO 3601) | ✅ **validado no SE (2026-09-21)** — canal de face e de eixo como coroa concêntrica extrudada: acompanha o furo movido **e** a mudança de Ø; nome do anel com instância, laranja de vedação, lado da pressão; eixo/furo pela normal da face; catálogo métrico DL Seals opcional |
| Aplicar GAP | 🚧 corrigido, **aguardando confirmação final no SE** |
| Duplicar eletrodo p/ próximo Ra | 🚧 construído, **aguardando validação no SE** |
| Copiar superfícies (Inter-Part Copy) | 🚧 só em edição em contexto (in-place) |
| Rosca física no furo M6 | 🚧 sonda de diagnóstico pronta; receita definitiva em aberto |
| Orquestrador "gerar todos os eletrodos" | 📋 planejado |
| Nova peça (molde) | 🚧 construída (2026-09-24), coberta por teste, **aguardando validação no SE** |
| Grupo **Molde** (Refrigeração, Canais, Pontos de injeção, Extratores, Gavetas) | 📋 planejado — [`docs/PLANO_MOLDE.md`](docs/PLANO_MOLDE.md); O'ring já movido para o grupo |

## Regra de ouro

Nenhuma assinatura COM é inventada. Toda assinatura vem do dump da typelib
(`SE_API_dump_*.txt`) ou de introspecção ao vivo. O botão **Inspecionar
seleção** da ribbon é a ferramenta que alimenta esse dump.

## Distribuição

- **`tools/pack.ps1`** monta o ZIP portátil: compila em Release, tira os `.pdb`,
  junta os arquivos do operador de `tools/dist/` (`instalar.cmd`,
  `desinstalar.cmd`, `LEIAME.txt`) e zipa em `dist/`.
- **GitHub Releases** — o link `releases/latest` sempre resolve para a versão
  mais nova.
- **kenatec.com/downloads** — card do AutoEDM apontando para `releases/latest`.
  A entrada vive em `kenatec-web/src/lib/downloads/catalogo.ts`; ao publicar uma
  versão nova, atualize o campo `versao` lá.

## Ponte MCP e Eng. Reversa (2026-09-17)

Dois grupos novos na ribbon (**Eng. Reversa** e **MCP**), `GuiVersion` = 15.

### MCP — como está montado

Três peças, e a divisão é imposta pelos alvos: o add-in é obrigatoriamente `net472`
(a SE hospeda .NET Framework in-process) e o mundo MCP é net8+.

| Peça | Onde | Papel |
|---|---|---|
| `Mcp/BridgeProtocol` + `ToolCatalog` | `Core` (2 alvos) | Contrato de fio e catálogo — escritos uma vez, usados pelos dois lados |
| `Mcp/BridgeServer` | `Core`, roda no add-in | Named pipe `AutoEDM.Bridge.v1`, um cliente por vez, reconecta |
| `Mcp/SeToolRunner` | `Core`, roda na thread da SE | Executa as ferramentas do catálogo, aplica a guarda de documento/ambiente |
| `Modeling/PrimitiveModel` | `Core` | Caixas e cilindros declarativos sobre o `BlankModeler` ja validado |
| `AddIn/McpBridgeHost` | add-in | Trampolim para a thread STA da SE + a chave de escrita |
| `src/AutoEDM.Mcp` | processo próprio | JSON-RPC 2.0 em stdio ↔ pipe |

**Decisões que valem registrar, porque são as que não são óbvias:**

1. **As ferramentas MCP pousam AO LADO da ribbon, sobre o mesmo `Core` — nunca em cima
   dos handlers dos botões.** Todo handler termina em `MessageBox`, e diálogo modal
   disparado por agente trava a thread da SE esperando um humano que não sabe que foi
   perguntado. Além disso o `Core` é o que já está validado no SE: descer direto nele
   não cria um segundo caminho de automação para manter em pé.
2. **Marshaling obrigatório.** O laço do pipe roda em thread de fundo; COM tocado de lá
   atravessa apartamento e rende `RPC_E_*` intermitente. O `McpBridgeHost` cria um
   `Control` **na thread da SE** (forçando o `.Handle`) e usa `BeginInvoke`. Funciona
   pela mesma razão que o `System.Windows.Forms.Timer` do relógio de estado funciona
   in-process: a SE bomba mensagens. A espera tem teto de 120 s, para o agente receber
   "a SE está ocupada" em vez de pendurar o Claude Code — o caso real é uma caixa de
   diálogo aberta no CAD.
3. **Duas travas.** A ponte não sobe sozinha, e nasce em somente-leitura a cada sessão;
   a liberação de escrita pede confirmação e morre junto com a ponte. Nenhuma
   ferramenta MCP alcança essa chave — só o botão.
4. **Protocolo à mão, sem SDK.** ~4 métodos (`initialize`, `tools/list`, `tools/call`,
   `ping`) de uma especificação publicada e estável valem mais que um pacote cuja API
   eu teria de adivinhar — o que é exatamente o que a Regra de Ouro proíbe. Zero
   dependência nova.
5. **`stdout` É o transporte, e o `Log` do Core escreve com `Console.WriteLine`.** A
   PRIMEIRA linha do `Main` desvia `Console.Out` para `stderr` e guarda o stdout real só
   para o protocolo. Sem isso, a primeira mensagem de log do Core entraria no meio do
   JSON-RPC e derrubaria a sessão com um erro de parsing que não aponta para nada.
   Conferido: a saída é **ASCII puro** (o `System.Text.Json` escapa acento como
   `é`), então a codepage do console não interfere — mas a codificação ficou
   amarrada em UTF-8 explícito, porque isso é propriedade do encoder padrão e não do
   protocolo.

**Validado nesta sessão, fora do CAD:** o servidor responde `initialize` (ecoando a
versão do cliente), lista as 9 ferramentas com `inputSchema` como objeto JSON, recusa
ferramenta desconhecida, recusa `resources/list` com `-32601`, não responde a
notificação, e sai com código 0 e `stderr` vazio. Os testes sobem servidor + cliente
num **pipe real** e cobrem ida-e-volta, duas chamadas na mesma conexão, reconexão,
segundo hospedeiro recusado e versão de contrato incompatível.

**O que falta:** nada da ponte MCP — ver "1º run com a SE aberta (2026-09-18)" abaixo.
Falta ainda rodar a **sonda de malha** sobre uma malha real, que é o que decide a rota da
Eng. Reversa.

### 1º run com a SE aberta (2026-09-18)

A ponte dirigiu a Solid Edge 226.00.08.04 pela primeira vez. Documento ativo `Peça1`
(.par, síncrono, vazio), escrita liberada pelo botão da ribbon.

- `se_status`, `se_arvore`, `se_planos`, `se_log` responderam sobre COM real.
- `se_modelar` com `exemplo:'carrinho'` (planoXY=1, planoXZ=2): **6 primitivas, 0
  falhas** — chassi 90×40×18, cabine 40×34×16 e 4 rodas Ø26×8. A árvore ficou com
  6 `ExtrudedProtrusion` e **um único corpo**: a fusão por contato entre primitivas que se
  tocam é real, não só esperada.
- **Achado:** `RefPlane.Normal` não é legível (`DISP_E_UNKNOWNNAME` nos três planos base),
  então `se_planos` devolve índice e nome mas não o eixo. Quem escolhe plano tem de
  **medir ou usar o mapeamento já validado** (XY=1, XZ=2 numa peça nova padrão), nunca
  supor pela normal — ela simplesmente não vem.

### Sonda de malha na malha real, e o reconhecedor que saiu dela (2026-09-18)

Malha `unityabsoluteriser` (corpo de facetas) aberta no SE, sonda rodada pelo botão da ribbon
com o teste de seccionamento autorizado. O que a medida deu:

- **`Body.GetFacetData` a 0,01 mm: 4.168 facetas, 12.504 pontos** (37.512 doubles — 3 vértices
  por triângulo, sopa sem índice). A malha lê inteira.
- **`Body.Faces` é INACESSÍVEL** num corpo de facetas. Não há face `igMesh` para enumerar, logo
  **não existe região pré-segmentada**: a segmentação tem de sair dos triângulos.
- `AddBodyByMeshFacets`, `DoRemesh`, `HealAndOptimizeWithMeshOptions` e `DeleteRegions` existem
  nesta versão — o caminho de volta (geometria reconhecida → sólido) está aberto.
- **`CreateSectionSketches` falhou com `DISP_E_TYPEMISMATCH`, e isso NÃO é veredito sobre malha:**
  é erro de marshaling de argumento. A typelib pede
  `psaObjects: SAFEARRAY(IDispatch)*` e a sonda passou um `object[]`, que o CLR marshala como
  `SAFEARRAY(VARIANT)`. Enquanto isso não for corrigido, **a rota prismática não pode ser dada
  como fechada nem como aberta** — a chamada nem entrou no comando.

Daí saíram três classes novas, todas em `src/AutoEDM.Core/Reverse`:

| Classe | O que é |
|---|---|
| `MeshReader` | lê os triângulos por `GetFacetData`, em **duas rotas**: a completa (pede também `Normals` e `FaceIDs`, que a sonda nem tinha pedido) e a mínima já provada, se a completa falhar. Converte m → mm uma vez só |
| `SurfaceRecognizer` | geometria **pura**, zero COM: solda vértices, quebra por QUINA, e pergunta de cada componente se é plano ou cilindro. Componente misto (o caso do raio, tangente à face e sem quina) é descascado |
| `SurfaceReport` | o texto para o agente, com o RMS de cada ajuste |

**A ordem quebrar-por-quina ANTES de ajustar custou um teste vermelho e vale como regra:** tentar
planos primeiro, soltos na malha inteira, faz **cada faixa de um cilindro tesselado virar um
"plano" perfeito de dois triângulos** — um Ø20 sai como 64 plaquinhas. A quina é que delimita
superfície; dentro dela é que se pergunta qual superfície é.

O mesmo defeito tem uma versão que nenhum ajuste evita: uma superfície curva **cabe** dentro da
tolerância de planaridade em pedacinhos. Por isso o relatório detecta **MOSAICO** (muitas
plaquinhas pequenas respondendo pela maior parte da área "plana") e **suspende o veredito** em vez
de o imprimir ao lado do aviso — dizer "atenção, pode ser curvo" e em seguida "a peça é
prismática" seria o relatório se contradizendo no mesmo parágrafo.

13 testes novos, todos contra malha **gerada com geometria conhecida** (caixa 20×30×40, cilindro
Ø20×30, meio cilindro, esfera R25): área, normais dos seis sentidos, Ø/eixo/altura, volta de 180°
contra 360° medida pelo maior vão (e não por `max − min`, que erraria atravessando o corte do
`atan2`), triângulo degenerado, malha vazia e estabilidade do sinal do eixo entre rodadas.

**O que falta:** trocar o add-in E o servidor MCP (são dois processos, com cópias diferentes do
`Core`) e rodar `se_reconhecer_malha` sobre a malha real — até aqui o reconhecedor só viu malha
sintética. E corrigir o `psaObjects` do seccionamento, que é a alavanca que pouparia escrever
reconhecimento de primitiva 2D.

### Eng. Reversa — por que começou por uma sonda

O dump da typelib (`docs/api`) foi conferido antes de qualquer código, e o resultado é
assimétrico:

- **EXISTE:** `MeshSurface.GetTriangleData/GetTrianglePoints/GetTriangleNormals`,
  `Body.GetFacetData`, `Model.IsFacetBody`/`IsMixedFacetBody`,
  `HealAndOptimizeWithMeshOptions(..., bFillHoles, FillHoleType)`,
  `Models.AddBodyByMeshFacets`, `DoRemesh`, `ConvertToMeshes`,
  `Sketches.CreateSectionSketches(..., bRecognizeLines/Arcs/Circles/Ellipses)`,
  `BSplineSurfaces.Add(poles, weights, knots...)`, `StitchSurfaces`, booleanas,
  `RecognizeAndCreateHoleGroups`, `RecognizeAndCreateChamfers` (estes dois só em B-rep).
- **NÃO EXISTE:** nenhum ajuste de plano/cilindro/cone/esfera sobre região de malha,
  nenhuma segmentação de malha, nenhuma seleção de região. Os comandos da aba NATIVA de
  Engenharia Reversa **não estão no modelo de objetos**, e `Application.StartCommand` só
  ABRE o comando interativo (espera o mouse) — não serve para script.

Logo o ajuste terá de ser nosso, e a pergunta real é qual rota:

- **prismática** — seccionar por Z com reconhecimento de retas/arcos/círculos, agrupar
  contornos iguais entre Z, círculo que persiste vira **furo com eixo e Ø reais**, e
  reconstruir como **features editáveis**. Serve postiço/eletrodo/placa (o trabalho real)
  e reaproveita a cadeia WEDM, que já faz `Z → contorno → geometria exata`. Não serve
  forma orgânica.
- **kernel geral free-form** — segmentação por curvatura, fit de primitivas e B-spline,
  interseção/trim/costura estanque. Cobre cavidade orgânica, mas é semanas de trabalho e
  o fechamento estanque no caso geral não é garantível.

A `MeshProbe` existe para essa escolha sair de medida, não de aposta. Só leitura, exceto
o teste de `CreateSectionSketches`, que **pede autorização** (cria esboços, tenta
apagá-los, nunca salva). Confere presença de membro por introspecção **antes** de chamar
e registra o erro exato do que falha — que é o dado que ela existe para trazer.

## Histórico

- **2026-09-24** — **Nova peça (molde), Fase 1 do plano.** Botão id 26 no grupo Molde. A
  janela de seleção do eletrodo virou genérica (`FacePickForm` + `FacePickSpec`) e serve os
  dois botões. Peça vazia `{molde}.{NNN}.par` no próximo número da série (fixa .100, móvel
  .200, extração .300), orientação da montagem, origem no centro das faces e no ponto mais
  baixo/alto do eixo de altura escolhido por projeto. MCP `se_nova_peca`. 406 testes, 0 falhas.
- **2026-09-24** — **Grupo "Molde" na ribbon e eletrodo manual por clique.** O
  Alojamento de O'ring saiu do grupo "Peça" para o novo grupo "Molde". O "Criar eletrodo
  (manual)" deixou de exigir faces pré-selecionadas: abre uma janela modeless que assume o
  mouse da SE com filtro de face (`ManualElectrodeForm` + `SePicker`), soma/retira faces
  clique a clique e cria um eletrodo atrás do outro. Core: `CreateElectrodeFromFaces` é o
  miolo, `TryUnwrapFace` desembrulha o item. Os seis botões de molde pedidos (Nova peça,
  Refrigeração, Canais, Pontos de injeção, Extratores, Gavetas) foram **planejados, não
  implementados**, em [`docs/PLANO_MOLDE.md`](docs/PLANO_MOLDE.md), com a sequência de
  desenvolvimento. No mesmo dia, a janela foi acertada ao vivo em cinco rodadas: filtro
  com todos os tipos de face; a localização da SE dentro do comando não respeita
  profundidade (QuickPick/Simples pegavam a peça de trás, Simples nem entrega o clique),
  então a face sai de um **raio de visão nosso** (`VisibleFacePicker`: câmera da janela +
  pixel do cursor via `DrawHwnd`/`TransformDCToModel` — o (x, y, z) do `MouseClick` NÃO
  está sob o cursor — e primeiro impacto na malha das faces); SmartLocate com clique sem
  geometria aceito para não acender a peça errada sob o mouse; caixa de faces de postiços
  diferentes levada ao espaço da ocorrência de referência (`OccurrenceTransform.MapBoxMm`;
  o eletrodo tinha saído 12 mm deslocado); leitura da cena guardada entre cliques
  (~0,35 s → 11–20 ms). 393 testes, 0 falhas.
- **2026-09-21** — **Alojamento de O'ring associativo, validado no SE.** Pedidos do
  Carlos: janela no lado direito da tela; feature com o nome do anel
  (`O'ring 2-214 - d2 3,53 x d1 24,99 - 1`, número por anel); canal pintado com o
  estilo `Orange` da peça; lado da pressão no canal de face (interna apoia o Ø
  externo, externa/vácuo o Ø interno, 1 % de aperto). Depois, o problema de fundo:
  **ser ordenado não é ser associativo** — o esboço num plano base com linhas em
  coordenada absoluta não tinha referência à peça, e o canal se perdia na 1ª edição
  no síncrono. Sete rodadas ao vivo: plano `AddNormalToCurve` na aresta (segue o furo
  movido, falha no Ø); amarras por API (colinear + `AddSet`, e depois cotas de
  distância que nasceram erradas) — descartadas, e nem à mão o revolvido se mantinha.
  O que ficou: **coroa circular extrudada** num plano preso a uma face plana, círculos
  `AddConcentric` à aresta incluída com cota de Ø, perfil validado com
  `igProfileAllowNested` (sem ele: −113 e feature falhada), extrusão **simétrica**
  (distância = total, medida) e o esboço escondido por `ShowDimensions = false`. O
  revolvido ficou só como reserva. 376 testes, 0 falhas. Tudo na skill
  (`modeling-recipes.md`, `errors.md`).
  No mesmo dia, um caso aberto do Carlos: numa peça ESCALONADA, um eixo Ø22 virou
  "furo" (a regra comparava o cilindro com o alcance do corpo, e a aba de baixo
  enganava) — o canal saiu para o lado errado e o anel ficou solto. Agora eixo/furo sai
  da **normal da própria face** (validado ao vivo). Critério dele para o anel: "um pouco
  esticado é melhor que frouxo, variando pouco" — folga pesa o dobro de aperto, e no
  furo a compressão até 1,5× o limite é aviso. E o **catálogo métrico da DL Seals**
  (Nitrílica 70 e Viton, 1014 medidas, extraído do PDF), opcional na janela. 382 testes.

- **2026-09-18** — **a ponte dirigiu a Solid Edge pela primeira vez** (226.00.08.04):
  leitura, planos, árvore e o carrinho de exemplo do `se_modelar` (6 primitivas, 0
  falhas, corpo único) sobre COM real. A sonda de malha rodou na malha real — 4.168
  facetas leem, `Body.Faces` é inacessível em corpo de facetas, e o seccionamento
  falhou por **marshaling** (`object[]` onde a typelib pede `SAFEARRAY(IDispatch)*`),
  o que não é resposta sobre malha. Corrigido com array tipado. Daí saíram
  `MeshReader`, `SurfaceRecognizer` e `SurfaceReport` + a ferramenta MCP
  `se_reconhecer_malha`. **272 testes, 0 falhas.** Dois achados que a documentação
  antiga contradizia: `RefPlane.Normal` **não é legível**, e quebrar por quina tem de
  vir ANTES de ajustar (senão um Ø20 tesselado sai como 64 planos).
- **2026-09-17** — **grupos "Eng. Reversa" e "MCP" na ribbon** (`GuiVersion` 15). A
  ponte MCP inteira construída e o protocolo validado ponta a ponta fora do CAD; a
  sonda de malha escrita depois de o dump da typelib provar que a SE **não** expõe
  ajuste de superfície sobre região de malha por COM. Ver a seção acima. 233 testes,
  0 falhas. Nada disso tocou COM ainda.
- **2026-09-16** — **"Curvas das superfícies" validado no SE**, depois de não
  reconhecer extremidade nenhuma num loft entre duas splines horizontais. A
  causa não era a geometria nem a tolerância: **`Edge.GetRange` devolve caixa
  INFLADA em aresta B-spline** (±0,005 mm por lado, medido), e o
  `FaceGeometry.TryGetRangeMm` pedia justamente ele primeiro — um rim
  perfeitamente plano chegava ao teste de horizontalidade com 0,01 mm de
  variação em Z e era reprovado contra a tolerância de 1 µm. `GetExactRange` dá
  `Δ = 0,00000 mm` na MESMA aresta. Criado `FaceGeometry.TryGetExactRangeMm`
  (mesma cadeia, exato primeiro) e usado só no teste de planaridade do WEDM; o
  `TryGetRangeMm` ficou intacto porque para agrupar detalhe por proximidade um
  bbox que só erra para MAIOR é a escolha segura. A falha parecia depender do
  ângulo da superfície (45° não reconhecia) porque as inclinadas eram as feitas
  por loft de spline — em reta e arco os dois métodos coincidem, e por isso o
  bug só apareceu quando o perfil passou a ser spline. Skill atualizada.
- **2026-09-11** — **análise de usinabilidade** no "Analisar (Z)": escada de
  fresas, jogo de brocas DIN 338, raio exato por B-Rep e canto vivo por
  topologia. A ocorrência **selecionada** passou a mandar sobre a mira por cor —
  o 1º run ao vivo revelou que a detecção automática pegava o ELETRODO (que
  também tem faces pintadas) em vez do postiço. Skill atualizada com
  `Face.GetParamRange`, a regra propriedade-sobre-método e a instabilidade do
  `Face.ID` entre rebuilds.
- **2026-08-07** — commit `1064304`: config externa de eletrodos
  (`AutoEdmConfig`), suíte de testes, `ElectrodeListForm`,
  `ElectrodeDiagnostics`, `ElectrodeNaming`, `FaceColorPainter`, `ComLifetime`
  e `Units`.
- **2026-09-09** — commit `073406a`: **alojamento de O'ring** pela ISO 3601, com
  seleção por etapas e vários furos por vez.
- **2026-09-09** — commit `701725f`: cada comando **declara** síncrono/ordenado;
  o add-in nunca mais troca o ambiente sozinho.
- **2026-09-09** — README reescrito para o GitHub (instalação, os 14 comandos da
  ribbon, ambiente, configuração, build, empacotamento, problemas comuns), com
  destaque para o `AutoEDM.Register.exe` como o programa que registra o add-in.
  Descoberto e corrigido que a regra `dist/` do `.gitignore` também casava com
  `tools/dist/` — por isso o `instalar.cmd`, o `desinstalar.cmd` e o `LEIAME.txt`
  que o README já documentava nunca tinham sido commitados, e o `pack.ps1`
  quebrava. Regra trocada para `/dist/` e os três arquivos criados.
  Repositório tornado **público**; primeira **release compilada** publicada;
  card criado na página de downloads do kenatec.com.

## Análise de usinabilidade (2026-09-11)

O "Analisar (Z)" deixou de ser só segmentação por profundidade: ele agora mede o
que a ferramentaria **consegue produzir** e sinaliza o que exige erosão. Núcleo
em `src/AutoEDM.Core/Machinability/`, todo somente-leitura:

| Arquivo | Papel |
|---|---|
| `ToolLadder.cs` | A escada de fresas (puro): alcance por raio e por família, `Classify`, e quais fresas realmente decidem |
| `DrillSet.cs` | O jogo de brocas DIN 338 (puro) — separado de propósito: fresa de raio *r* serve qualquer canto ρ ≥ *r*; broca de Ø *d* faz furo de Ø *d* e ponto |
| `BRepRadiusProbe.cs` | Raio exato por B-Rep (COM): cilindros e toros, via **propriedade** `Cylinder.Radius` / `Torus.MinorRadius` |
| `SharpCornerProbe.cs` | Canto vivo (COM): aresta entre dois planos, raio ZERO — não tem face curva para medir, quem responde é a topologia |

Três decisões que valem lembrar:

- **A premissa "Ø1 × 3 mm" estava errada** — a ferramentaria tem Ø1 × 10 mm
  longneck, então o teto a raio 0,5 mm é 10 mm. Calibrar em 3 mm condenaria
  bolsão que se fresa hoje, e falso positivo é o que faz o usuário parar de
  abrir o relatório. `ToolLadderTests` trava esse número.
- **Propriedade ganha de método** quando os dois oferecem o mesmo valor:
  `GetCylinderData` devolve o raio por `[out]` escalar, que é o que o late
  binding não popula confiável. Registrado na skill.
- **Concavidade é calibrada, não chutada.** O sinal de `(n1 × n2)·t` separa
  canto côncavo de convexo, mas a convenção do SE não está no dump — então o
  sinal é calibrado na própria peça pelas arestas da borda da caixa envolvente,
  e a análise **recusa classificar** se elas divergirem.

## Dívida resolvida: as falhas antigas de `ORingGrooveTests`

As 7 falhas registradas em `701725f` (testes escritos contra uma especificação anterior
à `ORingHousingTable`) não existem mais: os 376 testes passam, nenhum ignorado, e o
alojamento foi validado no SE em 2026-09-21. Vale a tabela de catálogo.

## Próxima ação

**Validar no SE a "Nova peça"** (grupo Molde): nome no próximo número da série, pasta, origem
no ponto certo do eixo de altura e orientação da montagem. Depois, a Fase 2 do
[plano de molde](docs/PLANO_MOLDE.md) (Refrigeração).

**Nível 2 da análise de usinabilidade** — a varredura do vazio da cavidade
(malha por `Body.GetFacetData` → rasterização por fatia → transformada de
distância → varredura de acesso de cima para baixo → componentes conexos 3D).
É o que faz a análise enxergar rasgo estreito entre paredes planas e região sem
acesso, e o que dá a **pegada real** de cada região em vez da caixa envolvente.
O plano completo, com as cinco fases e os riscos, está publicado como página
(ver o histórico de 2026-09-11).

Antes disso, confirmar no SE o **canto vivo**: o log dirá se a calibração do
sinal de concavidade (pelas arestas da borda da caixa envolvente) fecha, e
quantas arestas a versão plano↔plano descarta por encostar em face curva.

**No WEDM a cadeia fechou de ponta a ponta em 2026-09-16**, da peça ao programa
da máquina: o "Curvas das superfícies" cria as curvas, o "Exportar perfis (IGES)"
grava um `.igs` por altura Z e **o Pitágoras abriu esses arquivos sem problema**.
Não sobrou elo por ver funcionando.

Três correções desse mesmo dia, todas achadas por log e já na skill
`solid-edge-com`, valem para qualquer automação COM daqui em diante:

1. `Edge.GetRange` devolve caixa **inflada** em aresta B-spline (±0,005 mm
   medidos) — para DECIDIR planaridade só serve `GetExactRange`.
2. O raio de canto chega como **`igEllipse`**, com `MinorMajorRatio` 0,9999.
   Quem decide se é arco é a geometria medida ao longo da aresta, não o tipo nem
   a razão declarada — senão todo raio vira polilinha enquanto o "Salvar como" do
   próprio SE exporta o arco perfeito.
3. Cada `DerivedCurves.Add` **mata a superfície de origem e as arestas dela**: só
   a primeira curva da rodada saía, e as superfícies lidas depois dele vinham com
   arestas sem sentido. A cura é estrutural — ler tudo antes com o modelo parado,
   guardar identidade (índice + caixa da superfície, ID + geometria da aresta) e
   nunca proxy, e reencontrar documento, superfície e arestas antes de cada Add.
   Depois disso: 7 curvas numa rodada, todas aceitas.

Fica um ponto de projeto em aberto: os contornos têm saído **ABERTOS**, o que é
esperado para loft entre splines abertas, mas perfil de corte a fio normalmente
precisa fechar. Decidir se o botão deve fechar o contorno sozinho, se a
tolerância de encadeamento (0,01 mm) está apertada para essas peças, ou se isso é
responsabilidade do desenho.

Continuam pendentes de validação no CAD: **Aplicar
GAP**, **Duplicar eletrodo** e a **lista de corte na serra**; e em aberto a
receita da rosca física M6, os ícones da ribbon e o orquestrador completo.
