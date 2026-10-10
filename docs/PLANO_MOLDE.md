# Plano — grupo "Molde" da ribbon

> **Estado (2026-09-24):** planejamento. **Nada daqui está implementado**, exceto a
> Fase 0. Pedido do Carlos: uma seção da ribbon para recursos de molde, com o
> "Alojamento de O'ring" movido para ela e seis botões novos — Nova peça, Refrigeração,
> Canais de alimentação, Pontos de injeção, Extratores e Gavetas.

## Regras que valem para todos os botões

As mesmas do resto do add-in (ver `PROJECT_STATE.md` e a skill `solid-edge-com`):

- **Casca fina sobre o Core.** Cada janela só orquestra; a regra de projeto (diâmetros,
  distâncias, ângulos) mora num núcleo **puro** em `AutoEDM.Core/Mold/` com teste xUnit, e a
  modelagem num `*Modeler` separado. O MCP ganha uma ferramenta por botão, com as janelas
  virando argumentos.
- **O comando declara o ambiente, nunca troca.** Cada botão entra na tabela `Specs` com
  tipo de documento + `ModelingEnv`.
- **Nenhuma assinatura inventada.** Toda API nova começa por uma sonda ou por "Inspecionar
  seleção"/Gravador sobre o recurso feito à mão. As sondas de cada fase estão listadas nela.
- **Seleção por etapas** (`SePicker`, janela modeless à direita, igual ao O'ring e ao
  eletrodo manual) sempre que o usuário precisar clicar no modelo.
- **Conferir antes de criar.** Tudo que cria recurso mostra o plano (quantos furos, onde,
  com que folga) antes de modelar, e avisa quando uma regra foi violada — quem decide é o
  Carlos.

## Sequência de desenvolvimento

| Fase | Botão | Depende de | Por que nesta posição |
|---|---|---|---|
| 0 ✅ | Grupo "Molde" + O'ring movido; **Criar eletrodo (manual)** por seleção na janela | — | Feito em 2026-09-24. A janela do eletrodo manual valida `SePicker` **em montagem**, que as fases 1–5 também usam. |
| 1 ✅ | **Nova peça** | Fase 0 | Extrai a "fábrica de peça" do `CreateAndPlaceElectrode` — base de tudo que cria peça na montagem (inclusive as gavetas). |
| 2 🚧 | **Refrigeração** | — | Cria o motor "curva do esboço 3D → cilindro + furos nas pontas", reaproveitado pelas fases 3 e 5. |
| 3 | **Canais de alimentação** | Fase 2 | Mesmo motor, com mais seções (trapezoidal, meia-cana) e o plano de partição. |
| 4 | **Pontos de injeção** | Fase 3 | O ponto de injeção normalmente nasce na ponta de um canal; com a fase 3 pronta, o ponto e a direção já vêm do canal. |
| 5 | **Extratores** | Fase 2 + nível 2 da usinabilidade | Precisa conhecer os canais de refrigeração (distância) e usa a mesma varredura raster do nível 2. |
| 6 | **Gavetas** | Fase 1 | O mais complexo: alojamento, pino inclinado, calço/cunha e, opcionalmente, a peça da gaveta. |

A ordem difere da lista original (injeção antes de canais) de propósito: canal e
refrigeração compartilham o motor, e o ponto de injeção herda posição e direção da ponta
do canal. Se o Carlos precisar dos pontos de injeção antes, a fase 4 roda sozinha com
ponto clicado — só perde a ligação automática com o canal.

---

## Fase 0 — feita em 2026-09-24

- Grupo **Molde** na ribbon (`Ribbon.xml`), com o **Alojamento de O'ring** (id 14) movido
  do grupo "Peça". Id, pré-requisito (peça ordenada) e ferramenta MCP não mudaram.
- **Criar eletrodo (manual)** (id 10) não exige mais pré-seleção: o clique abre
  `ManualElectrodeForm` (modeless), que assume o mouse da SE com filtro de FACE. Cada clique
  soma uma face, clicar de novo na mesma face a retira (identidade = ocorrência +
  `Face.ID`), "Criar eletrodo" cria e posiciona, e a janela recomeça a coleta para o próximo.
  O que já estava selecionado entra na lista; "Usar seleção" é o modo reserva.
- Core: `ElectrodeBuilder.CreateElectrodeFromFaces(asm, params, faces)` é o miolo;
  `CreateElectrodeFromSelection` (usado pelo MCP) virou casca sobre ele;
  `TryUnwrapFace` desembrulha o item escolhido (face + ocorrência dona).
