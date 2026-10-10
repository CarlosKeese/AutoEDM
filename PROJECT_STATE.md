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
powershell -ExecutionPolicy Bypass -File tools\pack.ps1 -Version 2026.9.9                                 # gera dist\AutoEDM-2026.9.9.zip
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
| Reconhecimento de superfície sobre malha (`se_reconhecer_malha`) | ✅ **rodado nas 6 malhas reais (2026-10-09)** pelo Core, conferido contra a Eng. Reversa nativa (Ø7,00 e Ø14,00 iguais); plano + cilindro, medidas no centésimo; **2026-10-10: + cone, + toro/esfera, + identificação** (furos, raios, chanfros, faces inclinadas), cilindro falso de 3 pontos e fragmento de concordância recusados — ✅ instalado no add-in em 2026-10-10 |
| **Malha → sólido** (botão + `se_malha_para_solido`) | ✅ **Core validado no SE (2026-10-09)**: malhas 4, 5 e 6 do Rack_Anghinoni viraram sólido com corpo = volume EXATO do plano (0 / 0 / −0,034 mm³); ✅ **instalado em 2026-10-09** (`bcb8cbd`, GuiVersion 17, add-in + servidor MCP); **2026-10-10: correções de fronteira (chanfro/cone por revolução, raio X/Y e face inclinada por extrusão) + desvio geométrico — 5 de 6 corpos saem EXATOS (≤ 0,04 mm nos dois sentidos, alça com toro inclusive)**, validado por console externo; ✅ **add-in instalado em 2026-10-10** (Release; depois da fusão com o grupo Molde o botão é o **id 28** e a ribbon `GuiVersion` 19); 🚧 servidor MCP ainda com o catálogo antigo (só as descrições mudaram); as pernas (corpo 3) ainda divergem |
| Modelagem por primitivas (`se_modelar`) | ✅ **validada no SE (2026-09-18)** — exemplo `carrinho`: 6 primitivas, 0 falhas, **corpo único** |
| Botões da ribbon via MCP (15 ferramentas) + `se_trocar_ambiente` | 🚧 escritos (2026-09-21), compilam, catálogo coberto por teste; **aguardando o 1º run no SE** (exige trocar add-in + servidor MCP) |
| Testes de unidade | ✅ 459 passando, 0 falhas |
| Alojamento de O'ring (ISO 3601) | ✅ **validado no SE (2026-09-21)** — canal de face e de eixo como coroa concêntrica extrudada: acompanha o furo movido **e** a mudança de Ø; nome do anel com instância, laranja de vedação, lado da pressão; eixo/furo pela normal da face; catálogo métrico DL Seals opcional |
| Aplicar GAP | 🚧 corrigido, **aguardando confirmação final no SE** |
| Duplicar eletrodo p/ próximo Ra | 🚧 construído, **aguardando validação no SE** |
| Copiar superfícies (Inter-Part Copy) | 🚧 só em edição em contexto (in-place) |
| Rosca física no furo M6 | 🚧 sonda de diagnóstico pronta; receita definitiva em aberto |
| Orquestrador "gerar todos os eletrodos" | 📋 planejado |
| Nova peça (molde) | 🚧 construída (2026-09-24), coberta por teste, **aguardando validação no SE** |
| Refrigeração (molde) | 🚧 construída (2026-09-24), plano coberto por teste, **aguardando o 1º run no SE** |
| Grupo **Molde** (Canais, Pontos de injeção, Extratores, Gavetas) | 📋 planejado — [`docs/PLANO_MOLDE.md`](docs/PLANO_MOLDE.md); O'ring já movido para o grupo |

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

### 1º run do reconhecedor na malha real (2026-10-09)

Peça `Rack_Anghinoni (1).par`: 6 corpos de malha, mais superfícies feitas com a Eng. Reversa
NATIVA (FaceSet + "Superfície planar/cilíndrica" = `CopyConstruction`), usadas como gabarito:
Ø7,00 e Ø14,00, planos em valores redondos. Rodado por console externo (ROT), sem a ponte.

