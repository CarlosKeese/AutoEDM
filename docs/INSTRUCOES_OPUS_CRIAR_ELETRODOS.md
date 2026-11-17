# Instruções para o Opus 5.5 — "Criar Eletrodos", etapa 2

Você vai CONSTRUIR três coisas no fluxo novo do "Criar Eletrodos" do AutoEDM. Quem avalia e corrige o
seu trabalho é outro modelo (Fable), numa sessão à parte; quem decide é o Carlos, o dono do projeto e
ferramenteiro. Este arquivo é tudo o que você recebe: leia-o inteiro antes de abrir código.

## 1. Papéis e limites

- **Você constrói**: código, testes, sondas no Solid Edge, documentação do que mudou.
- **Não faça commit nem push.** Deixe tudo na árvore de trabalho para a avaliação.
- **Não decida no lugar do Carlos.** Onde este arquivo diz "decisão do Carlos", implemente o valor
  padrão indicado, deixe-o como parâmetro e liste a pergunta no relatório.
- **Economia de tokens é requisito.** Sem subagentes e sem workflows. Não leia arquivos grandes
  inteiros (`PROJECT_STATE.md`, `README.md`, `docs/MANUAL.md`, `ElectrodeBuilder.cs`,
  `SurfaceBlockBuilder.cs`): use Grep e leia trechos. Filtre a saída de build, teste e log
  (o script de ensaio tem `-Quiet`). Capturas de tela com no máximo 1000 px de largura, só para
  conferir um resultado.
