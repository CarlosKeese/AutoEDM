# Plano — "Criar Eletrodos" inteligente

Análise de 2026-11-10 sobre o molde real **15428** (cópia de teste, SE 2023), pedida pelo Carlos:
o botão é o recurso mais importante e não entregava um eletrodo. Este arquivo guarda o diagnóstico,
a rota técnica provada, as regras do Carlos e **o que já está implementado (seção 0)**.

## 0. Estado em 2026-11-12

Implementado e rodado no SE 2023 pela ponte (peça `15428.100`), `ElectrodeBuilder.CreateSmart`:

| Etapa | Onde | Estado |
|---|---|---|
| Botão usado numa PEÇA cria `<código>_EDM.asm` com a origem na face de fechamento (Z) e no centro da peça (X, Y) | `EdmAssemblyBuilder` | ✅ rodado. O fechamento é a face "10 - FECHAM." do lado em que a cavidade abre (a face de baixo do postiço também é pintada e é maior) |
| Queima = rosa "17" (Ra 1,6) + cores de Ra + roxo "16" que a fresa não faz (Ra 3,2), em todas as peças | `ElectrodeBuilder.Smart.cs` (`ScanBurnRegions`) | ✅ rodado: 105 rosa + 16 de 65 roxas |
| Divisão por aresta compartilhada | `RegionSplitter.SplitByConnectivity` | ✅ rodado: 6 regiões |
| Unir regiões que cabem num perfil de cobre; geometria idêntica (giro em Z de 90 em 90°) = um eletrodo em várias posições; espelhada = outro eletrodo | `SmartElectrodePlanner` | ✅ testado (lógica pura); a união rodou no SE, a idêntica só em teste |
| Ponta SÓLIDA = prisma − corpo da peça, com pescoço de 1 mm no contorno da boca | `ElectrodeTipBuilder` | ✅ rodado: 14.346,6 mm³, um corpo só |
| **T1 — pescoço reto**: a ponta vai só até o plano EXATO do aço (`Plane.GetRootPoint` da face de topo; a caixa do `Body.GetRange` passava 0,024 mm e deixava uma aba da pegada inteira); o pescoço é a face de topo da ponta (arestas por `Profile.IncludeEdge`) extrudada 1 mm na vertical. Sai a booleana do pescoço | `ElectrodeTipBuilder.RaiseNeck`, `ElectrodeBuilder.Smart.cs` (`TopPlaneZMm`) | ✅ rodado (2026-11-12, script de ensaio): 1 corpo, 11/11 features OK, pescoço 2.538,44 mm³ contra 2.538,46 (seção × 1 mm, −0,001 %), 39 faces laterais verticais; sólido 14.302,3 mm³. Com `"unir": false` os 6 saem com 1 corpo e pescoço exato. Aguarda avaliação |
| Faixa de medição + bloco + fixação a partir do topo do pescoço | `SurfaceBlockBuilder.PlanForFootprint` / `BuildOnPlan` | ✅ rodado (RET 50×19 deitada, M6 + 2×Ø4) |
| Usinabilidade do eletrodo (raios internos CÔNCAVOS e cantos vivos abaixo do fechamento) | `BRepRadiusProbe.IsConcave`, `DescribeElectrodeMachining` | ✅ só AVISA ("não fresável: 26 raios internos") |
| **T2 — dividir o que a fresa não faz**: os raios internos do eletrodo são PREVISTOS pela peça (raios convexos dela abaixo da menor fresa: 26 previstos = 26 medidos no eletrodo unido); corte na parede tangente a cada raio alinhado a X/Y; grade + reunião de células (lógica pura); cada pedaço construído é conferido de novo, redividido uma vez e, se sobrar raio, marcado "corte a fio" | `ElectrodeSplit.cs` (puro), `ElectrodeBuilder.Split.cs`, `BRepRadiusProbe.ProbeConvex` | ⚠️ rodado (2026-11-13, script de ensaio): 7 cortes → 9 pedaços, cada um 1 corpo; Σ pontas 11.764,57 mm³ contra 11.763,85 do inteiro (+0,006 %). **Nenhum pedaço sai fresável**: 14 dos 26 raios têm eixo inclinado (saída de 2°) e não têm corte, e o enchimento do raio cortado fica no pedaço (prisma − aço mantém o arredondamento do aço); `DeleteFaces`/`DeleteBlends` deram E_FAIL. Aguarda avaliação |
| **T2b — furo da fixação não estoura**: depois da fixação, cone da ponta e parede de cada furo conferidos pela área; se estourou, `Hole.Depth` encurta de 0,5 em 0,5 mm; abaixo do mínimo, o furo sai | `FixationHoleGuard.cs` | ✅ rodado: nenhum estouro com e sem divisão; M6 forçado a 16 mm estourou (cone 5,06 de 22,91 mm²) e foi encurtado para 14,5 mm |
| **T2c — correções da T2**: (C1) o enchimento do raio cortado sai do pedaço por recorte extrudado com a caixa EXATA da face, rente, recortada ao pedaço; a lâmina de 0,001 mm da sobreposição sai por uma faixa; toro que contorna canto de rebaixo passa a "sem corte" (as pontas dele saem pela caixa); (C2) sem `dividir`, divide só se a previsão disser que todos os pedaços saem fresáveis, e desfaz os pedaços (constrói o inteiro) se um construído não sair; (C3) `toleranciaAlinhamentoGraus` (padrão 0) | `ElectrodeSplit.cs` (`FillerBoxes`, `SplitDecision`, `SplitMode`), `ElectrodeTipBuilder.RemoveCuts`, `ElectrodeBuilder.Smart.cs` (`BuildPiecesOrWhole`) | ⚠️ rodado (2026-11-13, script de ensaio): padrão → 1 eletrodo inteiro "a divisão não resolve: 19 raios sem corte"; `dividir` → 8 pedaços, 0 enchimentos na divisa (eram 13), 1 fresável; com 3° (e `maxPedacos` 20) → 15 pedaços, 8 fresáveis, lasca de 0,056 mm. O desfazer da C2 depois de construir só tem teste puro (no 15428 a previsão já manda inteiro). Aguarda avaliação |
| Fechamento: se o ponto mais alto da peça não for a face de fechamento, vale a face pintada "10 - FECHAM." (regra 8) | `ElectrodeBuilder.Split.cs` (`ClosingZMm`) | ✅ implementado; no 15428.100 o topo É o fechamento (as duas leituras: Z −0,000811), então a troca não foi exercitada |
| Eletrodo cortado a fio, com o corte estendido até a base | — | ❌ falta |
| **T3 — GAP dentro do fluxo**: faces de queima guardadas pela geometria em síncrono (abaixo do fechamento, menos laterais do prisma e divisas), peça NOVA trocada para ordenado no fim, `FaceOffsets.AddEx` (−GAP do Ra), guarda pelo volume (área × GAP); depois, usinabilidade, furos, fundo e nervura medidos de novo | `ElectrodeGap.cs`, `ElectrodeBuilder.Smart.cs` (`ApplyGapAndRecheck`), argumento `aplicarGap` | ⛔ travado (2026-11-13): síncrono não tem o recuo (E_POINTER/DISP_E_TYPEMISMATCH); em ordenado o eletrodo inteiro e 4 dos 8 pedaços dão igFeatureFailed (até 1 face, 0,001 mm); 2 pedaços saem certos (volume = área × GAP, fundo +0,104 mm, R0,2 → 0,3) e 2 saem "OK" cortando o corpo por um plano (desfeito pela guarda). `aplicarGap` fica DESLIGADO por padrão. Aguarda avaliação |
| **T3b — GAP: achar o estágio que quebra** (cópias por estágio do eletrodo inteiro, trocadas para ordenado) | sondas temporárias, já removidas | ⛔ (2026-11-13): o recuo falha (igFeatureFailed) já na ponta SOZINHA, e em todos os estágios, no piso (a) e em todas as faces abaixo do fechamento (b). Passo 3: `FaceMoves.Add` no piso da ponta também falha; do lado do aço (cópia da cavidade, +0,10 mm) 1 face rosa vinga e as 105 juntas falham. Parado para avaliação |
| **T3c — GAP: mapa face a face** (cópia do eletrodo inteiro, ordenado, sonda temporária já removida) | — | ⛔ (2026-11-13): face a face 32/182 OK (29 planos, 3 cilindros); falham todos os toros, cones e esferas e 68/71 cilindros; 139 das 150 falhas são raio ≤ 0,5 ou vizinhas de um. Menor raio convexo do eletrodo R0,1. Conjunto todo falha já em 0,001 mm (e com blends 193). Grupos tangentes: 1 grupo de 154 faces (falha) + 28 isolados, 8 vingam. Não chegou a 100 %: item 4 não feito. Itens 5–6: 14 raios convexos ≤ GAP; `DeleteBlends.Add` em ORDENADO tira 12 (2 R0,101 recusam DeleteBlends e DeleteFaces); mesmo assim o recuo do conjunto falha, também sem as faces a até 1 mm dos 2 que sobram. Item 7 não feito |
| **T5 — separar por REGIÃO** (padrão sem `dividir`): o inteiro é construído e medido (raio interno < 0,5 + canto vivo côncavo entre paredes); os achados viram regiões (ligados por vizinha pequena, feição até 3 mm); cada região vira eletrodo (pegada = caixa + 0,25, mín. 2 mm, sem parede fina no principal) e o principal perde as pegadas (recorte do fundo ao fechamento e no pescoço) | `ElectrodeRegions.cs` (puro), `RegionFaceReader.cs`, `ElectrodeBuilder.Split.cs` (`RegionElectrodes`), `ElectrodeTipBuilder` (alívio, pescoço por face) | ⚠️ rodado (2026-11-16, script de ensaio): 10 regiões (as 7 do Carlos + moldura R0,302 + cantos R0,2; a faixa C sai em 2 pedaços), principal FRESÁVEL, Σ pontas 11.763,75 contra 11.763,85; B1/B2 o mesmo eletrodo em 2 posições; A1/A2 espelhados; D1/D2 idênticos por giro mas pegadas diferentes (borda). Aguarda avaliação |
| **T6 — juiz único + divisão NA QUINA** (2026-11-17, substitui a T5 reprovada): `MillJudge` (o teste da INSPEÇÃO T5 no Core: toda aresta côncava por amostra de material `Shell.IsPointInside`, raio < fresa, contra-saída, canal, alcance; lasca < 0,15 mm à parte) decide fresável/fio; reprovado → corte pelo plano da parede da aresta (`CornerSplitPlanner`, semiespaço por `CoordinateSystems.AddByMatrix` → `cs.Plane(4)` → `AddParallelByDistance(face,0,2)` → `AddFiniteMulti` simétrico), construir → julgar → repetir (prof. 4, até 12 eletrodos) | `MillJudge.cs`, `MillJudgeReader.cs`, `CornerSplit.cs`, `ElectrodeBuilder.Corner.cs`, `ElectrodeTipBuilder.CutHalfSpace` | 15428.100: inteiro → 8 partes, Σ pontas 11763,787 × 11763,852 mm³; 1 fresável (migalha de 0,05 mm³), 7 fio (raios < 0,5 não se resolvem por divisão; a face da divisão de parede com saída fica em contra-saída do outro lado). Aguarda decisão do Carlos |
| **T7 — plano da divisão antes de construir** (2026-11-17): `CornerPlanner` divide pelo juiz do inteiro (canto: plano da parede; raio < 0,5 com parede tangente plana: essa parede + enchimento em prisma triangular canto–tangentes, rota C1 da T2c com `CutPolygon`), a parte da frente leva plano VERTICAL recuado (só encolhe), reúne vizinhas cuja união passa e partes < 2 mm / ponta < 5 mm³; prof. 6, até 16 partes | `CornerPlan.cs`, `ElectrodeBuilder.Corner.cs`, `ElectrodeTipBuilder.CutPolygon`/`BodySide` | 15428.100: 16 → 14 partes; 3 fresáveis, 10 fio, 1 falha (RPC_E_SERVERFAULT no recorte); 10 raios em toro sem parede tangente plana ficam para fio; o recuo vertical deixa ~100–124 mm³ que nenhuma parte cobre (ver relatório T7) |
| **T8 — respostas do Carlos** (2026-11-17): só rosa queima (`roxoNaoFresavel` padrão falso); juiz com GAP (r + GAP ≥ 0,5); frente da divisão cresce até a vertical (sobreposição); toros divididos pelas paredes vizinhas (2 saltos) com enchimento em caixa | `MillJudge.GapMm`, `CornerPlan.SidePlane/GrowthStrip/TangentWalls`, `SmartElectrodeOptions` | 15428.100: 105 rosa → 8 regiões → 18 eletrodos, 2 fresáveis (2 migalhas), 16 fio — PIOR que a T7: o crescimento recria o degrau da quina e a caixa do toro abre contra-saída; reunião de partes não convexas e < 5 mm³ não vingou. PARADO (3 tentativas) |
| **T9 — padrão útil + semiautomático** (2026-11-17): eletrodo INTEIRO por região (só rosa), juiz com GAP listando cada achado com a posição na `_EDM`, veredito "fresável"/"precisa dividir" (sem fio); `dividir` "quina"/"guilhotina" experimentais; com faces selecionadas, 1 eletrodo só com elas, avisos de rosa fora da seleção dentro da pegada, numeração por ocorrências + arquivos | `SemiAuto.cs`, `ElectrodeBuilder.Selection.cs`, `MillJudge.Verdict/ReportLines`, `ElectrodeNaming.NextElectrodeIndex(…, pasta)`, `ElectrodeRibbon.CriarEletrodos` | 15428.100: padrão 3 eletrodos (30, 4 e 4 achados, todos "precisa dividir"); semi A1 → EE01 (8 faces, 3 rosa não selecionadas na pegada), A2 → EE02 na mesma `_EDM` (13 faces, 4 avisos). B1 não é rosa (era roxo): trocado por A2 |
| GAP nas faces de queima dentro do mesmo fluxo | — | ❌ falta (ver T3/T3b/T3c) |
| Pintar no eletrodo as faces de queima | — | ❌ falta |
| Peças dentro de submontagem | — | ❌ fora desta versão (só ocorrências do 1º nível) |

