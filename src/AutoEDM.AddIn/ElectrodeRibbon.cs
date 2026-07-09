using System;
using System.Windows.Forms;
using SolidEdgeCommunity.AddIn;
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

            var confirm = MessageBox.Show(
                "Isto vai CRIAR uma peça por eletrodo (com o bloco da base) na subpasta " +
                "'Eletrodos' ao lado da montagem e inseri-las posicionadas.\n\n" +
                "A montagem NÃO será salva automaticamente. Continuar?",
                "AutoEDM — Criar eletrodos", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            try
            {
                Log.Info("===== CRIAR ELETRODOS + BASE (add-in) =====");
                var connector = SolidEdgeConnector.Attach(app);
                var p = new ElectrodeParams { ElectrodeName = "ELD" };
                int n = new ElectrodeBuilder(connector).CreateElectrodesWithBlank(doc, p);
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

        private static void Fail(string what, Exception ex)
        {
            Log.Error($"Falha ao {what}.", ex);
            MessageBox.Show(ex.GetBaseException().Message, "AutoEDM — erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
