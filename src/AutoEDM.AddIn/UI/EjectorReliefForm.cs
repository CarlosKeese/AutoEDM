using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using AutoEDM.Diagnostics;
using AutoEDM.Mold.Ejector;

namespace AutoEDM.AddIn.UI
{
    /// <summary>
    /// Janela do botão "Alívio de extratores" (grupo Molde, PEÇA ORDENADA — Carlos, 2026-11-09).
    ///
    /// O Carlos clica na face de TRÁS da placa/postiço; a janela acha os furos que abrem nela, tira
    /// os roscados, com rebaixo, chanfro, cegos e incompletos, e mostra a guia e o alívio de cada um
    /// pela convenção (editável). Prévia por REALCE no modelo: verde = vai ser aliviado, laranja =
    /// ficou de fora. A regra mora no Core: <see cref="EjectorReliefPlanner"/> planeja,
    /// <see cref="EjectorReliefModeler"/> modela. Modeless pelo motivo de sempre.
    /// </summary>
    public sealed class EjectorReliefForm : Form
    {
        private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");

        private readonly dynamic _app;
        private object _face;
        private ReliefFace _reliefFace;
        private EjectorReliefPlan _plan;
        /// <summary>Escolhas na tabela que CONTRARIAM a regra, por <see cref="EjectorHole.Key"/>: true = forçar, false = desmarcar.</summary>
        private readonly Dictionary<string, bool> _choices = new Dictionary<string, bool>();

        private SePicker _picker;
        private bool _picking, _loading;
        private object _hlOk, _hlOff, _hlHover;
        private int _hoverRow = -1;

        private Button _btnPick, _btnCreate, _btnClose;
        private NumericUpDown _numFactor, _numMin, _numMax, _numSmall, _numLimit, _numLarge;
        private DataGridView _grid;
        private TextBox _txtReport;
        private readonly ToolTip _tips = new ToolTip();

        public EjectorReliefForm(object app)
        {
            _app = app;
            BuildUi();
        }

        private dynamic Doc() { try { return _app?.ActiveDocument; } catch { return null; } }

        // ------------------------------------------------------------------ interface

        private void BuildUi()
        {
            Text = "AutoEDM — Alívio de extratores";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Font = new Font("Segoe UI", 9F);

            int y = 12;
            Add(new Label
            {
                Left = 12, Top = y, Width = 556, Height = 48,
                Text = "Clique na face de TRÁS da placa ou do postiço (lado da placa extratora). Os furos que abrem nela ganham " +
                       "alívio a partir dela, e a guia do extrator fica na outra ponta. Roscados, com rebaixo de cabeça, " +
                       "chanfro/escareado, cegos e incompletos ficam de fora."
            });
            y += 52;

            _btnPick = Button("Selecionar face", 12, y, 140, (s, e) => StartPicking());
            y += 36;

            var opt = new EjectorReliefOptions();
            Add(new Label { Left = 12, Top = y + 4, Width = 46, Text = "Guia =" });
            _numFactor = Num(58, y, 1, 0.5M, 1, 5, opt.GuideFactor);
            Add(new Label { Left = 120, Top = y + 4, Width = 60, Text = "× Ø, mín" });
            _numMin = Num(180, y, 1, 1, 3, 50, opt.GuideMinMm);
            Add(new Label { Left = 242, Top = y + 4, Width = 32, Text = "máx" });
            _numMax = Num(274, y, 1, 1, 5, 80, opt.GuideMaxMm);
            Add(new Label { Left = 336, Top = y + 4, Width = 30, Text = "mm" });
            _tips.SetToolTip(_numFactor, "Faixa ajustada (H7) onde o extrator desliza, junto à moldagem. Convenção: 1,5 a 3 × Ø, entre ~10 e ~20 mm.");
            y += 32;

            Add(new Label { Left = 12, Top = y + 4, Width = 78, Text = "Folga no Ø:" });
            _numSmall = Num(90, y, 2, 0.1M, 0.1M, 3, opt.SmallClearanceMm);
            Add(new Label { Left = 152, Top = y + 4, Width = 44, Text = "até Ø" });
            _numLimit = Num(196, y, 1, 1, 1, 40, opt.ClearanceLimitMm);
            Add(new Label { Left = 258, Top = y + 4, Width = 18, Text = "e" });
            _numLarge = Num(276, y, 2, 0.1M, 0.1M, 3, opt.LargeClearanceMm);
            Add(new Label { Left = 338, Top = y + 4, Width = 80, Text = "mm acima" });
            _tips.SetToolTip(_numSmall, "Quanto o alívio é maior que o furo do extrator (no Ø). Convenção: +0,5 nos finos, +1,0 nos grossos.");
            y += 34;

            _grid = Add(new DataGridView
            {
                Left = 12, Top = y, Width = 556, Height = 170,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, EditMode = DataGridViewEditMode.EditOnEnter,
                BackgroundColor = Color.White,
            });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "On", HeaderText = "", Width = 26 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Key", Visible = false });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Label", HeaderText = "Furo", ReadOnly = true, Width = 44 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Dia", HeaderText = "Ø", ReadOnly = true, Width = 52 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Len", HeaderText = "Compr.", ReadOnly = true, Width = 62 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Guide", HeaderText = "Guia", ReadOnly = true, Width = 50 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Relief", HeaderText = "Alívio (Ø × prof.)", ReadOnly = true, Width = 120 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Note", HeaderText = "Situação", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _grid.CurrentCellDirtyStateChanged += (s, e) => { if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _grid.CellValueChanged += (s, e) => OnCheckChanged(e.RowIndex, e.ColumnIndex);
            _grid.DataError += (s, e) => { e.ThrowException = false; };
            // Destaque no modelo do furo sob o mouse (Carlos, 2026-11-09).
            _grid.CellMouseEnter += (s, e) => HoverRow(e.RowIndex);
            _grid.MouseLeave += (s, e) => HoverRow(-1);
            _tips.SetToolTip(_grid, "Passe o mouse para ver o furo no modelo. Marque um furo reprovado para FORÇAR o alívio nele; desmarque um aprovado para pular.");
            y += 176;

            _txtReport = Add(new TextBox
            {
                Left = 12, Top = y, Width = 556, Height = 120,
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 8.5F), BackColor = Color.White
            });
            y += 128;