Decisões minhas, a confirmar com o Carlos: margem da pegada de 1 mm por lado; regiões a até 5 mm uma da
outra podem ser unidas; a barra DEITADA tem de comportar ponta + pescoço + faixa + 3 mm de bloco.

No `15428.100` o plano padrão dá **1 eletrodo** (33,7 × 79,9 mm, RET 50×19 deitada, corte 83); com
`"unir": false` dá **6** (um de 33,6 × 74,2 e cinco pequenos). As faces roxas que a fresa não faz ligam
entre si regiões que, só pelas rosas, eram separadas.

## 1. O que o botão faz hoje (e por que "não funciona")

| # | Fato | Evidência |
|---|---|---|
| 1 | O automático cria **peças vazias posicionadas**. A cópia da queima só está ligada ao "Criar eletrodo (manual)". | `ElectrodeBuilder.CreateElectrodesWithBlank` chama `CreateAndPlaceElectrode` sem `burnSources` e sem Ra (`ElectrodeBuilder.cs:415-474`); o log final manda copiar as faces e usar o Criar Base |
| 2 | Num molde real a detecção acha **zero** faces. Só vale a paleta de Ra (`RaColorMap`: azul, vermelho, marrom, verde, amarelo). A cavidade a erodir é pintada com o estilo da casa **"17 - EROS. CAVIDADES" = RGB(176,0,178)**, que não está no mapa. | `se_analisar_z` no 15428: 0 faces. Histograma: 110 faces rosa em 3 peças (105 no `15428.100`, 4 no `.200`, 1 no `.210`) |
| 3 | Só **uma** peça é analisada por execução (a de mais faces mapeadas). | `FindBurnOccurrence`, `ElectrodeBuilder.cs:1116-1149` |
| 4 | A divisão em eletrodos é por caixa da face + nível de Z (1 mm) + folga XY de 50 mm: funde regiões separadas por parede e infla a pegada de queima em L/anel. | `ElectrodeZAnalyzer`, `RegionSplitter.SplitBySpatialProximity` |
| 5 | O "fundo" é o menor Z; a direção de queima é sempre +Z da montagem. Com o postiço virado (lado fixo de cabeça para baixo na montagem do molde) o eletrodo nasce do lado errado. | Teste de hoje: `15428.100` tem Z local → (0,0,−1); o EE01 foi posto em Z = 0 com a base para +Z, para dentro do aço |
| 6 | Não existe orquestrador: copiar → fechar → base → unir → GAP nunca rodaram encadeados. Base e Unir exigem síncrono, GAP exige ordenado. | `ApplyOffset`, `CreateBlankAndHolder`, `RecolorAndSave`, `CheckMinimumRadii` não têm chamador |
| 7 | A cor da casa não carrega Ra; sem Ra o "Aplicar GAP" não tem o que pré-selecionar. | — |
| 8 | Em peça importada (sem estilo de face nomeado) a superfície copiada chega cinza: a repintura é pelo NOME do estilo. | `BurnSurfaceCopier.cs:162`; EE01 de hoje: "0 repintada(s)" |
| 9 | O estado "Criar eletrodos c/ blank ✅ validado no SE" descreve um comportamento removido em 23/07 (bloco simples, sem queima). | `PROJECT_STATE.md`, histórico |