- **`Body.Faces` É acessível em corpo de facetas** (contradiz 09-18): `MeshSurface`,
  `GeometryForm` 9; o FaceSet nativo parte o corpo em várias faces de malha.
  `Face.GetFacetDataEx` devolve malha **indexada**.
- `FaceSet` não tem coleção com `Add`: a segmentação nativa não é scriptável.
- Defeitos do `SurfaceRecognizer` encontrados: (1) cilindro falso Ø135–143 com RMS 0,6–1 mm
  aceito — `CylinderRelativeTolerance` 2 % é frouxa demais; (2) super-segmentação (300–600
  "planos" por corpo): falta agrupar regiões coplanares/coaxiais numa superfície só, como o
  nativo faz; (3) furo inteiro sai com "volta 345–350°" (vão de 1 faceta); (4) falta arredondar
  a 0,01 mm (regra do Carlos) e alinhar normal a eixo; (5) raio de concordância vira mosaico
  de planos.
- **Rota B-spline por polos: NÃO serve nesta versão.** `BSplineSurfaces.Add` dá `E_FAIL` já na
  1ª chamada (bilinear 2×2 e biquadrática 3×3, em síncrono e ordenado; nós completos e pesos
  obrigatórios — nós distintos ou pesos vazios dão `E_INVALIDARG`). `AddBSplineCurveByPoles`
  só funciona na **1ª** chamada por documento; as seguintes deixam feature `Failed` e devolvem
  ponteiro nulo. **`u_order = 1` DERRUBA a Solid Edge** (processo morre) — nunca usar.

**Rota que funciona, provada em peça nova (ordenado) com volume exato:**
1. Plano de esboço em pose qualquer: `CoordinateSystems.AddByMatrix(16 doubles: linhas = eixos
   X,Y,Z, translação em [12..14], metros)` → `cs.Plane(4)` (é uma **Face**, não aceita em
   `Profiles.Add`) → `RefPlanes.AddParallelByDistance(face, 0, 2)` = RefPlane de verdade.
2. O RefPlane derivado **não herda o eixo X do CS**: nunca supor os eixos do esboço — converter
   cada ponto com `Profile.Convert3DCoordinate` (interop tipada, `out`).
3. Plano = linha no esboço extrudada simétrica (`ExtrudedSurfaces.AddFinite` com
   `Profile[]` TIPADO — `object[]` dá `DISP_E_TYPEMISMATCH`); cilindro = círculo extrudado.
   Saíram `Plane` e `Cylinder` exatos (Ø7,0000).
4. `ConstructionModel.IntersectSurfaces.AddByAutoTrim(n, Body[] TIPADO dos corpos de construção,
   false, true, true, 1e-5)` → sólido. Com features: `E_INVALIDARG`; com faces: feature `Failed`.
   (A coleção fica em `doc.Constructions.Item(i)`, não em `Constructions`.)
5. `Models.AddByConstruction(<a feature Intersect>)` → corpo de projeto. Com o corpo ou o
   ConstructionModel: `E_NOINTERFACE`.
6. Furo interno NÃO entra no auto-trim (as faces planas não são furadas e a costura não fecha):
   vai depois, como `Model.ExtrudedCutouts.AddFinite(perfil, 1, 3 simétrico, comprimento)`.
   Bloco 20×30×40 + furo Ø7: **22.460,6196 mm³ = teórico**, 7 faces.

7. **Limites medidos do auto-trim:** fecha casca CONVEXA. Furo/bolsão interno não: as faces
   planas não são furadas e a costura não fecha. `AddByPointOnFace`: o ponto marca o pedaço a
   **descartar** (6 × 14.400 − 5.200 = 81.200 mm² de folha, conferido), um por superfície; a
   variante `Ex` com `Invert = true` faz a feature falhar. `SurfaceByBoundaries.Add` com
   elementos de esboço (Line2d ou Profile) sai Failed — quer arestas 3D.
8. Perfil de linhas soltas devolve `End(1) = −103`; com `Relations2d.AddKeypoint(l[i], 1,
   l[i+1], 0)` entre as pontas, `End(1) = 0`.