- **A validar ao vivo:** que tipo de objeto o `MouseClick` entrega ao clicar numa face de
  ocorrência na montagem. O log do 1º clique diz o tipo. Se vier a face CRUA (sem
  `.ImmediateParent`), a ocorrência sai do casamento por nome de documento — que erra em
  cavidade repetida — e o próximo passo é passar o ponto clicado (x, y, z da montagem) do
  `SePicker` para desempatar pela caixa da ocorrência.

## Fase 1 — Nova peça (construída em 2026-09-24, aguardando validação no SE)

**Como ficou** (detalhe no MANUAL, seção 4.5b): código `{molde}.{NNN}.par` no próximo número
livre da série da parte escolhida (fixa .100, móvel .200, extração .300 — a série começa no
próprio 100/200/300, como no MD-15335, que tem 15335.200); pasta da montagem; orientação da
MONTAGEM; origem no centro das faces nos eixos de planta e no ponto mais baixo/alto no eixo de
altura, que é escolhido por projeto (X, Y ou Z). Ficaram para depois: template escolhido,
"editar em contexto depois de criar" e propriedades preenchidas.

**Plano original:**

**O que faz.** Na montagem, cria uma peça nova vazia, salva na pasta do projeto e
posicionada — o equivalente do "Criar eletrodo (manual)" para qualquer componente do molde
(postiço, gaveta, calço, bucha…).

**Janela.**
- Nome (sugestão pela convenção do projeto, a confirmar), pasta (padrão: a da montagem),
  modelo `.par` (template) e ambiente inicial (síncrono/ordenado).
- **Origem**: da montagem (padrão), ou clicar numa face/ponto (centro da face, como no
  eletrodo), com a orientação herdada da ocorrência clicada (`GetMatrix`, já usado).
- Opção **"Editar em contexto depois de criar"** (ativa a ocorrência para o Carlos
  modelar direto).

**Core.** Extrair de `ElectrodeBuilder.CreateAndPlaceElectrode` uma `PartFactory`
(criar documento → `SaveAs` → `AddByFilename` → `PutMatrix`/`PutTransform`), sem nada de
eletrodo. O eletrodo passa a usar a fábrica, o que também cumpre o fatiamento do
`ElectrodeBuilder` pendente desde a revisão.

