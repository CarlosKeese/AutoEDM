using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using AutoEDM.Com;
using AutoEDM.Diagnostics;
using AutoEDM.Electrode;
using AutoEDM.Model;
using AutoEDM.Reporting;

namespace AutoEDM.UI
{
    /// <summary>
    /// Tela de monitoramento do AutoEDM. Um botão inicia a extração de eletrodos da
    /// montagem ATIVA no Solid Edge; os painéis mostram, ao vivo, tudo o que
    /// acontece na comunicação COM (log com nível/cor), o estado da conexão, as
    /// regiões/eletrodos planejados e contadores de Info/Aviso/Erro. Todo o log é
    /// gravado em arquivo para análise posterior.
    ///
    /// A automação do Solid Edge roda numa thread STA dedicada; as mensagens de log
    /// (disparadas nessa thread via <see cref="Log.OnMessage"/>) são marshaladas
    /// para a thread da UI.
    /// </summary>
    public sealed class MainForm : Form
    {
        private Button _btnStart;
        private Button _btnReport;
        private Button _btnSpec;
        private Button _btnZ;
        private Button _btnModelTest;
        private Button _btnCopyTest;
        private Button _btnSurf;
        private Button _btnSaveLog;
        private Button _btnClear;
        private RichTextBox _log;
        private ListView _lvRegions;
        private Label _lblSeVersion, _lblActiveDoc, _lblDocType, _lblTarget;
        private ToolStripStatusLabel _status;
        private FileLogSink _fileSink;

        private int _info, _warn, _err;

        public MainForm()
        {
            BuildUi();
            Load += OnLoad;
            FormClosing += OnClosing;
        }

        // ---------------------------------------------------------------- UI

        private void BuildUi()
        {
            Text = "AutoEDM — Monitor de Eletrodos (Solid Edge COM)";
            Width = 2030;
            Height = 720;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);

            // Centro: log ao vivo (adicionado PRIMEIRO para ficar como Fill).
            var logGroup = new GroupBox { Text = "Comunicação COM — log ao vivo", Dock = DockStyle.Fill, Padding = new Padding(6) };
            _log = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(24, 24, 24),
                ForeColor = Color.Gainsboro,
                Font = new Font("Consolas", 9f),
                BorderStyle = BorderStyle.None,
                WordWrap = false,
                HideSelection = false
            };
            logGroup.Controls.Add(_log);

            // Painel direito.
            var right = new Panel { Dock = DockStyle.Right, Width = 320, Padding = new Padding(8) };

            var connGroup = new GroupBox { Text = "Conexão", Dock = DockStyle.Top, Height = 140 };
            _lblSeVersion = MakeInfo(connGroup, "Solid Edge:", "—", 0);
            _lblActiveDoc = MakeInfo(connGroup, "Documento:", "—", 1);
            _lblDocType = MakeInfo(connGroup, "Tipo:", "—", 2);
            _lblTarget = MakeInfo(connGroup, "Ocorrência-alvo:", "—", 3);

            var regGroup = new GroupBox { Text = "Regiões / Eletrodos planejados", Dock = DockStyle.Fill };
            _lvRegions = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            _lvRegions.Columns.Add("Det", 40);
            _lvRegions.Columns.Add("Ra", 40);
            _lvRegions.Columns.Add("Cor RGB", 80);
            _lvRegions.Columns.Add("Faces", 45);
            _lvRegions.Columns.Add("Blank", 70);
            _lvRegions.Columns.Add("Passes (offset)", 200);
            regGroup.Controls.Add(_lvRegions);

            // GroupBox empilha na ordem inversa de Dock: adiciona Fill antes do Top.
            right.Controls.Add(regGroup);
            right.Controls.Add(connGroup);

            // Topo: barra de ações.
            var top = new Panel { Dock = DockStyle.Top, Height = 64, Padding = new Padding(8) };
            _btnStart = new Button
            {
                Text = "▶  Projeto de eletrodos (plano)",
                Left = 8, Top = 8, Width = 300, Height = 44,
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };
            _btnStart.Click += (s, e) => StartBuild();