**Reconhecedor corrigido (2026-10-09), conferido contra o gabarito nativo** — malha 4 deu
Ø7,00 e pontas Ø14,00 em meia volta, iguais aos ajustes da ferramenta nativa, e caiu de 381 para
134 regiões:
- componente misto (curva TANGENTE a plano, sem quina) agora cresce CILINDRO primeiro, a partir de
  retalhos planos pequenos; só o que sobra vira plano. As 276 "faixas" que viravam plano sumiram;
- tolerância do cilindro: absoluta 0,005 mm (cilindro real de STL de CAD dá RMS 0,00000;
  fragmento de concordância de canto, 0,0002–0,02), relativa 0,1 % com teto de 5× a absoluta, e
  arco mínimo de 5° — o "cilindro Ø38.035 com volta 0°" que engolia o fundo da peça sumiu;
- furo inteiro tesselado dá 360° (vãos todos iguais = passo, não abertura);
- `Snap`: normal/eixo a < 0,05° de X/Y/Z vira o eixo exato; offset de plano, Ø e posição do eixo
  no passo de 0,01 mm (`RoundToMm`, `SnapAngleDeg`; 0 desliga);
- 7 testes novos (rasgo arredondado, canto com raio tangente, volta 360°, quase-plano,
  arredondamento ligado/desligado). 389 testes, 0 falhas.

Ainda NÃO reconhece toro (raio ao longo de arco — nervura da malha 2 cai em "livre") nem cone
(ângulo de saída). Concordância de canto (toro/esfera) não deve ser forçada em cilindro: na
reconstrução ela vira Round sobre o canto vivo.

**Decisão de rota (2026-10-09): prismática 2,5D primeiro.** Para placa/postiço/eletrodo:
níveis Z dos planos horizontais → secção da malha no meio de cada faixa → cada trecho do
contorno trocado pela geometria EXATA da superfície reconhecida (parede plana = reta, cilindro
vertical = arco com centro e Ø a 0,01) → extrusão por faixa. Sai sólido com features
editáveis. O kit superfícies + auto-trim fica para casca convexa.

**Feito no mesmo dia — malha → sólido, rota 2,5D** (`PrismaticSection`, `PrismaticBuilder`,
`PrismaticReport`, botão "Malha → sólido", ferramenta `se_malha_para_solido`):
- níveis Z dos planos horizontais; secção da malha no meio de cada faixa; cada trecho trocado pela
  superfície exata (reta, arco, círculo) com vértice pela interseção exata das vizinhas; faixas
  iguais fundidas; volume EXATO com arcos integrados (a malha tem furo/raio inscrito e dá a mais);
- construção numa PEÇA NOVA ordenada: plano por matriz + `Convert3DCoordinate`, arco por 3 pontos,
  `AddKeypoint` com o índice escolhido pela posição LIDA DE VOLTA, uma protrusão por faixa e os
  furos como UM recorte por faixa (furo a furo se o lote falhar);
- `AddFiniteExtrudedProtrusion` devolve o Model: o status se lê em `ExtrudedProtrusions` (que não
  tem `GetStatusEx` — usar `.Status`);
- furo TANGENTE a outro contorno: perfil com laços que se tocam dá protrusão SEM material e sem
  erro; recorte tangente o Parasolid recusa. Saída: recorte refeito com Ø +0,002 mm, declarado;
- encadeamento da secção: num vértice com duas saídas, a que vira mais à ESQUERDA (meia-volta por
  último) — confirmado por teste que falhava com a regra invertida;
- faixa NÃO prismática é denunciada (área da secção a 10/30/70/90 % contra o meio): o volume
  sozinho enganou na malha 6, que tem raio ~1 mm na borda do bolsão.

Resultado nas 6 malhas: **4, 5 e 6 constroem** (corpo = exato); 4 é integralmente prismática; 1, 2,
3, 5 e 6 têm faixas com raio em aresta horizontal, chanfro ou saída (avisado). A 3 tem paredes com
ângulo de saída e quebra a secção (vértice a 8 mm) — fora do alcance da rota 2,5D reta.

### Raios, cones e faces inclinadas — correções de fronteira (2026-10-10)