**Sondas.** Criar peça a partir de um template escolhido (`Documents.Add` com template);
ativar a ocorrência em contexto (`EditInPlaceScope` existe, mas está marcado "não usar em
produção" — é preciso reconferir).

**Perguntas ao Carlos.** Convenção de nome das peças do molde; onde fica o template;
se a peça nasce com material/propriedades (código do produto, nº do molde) preenchidos.

## Fase 2 — Refrigeração (construída em 2026-09-24, aguardando validação no SE)

**Como ficou** (detalhe no MANUAL, seção 4.5c). Respostas do Carlos: esboço 3D **na peça**; Ø
6/8/10/12; terminações cega, passante, engate e tampão. Decisões: linhas colineares = uma passada;
canal = recurso de FURO num plano normal à aresta (associativo); engate/tampão = furo roscado
coaxial com rosca de tubo lida da base de furos da SE; sobrefuro padrão Ø/2. **Ficaram para
depois:** distância mínima canal↔cavidade/canal (conferência), recurso de montagem atravessando
placas (V2), rebaixo do engate.

**1º run ao vivo (2026-09-24):** o clique numa linha entrega `Edge`; a leitura das linhas, o plano
normal à aresta e o 1º furo funcionaram (lado 1 certo, medição confirmou) — mas a aresta do esboço 3D
MORRE a cada furo criado (todo `AddNormalToCurve` seguinte deu E_FAIL). E o Carlos desenha o CAMINHO
da água, com cantos dentro da placa. Resposta, mesmo dia: plano pelo plano-base (sem aresta),
prolongamento automático dos cantos até a face medido na peça, tampão/engate automáticos, todas as
linhas ao abrir.

**A confirmar no 1º run ao vivo (original):** o que o clique numa linha de esboço 3D entrega; se
`AddNormalToCurve` aceita a aresta do esboço 3D; o lado do furo (a primeira tentativa usa
"lado 1 = −normal", observado na fixação do bloco, e a medição corrige); se a rosca de tubo liga
(`TreatmentType = 37` lido de volta).

**Plano original:**

**O que faz.** O Carlos desenha o traçado dos canais como **linhas num esboço 3D**; o botão
transforma cada linha num furo cilíndrico do Ø escolhido e trata cada **ponta** conforme o
tipo de terminal.

**Janela (modeless).**
1. **Linhas** — `SePicker` com filtro de linha/aresta (`seLocateLine`=14,
   `seLocateEdge`=31); clique a clique, ou "todas as linhas do esboço" de uma vez.
2. **Ø do canal** — lista padrão (Ø6 / 8 / 10 / 12 / 14 mm, configurável).
3. **Terminais (opcional)** — a janela lista as pontas (as compartilhadas entre duas linhas
   são cruzamentos e ficam de fora). Para cada ponta, ou para todas de uma vez:
   - **Cega com ponta de broca** (118°) — padrão;
   - **Passante**;
   - **Rosca de conexão** (engate rápido: 1/8" NPT, 1/4" NPT, G1/4, M8×1 — tabela a
     confirmar), com rebaixo para o engate;
   - **Tampão** (rosca + comprimento de tampão), para fechar furo de passagem;
   - **Sobrefuro** (prolonga além do cruzamento para garantir a interseção — padrão
     +2 mm).
4. **Conferência** — a distância mínima de cada canal até a cavidade e até outros canais
   (padrão ≥ 1,5 × Ø, configurável) é medida **antes** de cortar; canal próximo demais sai
   em vermelho.

**Ambiente.** V1 na **peça** (a placa/postiço aberto), **ordenado** — recursos editáveis na
árvore, esboço como pai do corte (mesma regra do O'ring). V2: na **montagem**, como recurso
de montagem que atravessa várias placas (`AssemblyFeaturesExtrudedCutouts`/`...Holes` com
lista de peças afetadas — existe no dump, nunca foi exercitado).

**Modelagem, em ordem de preferência.**
1. `Holes` com `HoleData` (furo de broca com ponta, rosca como feature), num plano normal
   à linha no ponto inicial (`RefPlanes.AddNormalToCurve`, já usado no O'ring). Resolve
   cego/passante/rosca num recurso só, e é o que o Carlos reconhece na árvore.
2. Reserva: `SolidSweptCutouts.Add(target, toolBody, path, …)` — varredura de um corpo
   ferramenta ao longo do caminho (está no dump). Serve para traçado que não é reto.

**Sondas.** (a) Ler a geometria das linhas de um esboço 3D: `Sketch3DFeatures` existe, mas
a interface não expõe as linhas — o caminho provável é `Sketch3DFeature.Edges[igQueryAll]`
+ `Edge.GetEndPoints` (já usado em `EdgeGeometry`). (b) O que o `MouseClick` entrega ao
clicar numa linha de esboço 3D. (c) Furo ao longo de um eixo qualquer (fora de Z).

**Perguntas ao Carlos.** Tabela Ø do canal ↔ rosca do engate ↔ tampão que ele usa; a
distância mínima canal–cavidade e canal–canal; se os canais costumam atravessar várias
placas (decide o quanto a V2 é urgente).

## Fase 3 — Canais de alimentação

**O que faz.** Igual à refrigeração, a partir de linhas/curvas de esboço, mas cortando o
canal de alimentação no **plano de partição**.

**Janela.** Linhas (como na fase 2); **seção** — circular (metade em cada lado da
partição), trapezoidal, meia-cana, parabólica; dimensões pela seção (Ø, ou largura ×
profundidade × ângulo, padrão 5–10° por lado no trapezoidal); **lado** (fixo, móvel, os
dois); pontas: **poço frio** (prolonga 1–1,5 × Ø) e **puxador de canal** (a detalhar).

**Modelagem.** Circular: o mesmo motor da fase 2, dividido pela partição (duas peças).
Não circular: varredura do perfil ao longo do caminho (`SweptCutouts`, está no dump, nunca
exercitado) — **sonda obrigatória antes**.

**Core puro.** Dimensionamento de seção (diâmetro hidráulico equivalente para comparar
circular × trapezoidal), com teste.

**Perguntas ao Carlos.** Quais seções ele usa de fato; se o canal fica sempre nos dois
lados ou só no móvel.

## Fase 4 — Pontos de injeção

**O que faz.** Clicar no ponto de injeção (na peça/cavidade ou na ponta de um canal da
fase 3), escolher o **tipo** e a **posição**, e cortar a geometria do ponto de injeção.

**Janela.**
- **Ponto** — `SePicker` com filtro de ponto/ponto-chave (`seLocatePoint`=13,
  `seLocateKeyPoint`=65) ou a ponta de um canal.
- **Tipo** — lateral (borda), submarino (túnel), banana (cashew), ponto (pin-point, 3
  placas), leque, direto (bucha), bico quente (só o alojamento). Cada tipo tem um perfil
  paramétrico próprio.
- **Posição** — direção de entrada (normal da face clicada ou eixo escolhido), ângulo
  (submarino: 30–45° com a vertical, cone 15–20°), dimensões do ponto (largura ×
  espessura × comprimento de "land", ou Ø do ponto).
- **Conferência** — o ponto de injeção não pode cruzar canal de refrigeração nem extrator.

**Core puro.** Uma classe por tipo, gerando o perfil + a direção a partir de (ponto,
normal, parâmetros), com teste. É onde vivem as regras de dimensionamento (espessura do
ponto de injeção como fração da espessura da peça, etc.) — **valores a confirmar com o
Carlos**; nenhum entra no código sem ele.

**Sondas.** Corte revolvido/cônico inclinado para o submarino (a coroa do O'ring já
resolveu plano normal a uma aresta — reaproveitar).

## Fase 5 — Extratores

**O que faz.** Configura os extratores, **propõe** a posição de cada um com a melhor
distribuição possível — longe dos canais de refrigeração e das paredes — e aplica os furos
nas placas.

**Janela.**
1. **Configurar** — série de pinos (Ø2–Ø16, catálogo DIN ISO 8694 / 6751 em tabela
   editável como a do O'ring), folga do furo, rebaixo da cabeça, placas atravessadas
   (postiço macho, placa porta-macho, placa suporte, placa porta-extratores).
2. **Identificar** — a região extraível: a projeção, no plano da extração, das faces do
   macho voltadas para a abertura; regiões críticas (nervuras, ressaltos, cantos fundos)
   ganham peso maior.
3. **Distribuir (automático)** — região admissível = região extraível **menos** faixas
   proibidas:
   - canal de refrigeração: distância ≥ folga mínima (padrão ≥ Ø do canal, a confirmar);
   - parede da cavidade/degrau: ≥ raio do pino + folga;
   - borda da placa, parafusos, colunas-guia, outros furos.

   Dentro dela, escolhe pontos pelo critério **maximin** (o próximo pino é o ponto mais
   longe de todos os já escolhidos e de todas as faixas proibidas), priorizando as
   regiões críticas, até a quantidade/densidade pedida. O Carlos arrasta, apaga ou soma
   pinos na lista antes de aplicar.
4. **Aplicar** — furos (e rebaixos) em cada placa, na montagem.

**Core puro.** Raster 2D da região + transformada de distância — **é o mesmo motor do
nível 2 da análise de usinabilidade** (já planejado em `PROJECT_STATE.md`), por isso esta
fase vem depois dele. O maximin sobre o raster é teste de unidade puro, sem CAD.

**Sondas.** Furo de montagem atravessando várias placas (`AssemblyFeaturesHoles`);
localizar os canais de refrigeração existentes (se foram criados pela fase 2, pelo nome do
recurso; senão, pelas faces cilíndricas da placa).

**Perguntas ao Carlos.** Catálogo de pinos usado (fornecedor); folgas mínimas
pino–canal e pino–parede; se o furo de montagem pode atravessar várias placas numa
feature só ou se ele prefere um furo por peça.

## Fase 6 — Gavetas

**O que faz.** Configura e aplica o **alojamento de gaveta**: a cavidade na placa onde a
gaveta corre, o furo do pino inclinado e o assento do calço/cunha de travamento.

**Janela.**
- **Seleção** — as faces da contrassaída (a região que a gaveta forma) e a **direção de
  saída** (clique numa face/aresta).
- **Cálculo** (Core puro, com teste):
  - curso = profundidade da contrassaída + folga (padrão 2–3 mm, a confirmar);
  - ângulo do pino inclinado α (padrão 15–25°);
  - comprimento útil do pino = curso / sen α + folgas;
  - ângulo do calço/cunha = α + 2° (a cunha trava antes do pino — regra usual, a
    confirmar);
  - dimensões da gaveta e do alojamento (guias, folga lateral, placa de desgaste).
- **Aplicar** — cavidade do alojamento, furo inclinado do pino e assento da cunha; opção
  de criar a **peça da gaveta** pela fase 1, já posicionada.

**Sondas.** Furo inclinado em ângulo qualquer; bolsão com guias em rabo-de-andorinha/T
(provavelmente perfil extrudado — receita existente).

**Perguntas ao Carlos.** Ângulos e folgas que ele usa; se usa gaveta padrão de catálogo
(unidade comprada) ou desenha cada uma; qual o acionamento mais comum (pino inclinado,
cilindro hidráulico).

---

## Onde isto entra no resto do projeto

- A Fase 5 depende do **nível 2 da usinabilidade**, que continua sendo a próxima ação do
  lado de eletrodos. Fazer o nível 2 primeiro serve às duas frentes.
- A Fase 1 cumpre parte da dívida de fatiamento do `ElectrodeBuilder` (`PartFactory`).
- A pendência de validação ao vivo que já existe (Aplicar GAP, Duplicar eletrodo, Lista de
  corte) não é bloqueada por nenhuma fase daqui.