## 2. O que a cadeia manual entrega hoje (rodada pelo agente, 2026-11-10)

Região R7 do `15428.100` (canto de bolsão, 6 faces rosa):

1. `AssemblyDocument.CreateReference(ocorrência, face)` + `se_selecionar` → faces selecionadas na montagem sem clique.
2. `se_criar_eletrodo_manual` → `15428_EE01.par` com a superfície "Queima" (6 de 6 faces). ✅
3. `se_criar_base` → blank RED Ø10, faixa de 5 mm com flat, eixo Ø6,1 × 20. ✅
4. Resultado: **base + superfície solta**. "superfícies unidas: não" — a forma da queima não vira sólido.
   A superfície é um conjunto ABERTO de faces (só as rosa); as paredes vizinhas (roxas, usinadas) não vêm.

Conclusão: copiar só as faces de queima e depois tentar fechar/costurar é o elo fraco. É onde a
cadeia sempre parou.

## 3. Rota provada: eletrodo = prisma − corpo da cavidade

Sonda de 2026-11-10, peça nova em **síncrono**:

1. Prisma de projeto sobre a pegada (aqui 20 × 20 × 10 mm).
2. `Constructions.CopyConstructions.Add(arquivo da cavidade, …)` — o MESMO passo que o
   `BurnSurfaceCopier` já faz — traz o corpo da cavidade como corpo de construção.