Pedido do Carlos: converter as malhas em sólido **com raios, cilindros e faces inclinadas
identificados**. Feito por console externo (ROT) com a ponte ligada; as 6 malhas foram
despejadas em arquivo e o reconhecimento iterado fora da SE.

**Reconhecedor:**
- **cone** (`TryCone`): eixo = direção de menor variância das normais; ÁPICE = ponto que melhor
  satisfaz n·x = n·c de todas as facetas (todo plano tangente de cone passa pelo ápice);
  semi-ângulo por ρ = h·tanβ. Malha 6: 48 chanfros 1×45° Ø4→Ø6 (eram ~20 "cilindros inclinados"
  de 3 triângulos cada; 1.009 superfícies → 97); malha 5: 24 chanfros 0,5×45°;
- **cilindro falso de 3 pontos**: parede + rampa davam "Ø53,57 de 39°, RMS 0" (três pontos de
  perfil sempre cabem num círculo). Agora exige FLECHA de tesselação ≤ 0,1 mm;
- **fragmento de concordância** (corpo 3: centenas de "R0,09 eixo (−0,097; 0,181; 0,979)"):
  dentro de componente misto, cilindro/cone só vale se for REGRADO (vértices em geratrizes).
  Superfície inteira delimitada por quina fica isenta — o chanfro da malha 6 tem tesselação
  torcida (anéis das bocas em ângulos diferentes) e é provado pelo ajuste;
- snap parcial de direção (parede de normal (−0,625; −0,781; 0,0003) vira vertical), volta
  inteira pelo quartil de cima dos vãos (o chanfro saía "346°");
- `SurfaceReport.Identify`: furos/pinos, raios, chanfros/cones, faces inclinadas (ângulo da
  vertical e da horizontal), agrupados pela medida (24 chanfros iguais = 1 linha).

**Construção — `BoundaryCorrections` + `PrismaticBuilder`:** no plano em que a superfície vira
curva, a curva parte uma região pequena em duas, e a MALHA (número de enrolamento,
`MeshSolid`) diz se cada uma tem material → protrusão ou recorte. As duas são idempotentes
contra o sólido prismático, que só erra dentro dessas regiões. Cone de volta inteira →
revolução de 2 triângulos; cilindro de eixo X/Y → canto entre arco e tangentes + segmento
circular, extrudados no eixo; plano inclinado → 2 triângulos da linha de maior declive,
extrudados na horizontal da face. Região meio cheia é recusada com aviso; região em que o
prismático já concorda é pulada.

Receitas provadas na SE 226:
- revolução: `Profile.SetAxisOfRevolution(linha)` → `RevolvedCutouts/RevolvedProtrusions.AddFinite(
  perfil, eixo, 2, 2, 2π)` — o chanfro tirou os 7,330 mm³ previstos;
- **perfil de extrusão tem de ser ANTI-HORÁRIO nas coordenadas do esboço** (lidas por
  `Convert3DCoordinate`, não nas de U/V): desenhado horário, o `igLeft` do recorte é o lado de
  FORA e a feature levou a fatia inteira (−17.700 mm³, malha 1);
- **as bocas do cone encostam nos planos reconhecidos** (< 0,02 mm): o ápice ajustado saiu em
  Z 82,9989 e o recorte deixava uma pele de 0,001 mm sobre o furo, com a quina viva embaixo.

**Conferência por DESVIO, não só por volume** (`MeshDeviation`, grade uniforme + distância
ponto-triângulo, nos dois sentidos, re-tesselando o sólido a 0,005 mm). Na malha 6 os 24
chanfros tiram exatamente o que os 24 colarinhos cônicos põem: o volume bateu com as correções
ERRADAS. Foi o desvio que achou a pele de 0,001 mm.