- **Só escreva na cópia de teste.** O Solid Edge do Carlos fica aberto com projetos REAIS (hoje:
  `W:\09 - Projetos\...\15335.asm`) e a ponte sobe com a escrita liberada. Antes de qualquer
  ferramenta que altere algo, rode `se_status` e confira que o documento ativo está em
  `C:\Users\carlos.keese\Downloads\MOLDE TESTE\`. Se não estiver, abra a peça de teste
  (`se_abrir_documento`) — nunca feche, salve nem altere um documento fora dessa pasta.

## 2. O que já existe (não refaça)

Estado e histórico: `docs/PLANO_CRIAR_ELETRODOS.md` (leia as seções 0, 3 e 6: é curto).

O fluxo é `ElectrodeBuilder.CreateSmart`, chamado pelo botão da ribbon e pela ferramenta MCP
`se_criar_eletrodos`:

| Arquivo (`src/AutoEDM.Core/Electrode/`) | O que faz |
|---|---|
| `ElectrodeBuilder.Smart.cs` | Orquestra: montagem de erosão → varredura da queima → plano → construção de cada eletrodo |
| `EdmAssemblyBuilder.cs` | Peça → montagem `<código>_EDM.asm`, origem na face de fechamento e no centro da peça |
| `SmartElectrodes.cs` | Lógica PURA do plano: unir regiões que cabem numa barra, dobrar geometria idêntica |
| `ElectrodeTipBuilder.cs` | A ponta sólida: prisma sobre a pegada − corpo da peça (duas booleanas) |
| `SurfaceBlockBuilder.cs` (`PlanForFootprint`, `BuildOnPlan`) | Faixa de medição + bloco + fixação |
| `../Machinability/BRepRadiusProbe.cs` (`IsConcave`), `SharpCornerProbe.cs` | Raios e cantos que a fresa não faz |

Testes da lógica pura: `tests/AutoEDM.Core.Tests/SmartElectrodePlannerTests.cs`. Hoje são 739 testes
passando; termine com todos passando.

Resultado atual na peça de teste `15428.100.par`: 105 faces rosa + 16 roxas não fresáveis, 6 regiões,
1 eletrodo unido de 33,7 × 79,9 mm (ou 6 com `"unir": false`), sólido de 14.346,6 mm³ com faixa, bloco e
furos. Sistema de coordenadas da peça do eletrodo: origem no ponto mais fundo da queima, centro da
pegada em X e Y, **+Z para cima** (a ponta fica de Z = 0 até o plano de fechamento, em Z = `RefZ`;
acima vêm pescoço, faixa e bloco).

## 3. Como trabalhar no Solid Edge sem gastar rodadas

Regras do projeto que valem sempre (estão no `CLAUDE.md`, que você recebe sozinho): nenhuma assinatura
COM é inventada; COM em metros e radianos; nada troca o ambiente síncrono/ordenado no meio de uma
operação. Use a skill `solid-edge-com` — em especial o fim de `references/modeling-recipes.md`
(booleana com corpo copiado, côncavo x convexo) e, em `references/discovery.md`, a seção
"With the AutoEDM bridge up".

| Preciso de… | Use |
|---|---|
| Saber se um membro existe e a assinatura real | `python .claude/skills/solid-edge-com/scripts/apiq.py sig "Tipo.Membro"` e `se_com_ler` com `"assinaturas": [...]` no objeto vivo |
| Ler qualquer coisa do modelo | `se_com_ler` (caminho: `doc.Models.Item(1).Body.Volume`) |
| Experimentar uma chamada | `se_com_chamar`, numa peça NOVA de teste |
| Ver o resultado | `se_captura` |
| Rodar código novo do Core SEM reinstalar | `powershell -ExecutionPolicy Bypass -File tools\se-harness.ps1 -Mode Write -Quiet -Tool 'se_criar_eletrodos::{"confirmar":true}'` (compile o add-in antes) |
| Instalar de verdade (fecha e reabre o SE) | `powershell -ExecutionPolicy Bypass -File tools\ciclo-se.ps1 -Descartar` — só no fim de cada tarefa, e só se os documentos abertos forem os de teste |

Pela ponte, `se_criar_eletrodos` só aceita `confirmar` enquanto o servidor MCP não for reinstalado
(isso exige o Claude Code fechado; não faça). Pelo script de ensaio valem todos os argumentos
(`unir`, `margemMm`, `folgaUniaoMm`, `roxoNaoFresavel` e os que você criar).

Entre uma rodada e outra, limpe: feche sem salvar a `15428.100_EDM.asm` e os `..._EDM_EE*.par`
(`se_fechar_documento` com `"descartar": true`), apague `15428.100_EDM.asm` e
`Eletrodos\15428.100_EDM_EE*.par` da pasta de teste e deixe `15428.100.par` ativa. Uma montagem
`_EDM` que já existe é reaberta em vez de recriada, então sem a limpeza você testa o arquivo velho.

Armadilhas já medidas neste trabalho (não as redescubra):

- `GNTTypePropertyConstants`: igPlane = −1909484335, igCylinder = −114972029.
- Só `SolidEdgeGeometry.Body[]` (o `Constructions.Item(n).Body`) é aceito como ferramenta da
  `BooleanFeatures.Add`; as outras seis rotas falham (tabela no plano, seção 3).
- `View.SaveAsImage` não grava `.png`; `View.Fit` marca o documento como alterado.
- O `SaveAs` da peça do eletrodo é feito por `app.ActiveDocument`, porque os furos re-adquirem o documento.
- A SE só carrega o add-in (e a ponte) quando o primeiro documento abre.
- Em comando Bash, texto com apóstrofo dentro de heredoc já quebrou o comando: grave scripts com a
  ferramenta de escrita de arquivo e rode o arquivo.

## 4. Tarefas, na ordem

Faça uma tarefa por vez e entregue o relatório dela (seção 5) antes de começar a seguinte.

### T1 — Pescoço reto (defeito de construção visto pelo Carlos)

**O defeito.** Na vista lateral do eletrodo aparecem entalhes e degraus logo acima do plano de
fechamento. A causa está em `ElectrodeTipBuilder`: o pescoço (1 mm entre o fechamento e a faixa) é
feito subtraindo de um prisma a peça DESLOCADA 1 mm para cima. Isso não extruda o contorno da boca:
copia para cima a camada de 1 mm logo abaixo do fechamento. Tudo o que a peça tem nesse primeiro
milímetro — quebra de canto, degrau raso, canal de 0,3 a 1,0 mm, ângulo de saída — reaparece 1 mm
acima, e onde há um detalhe mais raso que 1 mm o pescoço fica com um vazio.

**O que se quer.** Acima do fechamento, o eletrodo sobe RETO: a seção da ponta no plano de fechamento
(o contorno da boca do bolsão dentro da pegada), extrudada na vertical até a base da faixa. O motivo
do pescoço continua valendo: a faixa e o bloco não podem encostar na face de fechamento.

**Como.** A ponta sozinha (um prisma do fundo da queima até `RefZ`, menos a peça) já termina numa
face plana em `RefZ` com exatamente esse contorno. Falta subir essa face. Sonde no SE, nesta ordem, e
fique com a primeira rota que der sólido único e reto:

1. perfil feito das arestas da face de topo (projeção/inclusão de aresta num perfil no plano
   `RefZ`) + protrusão finita — a receita de perfil + `AddFiniteExtrudedProtrusion` validada está em
   `BlankModeler.CreateBox`;
2. mover a face de topo para cima em síncrono (`Model.SyncLinearMove` ou `FaceMoves`);
3. só se as duas falharem: manter a booleana, mas com a cópia da peça esticada em Z
   (`CopyConstructions.Add` tem `ZScale`) para que a camada copiada seja de micrômetros.

Remova a booleana do pescoço se a rota 1 ou 2 servir (uma booleana a menos por eletrodo).

**Aceite, medido no eletrodo unido do `15428.100`:**
- um corpo só (`Models.Count == 1`) e `Status` OK em cada feature criada;
- toda face do corpo com Z entre `RefZ` e `RefZ + 1` é vertical (componente Z da normal ≈ 0),
  tirando a face que encosta na faixa;
- volume do pescoço = área da seção em `RefZ` × 1 mm, com erro menor que 0,5 %;
- uma captura da vista lateral sem entalhes.

### T2 — Dividir o eletrodo que a fresa não faz

**Regra do Carlos (2026-11-12):** quando o eletrodo tem raio interno pequeno demais para a fresa, o
ideal é DIVIDIR em mais eletrodos, **mesmo que o resultado na erosão sejam cantos vivos**. Ou seja:
a aresta do aço que tinha um raio pequeno pode sair viva, feita por dois eletrodos que se encontram ali.

**O que impede a fresa.** `BRepRadiusProbe.Probe` no corpo do eletrodo devolve as faces CÔNCAVAS
(cilindro ou toro) de raio menor que o da menor fresa, ou fundas demais; `SharpCornerProbe` devolve os
cantos vivos internos. Só contam os que ficam abaixo do plano de fechamento
(`DescribeElectrodeMachining` já filtra). No eletrodo unido do `15428.100` são 26 (menor R 0,1).

**A ideia.** O eletrodo nasce de "prisma sobre uma pegada retangular − peça". Dividir é PARTIR A
PEGADA e construir cada pedaço pelo mesmo caminho. Um corte vertical que passa por um raio interno
transforma esse raio em duas quinas externas, uma em cada pedaço — e quina externa a fresa faz.

**Algoritmo proposto** (lógica pura, numa classe nova ao lado de `SmartElectrodePlanner`, com testes):

1. Para cada achado, a linha de corte em planta: raio de eixo horizontal (pé de degrau) → a reta do
   próprio eixo; canto de eixo vertical → uma reta por ele, na direção de uma das paredes vizinhas.
   Trate primeiro o caso alinhado a X ou a Y; achado inclinado fica sem corte e vai para o relatório.
2. Junte os cortes colineares (tolerância ~0,05 mm). O conjunto de cortes em X e em Y parte a pegada
   numa grade de células.
3. Reúna células vizinhas enquanto nenhum achado ficar no INTERIOR do pedaço reunido (achado em cima
   de uma divisa está resolvido) e a pegada reunida couber numa barra. O objetivo é o menor número de
   pedaços.
4. Descarte a célula que não toca nenhuma face de queima.
5. Cada pedaço passa pelo mesmo `BuildSmartElectrode`, com a sua pegada. Confira de novo a
   usinabilidade de cada pedaço construído; se sobrar achado, repita uma vez com os achados novos e,
   se ainda sobrar, marque o pedaço como "corte a fio" no relatório (não implemente o fio agora).

Decisões do Carlos (use o padrão, deixe como parâmetro, pergunte no relatório): largura mínima de um
pedaço (padrão 2 mm; abaixo disso não divide e marca "fio"); número máximo de pedaços por eletrodo
(padrão 12).

**Aceite, no `15428.100`:**
- o plano (sem `confirmar`) mostra os pedaços, a pegada e a barra de cada um, e quantos achados cada
  corte resolve;
- construído, cada pedaço é um corpo só e sai "fresável", ou vem marcado "corte a fio" com o motivo;
- a soma dos volumes das pontas dos pedaços bate com o volume da ponta do eletrodo inteiro (erro
  menor que 0,5 %): nenhuma parte da queima ficou sem eletrodo e nada foi contado duas vezes;
- testes da lógica pura cobrindo: um achado → dois pedaços; achados colineares → um corte só; células
  sem achado no meio reunidas; célula sem queima descartada; pedaço abaixo da largura mínima.

### T2b — O furo da fixação não pode estourar (resposta do Carlos, 2026-11-12)

O furo M6 (e os pinos Ø4) **podem passar** da faixa para dentro do pescoço e da ponta, mas **não podem
estourar** o cobre. Com a barra deitada o bloco fica fino (5,85 mm no eletrodo unido) e a broca já
chega a 0,35 mm do fechamento; num pedaço pequeno da T2 ela pode sair por baixo ou pelo lado.

Depois da fixação, confira em cada eletrodo que cada furo continua cego e inteiro (por exemplo: a face
cônica da ponta da broca está completa — área igual à do cone teórico — e a parede do furo não foi
cortada). Se estourar: encurte a profundidade até não estourar e avise no relatório do eletrodo; se
nem assim couber, deixe sem aquele furo e avise.

Aceite: no `15428.100`, com e sem divisão, nenhum eletrodo com furo estourado; um teste (ou uma
medição registrada) de um caso em que o furo teria estourado e foi encurtado.

### Respostas do Carlos às perguntas da T1 (2026-11-12)

- Furo M6 dentro do pescoço: pode passar, não pode estourar (virou a T2b).
- Região toda no fundo de um bolsão maior, com o pescoço saindo com a pegada inteira: "acho que serve";
  ele não achou a pergunta clara. Mantenha como está e mostre o caso com uma captura no relatório.
- Topo do aço: quando o ponto mais alto da peça não for a face de fechamento, use direto a face pintada
  "10 - FECHAM." (a mesma que `EdmAssemblyBuilder` acha). Implemente junto com a T2: é pequeno.

### T2c — Correções da T2 (avaliação de 2026-11-13)

A T2 entregou a mecânica (achados tirados da peça, cortes, grade, reunião, volume conservado, testes) e
foi aceita nessa parte. O RESULTADO não foi aceito: no `15428.100` saíram 9 pedaços e **nenhum fresável**,
todos marcados "corte a fio", e isso ficou ligado por padrão. Dois motivos, medidos pelo próprio relatório:
13 dos 39 achados depois da divisão são o **enchimento** do raio cortado, que continua num dos pedaços;
14 dos 26 raios da peça **não têm corte** (canto com 2° de saída, eixo pouco fora de Z ou de X/Y, arco
que não é um quarto alinhado).

**C1 — Tirar o enchimento.** A regra do Carlos já responde: a aresta do aço pode sair VIVA. O enchimento é
a lasca de cobre entre a divisa e a superfície do raio; ela cabe inteira na caixa envolvente da face do
raio, e o resto dessa caixa é aço (não é eletrodo). Portanto: no pedaço que ficou com o enchimento, remova
a caixa da face do raio (recortada ao pedaço). `DeleteFaces`/`DeleteBlends` já deram E_FAIL — não insista.
Sonde, nesta ordem: recorte extrudado com perfil retangular (a mesma receita de perfil do
`BlankModeler.CreateBox`, com o método de recorte finito que a API tiver — confira a assinatura no objeto
vivo); se falhar, uma caixa como corpo de construção subtraída por `BooleanFeatures.Add` (a rota de
`ElectrodeTipBuilder.Subtract`). Atenção à lição da T2: face de ferramenta coplanar ou tangente à peça
derruba a booleana; passe a caixa ~0,001 mm para dentro do aço e nunca deixe face dela coincidir com face
que deve ficar.

**C2 — Só dividir quando resolve.** O padrão passa a ser: aplicar a divisão apenas se, depois dela (e da
remoção do enchimento), TODOS os pedaços saírem fresáveis. Se não, entregar o eletrodo INTEIRO, com a nota
"a divisão não resolve: N raios sem corte possível" e a marca "corte a fio". O argumento `dividir` continua
existindo para forçar (true) ou proibir (false); sem ele vale essa regra automática. No PLANO (sem
`confirmar`) a decisão sai da previsão pela peça; na construção, a conferência no eletrodo construído
manda — se um pedaço construído não sair fresável, desfaça os pedaços daquele eletrodo e construa o inteiro.

**C3 — Canto com saída: medir, não decidir.** A regra para os 14 raios sem corte é decisão do Carlos e ainda
não veio. Deixe pronta como parâmetro DESLIGADO por padrão (`toleranciaAlinhamentoGraus`, padrão 0): com
tolerância > 0, um raio cujo eixo foge de Z ou de X/Y até esse ângulo é tratado como alinhado — o corte
vertical passa pela parede na altura do plano de fechamento e o enchimento sai pela caixa da face (C1) nos
dois pedaços vizinhos. Rode o `15428.100` com 3° e relate: quantos dos 14 passam a ter corte, quantos
pedaços, quais saem fresáveis, a maior lasca que sobra entre a divisa vertical e a parede inclinada
(espessura em mm), e uma captura de um pedaço. Não ligue por padrão.

**Aceite da T2c, no `15428.100`:**
- sem argumentos: sai o eletrodo inteiro com a nota de C2 (hoje a divisão não resolve), e não 9 pedaços;
- com `"dividir": true`: nenhum achado "enchimento na divisa" sobra depois de C1 (eram 13); liste por
  pedaço os achados que restam e o motivo;
- com `"dividir": true, "toleranciaAlinhamentoGraus": 3`: os números pedidos em C3;
- soma dos volumes das pontas dos pedaços = volume da ponta inteira − volume dos enchimentos removidos
  (mostre os três números);
- testes novos da lógica pura: decisão automática (divide só se todos fresáveis), caixa do enchimento
  recortada ao pedaço, tolerância de alinhamento; todos os testes do projeto passando, tirando
  `Catalog_WritingToolsAreExactlyTheExpectedSet`, que falha por uma ferramenta de outra sessão
  (`se_desenho_dobra`) — não mexa nele.

### T2c — avaliação (2026-11-13): ACEITA

Conferido por fora: build limpo, 766/767 testes, padrão = eletrodo inteiro com a nota de C2,
`"dividir": true` = 8 pedaços sem enchimento (Σ pontas 11.761,51 mm³), só 1 fresável. Ângulo de tolerância,
lasca de 0,056 mm, máximo de pedaços e o toro de canto de rebaixo são decisões do Carlos: **não mexa na
divisão** até virem. Três pendências pequenas da T2c entram na T3 (abaixo).

### T3 — GAP dentro do fluxo

**Antes do GAP (pendências da T2c, pequenas):**
1. `ToolCatalog`: o schema de `se_criar_eletrodos` não tem `dividir` (true/false/ausente),
   `toleranciaAlinhamentoGraus`, `larguraMinimaMm`, `maxPedacos`, `profFuroM6Mm`; com
   `additionalProperties:false` a ponte os recusa. Acrescente SÓ nessa entrada (o arquivo tem edições de
   outra sessão). `sobreposicaoDivisaMm` é de ensaio: não publique. Na T3, o que virar argumento novo entra
   no schema na mesma hora.
2. No plano, a linha do pedaço conta "N raio(s) sem corte" diferente do que a construção mede (E01: plano 2,
   construído 3). Use a mesma contagem (`DescribeRemaining`) nos dois.
3. `BuildPiecesOrWhole` (desfazer os pedaços e construir o inteiro) nunca rodou no SE. Force o caminho uma
   vez (ensaio, sem deixar gancho no código de produção) e relate se a montagem fica limpa: sem ocorrências
   nem arquivos dos pedaços desfeitos.

Mantenha o repositório compilando a cada passo (a sessão pode ser cortada pelo limite de gasto).

**O que falta.** O eletrodo sai no tamanho da cavidade. Ele tem de sair SUBDIMENSIONADO: as faces
que queimam recuam para dentro o valor do GAP, que depende do Ra (`RaOffsetTablePolicy` em
`IOffsetPolicy.cs`: Ra 1,6 → 0,10 mm; Ra 3,2 → 0,20 mm). O Ra do eletrodo já está em `SmartElectrode.Ra`.

**O que já existe.** `SurfaceBlockBuilder.ApplyGapToUnitedSurfaces` aplica `FaceOffsets.AddEx`, que
só existe em ORDENADO, e `FinalizeToOrdered` troca o ambiente de uma peça recém-criada. A regra do
projeto proíbe trocar o ambiente de um documento do usuário; uma peça que a própria ferramenta acabou
de criar é a exceção. A troca mata as referências de face: reencontre as faces DEPOIS da troca, pela
geometria.

**Quais faces recuam.** As que vieram da peça: tudo abaixo do plano de fechamento, menos as faces
planas verticais que são o corte do prisma (as laterais da pegada e, depois da T2, as divisas entre
pedaços). Pescoço, faixa e bloco não recuam.

**Como.** Sonde primeiro se há recuo de faces em síncrono (evitaria a troca); se não houver, construa
tudo em síncrono, troque para ordenado no fim e aplique o `FaceOffset`. Grave o Ra (`RaVariableStore`,
já é chamado) e nomeie a feature como o código existente faz ("GAP: g - Ra: r").

**Aceite:**
- feature de GAP com `Status` OK em cada eletrodo;
- uma medida conferida antes e depois: a largura de uma nervura da ponta diminui 2 × GAP e a
  profundidade diminui 1 × GAP, com erro menor que 0,005 mm;
- o relatório final de `se_criar_eletrodos` diz o GAP aplicado em cada eletrodo;
- as divisas da T2 continuam planas e no mesmo lugar (dois pedaços vizinhos não se sobrepõem);
- rode os dois casos no `15428.100`: padrão (eletrodo inteiro) e `"dividir": true` (8 pedaços);
- a conferência de fresabilidade (`DescribeElectrodeMachining`) e a do furo continuam funcionando depois da
  troca para ordenado; diga se os raios medidos mudam com o GAP (raio interno do eletrodo cresce ou encolhe?);
- o tempo total de cada execução (a troca de ambiente por peça custa; relate).

### T3b — GAP: mudar a ORDEM, não insistir na rota (avaliação de 2026-11-13)

A T3 aplicou o `FaceOffsets.AddEx` no eletrodo PRONTO (ponta + pescoço + faixa + bloco + furos) e só vingou
em 2 de 9; em 2 cortou o corpo por um plano. Falha com 1 face plana e 0,001 mm: não é raio que colapsa, é o
corpo. Hipótese do avaliador (a conferir, não a assumir): o recuo de PARTE das faces de um corpo composto é
frágil — parede da ponta fundida com a parede do pescoço (o pescoço sobe reto das arestas do topo, faces
coplanares se fundem) explica o "corte por um plano"; o resto do corpo (bloco, furos) explica o
igFeatureFailed. As pendências 1–3 da T2c foram aceitas.

**Passo 1 — achar o estágio que quebra (no máximo 5 sondas, eletrodo INTEIRO do `15428.100`).** Salve uma
cópia da peça do eletrodo depois de cada estágio (só ponta; + pescoço; + faixa/bloco; + furos), troque a
CÓPIA para ordenado e tente o recuo de 0,10 mm (a) na face do piso mais fundo e (b) em todas as faces abaixo
do plano de fechamento. Relate a tabela estágio × (a)/(b) com status e volume tirado. As cópias ficam na
pasta de teste e são apagadas no fim.

**Passo 2 — rota nova, se a ponta sozinha aceitar o recuo:** GAP logo depois da booleana da ponta, ANTES do
pescoço: peça nova em síncrono → ponta → troca para ordenado → `FaceOffsets` → volta para síncrono
(`ModelingMode = 1`; confira que a feature fica embutida no corpo e que o volume não muda na volta) →
pescoço, faixa, bloco e furos como hoje. Nesse momento o corpo é só a ponta, e o conjunto a recuar é simples:
**todas as faces menos a do topo** (plano de fechamento). Isso inclui as laterais da pegada e as divisas:
elas também soltam faísca, e a divisa que coincide com parede de aço PRECISA recuar (responde a sua pergunta
1; a regra antiga "divisa não recua" cai). O pescoço passa a subir do contorno do topo já recuado, até a
mesma cota de antes. Se voltar para síncrono não der, deixe a peça em ordenado e diga o que dos passos
seguintes precisa mudar — sem `ProfileSets.Add()` solto (regra do projeto) e sem reescrever o `BlankModeler`
antes de eu avaliar.

**Passo 3 — se nem a ponta sozinha aceitar:** uma sonda só de cada alternativa, e pare: (i) recuo no lado do
AÇO: cópia temporária do arquivo da cavidade, ordenado, `FaceOffsets` de +GAP nas faces de queima, e a
booleana da ponta usa essa cópia; (ii) outra coleção de recuo que a API viva mostrar no `Model`
(`apiq.py`, nada inventado). Relate e pare.

**Raio menor ou igual ao GAP** (raio convexo do eletrodo que zera com o recuo): relate o que o SE faz nas duas
variantes de BlendRecreation; não trate ainda.

**Aceite (igual ao da T3, com estas mudanças):** `aplicarGap` só vira padrão se der OK nos 9 eletrodos
(inteiro + 8 pedaços); divisas continuam PLANAS e recuam 1 × GAP (vão de 2 × GAP entre vizinhos); fundo sobe
1 × GAP; furos e conferência de usinabilidade depois do GAP; a guarda de volume continua. Fresabilidade:
relate antes e depois do recuo, sem mudar a regra de decisão. Limite: se o passo 2 falhar em algum eletrodo,
relate qual e por quê e pare — não empilhe remendos.

### T3c — GAP: mapa face a face (avaliação da T3b, 2026-11-13)

T3b aceita como diagnóstico: o recuo falha já na ponta sozinha, até com 1 face; no aço, 1 face plana rosa
aceita e as 105 juntas não. A hipótese do corpo composto caiu. Hipótese nova (a medir): é geometria local —
face vizinha TANGENTE (raio) que não acompanha, e/ou raio convexo do eletrodo ≤ GAP que zera. Só sonda, numa
CÓPIA do eletrodo inteiro pronto trocada para ordenado; nada de código de produção até o item 4.

1. **Mapa individual.** Para cada face abaixo do fechamento (182): `AddEx` de 0,10 mm só nela, status e
   volume, apaga a feature, reenumera. Relate OK/falha por tipo (plano, cilindro, cone, toro, outro) e, das
   que falham, quantas têm vizinha tangente (use o que a API viva der: `Face.Continuity`/arestas; se não der,
   "vizinha é cilindro ou toro de R ≤ 0,5").
2. **Limiar de distância.** O conjunto todo com 0,001, 0,01, 0,05 e 0,10 mm, nas duas variantes de
   BlendRecreation no valor que primeiro falhar. Relate o menor raio CONVEXO do eletrodo no conjunto (côncavo
   no aço). Passar em valor pequeno e falhar em 0,10 = raio que zera.
3. **Grupos tangentes.** Feche cada face com todas as ligadas a ela por tangência (cadeia) e recue grupo a
   grupo, em sequência, reenumerando entre um e outro. Relate nº de grupos, quantos vingam, volume total
   tirado contra área × GAP, e o que sobra sem recuar (tipo, raio, onde).
4. **Só se o item 3 der 100 %:** troque em `ElectrodeGap` a chamada única pelo laço por grupos, mantenha a
   guarda de volume, e rode os 9 eletrodos (inteiro + 8 pedaços) com `aplicarGap`. Continua desligado por
   padrão; eu avalio antes de ligar. Se não der 100 %, pare e relate — não remende.

Limite: 4 execuções de sonda no SE, mais as do item 4 se ele acontecer.

**Informação do Carlos (2026-11-13), que muda a T3c:** no Solid Edge o GAP costuma dar erro quando o recuo
ELIMINA um raio; no processo manual ele **exclui esses raios antes** de aplicar o GAP. Portanto a rota é:

5. **Achar os raios que somem.** No eletrodo o recuo é para dentro: raio CONVEXO do eletrodo (côncavo no aço)
   encolhe r − GAP e some quando r ≤ GAP (use folga de 0,005 mm); raio côncavo do eletrodo cresce e não some.
   Liste-os (face, tipo, raio, onde) — o item 2 já mede o menor.
6. **Removê-los antes do GAP** (a aresta fica viva; a faísca devolve um raio ≈ GAP no aço, é o esperado).
   Sonde, nesta ordem, com assinatura do `apiq`/objeto vivo: (a) excluir as faces do raio em ORDENADO com
   cicatrização (`DeleteFaces`/`DeleteBlends` — o E_FAIL da T2 foi noutra situação; confira ambiente e
   argumentos antes de descartar); (b) o mesmo na CÓPIA do aço antes da booleana da ponta (raio côncavo do
   aço), se no eletrodo não der; (c) o que o SE oferece em síncrono para excluir arredondamento, se (a) e (b)
   falharem. Depois de remover, repita o recuo do conjunto (e, se preciso, por grupos tangentes do item 3).
7. Se 5–6 fizerem o recuo vingar no eletrodo inteiro: implemente em `ElectrodeGap` (remover raios que somem →
   recuar), rode os 9 eletrodos com `aplicarGap`, relate por eletrodo quantos raios foram removidos. Continua
   desligado por padrão até eu avaliar. Se não vingar, pare e relate o que o mapa mostrou.

O recuo existente ("Aplicar GAP") já pinta as faces recuadas: mantenha esse comportamento na rota automática.

### T3c — avaliação (2026-11-13): GAP automático SUSPENSO

Medido: `DeleteBlends.Add` em ORDENADO exclui raio (12 de 14 convexos ≤ GAP; o E_FAIL da T2 era em
síncrono), mas o recuo do conjunto segue em igFeatureFailed com e sem eles, até em 0,001 mm; cones, esferas
e toros falham todos, sozinhos. `aplicarGap` fica desligado. **Não sondar mais o GAP** até o Carlos dizer se,
à mão, o "Aplicar GAP" vinga nesta mesma ponta e o que ele exclui antes (só raios ≤ GAP, todos os raios, a
moldura do rebaixo com planos de 0,01 mm, as esferas de canto).

### T5 — Separar por REGIÃO (rascunho; aguarda confirmação do Carlos, 2026-11-16)

O Carlos clicou o botão no add-in novo (aprovou: "baita avanço") e SELECIONOU no `15428.100_EDM_EE01.par`
as 86 faces que impedem fabricar o eletrodo inteiro. Lidas por `se_medir_selecao` (mm, referencial do
eletrodo; fechamento em Z 7,149; a seleção inclui o trecho do pescoço acima de cada região):

| Região | Caixa em planta (x; y) | Z da ponta | O que é |
|---|---|---|---|
| A1 | −11,06…16,85; −39,93…−31,29 | 4,84…7,15 | rasgo comprido da ponta −Y, com cones, toros R0,1 e moldura |
| A2 | −11,06…16,85; 30,42…39,93 | 4,84…7,15 | o mesmo, na ponta +Y |
| B1 | −17,85…−14,80; −31,39…−25,39 | 6,15…7,15 | ressalto 3 × 6 na borda da pegada |
| B2 | −17,85…−14,80; 25,61…31,61 | 6,15…7,15 | idem, lado +Y |
| C | −17,85…−16,60; −19,64…19,73 | 0,20…7,15 | faixa escalonada na borda x = −17,85, degraus com R0,1 |
| D1 | −13,88…−13,62; −7,19…10,41 | 1,21…2,82 | parede com 2° de saída do rebaixo central + 2 cantos R0,2 |
| D2 | 15,63…15,89; −7,19…10,41 | 1,21…2,82 | idem, parede oposta |

Leitura do avaliador (a confirmar): a divisão certa não é a guilhotina de borda a borda pelos raios, e sim
**destacar cada região-problema como eletrodo próprio** (ilha pela caixa da região), deixando o corpo
principal sem ela (aliviado) e fresável.

**CONFIRMADO pelo Carlos (2026-11-16): "Sim, é isso".** Ele também aplicou o GAP à mão nesse eletrodo e
falhou, pelos raios — o GAP continua fora desta tarefa. Tarefa:

1. **Achar as regiões sozinho.** Agrupe os achados (raios que a fresa não faz, já medidos) por proximidade
   em planta e junte ao grupo as faces da feição a que pertencem (rasgo, ressalto, degrau, parede). Critério
   de aceite: no `15428.100` os grupos têm de cair nas 7 caixas da tabela (tolerância de ~1 mm por lado);
   relate grupo × caixa. Se o agrupamento por achados não reproduzir alguma (B1/B2 talvez nem tenham raio:
   veja por que o Carlos as marcou — canto vivo interno contra o corpo? — e relate antes de inventar regra).
2. **Destacar.** Cada região vira um eletrodo próprio (pegada = caixa da região + margem; a mecânica de ponta,
   pescoço, barra e furos é a que já existe). O principal perde a região: recorte pela caixa, do fundo ao
   fechamento, com a rota de recorte da T2c — o alívio não pode deixar parede fina nem raio novo que a fresa
   não faça.
3. **Geometria idêntica** segue a regra do planejador (rotação 0/90/180/270 = mesmo eletrodo em mais
   posições; espelhado = outro). A1/A2 e B1/B2 são espelhados: confira o que o planejador decide e relate.
4. **Conferência.** Para o principal e para cada destacado: fresável ou não, com o motivo. O objetivo é o
   principal FRESÁVEL; os destacados que não forem saem marcados para fio. Volume: Σ pontas = ponta inteira
   − enchimentos (como na T2c).
5. Este modo substitui a guilhotina como divisão PADRÃO quando o inteiro não é fresável (a guilhotina fica
   sob `"dividir": true` até eu avaliar). Testes de lógica pura do agrupamento. Schema do `ToolCatalog`
   para o que virar argumento.

Limites e relatório ("RELATÓRIO T5") como sempre; pare e relate se o agrupamento não reproduzir as regiões.

**Regra do Carlos (2026-11-16), que responde às suas 3 perguntas:**
- "Se o encontro de faces for **canto vivo** ou **raio menor que 0,5**, fica impossível fabricar o eletrodo
  sem erosão a fio. Minhas fresas têm no mínimo 0,5 mm de raio."
- Portanto o achado não é só raio: **canto vivo INTERNO do eletrodo** (aresta côncava sem arredondamento
  entre duas faces) também é achado — é o que explica B1/B2. Use o `SharpCornerProbe` se servir; confira no
  SE que ele acusa B1/B2. Canto vivo côncavo no FUNDO (parede × piso) a fresa de topo faz: só conta o canto
  entre duas PAREDES (aresta ~vertical), como o raio de canto. Confirme essa leitura com o que B1/B2 mostram.
- Limite de raio: 0,5 mm (confira se a escada de fresas do config já dá isso; não mude o config do Carlos).
- A moldura R0,302 e os cantos R0,2 perto de x −9,8 TAMBÉM são achados pela regra (ele só não os marcou):
  viram regiões como as outras. O aceite passa a ser: as 7 regiões da tabela reproduzidas + essas duas,
  cada uma justificada pelo achado.
- Descarte autorizado: pode fechar SEM salvar a `15428.100_EDM.asm` aberta e apagar o `EE01` e a `.cfg`
  gerados na pasta de teste.

### T6 — Juiz único de fresabilidade + divisão NA QUINA (2026-11-16)

T5 avaliada: REPROVADA pelo Carlos e pela INSPEÇÃO T5. O recorte por caixa cria bolsos fechados de canto vivo
no principal (13 cantos verticais de 5–8 mm) e a conferência os ignorava; cantos cilindro × parede e
inclinados também escapavam. A caixa troca um canto não fresável por quatro. Método novo:

1. **Juiz único.** Leve para o Core, como lógica de produção, o teste da inspeção (`insp\Insp.cs`): toda
   aresta côncava do eletrodo abaixo do fechamento é medida (qualquer par de tipos de face, qualquer
   inclinação). Passa só: parede × piso com o piso voltado para a fresa; raio côncavo ≥ 0,5 (escada do
   config); canal ≥ 1 mm; parede dentro do alcance. Cantos de alívio CONTAM. Lascas: aresta < 0,15 mm vai
   para uma lista à parte ("lasca"), não reprova, mas aparece no relatório. `DescribeElectrodeMachining`, o
   veredito de fio e o `RegionFinder` passam a usar só este juiz (aposente o `SharpConcaveCorners` e o uso
   do `SharpCornerProbe` aqui). Testes de lógica pura.
2. **Dividir na quina.** O corte nasce na aresta reprovada e segue o plano de uma das duas faces dela
   (a parede) até sair do eletrodo, de modo que nas duas partes essa parede vire face EXTERNA livre. Nada
   de caixa em volta do achado. Escolha, entre os dois planos possíveis, o que resolve mais achados e cria
   menos (zero) cantos novos; relate a escolha. Parede com saída (2°): o corte segue o plano da parede
   (inclinado), não a vertical — se a rota de recorte não fizer plano inclinado, relate antes de aproximar.
3. **Alívio só aberto ou arredondado.** Se ainda for preciso rebaixar o principal: o rebaixo vai até a borda
   do eletrodo, ou tem cantos R ≥ 1 mm e largura ≥ 2 mm. Bolso fechado de canto vivo é proibido — o juiz
   do item 1 o reprovaria.
4. **Construir → julgar → repetir.** Depois de construir cada eletrodo roda o juiz; se reprovar, divide de
   novo naquela quina (limite de profundidade e de nº de eletrodos; o que não fechar sai marcado para fio
   com a lista de cantos). O relatório só diz "fresável" com juiz zerado.
5. **Aceite no `15428.100`:** tabela por eletrodo (juiz: reprovados, lascas); capturas iso da ponta de cada
   um em `scratchpad\prints6\`; volume Σ pontas = ponta inteira (± enchimentos); nenhum canto de alívio
   reprovado; diga com franqueza quantos eletrodos saem fresáveis e quantos vão para fio. Se a divisão na
   quina não for viável em alguma região, PARE nela e relate com captura — não volte à caixa.

GAP: o Carlos disse que o CAM dele só compensa em 2 eixos — em eletrodo 3D o GAP TEM de estar no modelo.
Fica para a tarefa seguinte (em eletrodos menores, removendo antes os raios ≤ GAP); não mexa nisso agora.

### T7 — Corrigir a T6: enchimento, recuo da face que não queima, reunir (2026-11-17)

T6 avaliada: o juiz (`MillJudge`) e o recorte por plano inclinado ficam ACEITOS (795/795, 19 recortes OK).
O resultado não: 8 eletrodos, 1 fresável (migalha). As suas 4 perguntas já têm resposta:

1. **Raio côncavo < 0,5 → divisão + tirar o enchimento.** O Carlos já decidiu em 2026-11-12: "dividir em mais
   eletrodos, mesmo que o resultado na erosão seja cantos vivos". É a C1 da T2c (`FillerCuts`/`RemoveCuts`,
   já provada): no raio côncavo, o plano de divisão é o da parede TANGENTE ao raio, e a parte que ficou com
   o enchimento perde a caixa da face do raio. O aço fica com canto vivo ali — é o esperado. Reaproveite o
   código da T2c; não reescreva.
2. **Contra-saída da face de divisão → a face que NÃO queima recua.** A face de divisão não queima aço: a
   parte vizinha cobre esse volume. Em cada divisão, a parte em que a parede vira face EXTERNA fica com o
   plano inclinado (é a parede, queima). A OUTRA parte recebe, no lugar do plano inclinado, um plano VERTICAL
   posto do lado dela de modo que ela só ENCOLHA (nunca cresça) — a cunha que sai (≤ tan 2° × altura, ~0,25
   mm) é coberta pela vizinha. O juiz deve passar a não acusar contra-saída nessas partes. Sobreposição
   entre partes: zero ou negativa, nunca positiva (não queimar duas vezes além do previsto). Isso é decisão
   minha de avaliador; o Carlos pode vetar.
3. **Fragmentação → reunir depois de dividir.** Mantenha o plano inteiro, mas no fim tente reunir vizinhas:
   duas partes que dividem uma face de divisão voltam a ser uma se a UNIÃO passar no juiz (lógica pura sobre
   os achados de cada uma + o achado da divisa, que some se as duas voltarem a ser uma). Faça a reunião no
   PLANO, antes de construir, para não construir e desmanchar. Relate antes × depois.
4. **Parte mínima:** parte com pegada < 2 mm em alguma direção ou ponta < 5 mm³ não vira eletrodo: é
   reunida à vizinha com quem divide a maior face (mesmo que a vizinha deixe de ser fresável — relate).
5. **Profundidade:** suba o limite para 6, mas diga quantas folhas pararam nele.

**Aceite no `15428.100`:** a mesma tabela da T6 (juiz, lascas, sai), mais: nº de eletrodos antes e depois
da reunião; volume Σ pontas = inteiro − enchimentos (mostre os três números); capturas iso em
`scratchpad\prints7\`; zero faces em contra-saída nas partes que recuaram. Diga com franqueza quantos saem
fresáveis. Se um raio côncavo não tiver parede tangente plana para o corte (cilindro × cilindro, toro),
liste-o e deixe para fio — não invente plano.

### T7 — avaliação (2026-11-17): PARCIAL, aguarda o Carlos

801/801, 0 erros. 3 fresáveis de 14 (eram 1 de 8), 1 falha (EE09, RPC_E_SERVERFAULT). O item 2 da T7 estava
ERRADO (erro do avaliador): o recuo vertical da face que não queima deixa uma lâmina de aço (~0,24 mm na
base, ~104–124 mm³ no 15428) que nenhuma parte cobre. Não seguir até o Carlos escolher entre: lâmina, 
sobreposição positiva ≤ 0,24 mm (prática comum de overlap; queima de novo só cavidade), ou plano inclinado
nas duas partes (contra-saída de 2°). Também pendentes: 10 raios em toro (fio?) e faixa C < 2 mm.

### T8 — Respostas do Carlos aplicadas (2026-11-17)

Respostas do Carlos, literais: "1 - a, 2 - prefiro separar em mais eletrodos, 3 - em aproximadamente 9
eletrodos, deixando as áreas em roxo para usinagem e rosa para erosão, na fio só se eu não conseguir usinar
os raios dos detalhes na ponta, mesmo com o gap aplicado."

1. **Sobreposição (substitui o item 2 da T7).** Na divisão em parede com saída, a parte cuja parede vira
   face externa fica com o plano inclinado; a VIZINHA cresce até a vertical que passa pelo ponto mais
   afastado do plano (sobreposição ≤ tan(saída) × altura, ~0,24 mm), cobrindo a cunha. A sobreposição só
   pode invadir CAVIDADE (o volume da parte de trás), nunca aço: confira com o corpo da cavidade e relate.
   Some a lâmina de ~104–124 mm³ da T7.
2. **Toros (raio de canto de canto, R0,1–0,3): separar em mais eletrodos, não fio.** Divida também pela(s)
   parede(s) vizinha(s) do toro até o enchimento dele sair (canto vivo no aço, como já aceito).
3. **Só ROSA queima.** A região de queima é só das faces rosa ("17 - EROS. CAVIDADES"). Roxo ("16") é
   usinado no aço e NÃO entra no eletrodo — inclusive as 16 roxas "fora do alcance" que entram hoje. Mude o
   padrão (`roxoNaoFresavel` passa a padrão falso; mantenha o argumento) e relate quantas regiões e faces
   sobram.
4. **O juiz considera o GAP.** O raio côncavo do eletrodo final é r + GAP (Ra 1,6 → 0,10; Ra 3,2 → 0,20;
   `RaOffsetTablePolicy`). Raio passa se r + GAP ≥ 0,5. Canto vivo continua reprovado. Fio é ÚLTIMO recurso:
   só quando nem a divisão resolve.
5. **Alvo: ~9 eletrodos no `15428.100`.** Reúna com força: depois de dividir, junte toda vizinha cuja união
   passe no juiz (com o GAP), inclusive regiões que hoje não são "região única" (resolva a representação da
   faixa C). Parte < 2 mm é reunida; se a reunião reprovar, a parte fica, com nota.
6. **EE09:** ache a causa do RPC_E_SERVERFAULT (faixa estreita x −16,9…−14,8) e corrija; não pule o recorte.

**Aceite no `15428.100`:** tabela da T7; nº de eletrodos (alvo ~9) e quantos vão para fio (alvo 0);
volume Σ pontas − sobreposições = ponta inteira − enchimentos (mostre os números); lâmina descoberta = 0;
capturas iso em `scratchpad\prints8\`. Diga com franqueza o que fica fora do alvo e por quê.

### T8 — avaliação e mudança de rumo (Carlos concordou, 2026-11-17)

T8 parada (18 eletrodos, 2 migalhas fresáveis). Dividir por planos um bloco pronto não converge nesta peça.
Ficam ACEITOS e são a base daqui em diante: só rosa queima (`roxoNaoFresavel` padrão falso), juiz com GAP
(`MillJudge`, r + GAP ≥ 0,5). A divisão por quina (T6–T8) e a guilhotina (T2) viram opção experimental.

### T9 — Padrão útil + modo semiautomático (2026-11-17)

**1. Padrão do botão (sem argumentos).** Só rosa → regiões → planejador (reunião/idênticas como já é) →
eletrodo INTEIRO por região, sem divisão automática. Depois de construir, o juiz (com o GAP do Ra) lista,
por eletrodo, cada achado que reprova: tipo (canto vivo vertical/inclinado, raio R, canal, contra-saída),
posição x/y/z em mm no referencial da MONTAGEM `_EDM` (para o Carlos achar no modelo), comprimento, e o
motivo. Lascas < 0,15 mm numa linha só, contadas. O veredito: "fresável" (juiz zerado) ou "precisa dividir:
N achados" — NÃO escreva "corte a fio" no padrão (fio é o último recurso, decisão dele).
- `dividir`: aceite `"quina"` (rota T6–T8) e `true`/`"guilhotina"` (rota T2), ambos experimentais; ausente
  ou `false` = inteiro. Atualize o schema do `ToolCatalog` e a descrição ("experimental").
- `aplicarGap` continua desligado por padrão; não mexa no GAP nesta tarefa.

**2. Modo semiautomático: um eletrodo pelas faces SELECIONADAS.** Se houver faces selecionadas quando o
botão (ou `se_criar_eletrodos`) roda, ele constrói UM eletrodo só com elas, em vez de varrer a peça:
- aceita a seleção na peça aberta sozinha e na peça dentro da montagem `_EDM` (ou outra montagem) — use o
  que `se_medir_selecao`/`SelectionReader` já sabe ler; confira com a API viva, sem inventar;
- monta (ou reaproveita) a `_EDM`, como o padrão; numeração segue os eletrodos que já existem nela (o
  Carlos vai rodar várias vezes, uma por eletrodo — não sobrescreva o EE anterior);
- pegada = retângulo das faces selecionadas projetadas + a margem de sempre; a mecânica de ponta, pescoço,
  barra, furos e o juiz são os mesmos do padrão;
- avise as faces de queima (rosa) NÃO selecionadas que caem dentro da pegada (o eletrodo vai queimá-las
  também) e as selecionadas que não são rosa;
- argumento `usarSelecao` (padrão true; false ignora a seleção) no schema.
- No add-in (`ElectrodeRibbon.CriarEletrodos`): com seleção, a confirmação diz "1 eletrodo com N faces
  selecionadas"; sem seleção, o fluxo atual. `CommandSpec` continua DocKind.Any.

**Aceite no `15428.100`:**
- padrão: 3 eletrodos (principal + 2 gêmeos, como na T8), a lista de achados de cada um com posições, e
  uma captura iso do principal em `scratchpad\prints9\`;
- semiautomático: selecione por `se_selecionar` as faces rosa da região A1 da tabela da T5 (caixa x −11…17,
  y −40…−30) e rode; depois as de B1; os dois eletrodos entram na mesma `_EDM` como EE01 e EE02, com o
  juiz de cada um e os avisos de faces fora da seleção; capturas iso;
- testes da lógica pura (numeração, aviso de faces, parse de `dividir`); todos passando.
Limites de sempre. Relatório "RELATÓRIO T9".

### T4 — CANCELADA (Carlos, 2026-11-13)

O recurso de GAP já pinta as faces; não há etapa de pintura separada. Não faça.

### (texto antigo da T4, sem efeito)

As faces que recuaram na T3 recebem a cor de queima (o CAM usa a cor). Em peça importada a face não
tem estilo nomeado: `FaceColorPainter.PaintWithNamedStyle` cria o estilo. Aceite: contagem de faces
pintadas no relatório e uma captura.

## 5. O que entregar ao fim de cada tarefa

Um relatório curto, neste formato, para a avaliação:

```
RELATÓRIO T<n>
Rota escolhida: <uma frase> (e as rotas sondadas que falharam, com o HRESULT)
Arquivos alterados: <lista>
Aceite: <cada critério da tarefa, com o número medido e o comando ou ferramenta que mediu>
Testes: <total passando>
Não consegui / ficou de fora: <lista honesta>
Perguntas para o Carlos: <lista>
```

Registre também, no mesmo turno:
- fato novo sobre a API COM → `.claude/skills/solid-edge-com/references/` (fonte) **e** o espelho em
  `%USERPROFILE%\.claude\skills\solid-edge-com` (as duas cópias têm de ficar idênticas; o `diff` está
  no fim do `SKILL.md`), mais uma linha no `CHANGELOG.md` da skill;
- o estado da tarefa na tabela da seção 0 de `docs/PLANO_CRIAR_ELETRODOS.md`.

Se uma tarefa travar por mais de três tentativas na mesma rota, pare, escreva o relatório com o que
mediu e espere a avaliação — não troque de estratégia por conta própria.