3. `Model.BooleanFeatures.Add(1, Body[] { Constructions.Item(1).Body }, seBooleanSubtract = 2)`.

Resultado: `Status = igFeatureOK`, volume 4000 → **2222,6 mm³**, e o corpo é o negativo exato da
cavidade dentro da pegada (nervuras, degraus e raios vêm do sólido real, sem costura).

| Tentativa | Resultado |
|---|---|
| `BooleanFeatures.Add` com `SolidEdgeGeometry.Body[]` do corpo de construção | ✅ funciona |
| idem com `CopyConstruction[]`, `object[]`, `DispatchWrapper[]` da feature | E_NOINTERFACE |
| idem com `ConstructionModel[]` / `DispatchWrapper[]` do ConstructionModel | E_FAIL |
| `Model.Subtracts.Add` (alvo Model, ferramenta CopyConstruction) | E_INVALIDARG / E_FAIL |
| `Models.AddByConstruction(CopyConstruction)` | E_NOINTERFACE |
| `Models.AddCopiedPart(arquivo)` | une ao corpo de projeto ativo (não serve de ferramenta) |
| `Models.AddBody(igPartType, nome)` em síncrono | sem efeito (Models.Count segue 1) |

Falta medir: o mesmo com o sistema de coordenadas do postiço (pose E⁻¹·C), apagar a
`CopyConstruction` depois da booleana, e a união do prisma com o bloco do Criar Base.

