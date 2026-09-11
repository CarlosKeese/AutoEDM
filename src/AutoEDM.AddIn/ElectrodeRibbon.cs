using System;
using System.Windows.Forms;
using SolidEdgeCommunity.AddIn;
using AutoEDM.AddIn.UI;
using AutoEDM.Com;
using AutoEDM.Config;
using AutoEDM.Diagnostics;
using AutoEDM.Electrode;
using AutoEDM.Model;
using AutoEDM.Reporting;
using AutoEDM.Sealing;

namespace AutoEDM.AddIn
{
    /// <summary>
    /// Ribbon "AutoEDM". Carrega o layout de Ribbon.xml (recurso embutido) e trata os
    /// cliques por CommandId (= id do XML). Cada comando é uma casca fina sobre um
    /// método do núcleo AutoEDM.Core, rodando in-process na montagem ATIVA.
    /// </summary>
    public class ElectrodeRibbon : Ribbon
    {
        private const int CmdCriarEletrodos = 1; // Criar eletrodos + base
        private const int CmdCoordenadas = 2;    // Coordenadas: janela com os eletrodos SELECIONADOS (posição + GAP/Ra)
        private const int CmdAnalisarZ = 3;      // Analisar eletrodos por Z
        private const int CmdSpecSheet = 4;      // Ficha (spec-sheet)
        private const int CmdCriarBase = 5;       // Criar Base (ambiente de PEÇA)
        private const int CmdInspecionar = 6;       // SPY: dumpa o objeto COM selecionado
        private const int CmdUnirSuperficies = 7;   // Engrossar a queima e unir ao bloco (ISOLADO)
        private const int CmdIniciarLeitura = 8;    // Gravador: snapshot inicial (ação manual)
        private const int CmdGravarLeitura = 9;     // Gravador: diff + dump das features novas
        private const int CmdCriarEletrodoManual = 10; // Criar 1 eletrodo a partir da seleção manual de faces
        private const int CmdAplicarGap = 11;       // GAP + cor + nome no corpo já unido (ambiente de PEÇA)
        private const int CmdDuplicarEletrodo = 12; // Duplicar eletrodo(s) selecionado(s) p/ o próximo Ra da tabela
        private const int CmdDiagRosca = 13;        // Sonda da rosca M6 (peça descartável)
        private const int CmdAlojamentoORing = 14;  // Alojamento de anel O'ring (janela modeless)
        private const int CmdSondaInterPart = 15;   // Sonda do inter-part (rodada 2) — só leitura + peça descartável

        /// <summary>Snapshot (nomes dos itens por coleção) no "Iniciar leitura" — diffado no "Gravar log".</summary>
        private static System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> _recBaseline;

        public ElectrodeRibbon() : base()
        {
            LoadXml(System.Reflection.Assembly.GetExecutingAssembly(), "AutoEDM.AddIn.Ribbon.xml");
            StartStateWatch();
        }

        public override void OnControlClick(RibbonControl control)
        {
            // Rede de segurança: o relógio de estado (StartStateWatch) já deixa o botão
            // cinza no ambiente errado, mas se a SE clicar assim mesmo (estado velho, ribbon
            // ainda não repintada), o comando explica em vez de estragar a peça.
            if (!AllowedHere(control.CommandId, explain: true)) return;

            switch (control.CommandId)
            {
                case CmdCriarEletrodos: CriarEletrodos(); break;
                case CmdCriarEletrodoManual: CriarEletrodoManual(); break;
                case CmdCoordenadas: AbrirCoordenadas(); break;
                case CmdAnalisarZ: AnalisarZ(); break;
                case CmdSpecSheet: GerarSpecSheet(); break;
                case CmdCriarBase: CriarBase(); break;
                case CmdUnirSuperficies: UnirSuperficies(); break;
                case CmdAplicarGap: AplicarGap(); break;
                case CmdDuplicarEletrodo: DuplicarEletrodo(); break;
                case CmdInspecionar: InspecionarSelecao(); break;
                case CmdDiagRosca: DiagnosticarRosca(); break;
                case CmdAlojamentoORing: AlojamentoORing(); break;
                case CmdSondaInterPart: SondarInterPart(); break;
                case CmdIniciarLeitura: IniciarLeitura(); break;
                case CmdGravarLeitura: GravarLeitura(); break;
            }
        }

        // -------------------------------------------------------------- comandos

