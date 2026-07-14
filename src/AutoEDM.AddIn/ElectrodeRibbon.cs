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
        private const int CmdBlocoSuperficies = 5; // Bloco sobre superfícies (ambiente de PEÇA)

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
                case CmdBlocoSuperficies: CriarBlocoSobreSuperficies(); break;
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
        /// Bloco sobre superfícies (ambiente de PEÇA): abre a janela com parâmetros +
        /// Preview. O usuário já copiou as faces de queima na peça manualmente; o AutoEDM
        /// cria o bloco, estende/fecha/une as superfícies e aplica a fixação. ESCREVE na peça.
        /// </summary>
        private void CriarBlocoSobreSuperficies()
        {
            if (!TryPart(out dynamic app, out dynamic doc)) return;
            try
            {
                Log.Info("===== BLOCO SOBRE SUPERFÍCIES (add-in) =====");
                // Garante o log de arquivo do add-in; o Core loga cada passo + o probe.
                var builder = new SurfaceBlockBuilder();
                using (var form = new BlockOverSurfacesForm(builder, (object)doc))
                {
                    form.ShowDialog();
                }
                Log.Info("===== FIM (BLOCO SOBRE SUPERFÍCIES) =====");
            }
            catch (Exception ex) { Fail("criar o bloco sobre superfícies", ex); }
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

        /// <summary>Valida que o documento ativo é uma PEÇA (.par) — para o "Bloco sobre superfícies".</summary>
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

        private static void Fail(string what, Exception ex)
        {
            Log.Error($"Falha ao {what}.", ex);
            MessageBox.Show(ex.GetBaseException().Message, "AutoEDM — erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