## 4. Divisão em eletrodos por adjacência (medida no 15428.100)

Componentes conexos das faces rosa por ARESTA compartilhada (`Edge.ID`): 105 faces → **8 regiões**.

| Região | Faces | Pegada X × Y (mm) | Profundidade (mm) | Observação |
|---|---|---|---|---|
| R1 | 40 | 33,5 × 35,1 | 7,2 | moldura principal |
| R2 | 29 | 26,3 × 13,9 | 0,7 | moldura interna (dentro da caixa da R1) |
| R3 / R4 | 8 / 6 | 27,8 × 6,3 | 2,2 | espelhadas em Y |
| R5 / R6 | 5 / 5 | 25,7 × 5,6 | 1,8 | espelhadas em Y |
| R7 / R8 | 6 / 6 | 3,9 × 3,5 | 5,0 | espelhadas em Y |

Raios nas regiões: 0,10 a 2,0 mm — é o motivo de serem erodidas.

## 5. Desenho proposto

1. **Detectar** queima em TODAS as ocorrências: paleta de Ra **ou** estilo "17 - EROS. CAVIDADES"
   (pelo nome quando existir, pelo RGB quando a peça é importada).
2. **Dividir** por adjacência de aresta; juntar regiões cuja pegada se sobrepõe; reconhecer regiões
   espelhadas/repetidas.