            _btnModelTest = new Button
            {
                Text = "🧱  Testar bloco (blank)",
                Left = 316, Top = 8, Width = 230, Height = 44,
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                BackColor = Color.FromArgb(96, 96, 96),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };
            _btnModelTest.Click += (s, e) => StartModelTest();

            _btnCopyTest = new Button
            {
                Text = "🏗️  Criar eletrodos + bloco",
                Left = 552, Top = 8, Width = 190, Height = 44,
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                BackColor = Color.FromArgb(120, 80, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };
            _btnCopyTest.Click += (s, e) => StartCopyTest();

            _btnReport = new Button
            {
                Text = "📄  Relatório de coordenadas",
                Left = 750, Top = 8, Width = 250, Height = 44,
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                BackColor = Color.FromArgb(0, 130, 90),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };
            _btnReport.Click += (s, e) => StartReport();

            _btnSpec = new Button
            {
                Text = "📋  Spec-sheet de eletrodos",
                Left = 1008, Top = 8, Width = 250, Height = 44,
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                BackColor = Color.FromArgb(70, 110, 160),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };
            _btnSpec.Click += (s, e) => StartSpecSheet();

            _btnZ = new Button
            {
                Text = "🧠  Analisar eletrodos (Z)",
                Left = 1266, Top = 8, Width = 244, Height = 44,
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                BackColor = Color.FromArgb(110, 70, 150),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };
            _btnZ.Click += (s, e) => StartZAnalysis();

            _btnSurf = new Button
            {
                Text = "🧩  Bloco sobre superfícies",
                Left = 1514, Top = 8, Width = 250, Height = 44,
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                BackColor = Color.FromArgb(150, 90, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };
            _btnSurf.Click += (s, e) => StartBlockOverSurfaces();

            _btnClear = new Button { Text = "Limpar", Dock = DockStyle.Right, Width = 90, Height = 44 };
            _btnClear.Click += (s, e) => ClearLog();
            _btnSaveLog = new Button { Text = "Salvar log...", Dock = DockStyle.Right, Width = 110, Height = 44 };
            _btnSaveLog.Click += (s, e) => SaveLog();

            top.Controls.Add(_btnStart);
            top.Controls.Add(_btnModelTest);
            top.Controls.Add(_btnCopyTest);
            top.Controls.Add(_btnReport);
            top.Controls.Add(_btnSpec);
            top.Controls.Add(_btnZ);
            top.Controls.Add(_btnSurf);
            top.Controls.Add(_btnSaveLog);
            top.Controls.Add(_btnClear);

            // Rodapé: status + contadores + arquivo de log.
            var statusStrip = new StatusStrip();
            _status = new ToolStripStatusLabel("Ocioso") { Spring = false };
            var counts = new ToolStripStatusLabel("Info 0   Avisos 0   Erros 0") { Spring = true, TextAlign = ContentAlignment.MiddleRight };
            var fileLbl = new ToolStripStatusLabel("log: —");
            statusStrip.Items.Add(_status);
            statusStrip.Items.Add(counts);
            statusStrip.Items.Add(fileLbl);
            _countsItem = counts;
            _fileItem = fileLbl;

            // Ordem de adição: Fill primeiro, depois bordas (regra de docking).
            Controls.Add(logGroup);
            Controls.Add(right);
            Controls.Add(top);
            Controls.Add(statusStrip);
        }

        private ToolStripStatusLabel _countsItem;
        private ToolStripStatusLabel _fileItem;

        private static Label MakeInfo(Control parent, string caption, string value, int row)
        {
            int y = 20 + row * 26;
            parent.Controls.Add(new Label { Text = caption, Left = 10, Top = y, Width = 110, ForeColor = Color.Gray });
            var val = new Label { Text = value, Left = 120, Top = y, Width = 185, Font = new Font("Segoe UI", 9f, FontStyle.Bold), AutoEllipsis = true };
            parent.Controls.Add(val);
            return val;
        }

        // ------------------------------------------------------------- lifecycle

        private void OnLoad(object sender, EventArgs e)
        {
            _fileSink = new FileLogSink();
            _fileItem.Text = "log: " + _fileSink.FilePath;
            Log.OnMessage += OnLog;
            Log.Info("AutoEDM iniciado. Abra a montagem no Solid Edge e clique em Iniciar.");
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            Log.OnMessage -= OnLog;
            _fileSink?.Dispose();
        }

        // ------------------------------------------------------------- log sink

        private void OnLog(LogLevel level, string message)
        {
            if (IsHandleCreated && InvokeRequired)
            {
                BeginInvoke((Action)(() => OnLog(level, message)));
                return;
            }

            if (level == LogLevel.Error) _err++;
            else if (level == LogLevel.Warn) _warn++;
            else _info++;
            if (_countsItem != null)
                _countsItem.Text = $"Info {_info}   Avisos {_warn}   Erros {_err}";

            Color c = level == LogLevel.Error ? Color.FromArgb(255, 105, 97)
                    : level == LogLevel.Warn ? Color.Goldenrod
                    : Color.Gainsboro;

            _log.SelectionStart = _log.TextLength;
            _log.SelectionColor = Color.DimGray;
            _log.AppendText($"{DateTime.Now:HH:mm:ss}  ");
            _log.SelectionColor = c;
            _log.AppendText($"[{level.ToString().ToUpperInvariant()}] {message}{Environment.NewLine}");
            _log.SelectionColor = _log.ForeColor;
            _log.ScrollToCaret();
        }

        // ------------------------------------------------------------- actions

        private void StartBuild()
            => RunOnSta("Analisando montagem...", (connector, app, doc) =>
            {
                var p = new ElectrodeParams { ElectrodeName = "ELD", Material = "Cobre" };
                Log.Info("Analisando a montagem ativa (leitura não-destrutiva)...");
                ElectrodeBuildPlan plan = new ElectrodeBuilder(connector).PlanFromAssemblyDocument(doc, p);
                PopulatePlan(plan);
                Log.Info($"Análise concluída: {plan.Regions.Count} região(ões) de queima.");
            });

        private void StartReport()
            => RunOnSta("Gerando relatório de coordenadas...", (connector, app, doc) =>
            {
                var p = new ElectrodeParams { ElectrodeName = "ELD", Material = "Cobre" };
                Log.Info("Gerando relatório de coordenadas de queima (leitura não-destrutiva)...");
                BurnCoordinateReport report = new ElectrodeBuilder(connector).BuildBurnReport(doc, p);

                // Ecoa a tabela no log ao vivo e grava .txt + .csv.
                foreach (var line in BurnReportFormatter.ToText(report).Split('\n'))
                    Log.Info(line.TrimEnd('\r'));
                string path = BurnReportWriter.Save(report);
                Log.Info($"Relatório salvo (.txt + .csv) em: {path}");
            });

        private void StartSpecSheet()
            => RunOnSta("Gerando spec-sheet de eletrodos...", (connector, app, doc) =>
            {
                var p = new ElectrodeParams { ElectrodeName = "ELD", Material = "Cobre" };
                Log.Info("Gerando folha de dados de eletrodos (leitura não-destrutiva)...");
                ElectrodeBuildPlan plan = new ElectrodeBuilder(connector).PlanFromAssemblyDocument(doc, p);
                PopulatePlan(plan);

                // Ecoa a folha no log ao vivo e grava .txt + .csv (1 linha/passe no CSV).
                foreach (var line in ElectrodeSpecSheet.ToText(plan, p).Split('\n'))
                    Log.Info(line.TrimEnd('\r'));
                string path = ElectrodeSpecSheetWriter.Save(plan, p);
                Log.Info($"Spec-sheet salva (.txt + .csv) em: {path}");
            });

        private void StartZAnalysis()
            => RunOnSta("Analisando eletrodos por Z...", (connector, app, doc) =>
            {
                var p = new ElectrodeParams { ElectrodeName = "ELD", Material = "Cobre" };
                Log.Info("Analisando eletrodos por níveis de Z (leitura não-destrutiva — NÃO cria peças)...");
                var res = new ElectrodeBuilder(connector).AnalyzeElectrodesByZ(doc, p);
                Log.Info($"Análise Z concluída: {res.Electrodes.Count} eletrodo(s) proposto(s) " +
                         $"({res.FlatFaces} piso / {res.SteepFaces} parede de {res.FacesWithBox} face(s) com bbox).");
                foreach (var w in res.Warnings) Log.Warn(w);
            });

        private void StartModelTest()
            => RunOnSta("Validando bloco do blank...", (connector, app, doc) =>
            {
                new Experiments.BlankBoxProbe().Run(connector);
            });

        private void StartCopyTest()
            => RunOnSta("Criando eletrodos + bloco...", (connector, app, doc) =>
            {
                var p = new ElectrodeParams { ElectrodeName = "ELD", Material = "Cobre" };
                var builder = new ElectrodeBuilder(connector);

                // CONFERIR ANTES DE CRIAR (Log 57): não criar em cor residual.
                var res = builder.AnalyzeElectrodesByZ(doc, p);
                if (res.Electrodes.Count == 0)
                {
                    Log.Warn("Nenhum eletrodo detectado (sem cor de queima MAPEADA). Nada será criado — confira as cores no log.");
                    return;
                }
                foreach (var line in res.DescribeBurnDetection().Split('\n')) Log.Info(line.TrimEnd('\r'));
                if (!ConfirmOnUi(res.DescribeBurnDetection() + "\n\nA queima detectada está CORRETA? Criar os eletrodos?",
                                 "AutoEDM — Conferir antes de criar", res.HasDominantUnmappedColor))
                {
                    Log.Info("Criação cancelada pelo usuário (conferência).");
                    return;
                }
                Log.Info("Criando eletrodos posicionados com bloco (peça standalone + AddByFilename + PutOrigin)...");
                int n = builder.CreateElectrodesWithBlank(doc, p);
                Log.Info($"Concluído: {n} eletrodo(s). Revise no SE, salve a montagem e subtraia a cavidade de cada bloco.");
            });

        /// <summary>Confirmação (Yes/No) marshalada para a thread da UI — chamável da thread STA de trabalho.</summary>
        private bool ConfirmOnUi(string text, string caption, bool warn)
        {
            if (InvokeRequired)
                return (bool)Invoke((Func<bool>)(() => ConfirmOnUi(text, caption, warn)));
            return MessageBox.Show(this, text, caption, MessageBoxButtons.YesNo,
                warn ? MessageBoxIcon.Warning : MessageBoxIcon.Question) == DialogResult.Yes;
        }

        private void StartBlockOverSurfaces()
            => RunOnSta("Bloco sobre superfícies...", (connector, app, doc) =>
            {
                int type = 0; try { type = (int)doc.Type; } catch { }
                if (type != 1) // 1 = igPartDocument
                {
                    Log.Warn("Abra uma PEÇA (.par) ATIVA, com as faces de queima já copiadas nela (doc.Type != 1).");
                    return;
                }
                Log.Info("Bloco sobre superfícies (peça ativa, opções default; 1ª vez também loga o PROBE das APIs de superfície)...");
                var res = new SurfaceBlockBuilder().Build(doc, new BlockOverSurfacesOptions());
                Log.Info($"Concluído: bloco={res.BlockCreated}, faixa={res.BandCreated}, offset={res.SurfacesOffset}, " +
                         $"unidas={res.SurfacesUnited}, ordenado={res.SwitchedToOrdered}, fixação={res.FixationApplied}, " +
                         $"features criadas={res.CreatedFeatures.Count}. Revise no SE.");
            });

        /// <summary>
        /// Executa <paramref name="work"/> numa thread STA dedicada, com a conexão e o
        /// documento ativo já resolvidos. STA é obrigatório para a automação do SE.
        /// </summary>
        private void RunOnSta(string statusText, Action<SolidEdgeConnector, dynamic, dynamic> work)
        {
            SetButtonsEnabled(false);
            SetStatus(statusText, Color.DarkKhaki);

            var t = new Thread(() =>
            {
                try
                {
                    using (var connector = new SolidEdgeConnector())
                    {
                        Log.Info("Conectando à instância ativa do Solid Edge...");
                        dynamic app;
                        try { app = connector.Connect(startIfNotRunning: false, makeVisible: true); }
                        catch (Exception ex)
                        {
                            Log.Error("Solid Edge não está em execução. Abra o SE com a montagem e tente novamente.", ex);
                            return;
                        }

                        string ver = "—";
                        try { ver = $"{app.Name} {app.Version}"; } catch { }

                        dynamic doc = connector.GetActiveDocument();
                        string dname = "(nenhum)", dtype = "—";
                        if (doc != null) { try { dname = doc.Name; } catch { } try { dtype = Convert.ToString(doc.Type); } catch { } }
                        UpdateConnection(ver, dname, dtype);

                        if (doc == null) { Log.Error("Nenhum documento ativo. Abra a montagem no Solid Edge."); return; }

                        work(connector, app, doc);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Falha inesperada no processo.", ex);
                }
                finally
                {
                    if (IsHandleCreated)
                        BeginInvoke((Action)(() => { SetButtonsEnabled(true); SetStatus("Ocioso", Color.ForestGreen); }));
                }
            })
            { IsBackground = true, Name = "SolidEdgeSTA" };
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        private void SetButtonsEnabled(bool on)
        {
            _btnStart.Enabled = on;
            _btnReport.Enabled = on;
            _btnSpec.Enabled = on;
            _btnZ.Enabled = on;
            _btnModelTest.Enabled = on;
            _btnCopyTest.Enabled = on;
            _btnSurf.Enabled = on;
        }

        // ----------------------------------------------------------- UI marshaling

        private void UpdateConnection(string ver, string doc, string type)
        {
            if (InvokeRequired) { BeginInvoke((Action)(() => UpdateConnection(ver, doc, type))); return; }
            _lblSeVersion.Text = ver;
            _lblActiveDoc.Text = doc;
            _lblDocType.Text = type;
        }

        private void PopulatePlan(ElectrodeBuildPlan plan)
        {
            if (InvokeRequired) { BeginInvoke((Action)(() => PopulatePlan(plan))); return; }

            _lblTarget.Text = plan.TargetOccurrenceName ?? "(nenhuma)";
            _lvRegions.Items.Clear();
            foreach (var r in plan.Regions)
            {
                var it = new ListViewItem($"D{r.DetailIndex:00}");
                it.SubItems.Add(r.Ra.ToString("0.0"));
                it.SubItems.Add($"{r.Color.R},{r.Color.G},{r.Color.B}");
                it.SubItems.Add(r.FaceCount.ToString());
                it.SubItems.Add(r.SelectedBlank?.Name ?? "—");
                it.SubItems.Add(string.Join("  ", r.Passes.Select(pp => $"{pp.Pass.Suffix}:{pp.InwardOffsetMm:0.###}")));
                _lvRegions.Items.Add(it);
            }
        }

        private void SetStatus(string text, Color color)
        {
            if (InvokeRequired) { BeginInvoke((Action)(() => SetStatus(text, color))); return; }
            _status.Text = text;
            _status.ForeColor = color;
        }

        private void ClearLog()
        {
            _log.Clear();
            _info = _warn = _err = 0;
            _countsItem.Text = "Info 0   Avisos 0   Erros 0";
        }

        private void SaveLog()
        {
            using (var dlg = new SaveFileDialog { Filter = "Log (*.log)|*.log|Texto (*.txt)|*.txt", FileName = "AutoEDM.log" })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    System.IO.File.WriteAllText(dlg.FileName, _log.Text);
                    Log.Info($"Log salvo em {dlg.FileName}");
                }
            }
        }
    }
}
