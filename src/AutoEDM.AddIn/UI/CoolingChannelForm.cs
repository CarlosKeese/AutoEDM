using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using AutoEDM.Diagnostics;
using AutoEDM.Mold.Cooling;

namespace AutoEDM.AddIn.UI
{
    /// <summary>
    /// Janela do botão "Refrigeração" (grupo Molde, PEÇA ORDENADA — Carlos, 2026-09-24).
    ///
    /// O Carlos desenha o traçado dos canais como linhas num esboço 3D da placa; aqui ele escolhe
    /// as linhas (clicando, ou todas de uma vez), o Ø, e o que acontece em cada PONTA LIVRE:
    /// cega (ponta de broca), passante, engate ou tampão (rosca de tubo da base de furos da SE).
    /// O plano — que furo sai, por onde entra, com que profundidade — é refeito a cada mudança e
    /// fica visível ANTES de criar.
    ///
    /// Modeless pelo motivo de sempre (clicar no modelo com a janela aberta). A regra toda mora no
    /// Core: <see cref="CoolingPlanner"/> planeja, <see cref="CoolingChannelModeler"/> modela.
    /// </summary>
    public sealed class CoolingChannelForm : Form
    {
        private static readonly string[] TerminalNames = { "Cega (ponta de broca)", "Passante", "Engate (rosca)", "Tampão (rosca)" };

        private readonly dynamic _app;
        private readonly List<PipeThread> _threads;
        private readonly List<object> _picked = new List<object>();   // arestas/esboços clicados
        private List<CoolingLine> _lines = new List<CoolingLine>();
        private CoolingPlan _plan;
        private bool _allLines;                                        // true = todas as linhas dos esboços 3D
        private readonly Dictionary<string, CoolingTerminal> _endChoices = new Dictionary<string, CoolingTerminal>();

        private SePicker _picker;
        private bool _picking, _loading;
        private object _highlight;

        private Button _btnPick, _btnAll, _btnClear, _btnCreate, _btnClose;
        private ComboBox _cboDia, _cboFitting, _cboPlug;
        private NumericUpDown _numOvershoot;
        private CheckBox _chkAutoOvershoot;
        private DataGridView _grid;
        private TextBox _txtReport;
        private readonly ToolTip _tips = new ToolTip();

        public CoolingChannelForm(object app)
        {
            _app = app;
            _threads = CoolingService.Threads(app);
            BuildUi();
        }

        private dynamic Doc() { try { return _app?.ActiveDocument; } catch { return null; } }

        // ------------------------------------------------------------------ interface