3. **Direção de queima** pela geometria (lado aberto da região), não pelo +Z da montagem.
4. **Construir** cada eletrodo: prisma sobre a pegada − corpo da cavidade (rota do item 3), unido a
   bloco + faixa + fixação (o que o Criar Base já faz), faces vindas da cavidade pintadas como queima.
5. **GAP** nas faces de queima, sem trocar de ambiente no meio (a medir: `FaceOffsets` em síncrono).
6. **Conferência antes de criar**: realce das regiões na tela + lista (pegada, profundidade, blank,
   raio mínimo) e, depois, captura de cada eletrodo.
7. Relatório de coordenadas e lista de corte a partir dos eletrodos CRIADOS.

## 6. Regras do Carlos (2026-11-10)

1. Roxo ("16", usinado) também pede eletrodo quando a fresa for pequena demais para o raio, o diâmetro ou a profundidade.
2. Ra: rosa = 1,6 no mínimo; roxo = 3,2.
3. Botão numa PEÇA: cria a montagem `<código da peça>_EDM` com a peça, origem na face de fechamento em Z e no centro da peça em X e Y. Em montagem: ela já é a `_EDM`.
4. Todo eletrodo tem de ser usinável; se não for, dividir em mais eletrodos. Raio pequeno demais para fresar: o eletrodo pode ser cortado a fio, com o corte estendido até a base do eletrodo.
5. Geometria idêntica em mais de uma região = um eletrodo só, usado nas várias posições.
6. Unir 2 ou mais eletrodos num só quando continuar usinável e a pegada couber num perfil de cobre do catálogo.

7. O furo da fixação (M6 e pinos) pode entrar no pescoço e na ponta, mas não pode estourar o cobre (2026-11-12).
8. Quando o ponto mais alto da peça não for a face de fechamento, o fechamento é a face pintada "10 - FECHAM." (2026-11-12).

Sem resposta ainda: margem da pegada e alívio das faces usinadas vizinhas que caem dentro dela.

## 7. Próximos passos

**Avaliação do Carlos em 2026-11-12, vendo o eletrodo do `15428.100`:** (a) por causa dos raios pequenos,
o ideal é DIVIDIR em mais eletrodos, mesmo que o resultado na erosão sejam cantos vivos; (b) o pescoço
feito pela 2ª booleana tem defeito de construção (copia para cima a camada de 1 mm abaixo do fechamento,
em vez de extrudar o contorno da boca); (c) o GAP não foi aplicado. A construção dessas três correções
foi passada ao Opus 5.5 por `docs/INSTRUCOES_OPUS_CRIAR_ELETRODOS.md`; a avaliação fica com o Fable.


1. Dividir o eletrodo não usinável (regra 4): localizar os raios internos reprovados e separar por eles.
2. Eletrodo a fio: reconhecer ponta prismática e estender o perfil até a base.
3. GAP no mesmo fluxo (medir `FaceOffsets` em síncrono) e pintura das faces de queima.
4. Conferência visual antes de criar: realce das regiões na tela.
5. Validar pelo botão da ribbon e num molde com mais de uma peça com queima.