            _btnCreate = Button("Criar alívios", 358, y, 110, (s, e) => Create());
            _btnCreate.Enabled = false;
            _btnClose = Button("Fechar", 476, y, 92, (s, e) => Close());
            ClientSize = new Size(580, y + 26 + 12);

            foreach (NumericUpDown n in new[] { _numFactor, _numMin, _numMax, _numSmall, _numLimit, _numLarge })
                n.ValueChanged += (s, e) => Recompute();
        }

        private NumericUpDown Num(int left, int top, int decimals, decimal step, decimal min, decimal max, double value) =>
            Add(new NumericUpDown
            {
                Left = left, Top = top, Width = 58, DecimalPlaces = decimals, Increment = step, Minimum = min, Maximum = max,
                Value = Math.Max(min, Math.Min(max, (decimal)value))
            });

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(area.Right - Width - 8, Math.Max(area.Top + 8, area.Top + (area.Height - Height) / 2));
        }

        /// <summary>Face já selecionada no SE? Usa. Senão, já abre clicando.</summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            object pre = null;
            try
            {
                dynamic ss = Doc()?.SelectSet;
                if (ss != null && (int)ss.Count >= 1) pre = (object)ss.Item(1);
            }
            catch { }
            if (pre != null && EjectorHoleReader.ReadFace(pre, out _) != null) UseFace(pre);
            else { Report("Clique na face de trás da placa (lado da placa extratora)."); StartPicking(); }
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

        private void Report(string text) { if (_txtReport != null) _txtReport.Text = text; }

        // ------------------------------------------------------------------ seleção

        private void StartPicking()
        {
            StopPicking();
            if (_picker == null)
            {
                try
                {
                    _picker = new SePicker((SolidEdgeFramework.Application)_app);
                    _picker.Picked += g => { try { BeginInvoke(new Action(() => { StopPicking(); UseFace(g); })); } catch { } };
                    _picker.Cancelled += () => { try { BeginInvoke(new Action(() => { _picking = false; UpdatePickButton(); })); } catch { } };
                }
                catch (Exception e) { Log.Warn("Alívio de extratores: seleção indisponível — " + e.GetBaseException().Message); _picker = null; }
            }
            // seLocateFace localiza só face PLANA — é exatamente a que serve aqui.
            if (_picker != null && _picker.Start("AutoEDM — clique na face de TRÁS da placa (Esc encerra).",
                    SolidEdgeConstants.seLocateFilterConstants.seLocateFace))
            {
                _picking = true;
                UpdatePickButton();
                return;
            }
            Report("Não deu para assumir o mouse da SE — selecione a face no SE e reabra a janela.");
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
            _btnPick.Text = _picking ? "Clique na face…" : "Selecionar face";
            _btnPick.BackColor = _picking ? Color.FromArgb(255, 244, 205) : SystemColors.Control;
            _btnPick.UseVisualStyleBackColor = !_picking;
        }

        private void UseFace(object face)
        {
            if (face == null) return;
            ReliefFace rf = EjectorHoleReader.ReadFace(face, out string error);
            if (rf == null) { Report("Face recusada: " + error); _face = null; _reliefFace = null; _plan = null; FillGrid(); ClearHighlight(); return; }
            _face = face;
            _reliefFace = rf;
            _choices.Clear();
            Recompute();
        }

        // ------------------------------------------------------------------ plano

        private EjectorReliefOptions Options() => new EjectorReliefOptions
        {
            GuideFactor = (double)_numFactor.Value,
            GuideMinMm = (double)_numMin.Value,
            GuideMaxMm = (double)Math.Max(_numMin.Value, _numMax.Value),
            SmallClearanceMm = (double)_numSmall.Value,
            ClearanceLimitMm = (double)_numLimit.Value,
            LargeClearanceMm = (double)_numLarge.Value,
        };

        private List<HoleSurface> _surfaces;

        private void Recompute()
        {
            if (_loading || _reliefFace == null) return;
            var warnings = new List<string>();
            Cursor = Cursors.WaitCursor;
            try
            {
                if (_surfaces == null || _surfacesFor != _face)
                {
                    dynamic doc = Doc();
                    _surfaces = doc == null ? new List<HoleSurface>() : EjectorHoleReader.ReadSurfaces(doc, _reliefFace, warnings);
                    _surfacesFor = _face;
                    _readWarnings = warnings;
                }
                _plan = EjectorReliefPlanner.Plan(_reliefFace, _surfaces, Options(), _choices);
            }
            catch (Exception e)
            {
                Log.Error("Alívio de extratores: falha ao ler os furos.", e);
                Report("Não deu para ler os furos da peça. Detalhes no log:\r\n" + ElectrodeAddIn.Current?.LogPath);
                _plan = null;
            }
            finally { Cursor = Cursors.Default; }

            FillGrid();
            RebuildHighlight();
            _btnCreate.Enabled = _plan != null && _plan.ToRelieve.Any();
            if (_plan == null) return;
            string text = _plan.Holes.Count == 0
                ? "Nenhum furo perpendicular abre nesta face. É a face de trás da placa?"
                : EjectorReliefPlanner.Describe(_plan, Options());
            if (_readWarnings != null && _readWarnings.Count > 0) text += "AVISO: " + string.Join("\r\nAVISO: ", _readWarnings);
            Report(text.Replace("\r\n", "\n").Replace("\n", "\r\n"));
        }

        private object _surfacesFor;
        private List<string> _readWarnings;

        private void FillGrid()
        {
            _loading = true;
            _hoverRow = -1;
            try
            {
                _grid.Rows.Clear();
                if (_plan == null) return;
                foreach (EjectorHole h in _plan.Holes)
                {
                    string note = h.Forced ? "FORÇADO — " + h.SkipReason
                                : h.Relieve ? "aliviar"
                                : h.UserChoice == false && h.SkipReason == null ? "desmarcado"
                                : h.SkipReason;
                    int i = _grid.Rows.Add(h.Relieve, h.Key, h.Label, "Ø" + h.DiameterMm.ToString("0.##", Pt),
                        h.LengthMm.ToString("0.##", Pt),
                        h.CanRelieve ? h.GuideMm.ToString("0.#", Pt) : "—",
                        h.CanRelieve ? string.Format(Pt, "Ø{0:0.0} × {1:0.##}", h.ReliefDiameterMm, h.ReliefDepthMm) : "—",
                        note);
                    DataGridViewRow row = _grid.Rows[i];
                    row.Cells["On"].ReadOnly = !h.CanRelieve;               // curto: nem forçando sai
                    if (h.Forced) row.DefaultCellStyle.ForeColor = Color.FromArgb(200, 90, 0);
                    else if (!h.Relieve) row.DefaultCellStyle.ForeColor = Color.DimGray;
                }
            }
            finally { _loading = false; }
        }

        private void OnCheckChanged(int row, int col)
        {
            if (_loading || row < 0 || col != _grid.Columns["On"].Index) return;
            string key = _grid.Rows[row].Cells["Key"].Value as string;
            bool on = _grid.Rows[row].Cells["On"].Value is bool b && b;
            EjectorHole h = _plan?.Holes.FirstOrDefault(x => x.Key == key);
            if (h == null) return;
            if (on == (h.SkipReason == null)) _choices.Remove(key);   // voltou ao que a regra diz
            else _choices[key] = on;
            BeginInvoke(new Action(Recompute));
        }

        // ------------------------------------------------------------------ destaque do furo sob o mouse

        private void HoverRow(int row)
        {
            if (row == _hoverRow) return;
            _hoverRow = row;
            ClearHover();
            EjectorHole h = row >= 0 && row < _grid.Rows.Count && _plan != null
                ? _plan.Holes.FirstOrDefault(x => x.Key == (_grid.Rows[row].Cells["Key"].Value as string))
                : null;
            if (h == null || h.Faces.Count == 0) return;
            try
            {
                dynamic doc = Doc();
                if (doc == null) return;
                dynamic hs = doc.HighlightSets.Add();
                _hlHover = (object)hs;
                hs.Color = 255 | (235 << 8) | (110 << 16);      // amarelo, como o destaque das outras janelas
                foreach (object f in h.Faces) { try { hs.AddItem(f); } catch { } }
                hs.Draw();
            }
            catch (Exception e) { Log.Warn("Alívio de extratores: destaque do furo não aplicado — " + e.GetBaseException().Message); }
        }

        private void ClearHover()
        {
            object s = _hlHover;
            _hlHover = null;
            if (s == null) return;
            try { dynamic hs = s; hs.RemoveAll(); hs.Draw(); hs.Delete(); } catch { }
            // O furo volta à cor dele (verde/laranja).
            foreach (object o in new[] { _hlOk, _hlOff }) { if (o == null) continue; try { ((dynamic)o).Draw(); } catch { } }
        }

        // ------------------------------------------------------------------ criar

        private void Create()
        {
            if (_plan == null || !_plan.ToRelieve.Any()) return;
            StopPicking();
            dynamic doc = Doc();
            int type = -1; try { type = (int)doc.Type; } catch { }
            if (type != 1) { Report("O documento ativo não é mais a PEÇA. Volte para a peça e clique em \"Criar alívios\"."); return; }

            ClearHighlight();                       // as faces realçadas morrem com os furos
            EjectorReliefResult res;
            Cursor = Cursors.WaitCursor;
            try
            {
                Log.Info("===== ALÍVIO DE EXTRATORES: criar =====\n" + EjectorReliefPlanner.Describe(_plan, Options()));
                res = EjectorReliefModeler.Create(_app, _face, _plan);
            }
            catch (Exception e)
            {
                Log.Error("Alívio de extratores: falha ao criar.", e);
                Report("Não foi possível criar os alívios. Detalhes no log:\r\n" + ElectrodeAddIn.Current?.LogPath);
                return;
            }
            finally { Cursor = Cursors.Default; }

            var lines = new List<string> { $"{res.Created} de {_plan.ToRelieve.Count()} alívio(s) criado(s), em {res.Features} recurso(s) de furo." };
            lines.AddRange(res.Failed.Select(f => "FALHOU: " + f));
            lines.AddRange(res.Warnings.Select(w => "AVISO: " + w));
            lines.Add("Furos de mesmo alívio (Ø e profundidade) ficam num recurso só: \"Alívio Ø… × … (E…)\" na árvore (Ctrl+Z desfaz). A peça NÃO foi salva.");
            lines.Add("Para outra face, clique em \"Selecionar face\".");

            // A face e as faces lidas morreram com os furos: começa de novo.
            _face = null; _reliefFace = null; _plan = null; _surfaces = null; _surfacesFor = null;
            FillGrid();
            _btnCreate.Enabled = false;
            Report(string.Join("\r\n", lines));
        }

        // ------------------------------------------------------------------ realce

        private void RebuildHighlight()
        {
            ClearHighlight();
            if (_plan == null) return;
            try
            {
                dynamic doc = Doc();
                if (doc == null) return;
                _hlOk = MakeSet(doc, 0 | (170 << 8) | (60 << 16) /* verde */, _plan.Holes.Where(h => h.Relieve).SelectMany(h => h.Faces));
                _hlOff = MakeSet(doc, 255 | (140 << 8) | (0 << 16) /* laranja */, _plan.Holes.Where(h => !h.Relieve).SelectMany(h => h.Faces));
            }
            catch (Exception e) { Log.Warn("Alívio de extratores: realce não aplicado — " + e.GetBaseException().Message); }
        }

        private static object MakeSet(dynamic doc, int color, IEnumerable<object> faces)
        {
            List<object> list = faces.ToList();
            if (list.Count == 0) return null;
            dynamic hs = doc.HighlightSets.Add();
            hs.Color = color;
            foreach (object f in list) { try { hs.AddItem(f); } catch { } }
            hs.Draw();
            return (object)hs;
        }

        private void ClearHighlight()
        {
            _hoverRow = -1;
            object hv = _hlHover;
            _hlHover = null;
            if (hv != null) { try { dynamic hs = hv; hs.RemoveAll(); hs.Draw(); hs.Delete(); } catch { } }
            foreach (object s in new[] { _hlOk, _hlOff })
            {
                if (s == null) continue;
                try { dynamic hs = s; hs.RemoveAll(); hs.Draw(); hs.Delete(); } catch { }
            }
            _hlOk = _hlOff = null;
        }
    }
}