| Corpo | Conteúdo | malha→sólido (mm) | sólido→malha (mm) |
|---|---|---|---|
| 1 (Desenho_6) | bandeja + rampa 19,47° | 0,005 | 0,023 ✅ |
| 2 (Desenho_5) | placa + alça (R2,5 eixo Y + toro R2,5 nas pontas R3) | 0,005 | 0,039 ✅ |
| 3 (Desenho_4) | tampo + 4 pernas (cantoneira R1–R10–R1, R5 até o tampo) | 0,77 | 1,57 ⚠️ só as 8 concordâncias de canto, forma livre |
| 4 (Desenho_3) | prismática pura | 0,005 | 0,022 ✅ |
| 5 (Desenho_2) | placa + 24 chanfros 0,5×45° | 0,009 | 0,023 ✅ |
| 6 (Desenho_1) | bloco + 24 chanfros + 24 colarinhos cônicos | 0,009 | 0,023 ✅ |

**Alça do corpo 2 — "raios identificados como múltiplas linhas" (Carlos, Peça18).** A "cadeia
R2,51 + 22,5° + 7,5°" era leitura errada: a alça é um oblongo de 6 mm com pontas R3 e as arestas
de cima arredondadas R2,5, que nas pontas contorna o R3 como TORO (R2,5 a R0,5 do eixo Z, 180°).
O reconhecedor errava em três pontos, todos corrigidos:
- a semente do cilindro levava todo vizinho de área parecida; na ponta, cada faceta da parede R3
  tinha uma faceta do toro por vizinha e o ajuste falhava sempre — a ponta caía em "livre" e o
  esboço saía com **26 retas por ponta**. Agora a semente só leva vizinhos que concordam no EIXO
  (produto vetorial das normais);
- o crescimento testava cada vizinho contra o ajuste dos 3 primeiros retalhos (R2,51), e as 2
  facetas de baixo do raio viravam "faces inclinadas" de 7,5° e 22,5°. Agora reajusta a cada
  retalho aceito → R2,50 de 90°; pontas do arco a < 1° de X/Y/Z encostam no eixo (90,4° → 90°,
  senão o canto virava dois pedaços de 45,2° e o de cima falhava na SE);
- **toro e esfera** (`TryTorus`, `MergeIntoTori`): toda normal de superfície de revolução corta o
  eixo — linear em Plücker (6×6, Jacobi); como a tesselação a 15° erra o eixo em ~13°, refino por
  Nelder–Mead sobre os VÉRTICES (círculo do perfil no meridiano), partindo do Plücker e do eixo
  X/Y/Z mais próximo. Ajustar par a par não serve (as normais de duas faixas cortam também a reta
  dos centros dos perfis): primeiro o grupo conectado inteiro, depois absorção de vizinho de
  qualquer tipo se a união ainda couber. Raio maior ~0 = esfera. Resultado na malha 2: R2,500 a
  R0,500, 180°, RMS 0,000002 mm; 78 → 50 superfícies.
- correção do toro = revolução PARCIAL, esboço no meio do arco, extensão simétrica (`igSymmetric`);
- pontos de teste da região: a média dos vértices caía do lado de dentro do arco na meia-lua de
  0,21 mm do canto do R2,5 (região "meio cheia"). Agora grade dentro do polígono, a ≥ 0,04 mm da
  borda (acima da flecha da malha).
Peça construída: 50 faces = 12 planos + 36 cilindros + **2 toros**, nenhuma faceta.

**Esboços ocultos** (pedido do Carlos): feature criada pela API deixa o perfil aceso — a Peça7
tinha 32 de 32 perfis visíveis. No fim da construção todo `Profile.Visible = false` (Peça8: 0 de
32). CS e plano de referência já nasciam ocultos.

**Furo na conferência, fechado:** desvio ≤ 0,05 mm NÃO prova geometria exata — trecho copiado da
malha (raio virado retas) coincide com ela por construção. O veredito do relatório agora só diz
"EXATO" se o desvio passa E nenhum trecho do contorno é polígono da malha sem correção.

### Pernas do corpo 3 — "vários detalhes não são reconhecidos" (Carlos, 2026-10-10)

