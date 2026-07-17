using System;
using System.Windows.Forms;
using SolidEdgeCommunity.AddIn;
using AutoEDM.AddIn.UI;
using AutoEDM.Com;
using AutoEDM.Diagnostics;
using AutoEDM.Electrode;
using AutoEDM.Model;
using AutoEDM.Reporting;

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
        private const int CmdRelatorio = 2;      // Coordenadas de queima
        private const int CmdAnalisarZ = 3;      // Analisar eletrodos por Z
        private const int CmdSpecSheet = 4;      // Ficha (spec-sheet)
        private const int CmdCriarBase = 5;       // Criar Base (ambiente de PEÇA)
        private const int CmdInspecionar = 6;       // SPY: dumpa o objeto COM selecionado
        private const int CmdUnirSuperficies = 7;   // Engrossar a queima e unir ao bloco (ISOLADO)
        private const int CmdIniciarLeitura = 8;    // Gravador: snapshot inicial (ação manual)
        private const int CmdGravarLeitura = 9;     // Gravador: diff + dump das features novas

        /// <summary>Snapshot (nomes dos itens por coleção) no "Iniciar leitura" — diffado no "Gravar log".</summary>
        private static System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> _recBaseline;

        public ElectrodeRibbon() : base()
        {
            LoadXml(System.Reflection.Assembly.GetExecutingAssembly(), "AutoEDM.AddIn.Ribbon.xml");
        }

        public override void OnControlClick(RibbonControl control)
        {
            switch (control.CommandId)
            {
                case CmdCriarEletrodos: CriarEletrodos(); break;
                case CmdRelatorio: GerarRelatorioCoordenadas(); break;
                case CmdAnalisarZ: AnalisarZ(); break;
                case CmdSpecSheet: GerarSpecSheet(); break;
                case CmdCriarBase: CriarBase(); break;
                case CmdUnirSuperficies: UnirSuperficies(); break;
                case CmdInspecionar: InspecionarSelecao(); break;
                case CmdIniciarLeitura: IniciarLeitura(); break;
                case CmdGravarLeitura: GravarLeitura(); break;
            }
        }

        // -------------------------------------------------------------- comandos

        /// <summary>Analisa (NÃO-destrutivo) e propõe os eletrodos por nível de Z.</summary>
        private void AnalisarZ()
        {
            Run("ANALISAR ELETRODOS (Z)", (connector, doc, p) =>
            {
                Selection.ZAnalysisResult res = new ElectrodeBuilder(connector).AnalyzeElectrodesByZ(doc, p);
                MessageBox.Show(
                    $"{res.Electrodes.Count} eletrodo(s) proposto(s), por nível de Z.\n" +
                    $"({res.FlatFaces} piso / {res.SteepFaces} parede)\n\nVeja as posições no log.",
                    "AutoEDM — Analisar eletrodos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        /// <summary>Cria as peças de eletrodo com a base (holder) e as posiciona. ESCREVE na montagem.</summary>
        private void CriarEletrodos()
        {
            if (!TryAssembly(out dynamic app, out dynamic doc)) return;

            try
            {
                Log.Info("===== CRIAR ELETRODOS + BASE (add-in) =====");
                var connector = SolidEdgeConnector.Attach(app);
                var p = new ElectrodeParams { ElectrodeName = "ELD" };
                var builder = new ElectrodeBuilder(connector);

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
                    Log.Info("===== FIM (CRIAR ELETRODOS — nada detectado) =====");
                    return;
                }

                var icon = res.HasDominantUnmappedColor ? MessageBoxIcon.Warning : MessageBoxIcon.Question;
                var confirm = MessageBox.Show(
                    res.DescribeBurnDetection() +
                    "\n\nIsto vai CRIAR uma peça por eletrodo (com o bloco da base) na subpasta 'Eletrodos' " +
                    "ao lado da montagem e inseri-las posicionadas. A montagem NÃO será salva automaticamente.\n\n" +
                    "A queima detectada está CORRETA? Criar os eletrodos?",
                    "AutoEDM — Conferir antes de criar", MessageBoxButtons.YesNo, icon);
                if (confirm != DialogResult.Yes) { Log.Info("Criação cancelada pelo usuário (conferência)."); return; }

                int n = builder.CreateElectrodesWithBlank(doc, p);
                MessageBox.Show(
                    $"{n} eletrodo(s) criado(s) e posicionado(s).\n\n" +
                    "Revise, SALVE a montagem e copie as faces de queima em cada base.",
                    "AutoEDM — Criar eletrodos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Log.Info("===== FIM (CRIAR ELETRODOS) =====");
            }
            catch (Exception ex) { Fail("criar os eletrodos", ex); }
        }

        /// <summary>Relatório de coordenadas de queima (.txt + .csv). Somente leitura.</summary>
        private void GerarRelatorioCoordenadas()
        {
            Run("RELATÓRIO DE COORDENADAS", (connector, doc, p) =>
            {
                BurnCoordinateReport report = new ElectrodeBuilder(connector).BuildBurnReport(doc, p);
                Log.Info(BurnReportFormatter.ToText(report));
                string path = BurnReportWriter.Save(report);
                MessageBox.Show(
                    $"Coordenadas geradas: {report.Coordinates.Count}\n" +
                    $"Cavidade: {report.TargetOccurrenceName ?? "—"}\n\nArquivos (.txt e .csv):\n{path}",
                    "AutoEDM — Coordenadas de queima", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        /// <summary>Folha de dados (spec-sheet) por eletrodo (.txt + .csv). Somente leitura.</summary>
        private void GerarSpecSheet()
        {
            Run("FICHA DE ELETRODOS (spec-sheet)", (connector, doc, p) =>
            {
                ElectrodeBuildPlan plan = new ElectrodeBuilder(connector).PlanFromAssemblyDocument(doc, p);
                Log.Info(ElectrodeSpecSheet.ToText(plan, p));
                string path = ElectrodeSpecSheetWriter.Save(plan, p);
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
            if (!TryPart(out dynamic app, out dynamic doc)) return;
            try
            {
                Log.Info("===== CRIAR BASE (add-in) =====");
                // Garante o log de arquivo do add-in; o Core loga cada passo + o probe.
                var builder = new SurfaceBlockBuilder();
                using (var form = new BlockOverSurfacesForm(builder, (object)app, (object)doc))
                {
                    form.ShowDialog();
                }
                Log.Info("===== FIM (CRIAR BASE) =====");
            }
            catch (Exception ex) { Fail("criar a base", ex); }
        }

        /// <summary>
        /// Botão ISOLADO "Unir superfícies" (ambiente de PEÇA): engrossa a superfície de
        /// queima selecionada para cima até dentro do bloco e une num sólido único. Separado
        /// do "Criar Base" porque o thicken, ao falhar, envenenava o doc e
        /// derrubava a fixação — aqui o experimento fica isolado. ESCREVE na peça.
        /// </summary>
        private void UnirSuperficies()
        {
            if (!TryPart(out dynamic app, out dynamic doc)) return;
            try
            {
                Log.Info("===== UNIR SUPERFÍCIES (add-in) =====");

                // Lista suspensa Ra→GAP→cor (Carlos, 2026-07-17): escolher Ra já aplica o GAP
                // (Model.FaceOffsets, editável em Ordenado) e a cor; "Só diagnosticar" mantém o
                // comportamento antigo (só relata arestas abertas, não altera a peça).
                var opt = new BlockOverSurfacesOptions();
                using (var picker = new RaGapPickerForm())
                {
                    if (picker.ShowDialog() == DialogResult.OK && picker.Chosen != null)
                    {
                        opt.UniteChoice = picker.Chosen;
                        Log.Info($"Unir: escolhido {picker.Chosen.Label}.");
                    }
                }

                var builder = new SurfaceBlockBuilder();
                BlockOverSurfacesResult res = builder.UniteSurfacesToBlock(doc, opt);
                string msg = res.SurfacesUnited
                    ? $"Superfície unida ao bloco{(res.SurfacesOffset ? " + GAP aplicado" : "")}. Confira no modelo.\n\nDetalhe no log (linhas 'Unir:')."
                    : "Não uni as superfícies ao bloco ainda.\n\nVeja o log (linhas 'Unir:') para o ponto exato — o bloco/faixa/furos NÃO foram tocados.";
                MessageBox.Show(msg, "AutoEDM — Unir superfícies", MessageBoxButtons.OK,
                    res.SurfacesUnited ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                Log.Info("===== FIM (UNIR SUPERFÍCIES) =====");
            }
            catch (Exception ex) { Fail("unir as superfícies", ex); }
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

        // -------------------------------------------------------------- infra

        /// <summary>Boilerplate comum dos comandos SOMENTE LEITURA: valida montagem, conecta, roda.</summary>
        private void Run(string title, Action<SolidEdgeConnector, object, ElectrodeParams> body)
        {
            if (!TryAssembly(out dynamic app, out dynamic doc)) return;
            try
            {
                Log.Info($"===== {title} (add-in) =====");
                var connector = SolidEdgeConnector.Attach(app);
                var p = new ElectrodeParams { ElectrodeName = "ELD" };
                body(connector, (object)doc, p);
                Log.Info($"===== FIM ({title}) =====");
            }
            catch (Exception ex) { Fail(title.ToLowerInvariant(), ex); }
        }

        private static bool TryAssembly(out dynamic app, out dynamic doc)
        {
            app = ElectrodeAddIn.Current?.App;
            doc = null;
            if (app == null) { MessageBox.Show("Add-in não inicializado.", "AutoEDM"); return false; }
            doc = app.ActiveDocument;
            if (doc == null || (int)doc.Type != 3) // 3 = igAssemblyDocument
            {
                MessageBox.Show(
                    "Abra uma MONTAGEM (.asm) ativa (a cavidade no zero-máquina) para usar esta ferramenta.",
                    "AutoEDM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        /// <summary>Valida que o documento ativo é uma PEÇA (.par) — para "Criar Base"/"Unir superfícies".</summary>
        private static bool TryPart(out dynamic app, out dynamic doc)
        {
            app = ElectrodeAddIn.Current?.App;
            doc = null;
            if (app == null) { MessageBox.Show("Add-in não inicializado.", "AutoEDM"); return false; }
            doc = app.ActiveDocument;
            if (doc == null || (int)doc.Type != 1) // 1 = igPartDocument
            {
                MessageBox.Show(
                    "Abra uma PEÇA (.par) ativa, com as faces de queima já copiadas nela, para usar esta ferramenta.",
                    "AutoEDM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

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
            MessageBox.Show(ex.GetBaseException().Message, "AutoEDM — erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