        /// <summary>Analisa (NÃO-destrutivo) e propõe os eletrodos por nível de Z.</summary>
        private void AnalisarZ()
        {
            Run(CmdAnalisarZ, (connector, doc, p) =>
            {
                Selection.ZAnalysisResult res = NewBuilder(connector).AnalyzeElectrodesByZ(doc, p);

                // A usinabilidade só aparece quando reprovou alguma coisa: silêncio aqui é
                // resultado bom, e um bloco fixo dizendo "nada encontrado" a cada clique treina o
                // usuário a fechar a janela sem ler.
                string machinability = res.DescribeMachinability();
                MessageBox.Show(
                    $"{res.Electrodes.Count} eletrodo(s) proposto(s), por nível de Z.\n" +
                    $"({res.FlatFaces} piso / {res.SteepFaces} parede)\n" +
                    (machinability.Length > 0 ? "\n" + machinability + "\n" : "") +
                    "\nVeja as posições no log.",
                    "AutoEDM — Analisar eletrodos", MessageBoxButtons.OK,
                    res.HasEdmOnlyGeometry ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            });
        }

        /// <summary>
        /// SONDA DO INTER-PART (rodada 2). Só leitura na montagem + peças DESCARTÁVEIS: responde,
        /// numa rodada, as rotas de cópia entre peças que nunca foram executadas e mapeia a API
        /// para a skill. Não altera a cavidade, não salva a montagem.
        /// </summary>
        private void SondarInterPart()
        {
            Run(CmdSondaInterPart, (connector, doc, p) =>
            {
                AutoEDM.Experiments.InterPartProbe.Run(connector, doc);
                MessageBox.Show(
                    "Sonda concluída. TODO o resultado está no log — é ele que interessa, não esta janela.\n\n" +
                    "Se quiser o teste decisivo: entre em EDIÇÃO EM CONTEXTO no eletrodo à mão, " +
                    "deixe aberta e rode a sonda de novo. Comparar os dois logs responde se a cópia " +
                    "entre peças depende só desse estado.",
                    "AutoEDM — Sonda inter-part", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        /// <summary>Cria as peças de eletrodo (vazias) e as posiciona. ESCREVE na montagem. A
        /// base (bloco/holder) é gerada depois, pela "Criar Base", sobre a geometria real.</summary>
        private void CriarEletrodos()
        {
            Run(CmdCriarEletrodos, (connector, doc, p) =>
            {
                var builder = NewBuilder(connector);

                // CONFERIR ANTES DE CRIAR (Log 57): mostra a queima detectada + as cores não
                // mapeadas; se a maior região colorida não estiver mapeada, avisa. Assim o
                // usuário não cria eletrodos em cor RESIDUAL quando a queima real está fora do mapa.
                Selection.ZAnalysisResult res = builder.AnalyzeElectrodesByZ(doc, p);
                if (res.Electrodes.Count == 0)
                {
                    MessageBox.Show(
                        "Nenhum eletrodo detectado (sem cor de queima MAPEADA). Nada foi criado.\n\n" +
                        "Veja no log as cores encontradas — a queima pode estar numa cor fora do mapa.",
                        "AutoEDM — Criar eletrodos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    Log.Info("Criar eletrodos: nada detectado.");
                    return;
                }

                var icon = res.HasDominantUnmappedColor ? MessageBoxIcon.Warning : MessageBoxIcon.Question;
                var confirm = MessageBox.Show(
                    res.DescribeBurnDetection() +
                    "\n\nIsto vai CRIAR uma peça VAZIA por eletrodo (sem bloco/base ainda) na subpasta 'Eletrodos' " +
                    "ao lado da montagem e inseri-las posicionadas. A montagem NÃO será salva automaticamente.\n\n" +
                    "A queima detectada está CORRETA? Criar os eletrodos?",
                    "AutoEDM — Conferir antes de criar", MessageBoxButtons.YesNo, icon);
                if (confirm != DialogResult.Yes) { Log.Info("Criação cancelada pelo usuário (conferência)."); return; }

                int n = builder.CreateElectrodesWithBlank(doc, p);
                MessageBox.Show(
                    $"{n} eletrodo(s) criado(s) e posicionado(s).\n\n" +
                    "Revise, SALVE a montagem, edite cada peça em contexto, copie (Inter-Part Copy) as faces de " +
                    "queima e use 'Criar Base' para gerar o bloco.",
                    "AutoEDM — Criar eletrodos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        /// <summary>
        /// Versão MANUAL do "Criar eletrodos" (Carlos): em vez da detecção automática por
        /// cor/nível de Z, o usuário SELECIONA à mão a(s) FACE(s) do fundo do bolsão (no SE,
        /// clique na ocorrência e clique DE NOVO no mesmo ponto — ou segure Alt — para
        /// selecionar a FACE em vez da peça inteira; não existe um "modo" de seleção
        /// separado a ligar, é o comportamento nativo de seleção em 2 cliques do SE) e
        /// clica este botão UMA vez por eletrodo. Cria e posiciona UMA peça VAZIA (sem
        /// bloco/base ainda) no centro XY + Z mais fundo das faces escolhidas — mesmo
        /// pipeline do "Criar eletrodos" automático. ESCREVE na montagem (não salva).
        /// </summary>
        private void CriarEletrodoManual()
        {
            Run(CmdCriarEletrodoManual, (connector, doc, p) =>
            {
                ManualElectrodeResult res = NewBuilder(connector).CreateElectrodeFromSelection(doc, p);
                MessageBox.Show(res.Message, "AutoEDM — Criar eletrodo (manual)", MessageBoxButtons.OK,
                    res.Created ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            });
        }

        /// <summary>
        /// Botão "Coordenadas" (ambiente de MONTAGEM, Carlos, 2026-08-04): SEM detecção
        /// automática por cor — o usuário SELECIONA na montagem os eletrodos de interesse
        /// (ocorrências) e clica. Abre uma janela com uma linha por eletrodo: a posição
        /// (mesma leitura de "Propriedades de Ocorrência" no SE) e o GAP/Ra gravados na
        /// peça (mesma fonte que "Duplicar eletrodo" usa). Somente leitura — não altera a
        /// montagem nem as peças.
        /// </summary>
        private void AbrirCoordenadas()
        {
            Run(CmdCoordenadas, (connector, doc, p) =>
            {
                var items = NewBuilder(connector).ListSelectedElectrodes(doc);
                if (items.Count == 0)
                {
                    MessageBox.Show(
                        "Nenhum eletrodo selecionado. Na montagem, selecione a(s) ocorrência(s) do(s) eletrodo(s) " +
                        "(clique na peça, não numa face) e clique em \"Coordenadas\" de novo.",
                        "AutoEDM — Coordenadas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                using (var form = new ElectrodeListForm(items))
                {
                    form.ShowDialog();
                }
            });
        }

        /// <summary>Folha de dados (spec-sheet) por eletrodo (.txt + .csv). Somente leitura.</summary>
        private void GerarSpecSheet()
        {
            Run(CmdSpecSheet, (connector, doc, p) =>
            {
                ElectrodeBuildPlan plan = NewBuilder(connector).PlanFromAssemblyDocument(doc, p);
                Log.Info(ElectrodeSpecSheet.ToText(plan, p));
                string path = ElectrodeSpecSheetWriter.Save(plan, p, folder: ElectrodeNaming.ResolveProjectFolder(doc));
                MessageBox.Show(
                    $"Ficha gerada para {plan.Regions.Count} detalhe(s).\n\nArquivos (.txt e .csv):\n{path}",
                    "AutoEDM — Ficha", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        /// <summary>
        /// "Criar Base" (ambiente de PEÇA): abre a janela com parâmetros + Preview. O usuário
        /// já copiou as faces de queima na peça manualmente; o AutoEDM cria a base (bloco +
        /// faixa de medição) e aplica a fixação. SEM gap/offset por cor (a superfície copiada
        /// não carrega a cor original) — isso e o trabalho de superfície ficam no botão
        /// "Unir superfícies". ESCREVE na peça.
        /// </summary>
        private void CriarBase()
        {
            Run(CmdCriarBase, (connector, doc, p) =>
            {
                // O Core loga cada passo + o probe.
                var builder = new SurfaceBlockBuilder();
                using (var form = new BlockOverSurfacesForm(builder, (object)connector.Application, (object)doc))
                {
                    form.ShowDialog();
                }
            });
        }

        /// <summary>
        /// Botão ISOLADO "Unir superfícies" (ambiente de PEÇA): engrossa a superfície de
        /// queima selecionada para cima até dentro do bloco e une num sólido único. Separado
        /// do "Criar Base" porque o thicken, ao falhar, envenenava o doc e
        /// derrubava a fixação — aqui o experimento fica isolado. SÓ UNE (Carlos, 2026-07-21:
        /// separado do GAP/cor/nome — quando a união automática falha, ele une NA MÃO no SE e
        /// segue direto para "Aplicar GAP", sem precisar que este botão também acerte a união).
        /// ESCREVE na peça.
        /// </summary>
        private void UnirSuperficies()
        {
            Run(CmdUnirSuperficies, (connector, doc, p) =>
            {
                var builder = new SurfaceBlockBuilder();
                BlockOverSurfacesResult res = builder.UniteSurfacesToBlock(doc, new BlockOverSurfacesOptions());
                string gaps = res.SideGapsPatched > 0
                    ? $"{res.SideGapsPatched} vão(s) lateral(is) fechado(s) automaticamente com 'Limite'.\n\n"
                    : "";
                string msg = res.SurfacesUnited
                    ? gaps + "Superfície unida ao bloco ✓. Confira no modelo — depois use 'Aplicar GAP' para o offset/cor.\n\nDetalhe no log (linhas 'Unir:')."
                    : gaps + "Não uni as superfícies ao bloco ainda.\n\nVeja o log (linhas 'Unir:') para o ponto exato — o bloco/faixa/furos NÃO foram tocados. " +
                      "Se preferir, una NA MÃO no SE e use 'Aplicar GAP' no corpo já mesclado.";
                if (res.Warnings.Count > 0) msg += "\n\n⚠ " + string.Join("\n⚠ ", res.Warnings);
                MessageBox.Show(msg, "AutoEDM — Unir superfícies", MessageBoxButtons.OK,
                    res.SurfacesUnited ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            });
        }

        /// <summary>
        /// Botão "Aplicar GAP" (ambiente de PEÇA, Carlos, 2026-07-21): separado de "Unir
        /// superfícies" — roda tanto depois de uma união automática quanto de uma união MANUAL
        /// feita direto no SE (quando o botão "Unir" falha, o Carlos tem essa saída). Selecione
        /// as faces de queima no corpo já unido (ou deixe em branco logo após um "Unir
        /// superfícies" bem-sucedido na mesma sessão — cai no heurístico antigo) e clique.
        /// A lista já abre pré-selecionada no Ra gravado na peça, se houver (detectado da cor
        /// pela seleção manual, ou de uma aplicação anterior). ESCREVE na peça.
        /// </summary>
        private void AplicarGap()
        {
            Run(CmdAplicarGap, (connector, doc, p) =>
            {
                double? preselect = RaVariableStore.TryRead(doc, out double ra) ? (double?)ra : null;
                var choices = RaGapPresets.All(p.Material, Config.BuildColorMap(), Config.BuildOffsetPolicy());
                RaGapPresets.Choice chosen;
                using (var picker = new RaGapPickerForm(preselect, choices))
                {
                    if (picker.ShowDialog() != DialogResult.OK || picker.Chosen == null)
                    {
                        Log.Info("Aplicar GAP: cancelado pelo usuário.");
                        return;
                    }
                    chosen = picker.Chosen;
                }
                Log.Info($"Aplicar GAP: escolhido {chosen.Label}.");

                var builder = new SurfaceBlockBuilder();
                BlockOverSurfacesResult res = builder.ApplyGapToUnitedSurfaces(doc, chosen);
                string msg = res.SurfacesOffset
                    ? $"GAP {chosen.GapMm:0.00}mm (Ra {chosen.Ra:0.0}) aplicado + cor + nome da feature. Confira no modelo.\n\nDetalhe no log (linhas 'Aplicar GAP:')."
                    : "Não apliquei o GAP.\n\n" + (res.Warnings.Count > 0 ? res.Warnings[0] : "Veja o log (linhas 'Aplicar GAP:').");
                MessageBox.Show(msg, "AutoEDM — Aplicar GAP", MessageBoxButtons.OK,
                    res.SurfacesOffset ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            });
        }

        /// <summary>
        /// Botão "Duplicar eletrodo" (ambiente de MONTAGEM, Carlos, 2026-07-21): selecione a
        /// ocorrência de UM eletrodo já com GAP aplicado (tem Ra gravado — variável ou feature
        /// "GAP: ... - Ra: ...") e clique. Cria uma cópia da peça com o GAP no PRÓXIMO Ra da
        /// escada (mais grosso = desbaste, <see cref="RaColorMap.RoughingRaFor"/>) e posiciona
        /// essa cópia em TODAS as posições onde o eletrodo original aparece na montagem (copia
        /// as instâncias — não só a selecionada) — assim um par desbaste/acabamento sai em 1
        /// clique por posição repetida. Não salva a montagem.
        /// </summary>
        private void DuplicarEletrodo()
        {
            Run(CmdDuplicarEletrodo, (connector, doc, p) =>
            {
                DuplicateElectrodeResult res = NewBuilder(connector).DuplicateElectrodeToNextGap(doc, p);
                MessageBox.Show(res.Message, "AutoEDM — Duplicar eletrodo", MessageBoxButtons.OK,
                    res.Created ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            });
        }

        /// <summary>
        /// GRAVADOR (Carlos, 2026-07-16) — passo 1: tira o snapshot das contagens das coleções
        /// (superfícies de construção + features do Model) do documento ativo. O usuário clica,
        /// faz a ação manual no SE (criar "Limite", costurar, estender/mover…) e depois clica em
        /// "Gravar log da leitura". Assim o AutoEDM dumpa exatamente as features que ele criou.
        /// </summary>
        private void IniciarLeitura()
        {
            dynamic app = ElectrodeAddIn.Current?.App;
            if (app == null) { MessageBox.Show("Add-in não inicializado.", "AutoEDM"); return; }
            try
            {
                Log.Info("===== INICIAR LEITURA DE AÇÃO MANUAL =====");
                dynamic doc = app.ActiveDocument;
                if (doc == null) { MessageBox.Show("Nenhum documento ativo.", "AutoEDM"); return; }
                if (!ConfirmDocParaGravacao(doc, "iniciar a gravação")) return;
                _recBaseline = ComDiagnostics.SnapshotInventory((object)doc);
                MessageBox.Show(
                    "Gravação INICIADA.\n\nAgora faça a ação manual no Solid Edge (ex.: criar a superfície 'Limite', costurar, estender/mover a face até o bloco).\n\n" +
                    "Quando terminar, clique em \"Gravar log da leitura\".",
                    "AutoEDM — Gravador", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { Fail("iniciar a leitura de ação manual", ex); }
        }

        /// <summary>
        /// GRAVADOR — passo 2: diffa contra o snapshot do "Iniciar leitura" e dumpa (SPY) as
        /// features NOVAS de cada coleção que cresceu — revela a coleção/tipo/propriedades das
        /// features que o usuário criou à mão, para reproduzir por COM.
        /// </summary>
        private void GravarLeitura()
        {
            dynamic app = ElectrodeAddIn.Current?.App;
            if (app == null) { MessageBox.Show("Add-in não inicializado.", "AutoEDM"); return; }
            try
            {
                Log.Info("===== GRAVAR LOG DA LEITURA =====");
                if (_recBaseline == null)
                {
                    MessageBox.Show("Clique primeiro em \"Iniciar leitura de ação manual\", faça a ação, e só então grave.", "AutoEDM — Gravador", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                dynamic doc = app.ActiveDocument;
                if (doc == null) { MessageBox.Show("Nenhum documento ativo.", "AutoEDM"); return; }
                if (!ConfirmDocParaGravacao(doc, "gravar o log")) return;
                ComDiagnostics.DumpNewSince((object)doc, _recBaseline);
                _recBaseline = null; // consome o snapshot (evita diff contra estado velho)
                MessageBox.Show(
                    "Leitura gravada no log (procure as linhas [REC] e [SPY]):\n%LOCALAPPDATA%\\AutoEDM\\logs\n\nMe mande esse log.",
                    "AutoEDM — Gravador", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Log.Info("===== FIM (GRAVAR LOG DA LEITURA) =====");
            }
            catch (Exception ex) { Fail("gravar o log da leitura", ex); }
        }

        /// <summary>
        /// SPY: dumpa o(s) objeto(s) COM selecionado(s) no SE (tipo + propriedades/valores +
        /// assinaturas de método/setter, recursivo 1 nível, coleções expandidas) para o log.
        /// Serve para descobrir a API REAL sem adivinhar — ex.: crie um furo (ou uma superfície
        /// copiada/offsetada/costurada) DO SEU JEITO no SE, selecione a feature e clique aqui: o
        /// log revela a coleção/método/propriedades para reproduzir por COM. Funciona em
        /// qualquer documento (peça/montagem). Cada clique TAMBÉM engorda o dump acumulado do
        /// SDK (%LOCALAPPDATA%\AutoEDM\logs\SE_API_dump_&lt;versão&gt;.txt) com a type library de
        /// tudo que foi tocado — é assim que Geometry/Assembly (que a sonda estática não
        /// alcançava) entram no mapa: basta selecionar uma Face/Edge/Ocorrência qualquer.
        /// </summary>
        private void InspecionarSelecao()
        {
            dynamic app = ElectrodeAddIn.Current?.App;
            if (app == null) { MessageBox.Show("Add-in não inicializado.", "AutoEDM"); return; }
            try
            {
                Log.Info("===== INSPECIONAR SELEÇÃO (SPY) =====");
                dynamic doc = app.ActiveDocument;
                if (doc == null) { MessageBox.Show("Nenhum documento ativo.", "AutoEDM"); return; }

                dynamic ss = doc.SelectSet;
                int n = 0; try { n = (int)ss.Count; } catch { }
                if (n == 0)
                {
                    MessageBox.Show(
                        "Nada selecionado. Selecione uma feature no SE (ex.: um furo, uma superfície copiada/offsetada) e clique de novo.\n\n" +
                        "O dump vai para o log (%LOCALAPPDATA%\\AutoEDM\\logs).",
                        "AutoEDM — Inspecionar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Log.Info("Inspecionar: nada selecionado.");
                    return;
                }

                Log.Info($"Inspecionando {n} objeto(s) selecionado(s) — veja [SPY] no log.");
                for (int i = 1; i <= n; i++)
                {
                    object item; try { item = ss.Item(i); } catch { continue; }
                    ComDiagnostics.DumpObject($"Seleção[{i}]", item, 1);
                }
                MessageBox.Show(
                    $"{n} objeto(s) inspecionado(s). Veja o dump [SPY] no log e o mapa acumulado da API:\n" +
                    "%LOCALAPPDATA%\\AutoEDM\\logs  (AutoEDM_*.log = [SPY]; SE_API_dump_<versão>.txt = mapa completo)",
                    "AutoEDM — Inspecionar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Log.Info("===== FIM (INSPECIONAR SELEÇÃO) =====");
            }
            catch (Exception ex) { Fail("inspecionar a seleção", ex); }
        }

        /// <summary>
        /// SONDA DA ROSCA M6 (diagnóstico): cria uma peça DESCARTÁVEL com um bloco e quatro
        /// furos M6, um por receita de API, e loga tudo (HoleData completo, Status da feature,
        /// CreatePhysicalThread, código de erro da rosca física e o Ø REAL medido de cada
        /// cilindro). Roda numa peça própria de propósito — um Holes.AddSync que falha envenena
        /// o proxy do documento e derrubaria a furação que já funciona no fluxo do eletrodo.
        /// Também confere a opção GLOBAL de exibição de rosca do SE: com ela desligada, mesmo o
        /// furo roscado certo desenha liso. Ver <see cref="AutoEDM.Electrode.ThreadProbe"/>.
        /// </summary>
        private void DiagnosticarRosca()
        {
            dynamic app = ElectrodeAddIn.Current?.App;
            if (app == null) { MessageBox.Show("Add-in não inicializado.", "AutoEDM"); return; }

            var confirm = MessageBox.Show(
                "Isto vai CRIAR uma PEÇA NOVA (descartável, não salva) com um bloco 120×40×15 e quatro furos M6 — " +
                "cada um por uma receita diferente da API de rosca.\n\n" +
                "Depois: olhe a peça e me diga em qual X a rosca saiu CORTADA de verdade\n" +
                "(1=-42  2=-14  3=+14  4=+42 mm) e me mande o log.\n\nContinuar?",
                "AutoEDM — Sonda de rosca M6", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) { Log.Info("Sonda de rosca cancelada pelo usuário."); return; }

            try
            {
                bool displayOff = ThreadProbe.ThreadedDisplayIsOff(app);
                ThreadProbe.RunM6Matrix(app);

                // A exibição de rosca é uma OPÇÃO DO APLICATIVO, não do documento: mexer nela
                // muda o Solid Edge inteiro do usuário, então só com o "sim" dele.
                if (displayOff)
                {
                    var liga = MessageBox.Show(
                        "A opção GLOBAL do Solid Edge \"exibir roscas\" está DESLIGADA nesta instalação.\n\n" +
                        "Com ela desligada, um furo roscado CORRETO aparece liso na tela — dá para confundir " +
                        "com \"a rosca não foi criada\".\n\n" +
                        "Ligar agora? (é uma opção do Solid Edge inteiro, não só desta peça)",
                        "AutoEDM — exibição de rosca", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (liga == DialogResult.Yes) ThreadProbe.EnableThreadedDisplay(app);
                    else Log.Info("Exibição de rosca deixada DESLIGADA a pedido do usuário.");
                }

                MessageBox.Show(
                    "Sonda concluída. Na peça nova, os quatro furos M6 (X = -42, -14, +14, +42 mm):\n\n" +
                    "1 (-42): base de furos + rosca só como anotação\n" +
                    "2 (-14): base de furos + hélice pedida na CRIAÇÃO (AddSyncEx)\n" +
                    "3 (+14): base de furos + hélice pedida DEPOIS do furo\n" +
                    "4 (+42): valores na mão, sem a base de furos\n\n" +
                    "O log traz o Ø REAL medido de cada furo — o esperado é 5,0 mm (broca de M6), não 6,0.\n\n" +
                    "Diga qual saiu com a hélice CORTADA e mande o log (%LOCALAPPDATA%\\AutoEDM\\logs).",
                    "AutoEDM — Sonda de rosca M6", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { Fail("rodar a sonda de rosca M6", ex); }
        }

        /// <summary>
        /// Alojamento de anel O'ring (ambiente de PEÇA). Abre a janela MODELESS — a única do
        /// add-in — porque o fluxo é "selecione uma face, depois uma aresta" e uma janela modal
        /// congelaria o Solid Edge justamente na hora de clicar no modelo.
        ///
        /// A janela é guardada num campo estático: um Form modeless que sai de escopo é
        /// coletado e some da tela sem erro nenhum. Se já estiver aberta, traz para a frente
        /// em vez de abrir uma segunda.
        /// </summary>
        private static ORingGrooveForm _oringForm;

        private void AlojamentoORing()
        {
            if (!AllowedHere(CmdAlojamentoORing, explain: true)) return;
            dynamic app = ElectrodeAddIn.Current?.App;
            if (app == null) { MessageBox.Show("Add-in não inicializado.", "AutoEDM"); return; }
            try
            {
                if (_oringForm != null && !_oringForm.IsDisposed)
                {
                    _oringForm.BringToFront();
                    _oringForm.Activate();
                    return;
                }

                Log.Info("===== ALOJAMENTO DE O'RING (janela aberta) =====");
                var catalog = ORingCatalog.LoadOrCreateDefault();
                _oringForm = new ORingGrooveForm((object)app, catalog);
                _oringForm.FormClosed += (s, e) => { _oringForm = null; Log.Info("===== FIM (ALOJAMENTO DE O'RING) ====="); };
                _oringForm.Show();
            }
            catch (Exception ex) { Fail("abrir o alojamento de O'ring", ex); }
        }

        // -------------------------------------------------------------- infra

        /// <summary>Tipo de documento exigido por um comando (= SolidEdgeFramework.DocumentTypeConstants).</summary>
        private enum DocKind { Any = 0, Part = 1, Assembly = 3 } // igPartDocument / igAssemblyDocument

        /// <summary>O que um comando exige do documento ativo para PODER rodar.</summary>
        private sealed class CommandSpec
        {
            public readonly string Title;
            public readonly DocKind Kind;
            public readonly ModelingEnv Env;
            public CommandSpec(string title, DocKind kind, ModelingEnv env) { Title = title; Kind = kind; Env = env; }
        }

        /// <summary>
        /// PRÉ-REQUISITO DE CADA BOTÃO, num lugar só (Carlos, 2026-09-08). Antes cada comando
        /// declarava apenas o TIPO de documento, e o ambiente de modelagem era resolvido lá
        /// dentro — às vezes TROCANDO o modo da peça pelas costas do usuário. Daí saíram os
        /// dois problemas relatados:
        ///
        /// • esboços presos entre síncrono e ordenado, impossíveis de apagar: o alojamento de
        ///   O'ring criava esboço ORDENADO (<c>ProfileSets</c> só faz desse tipo) e o consumia
        ///   com um recurso SÍNCRONO — o esboço fica órfão no nó "Ordenado" do PathFinder e a
        ///   interface do SE se recusa a removê-lo;
        /// • "Aplicar GAP" errando quando a peça estava em síncrono: ele coletava as faces
        ///   selecionadas, TROCAVA o documento para ordenado e só então pintava/offsetava — a
        ///   troca reconstrói o corpo, e as faces lidas antes dela viram proxies mortos.
        ///
        /// A regra passa a ser uma só, para todo botão: <b>o comando declara o ambiente que
        /// exige; o AutoEDM nunca troca o ambiente da peça.</b> No ambiente errado o botão fica
        /// CINZA (<see cref="RefreshControlStates"/>) e, se ainda assim for clicado, explica ao
        /// usuário como trocar no SE.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<int, CommandSpec> Specs =
            new System.Collections.Generic.Dictionary<int, CommandSpec>
            {
                // --- montagem: nada aqui depende do ambiente de modelagem de peça ---
                { CmdAnalisarZ,           new CommandSpec("ANALISAR ELETRODOS (Z)",               DocKind.Assembly, ModelingEnv.Any) },
                { CmdCriarEletrodos,      new CommandSpec("CRIAR ELETRODOS",                      DocKind.Assembly, ModelingEnv.Any) },
                { CmdCriarEletrodoManual, new CommandSpec("CRIAR ELETRODO (MANUAL, da seleção)",  DocKind.Assembly, ModelingEnv.Any) },
                { CmdDuplicarEletrodo,    new CommandSpec("DUPLICAR ELETRODO",                    DocKind.Assembly, ModelingEnv.Any) },
                { CmdCoordenadas,         new CommandSpec("COORDENADAS (eletrodos selecionados)", DocKind.Assembly, ModelingEnv.Any) },
                { CmdSpecSheet,           new CommandSpec("FICHA DE ELETRODOS (spec-sheet)",      DocKind.Assembly, ModelingEnv.Any) },

                // --- peça: cada botão tem UM ambiente, o do recurso que ele cria ---
                // Bloco + faixa + furos: extrusão e furação da família síncrona (BlankModeler),
                // validadas em campo com a peça em síncrono (ModelingMode=1 nos logs 55/58).
                { CmdCriarBase,           new CommandSpec("CRIAR BASE",           DocKind.Part, ModelingEnv.Synchronous) },
                // "Limite", Costurar, Anexar e a booleana de união são SÍNCRONAS — em ordenado
                // o botão criava só uma feature de costura e não unia (relato de 2026-07-20).
                { CmdUnirSuperficies,     new CommandSpec("UNIR SUPERFÍCIES",     DocKind.Part, ModelingEnv.Synchronous) },
                // Model.FaceOffsets (o GAP que fica editável na árvore) só existe em ORDENADO.
                { CmdAplicarGap,          new CommandSpec("APLICAR GAP",          DocKind.Part, ModelingEnv.Ordered) },
                // Esboço + corte revolvido: em ORDENADO o esboço é filho legítimo do recurso e
                // some junto quando o usuário apaga o canal. Em síncrono viraria órfão.
                { CmdAlojamentoORing,     new CommandSpec("ALOJAMENTO DE O'RING", DocKind.Part, ModelingEnv.Ordered) },

                // --- diagnóstico: só leem, ou criam documento próprio — servem em qualquer ambiente ---
                { CmdInspecionar,         new CommandSpec("INSPECIONAR SELEÇÃO",   DocKind.Any, ModelingEnv.Any) },
                { CmdIniciarLeitura,      new CommandSpec("INICIAR LEITURA",       DocKind.Any, ModelingEnv.Any) },
                { CmdGravarLeitura,       new CommandSpec("GRAVAR LOG DA LEITURA", DocKind.Any, ModelingEnv.Any) },
                { CmdDiagRosca,           new CommandSpec("SONDA DE ROSCA M6",     DocKind.Any, ModelingEnv.Any) },
                // Sonda do inter-part: lê a montagem e trabalha em peça DESCARTÁVEL. Serve
                // tanto com a montagem normal quanto com o usuário em edição em contexto —
                // é justamente a diferença entre os dois estados que ela mede.
                { CmdSondaInterPart,      new CommandSpec("SONDA INTER-PART",      DocKind.Assembly, ModelingEnv.Any) },
            };

        /// <summary>
        /// O documento ativo atende ao pré-requisito deste comando? Com
        /// <paramref name="explain"/>, também diz ao usuário o que falta — o clique explica, o
        /// relógio de estado não (senão abriria caixas de diálogo sozinho).
        /// </summary>
        private static bool AllowedHere(int commandId, bool explain)
        {
            CommandSpec spec;
            if (!Specs.TryGetValue(commandId, out spec)) return true; // comando sem pré-requisito declarado

            dynamic app = ElectrodeAddIn.Current?.App;
            if (app == null)
            {
                if (explain) MessageBox.Show("Add-in não inicializado.", "AutoEDM");
                return false;
            }

            dynamic doc = null;
            try { doc = app.ActiveDocument; } catch { }
            if (doc == null)
            {
                if (explain) MessageBox.Show("Nenhum documento ativo.", "AutoEDM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (spec.Kind != DocKind.Any)
            {
                int type = -1; try { type = (int)doc.Type; } catch { }
                if (type != (int)spec.Kind)
                {
                    if (explain)
                        MessageBox.Show(spec.Kind == DocKind.Assembly
                            ? "Abra uma MONTAGEM (.asm) ativa (a cavidade no zero-máquina) para usar esta ferramenta."
                            : "Abra uma PEÇA (.par) ativa, com as faces de queima já copiadas nela, para usar esta ferramenta.",
                            "AutoEDM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
            }

            ModelingEnv actual = ModelingEnvironment.Read(doc);
            if (!ModelingEnvironment.Matches(spec.Env, actual))
            {
                if (explain)
                    MessageBox.Show(ModelingEnvironment.WrongEnvironmentMessage(spec.Title, spec.Env, actual),
                        "AutoEDM — ambiente de modelagem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        // --------------------------------------------- botão cinza quando o ambiente é o errado

        /// <summary>
        /// O Solid Edge pergunta o estado de cada comando do add-in enquanto a faixa está
        /// visível (<c>ISEAddInEventsEx.OnCommandUpdateUI</c>) e o framework responde com o que
        /// estiver em <c>RibbonControl.Enabled</c> naquele instante — é uma propriedade comum
        /// (auto-property), sem callback para interceptar. Então quem mantém esse valor fresco
        /// é este relógio: a cada tique ele relê documento + ambiente e liga/desliga cada botão.
        ///
        /// Roda na thread STA da própria SE (o add-in é in-process), então ler COM daqui é
        /// seguro e não passa por marshaling. Tudo é best-effort: um tique que falhe não pode
        /// derrubar a ribbon e, no pior caso, o botão continua clicável — por isso o clique
        /// ainda passa por <see cref="AllowedHere"/>.
        /// </summary>
        private Timer _stateTimer;
        private bool _refreshing;

        private void StartStateWatch()
        {
            try
            {
                _stateTimer = new Timer { Interval = 750 };
                _stateTimer.Tick += (s, e) => RefreshControlStates();
                _stateTimer.Start();
            }
            catch (Exception ex) { Log.Warn("Estado dos botões: relógio não iniciou — " + ex.GetBaseException().Message); }
        }

        private void RefreshControlStates()
        {
            if (_refreshing) return;                 // um tique lento não pode empilhar o próximo
            _refreshing = true;
            try
            {
                foreach (RibbonControl c in Controls)
                {
                    bool allowed = AllowedHere(c.CommandId, explain: false);
                    if (c.Enabled != allowed) c.Enabled = allowed;
                }
            }
            catch { /* estado do botão é conforto, nunca motivo de erro dentro da SE */ }
            finally { _refreshing = false; }
        }

        /// <summary>
        /// Boilerplate comum aos comandos: confere o pré-requisito declarado em
        /// <see cref="Specs"/> (tipo de documento + ambiente de modelagem), conecta, loga
        /// início/fim e envolve qualquer exceção em <see cref="Fail"/> — nenhuma exceção sobe
        /// direto pro Solid Edge por esquecimento de try/catch (achado A1 da revisão do
        /// add-in). Os 3 comandos de diagnóstico/gravador
        /// (IniciarLeitura/GravarLeitura/InspecionarSelecao) têm fluxo próprio
        /// (ConfirmDocParaGravacao) — ficam fora deste wrapper de propósito.
        /// </summary>
        private void Run(int commandId, Action<SolidEdgeConnector, dynamic, ElectrodeParams> body)
        {
            CommandSpec spec = Specs[commandId];
            if (!AllowedHere(commandId, explain: true)) return;

            dynamic app = ElectrodeAddIn.Current?.App;
            dynamic doc = null;
            try { doc = app?.ActiveDocument; } catch { }
            if (app == null || doc == null) { MessageBox.Show("Nenhum documento ativo.", "AutoEDM"); return; }

            try
            {
                Log.Info($"===== {spec.Title} (add-in) — modelagem {ModelingEnvironment.Name(ModelingEnvironment.Read(doc))} =====");
                body(SolidEdgeConnector.Attach(app), doc, LoadParams());
                Log.Info($"===== FIM ({spec.Title}) =====");
            }
            catch (Exception ex) { Fail(spec.Title.ToLowerInvariant(), ex); }
        }

        /// <summary>
        /// Configuração do usuário (revisão A3, docs/REVISAO-AutoEDM.md): lida uma vez por
        /// sessão do add-in de %LOCALAPPDATA%\AutoEDM\config.json (criado com os defaults de
        /// sempre se ainda não existir). Prefixo do eletrodo, tabela de Ra e mapa de cor de
        /// queima passam a vir daqui em vez de fixos no código.
        /// </summary>
        private static AutoEdmConfig _config;
        private static AutoEdmConfig Config => _config ?? (_config = AutoEdmConfig.LoadOrCreateDefault());

        private static ElectrodeParams LoadParams() => Config.ToElectrodeParams();

        /// <summary>Novo <see cref="ElectrodeBuilder"/> já com a tabela de Ra e o mapa de cor
        /// desta configuração — sem isso, a detecção de queima e o cálculo de GAP usariam
        /// sempre a paleta de fábrica mesmo com um config.json customizado.</summary>
        private static ElectrodeBuilder NewBuilder(SolidEdgeConnector connector) =>
            new ElectrodeBuilder(connector, offsetPolicy: Config.BuildOffsetPolicy(), raColorMap: Config.BuildColorMap());

        /// <summary>
        /// Avisa (não bloqueia) quando o documento ativo NÃO é uma PEÇA — o Gravador quase
        /// sempre registra ação manual numa peça (fechar/costurar/estender superfície), e
        /// `Application.ActiveDocument` segue a janela do SE com FOCO no momento do clique: se
        /// a janela da MONTAGEM estiver na frente (em vez da peça) quando o usuário clica
        /// "Iniciar leitura" OU "Gravar log", o snapshot sai do documento errado e o diff dá
        /// "nada mudou" mesmo com features novas de verdade na peça (2026-07-17, log real: os
        /// dois cliques leram '14595.101_EDM.asm' Type=3 — a peça nunca foi vista). Pega o erro
        /// NA HORA em vez de só no fim, quando o trabalho manual já foi feito.
        /// </summary>
        private static bool ConfirmDocParaGravacao(dynamic doc, string acao)
        {
            int type = -1; string name = "?";
            try { type = (int)doc.Type; } catch { }
            try { name = (string)doc.Name; } catch { }
            if (type == 1) return true; // 1 = igPartDocument — caso normal, sem aviso

            string tipoDesc = type == 3 ? "uma MONTAGEM" : type == 2 ? "um DESENHO" : $"tipo {type}";
            var r = MessageBox.Show(
                $"O documento ativo agora é {tipoDesc} ('{name}'), não uma PEÇA.\n\n" +
                "O Gravador normalmente registra ações numa PEÇA (fechar/costurar/estender superfície de queima). " +
                "Se a janela da peça não estiver em foco no SE neste clique, a gravação sai vazia ou compara o documento errado.\n\n" +
                $"Trocar para a janela da peça e clicar de novo é o mais seguro. Continuar mesmo assim ({acao})?",
                "AutoEDM — Gravador", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            return r == DialogResult.Yes;
        }

        private static void Fail(string what, Exception ex)
        {
            Log.Error($"Falha ao {what}.", ex);
            string logPath = ElectrodeAddIn.Current?.LogPath;
            string detail = string.IsNullOrEmpty(logPath)
                ? "Detalhes técnicos no log em %LOCALAPPDATA%\\AutoEDM\\logs."
                : $"Detalhes técnicos no log:\n{logPath}";
            MessageBox.Show($"Não foi possível {what}.\n\n{detail}", "AutoEDM — erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