O botão gerava 22.707,6 mm³ contra 24.094,4 da malha (−1.387 mm³, 5,8%), com vértices a até 4,5 mm
em toda a altura das pernas. A perna é uma cantoneira: paredes de 2 mm, pontas R1, **face interna
R10 tangente às duas pontas** (em S), canto externo R2,5 e um **raio R5** até o fundo do tampo. Seis
defeitos, todos corrigidos:
- **corte da secção passando por vértice**: a faixa 4–40 era cortada em Z 22, altura de vértices da
  face R10; aresta com ponta exatamente no plano não contava, o segmento sumia e o laço pulava da
  ponta R1 para a parede X = 45 — R10, a outra ponta e um trecho de parede saíam da perna inteira.
  `PrismaticSection.OffVertex` afasta o corte 0,1 µm de qualquer vértice;
- faixa estreita que é UM plano saía "livre" (regra das faixas de cadeia curva);
- crescimento de retalho plano dentro de região mista com a tolerância do plano solto atravessava
  da parede para o raio tangente — agora 1/4 do ângulo e 1/5 da distância;
- três facetas vizinhas viravam um cilindro falso antes de o R1 crescer até elas: **disputa** —
  cilindro de poucos retalhos devolve o que cabe no vizinho maior, ou se desfaz;
- pedaços **coaxiais** (R0,905…R0,965 no alto da ponta R1, tesselação girada) voltam ao R1;
- **raio de concordância por hipótese e verificação** (`FilletRecognizer`): entre plano e plano é
  cilindro de eixo n₁ × n₂; entre plano e cilindro perpendicular, toro no eixo dele. O centro fica a
  r das duas superfícies, então o perfil só tem r livre: busca de CONSENSO em r, refino só com os
  pontos que concordam, cobertura mínima de 60% do arco, só o pedaço conexo, e raio redondo
  (múltiplo de 0,05 a menos de 0,015). Resultado: 8 × R5 de 90° (2 por perna), viram protrusões.
Resultado: **24.036,7 mm³ (−58 mm³, 0,24%)**; desvio só em Z 35–40 nas 4 pernas. O que sobra é a
concordância de CANTO em volta da ponta R1 (R5 da parede → ponta R1 → face R10): medida em 11
meridianos, nenhum perfil é círculo (resíduo até 0,09 mm, "raio" de 2,7 a 4,7) — forma livre,
avisada como tal. Corpos 1, 2, 4, 5 e 6 reconferidos, continuam exatos. 457 testes.

### Lote e montagem (Carlos, 2026-10-10)

"Converter mais de um corpo, e todos os convertidos em peças separadas numa única montagem."
`MeshToSolidBatch`: lista os corpos de malha, converte os escolhidos (cada um numa peça nova) e,
com montagem, salva cada peça ("&lt;peça&gt; - &lt;corpo&gt;.par" na pasta "&lt;peça&gt; - sólidos" ao lado da
peça da malha; nada sobrescrito) e monta todas por `AddByFilename` + `PutMatrix` identidade — o
arranjo original sai sozinho, porque cada sólido nasce nas coordenadas da malha. Botão: lista com
caixas de seleção + "reunir numa montagem" + pasta; MCP: `corpos`/`todos`/`montagem`/`pasta`.
Validado por console nas 6 malhas: 6 peças + montagem em 100 s, ocorrências na origem sem rotação.
459 testes.

**Próxima ação:**
1. add-in já instalado (2026-10-10); falta o servidor MCP — `powershell -ExecutionPolicy Bypass -File tools\deploy-dev.ps1
   -Configuration Release -IncludeMcp` com SE e Claude Code fechados — e rodar o botão
   "Malha → sólido" nos corpos 1, 2, 4, 5 e 6;
2. concordância de canto de forma livre (perna do corpo 3, ~58 mm³ no total): não é primitiva —
   decidir se vale aproximar por superfície ou deixar avisado;
4. cone parcial e cilindro de eixo inclinado (hoje avisados, não corrigidos);
5. toro (nervura com raio ao longo de arco) no reconhecedor.

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

- **2026-10-10 (noite, 3)** — **Skill `solid-edge-com` atualizada com a sessão** (secção fora de
  vértice, arcos tangentes, raio de concordância por hipótese, disputa entre cilindros, lote numa
  montagem por PutMatrix identidade, orientação de perfil fechado × `igLeft`, dados da SE no idioma
  da instalação, ids de botão ao fundir branches, NU1701, deploy sem `pwsh`) e `.skill`
  reempacotado. A skill lembrou um defeito: o `RefAxis` das revoluções ficava visível — agora oculto
  (corpo 6: 0 de 48 eixos visíveis).
