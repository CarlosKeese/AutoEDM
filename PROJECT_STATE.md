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
em Release já deixa o servidor pronto. Ele NÃO é ainda empacotado pelo `pack.ps1`:
enquanto a ponte não tiver o 1º run validado no SE, não faz sentido enviar o binário no
instalador do operador.

## Decisão travada: o add-in nunca troca o ambiente de modelagem

Cada botão **declara** se exige síncrono ou ordenado, na tabela `CommandSpec`
de `ElectrodeRibbon`. No ambiente errado o botão fica cinza e explica o que
trocar. Quem troca é o usuário. Isso veio de dois estragos concretos: esboços
órfãos presos no nó "Ordenado" do PathFinder (`ProfileSets.Add()` cria esboço
ordenado mesmo em peça síncrona) e o "Aplicar GAP" falhando em peça síncrona,
porque a troca de ambiente reconstrói o corpo e mata as faces já lidas.

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
| Criar eletrodo (manual) | ✅ validado no SE |
| Unir superfícies | ✅ validado no SE |
| Guarda de ambiente por comando | ✅ construído |
| Configuração externa (`config.json`) | ✅ construído, coberto por teste |
| Análise de usinabilidade (nível 1: raio + canto vivo) | ✅ construído, coberto por teste; raio validado no SE |
| Curvas das superfícies (WEDM) | ✅ validado no SE (2026-09-16) |
| Exportar perfis WEDM (IGES por Z) | ✅ validado no SE e **no Pitágoras** (2026-09-16) — a cadeia inteira, da peça ao programa da máquina |
| Lista de corte na serra | 🚧 construído, coberto por teste, **aguardando validação no SE** |
| Ponte MCP (Claude Code → Solid Edge) | 🚧 construída; protocolo validado ponta a ponta FORA do CAD, **aguardando o 1º run com a SE aberta** |
| Sonda de malha (Eng. Reversa) | 🚧 construída, **aguardando rodar sobre uma malha real** |
| Testes de unidade | ✅ 233 passando, 0 falhas |
| Alojamento de O'ring (ISO 3601) | 🚧 construído, **aguardando validação no SE** |
| Aplicar GAP | 🚧 corrigido, **aguardando confirmação final no SE** |
| Duplicar eletrodo p/ próximo Ra | 🚧 construído, **aguardando validação no SE** |
| Copiar superfícies (Inter-Part Copy) | 🚧 só em edição em contexto (in-place) |
| Rosca física no furo M6 | 🚧 sonda de diagnóstico pronta; receita definitiva em aberto |
| Orquestrador "gerar todos os eletrodos" | 📋 planejado |

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
| `Mcp/SeToolRunner` | `Core`, roda na thread da SE | Executa as 9 ferramentas, aplica a guarda de documento/ambiente |
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

**O que falta:** o 1º run com a Solid Edge ABERTA. Nada da ponte tocou COM ainda.

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

## Dívida conhecida: as 7 falhas de `ORingGrooveTests`

Já existiam em `073406a` e o commit `701725f` as registra explicitamente. Os
testes foram escritos contra uma especificação **anterior** à implementação que
ficou, e as duas divergiram:

| Teste | Espera | Implementação entrega |
|---|---|---|
| `CatalogoEmbutido_temAsTresSecoesDeMoldeETodasAConferir` | 3 seções: 1,78 / 2,62 / 3,53 | 5 seções — o catálogo ganhou 1,02 / 1,27 / 1,52 |
| `CanalEstatico_temProfundidade80PorCento...` (×3) | profundidade = 0,80·d2, largura = 1,31·d2 | valores da `ORingHousingTable`, que não seguem uma razão fixa |
| `Preenchimento_ficaNoAlvoEAbaixoDoLimite` | 0,70–0,80 | 0,6947 |
| `Fkm_deixaOCanalMaisFolgadoQueNbr` | FKM mais folgado que NBR | não é o que sai |
| `CatalogoEmbutido_asProgressoesBatemComOsCodigosConhecidos` | códigos que existiam antes | `Sequence contains no matching element` |

**Decidir qual lado é a verdade é uma questão de domínio, não de código:** ou a
`ORingHousingTable` (tabelas Parker/DL Seals, medidas reais de catálogo) está
certa e os testes precisam ser reescritos contra ela, ou as razões da ISO 3601
(80 % / 131 %) são o alvo e a tabela precisa ceder. **Resolver isso junto com a
validação da ferramenta no SE** — não antes, para não travar uma fórmula que a
peça real ainda vai contestar.

## Próxima ação

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

Continuam pendentes de validação no CAD: **Alojamento de O'ring**, **Aplicar
GAP**, **Duplicar eletrodo** e a **lista de corte na serra**; e em aberto a
receita da rosca física M6, os ícones da ribbon e o orquestrador completo.