        private void BuildUi()
        {
            Text = "AutoEDM — Refrigeração";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Font = new Font("Segoe UI", 9F);

            int y = 12;
            Add(new Label
            {
                Left = 12, Top = y, Width = 516, Height = 32,
                Text = "Desenhe o caminho da água no esboço 3D da placa. A janela usa todas as linhas, prolonga até a " +
                       "face os trechos que acabam em cantos, põe TAMPÃO nessas bocas e ENGATE nas pontas do caminho."
            });
            y += 38;

            _btnAll = Button("Todas do esboço 3D", 12, y, 140, (s, e) => UseAllLines());
            _tips.SetToolTip(_btnAll, "Todas as linhas retas de todos os esboços 3D da peça (é o que a janela usa ao abrir).");
            _btnPick = Button("Só as clicadas", 160, y, 140, (s, e) => StartPicking());
            _tips.SetToolTip(_btnPick, "Para usar só parte do traçado: clique nas linhas (de novo numa linha a retira; Esc encerra).");
            _btnClear = Button("Limpar", 308, y, 80, (s, e) => { _picked.Clear(); _allLines = false; ClearHighlight(); Recompute(); });
            y += 36;

            Add(new Label { Left = 12, Top = y + 4, Width = 90, Text = "Ø do canal:" });
            _cboDia = Add(new ComboBox { Left = 100, Top = y, Width = 70, DropDownStyle = ComboBoxStyle.DropDownList });
            foreach (double d in CoolingService.Diameters) _cboDia.Items.Add(d.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR")));
            _cboDia.SelectedIndex = 1;                         // Ø8

            _chkAutoOvershoot = Add(new CheckBox { Left = 190, Top = y + 2, Width = 150, Text = "Sobrefuro = Ø/2", Checked = true });
            _numOvershoot = Add(new NumericUpDown { Left = 344, Top = y, Width = 70, DecimalPlaces = 1, Increment = 0.5M, Minimum = 0, Maximum = 50, Value = 4, Enabled = false });
            Add(new Label { Left = 418, Top = y + 4, Width = 110, Text = "mm no cruzamento" });
            _tips.SetToolTip(_chkAutoOvershoot, "Quanto o furo passa do cruzamento. Ø/2 = a broca chega na parede oposta do canal cruzado.");
            y += 32;

            Add(new Label { Left = 12, Top = y + 4, Width = 140, Text = "Rosca do engate:" });
            _cboFitting = ThreadCombo(150, y, "G1/4");
            y += 30;
            Add(new Label { Left = 12, Top = y + 4, Width = 140, Text = "Rosca do tampão:" });
            _cboPlug = ThreadCombo(150, y, "G1/8");
            y += 34;

            _grid = Add(new DataGridView
            {
                Left = 12, Top = y, Width = 516, Height = 130,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect, EditMode = DataGridViewEditMode.EditOnEnter,
                BackgroundColor = Color.White,
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Key", HeaderText = "Boca", ReadOnly = true, Width = 60 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Point", HeaderText = "Na face (mm)", ReadOnly = true, Width = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ext", HeaderText = "Prolongada", ReadOnly = true, Width = 100 });
            var term = new DataGridViewComboBoxColumn { Name = "Terminal", HeaderText = "Terminação", Width = 185, FlatStyle = FlatStyle.Flat };
            term.Items.AddRange(TerminalNames);
            _grid.Columns.Add(term);
            _grid.CurrentCellDirtyStateChanged += (s, e) => { if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _grid.CellValueChanged += (s, e) => OnGridChanged(e.RowIndex);
            _grid.DataError += (s, e) => { e.ThrowException = false; };
            y += 136;

            _txtReport = Add(new TextBox
            {
                Left = 12, Top = y, Width = 516, Height = 170,
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 8.5F), BackColor = Color.White
            });
            y += 178;

            _btnCreate = Button("Criar canais", 318, y, 110, (s, e) => Create());
            _btnClose = Button("Fechar", 436, y, 92, (s, e) => Close());

            ClientSize = new Size(540, y + 26 + 12);

            _cboDia.SelectedIndexChanged += (s, e) => { if (_chkAutoOvershoot.Checked) _numOvershoot.Value = (decimal)(Diameter() / 2); Recompute(); };
            _chkAutoOvershoot.CheckedChanged += (s, e) => { _numOvershoot.Enabled = !_chkAutoOvershoot.Checked; Recompute(); };
            _numOvershoot.ValueChanged += (s, e) => Recompute();
            _cboFitting.SelectedIndexChanged += (s, e) => Recompute();
            _cboPlug.SelectedIndexChanged += (s, e) => Recompute();

            if (_threads.Count == 0)
                Report("ATENÇÃO: a base de furos da SE (Preferences\\Holes\\*.xlsx) não foi encontrada — engate e tampão ficam sem rosca.");
        }

        private ComboBox ThreadCombo(int left, int top, string preferred)
        {
            var c = Add(new ComboBox { Left = left, Top = top, Width = 378, DropDownStyle = ComboBoxStyle.DropDownList });
            foreach (PipeThread t in _threads) c.Items.Add(new ThreadItem(t));
            PipeThread p = CoolingService.FindThread(_threads, preferred);
            if (p != null) c.SelectedIndex = _threads.IndexOf(p);
            else if (c.Items.Count > 0) c.SelectedIndex = 0;
            return c;
        }

        private sealed class ThreadItem
        {
            public readonly PipeThread Thread;
            public ThreadItem(PipeThread t) { Thread = t; }
            public override string ToString() => string.Format(CultureInfo.GetCultureInfo("pt-BR"),
                "{0}  —  {1}, broca Ø{2:0.##}  [{3}]", Thread.Size.Trim(), Thread.Tapered ? "cônica" : "paralela", Thread.TapDrillMm, Thread.Standard);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(area.Right - Width - 8, Math.Max(area.Top + 8, area.Top + (area.Height - Height) / 2));
        }

        /// <summary>
        /// Abre AUTOMÁTICO (Carlos, 2026-09-24: "quero que esse recurso seja mais automático"): com
        /// linhas selecionadas no SE, usa elas; senão, todas as linhas dos esboços 3D da peça — o
        /// plano já aparece pronto, e o usual é só conferir e clicar em "Criar canais".
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                dynamic doc = Doc();
                dynamic ss = doc?.SelectSet;
                int n = 0; try { n = (int)ss.Count; } catch { }
                for (int i = 1; i <= n; i++) { try { _picked.Add((object)ss.Item(i)); } catch { } }
            }
            catch { }
            if (CoolingLineReader.FromItems(_picked, null).Count == 0) { _picked.Clear(); _allLines = true; }
            Recompute();
            RebuildHighlight();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            StopPicking();
            ClearHighlight();
            if (_picker != null) { _picker.Dispose(); _picker = null; }
            base.OnFormClosed(e);
        }

        private T Add<T>(T c) where T : Control { Controls.Add(c); return c; }

        private Button Button(string text, int left, int top, int width, EventHandler onClick)
        {
            var b = Add(new Button { Text = text, Left = left, Top = top, Width = width, Height = 26 });
            b.Click += onClick;
            return b;
        }

        private double Diameter() =>
            CoolingService.Diameters[Math.Max(0, _cboDia.SelectedIndex)];

        // ------------------------------------------------------------------ seleção

        private void StartPicking()
        {
            StopPicking();
            if (_picker == null)
            {
                try
                {
                    _picker = new SePicker((SolidEdgeFramework.Application)_app);
                    _picker.Picked += g => { try { BeginInvoke(new Action(() => Accept(g))); } catch { } };
                    _picker.Cancelled += () => { try { BeginInvoke(new Action(() => { _picking = false; UpdatePickButton(); })); } catch { } };
                }
                catch (Exception e) { Log.Warn("Refrigeração: seleção por etapas indisponível — " + e.GetBaseException().Message); _picker = null; }
            }
            // Na PEÇA a localização da SE entrega a aresta clicada (é o caminho do O'ring); a linha
            // do esboço 3D é localizada como linha ou como aresta.
            if (_picker != null && _picker.Start("AutoEDM — clique nas LINHAS do esboço 3D (de novo para tirar; Esc encerra).",
                    SolidEdgeConstants.seLocateFilterConstants.seLocateLine,
                    SolidEdgeConstants.seLocateFilterConstants.seLocateEdge))
            {
                _picking = true;
                UpdatePickButton();
                return;
            }
            Report("Não deu para assumir o mouse da SE — selecione as linhas no SE e reabra a janela, ou use \"Todas do esboço 3D\".");
        }

        private void StopPicking()
        {
            _picker?.Stop();
            _picking = false;
            UpdatePickButton();
        }

        private void UpdatePickButton()
        {
            if (_btnPick == null) return;
            _btnPick.Text = _picking ? "Clique nas linhas…" : "Selecionar linhas";
            _btnPick.BackColor = _picking ? Color.FromArgb(255, 244, 205) : SystemColors.Control;
            _btnPick.UseVisualStyleBackColor = !_picking;
        }

        private void Accept(object graphic)
        {
            StopPicking();
            try
            {
                if (graphic == null) return;
                if (_allLines) { _allLines = false; _picked.Clear(); }
                Log.Info("Refrigeração: clique — tipo " + AutoEDM.Com.ComDiagnostics.TypeNameOf(graphic));

                // Clicou de novo numa linha já escolhida? Sai (compara pelas pontas, não pelo proxy).
                List<CoolingLine> one = CoolingLineReader.FromItems(new[] { graphic }, null);
                if (one.Count == 0) { Report("O que foi clicado não é uma linha reta (o log mostra o tipo)."); return; }
                int existing = _picked.FindIndex(p =>
                {
                    List<CoolingLine> l = CoolingLineReader.FromItems(new[] { p }, null);
                    return l.Count == 1 && SameLine(l[0], one[0]);
                });
                if (existing >= 0) _picked.RemoveAt(existing); else _picked.Add(graphic);
                RebuildHighlight();
                Recompute();
            }
            finally { StartPicking(); }
        }

        private void UseAllLines()
        {
            StopPicking();
            _allLines = true;
            _picked.Clear();
            Recompute();
            RebuildHighlight();
        }

        private static bool SameLine(CoolingLine a, CoolingLine b)
        {
            double D(double[] p, double[] q) => Math.Sqrt((p[0] - q[0]) * (p[0] - q[0]) + (p[1] - q[1]) * (p[1] - q[1]) + (p[2] - q[2]) * (p[2] - q[2]));
            return (D(a.StartMm, b.StartMm) < 1e-6 && D(a.EndMm, b.EndMm) < 1e-6) || (D(a.StartMm, b.EndMm) < 1e-6 && D(a.EndMm, b.StartMm) < 1e-6);
        }

        // ------------------------------------------------------------------ plano

        private CoolingOptions Options() => new CoolingOptions
        {
            DiameterMm = Diameter(),
            OvershootMm = _chkAutoOvershoot.Checked ? (double?)null : (double)_numOvershoot.Value,
            FittingThread = (_cboFitting.SelectedItem as ThreadItem)?.Thread,
            PlugThread = (_cboPlug.SelectedItem as ThreadItem)?.Thread,
        };

        private void Recompute()
        {
            if (_loading) return;
            var warnings = new List<string>();
            string source;
            if (_allLines)
            {
                dynamic doc = Doc();
                _lines = doc == null ? new List<CoolingLine>() : CoolingLineReader.AllSketch3D(doc, warnings, out int n);
                source = "todas as linhas dos esboços 3D";
            }
            else
            {
                _lines = CoolingLineReader.FromItems(_picked, warnings);
                source = $"{_picked.Count} item(ns) clicado(s)";
            }

            CoolingOptions opt = Options();
            string note = "";
            dynamic pdoc = Doc();
            Cursor = Cursors.WaitCursor;
            try { _plan = pdoc == null ? CoolingPlanner.Plan(_lines, _endChoices, opt) : CoolingService.BuildPlan(pdoc, _lines, _endChoices, opt, out note); }
            finally { Cursor = Cursors.Default; }

            FillGrid();
            _btnCreate.Enabled = _plan.Holes.Count > 0;
            if (_lines.Count == 0)
                Report("Nenhuma linha ainda. Clique nas linhas do esboço 3D, ou use \"Todas do esboço 3D\"." +
                       (warnings.Count > 0 ? "\r\n" + string.Join("\r\n", warnings) : ""));
            else
                Report("Linhas: " + source + ". " + note + "\r\n" + CoolingService.Describe(_lines, _plan, opt).Replace("\n", "\r\n") +
                       (warnings.Count > 0 ? "AVISO: " + string.Join("\r\nAVISO: ", warnings) : ""));
        }

        private void FillGrid()
        {
            _loading = true;
            try
            {
                _grid.Rows.Clear();
                var pt = CultureInfo.GetCultureInfo("pt-BR");
                foreach (CoolingEnd e in _plan.FreeEnds)
                    _grid.Rows.Add(e.Key, string.Format(pt, "{0:0.#}; {1:0.#}; {2:0.#}", e.PointMm[0], e.PointMm[1], e.PointMm[2]),
                        e.Extended ? string.Format(pt, "{0:0.#} mm", e.ExtensionMm) : "—",
                        TerminalNames[(int)e.Terminal]);
            }
            finally { _loading = false; }
        }

        private void OnGridChanged(int row)
        {
            if (_loading || row < 0 || row >= _grid.Rows.Count) return;
            string key = _grid.Rows[row].Cells["Key"].Value as string;
            int t = Array.IndexOf(TerminalNames, _grid.Rows[row].Cells["Terminal"].Value as string);
            if (key == null || t < 0) return;
            CoolingEnd end = _plan?.FreeEnds.FirstOrDefault(x => x.Key == key);
            if (end != null && (CoolingTerminal)t == end.AutoTerminal) _endChoices.Remove(key);   // voltou ao automático
            else _endChoices[key] = (CoolingTerminal)t;
            BeginInvoke(new Action(Recompute));        // fora do evento da grade, que está sendo editada
        }

        private void Report(string text) { if (_txtReport != null) _txtReport.Text = text; }

        // ------------------------------------------------------------------ criar

        private void Create()
        {
            if (_plan == null || _plan.Holes.Count == 0) return;
            StopPicking();

            dynamic doc = Doc();
            int type = -1; try { type = (int)doc.Type; } catch { }
            if (type != 1) { Report("O documento ativo não é mais a PEÇA. Volte para a peça e clique em \"Criar canais\"."); return; }
            if (_plan.Problems.Count > 0 &&
                MessageBox.Show(this, $"O plano tem {_plan.Problems.Count} problema(s) — essas passadas NÃO vão virar furo.\n\nCriar o resto assim mesmo?",
                    "AutoEDM — Refrigeração", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            CoolingResult res;
            Cursor = Cursors.WaitCursor;
            try
            {
                Log.Info("===== REFRIGERAÇÃO: criar =====\n" + CoolingService.Describe(_lines, _plan, Options()));
                res = CoolingChannelModeler.Create(_app, _lines, _plan, Options());
            }
            catch (Exception e)
            {
                Log.Error("Refrigeração: falha ao criar.", e);
                Report("Não foi possível criar os canais. Detalhes no log:\r\n" + ElectrodeAddIn.Current?.LogPath);
                return;
            }
            finally { Cursor = Cursors.Default; }

            var lines = new List<string> { $"{res.Created} de {_plan.Holes.Count} furo(s) criado(s)." };
            lines.AddRange(res.Failed.Select(f => "FALHOU: " + f));
            lines.AddRange(res.Warnings.Select(w => "AVISO: " + w));
            lines.Add("Confira na árvore (cada furo tem o nome do canal ou da rosca). A peça NÃO foi salva.");
            Report(string.Join("\r\n", lines));
            ClearHighlight();
        }

        // ------------------------------------------------------------------ realce

        private void RebuildHighlight()
        {
            ClearHighlight();
            try
            {
                dynamic doc = Doc();
                if (doc == null) return;
                IEnumerable<object> items = _allLines ? _lines.Select(l => l.Edge) : _picked;
                dynamic hs = doc.HighlightSets.Add();
                _highlight = (object)hs;
                hs.Color = 0 | (176 << 8) | (240 << 16);          // azul-água
                foreach (object o in items) { try { hs.AddItem(o); } catch { } }
                hs.Draw();
            }
            catch (Exception e) { Log.Warn("Refrigeração: realce não aplicado — " + e.GetBaseException().Message); }
        }

        private void ClearHighlight()
        {
            object s = _highlight;
            _highlight = null;
            if (s == null) return;
            try { dynamic hs = s; hs.RemoveAll(); hs.Draw(); hs.Delete(); } catch { }
        }
    }
}