- **2026-10-10 (noite, 2)** — **Malha → sólido em lote, com montagem.** Vários corpos de uma vez;
  peças salvas e reunidas numa montagem nova na posição original. Validado nas 6 malhas (100 s).
  459 testes.
- **2026-10-10 (noite)** — **Pernas do corpo 3 reconhecidas.** Corte de secção fora de vértice,
  faixa plana, disputa entre cilindros, fragmentos coaxiais e raio de concordância por hipótese
  (cilindro/toro tangente a duas superfícies conhecidas). Corpo 3: −1.387 → −58 mm³; sobra só a
  concordância de canto de forma livre. 457 testes.
- **2026-10-10 (fusão)** — **`main` desta máquina e do GitHub reunidas.** Separadas desde
  2026-09-21: aqui os 10 commits da Eng. Reversa (09–10/10), lá os 6 do grupo Molde, revisões e
  GAP (24/09–02/10). Os dois lados tinham dado o **id 26** a botões diferentes, e o `Ribbon.xml`
  fundia SEM conflito com dois botões 26: "Malha → sólido" passou a **id 28**, `GuiVersion` 19.
  A fusão expôs um defeito da Refrigeração nesta máquina: a SE 2026 está em PORTUGUÊS, a base de
  furos tem a aba "Rosqueado" (não "Threaded") e sub-tipos "Rosca de Tubo Reta/Cônica" — o leitor
  achava zero roscas de tubo. `HoleDatabase` agora lê as duas línguas (o texto do sub-tipo segue
  cru, é com ele que o `AddEx` casa — **não conferido ao vivo** na SE em português). 455 testes.
- **2026-10-10 (tarde)** — **Alça do corpo 2 exata.** Raio que saía em retas: semente de
  cilindro por eixo, crescimento com reajuste, pontas de arco nos eixos, TORO/ESFERA reconhecidos
  (Plücker + Nelder–Mead nos vértices) e corrigidos por revolução parcial; amostragem da região
  por grade interna. Veredito "exato" passa a exigir zero trecho copiado da malha. 5 de 6 corpos
  exatos. 409 testes. Add-in instalado; README, MANUAL e skill atualizados.
- **2026-10-10** — **Raios, cones e faces inclinadas identificados e construídos.** Reconhecedor
  ganhou cone, identificação agrupada e recusa de cilindro falso/fragmento; `BoundaryCorrections`
  corrige o sólido 2,5D por revolução/extrusão decidida pela malha; `MeshDeviation` confere o
  sólido nos dois sentidos. Corpos 1, 4, 5 e 6 do Rack_Anghinoni saem exatos (≤ 0,023 mm);
  2 (alça em cadeia) e 3 (pernas orgânicas) ainda divergem. 407 testes. Não instalado.

- **2026-10-09** — **Eng. Reversa: malha vira sólido.** 1º run do reconhecedor nas 6 malhas do
  Rack_Anghinoni, conferido contra a Eng. Reversa nativa. Rota B-spline por polos testada e
  descartada (só a 1ª criação por documento funciona; `u_order = 1` derruba a SE). Rota de
  superfícies + auto-trim provada para casca convexa. Rota prismática 2,5D construída: malhas 4,
  5 e 6 viram sólido com volume exato. Botão "Malha → sólido" e `se_malha_para_solido`. 398
  testes. Commits `4f0c3ea` → `77e34fb`.
- **2026-09-24** — **Refrigeração (Fase 2 do plano de molde).** Botão id 27 (peça ordenada) +
  MCP `se_refrigeracao_plano` (só leitura) / `se_refrigeracao`. Linhas retas do esboço 3D da
  peça → um furo por passada (colineares juntas), plano normal à aresta na ponta de entrada,
  sobrefuro Ø/2 e ponta 118° nos cruzamentos; engate/tampão = furo roscado coaxial com rosca
  de tubo lida da base de furos da SE (strings exatas — a NPT tem espaços). 415 testes.
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
