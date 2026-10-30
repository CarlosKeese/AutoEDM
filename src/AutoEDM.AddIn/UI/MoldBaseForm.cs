using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AutoEDM.Mold;
using AutoEDM.Mold.MoldBase;

namespace AutoEDM.AddIn.UI
{
    /// <summary>
    /// Janela do "Porta-molde" (Carlos, 2026-10-23). À esquerda, o que entra na conta: o TIPO de porta-molde
    /// (Polimold, MUD, Babyplast) e os componentes a gerar, as peças da montagem (o que vai na caixa e o que é
    /// produto), a disposição, os números da casa e as escolhas Polimold. À direita, o porta-molde DESENHADO em corte
    /// e em planta (<see cref="MoldBaseView"/>) e o plano com as conferências — os dois refeitos a cada mudança.
    /// Nada é criado antes do OK. Enquanto a janela está aberta, as peças marcadas ficam acesas no modelo
    /// (laranja = caixa, verde = produto) e acompanham a grade.
    /// </summary>
    public sealed class MoldBaseForm : Form
    {
        private readonly MoldBaseScan _scan;
        private readonly Func<MoldBaseOptions, MoldBasePlan> _planner;
        private readonly DataGridView _grid;
        private readonly ComboBox _family, _center, _fixedSide, _series, _p1, _p2, _stroke, _type, _steel, _mounting, _guiding;
        private readonly CheckBox _autoParting, _rotate, _guides, _returnPins;
        private readonly NumericUpDown _parting, _margin, _floor, _clearance, _extract, _channel;
        private readonly GroupBox _poli;
        private readonly Button _auto;
        private readonly MoldBaseView _view;
        private readonly TextBox _report;
        private readonly Button _ok;
        private bool _loading = true;
        private readonly OccurrenceHighlight _highlight;   // prévia: as peças da caixa e o produto acesos no modelo
        private string _highlightKey;

        public MoldBaseOptions Options { get; private set; }
        public MoldBasePlan Plan { get; private set; }

        public MoldBaseForm(MoldBaseScan scan, HeightAxis axis, MoldBaseOptions initial, Func<MoldBaseOptions, MoldBasePlan> planner, object doc = null)
        {
            _scan = scan;
            if (doc != null) _highlight = new OccurrenceHighlight(doc, "Porta-molde");
            _planner = planner;
            Options = initial.Clone();
            Text = "AutoEDM — Porta-molde";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = MaximizeBox = false;
            ShowInTaskbar = false;
            Font = new Font("Segoe UI", 9F);
            ClientSize = new Size(1260, 712);

            int x = 12, y = 8, w = 440;

            // Tipo e componentes
            var kind = Group("Tipo de porta-molde", x, y, w, 76);
            _family = Combo(kind, "Tipo", 0, new[] { "Polimold (2 placas)", "MUD — tipo gaveta (quadro 7160)", "Babyplast 76 × 76", "Zamak MINI 160 × 160" });
            _family.SelectedIndex = (int)Options.Family - 1;
            _guides = new CheckBox { Left = 10, Top = 47, Width = 270, Text = "Colunas e buchas", Checked = Options.WithGuides };
            _returnPins = new CheckBox { Left = 290, Top = 47, Width = 140, Text = "Pinos de retorno", Checked = Options.WithReturnPins };
            kind.Controls.AddRange(new Control[] { _guides, _returnPins });
            y += 80;

            Controls.Add(new Label { Left = x, Top = y, Width = w, Height = 18, Text = "Peças da montagem (no modelo: laranja = caixa, verde = produto):" });
            y += 19;
            _grid = new DataGridView
            {
                Left = x, Top = y, Width = w, Height = 116, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            };
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Caixa", FillWeight = 14 });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Produto", FillWeight = 16 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Peça", ReadOnly = true, FillWeight = 50 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Altura", ReadOnly = true, FillWeight = 20 });
            int[] ax = MoldBaseBuilder.PlanAxes(axis);
            foreach (DataGridViewColumn col in _grid.Columns) col.SortMode = DataGridViewColumnSortMode.NotSortable;
            foreach (MoldBaseItem it in scan.Items)
            {
                int r = _grid.Rows.Add(it.Include, it.IsProduct, Path.GetFileName(it.FileName ?? it.Name), F(it.MaxMm[ax[2]] - it.MinMm[ax[2]]));
                _grid.Rows[r].Tag = it;   // a linha leva o item: nada de casar pela posição
            }
            _grid.CurrentCellDirtyStateChanged += (s, e) => { if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _grid.CellValueChanged += (s, e) => Recalc();
            Controls.Add(_grid);
            y += 122;

            // Disposição
            var disp = Group("Disposição", x, y, w, 104);
            string axisName = axis.ToString();
            _center = Combo(disp, "Centro", 0, new[] { "Origem da montagem", "Centro das peças" });
            _center.SelectedIndex = Options.Centering == MoldBaseCentering.ContentCenter ? 1 : 0;
            _fixedSide = Combo(disp, "Lado fixo (P1)", 1, new[] { "+" + axisName, "−" + axisName });
            _fixedSide.SelectedIndex = Options.FixedSideNegative ? 1 : 0;
            disp.Controls.Add(new Label { Left = 10, Top = 74, Width = 120, Text = "Partição (" + axisName + ")" });
            _autoParting = new CheckBox { Left = 135, Top = 72, Width = 95, Text = "automática", Checked = !Options.PartingMm.HasValue };
            _parting = new NumericUpDown { Left = 232, Top = 72, Width = 80, Minimum = -5000, Maximum = 5000, DecimalPlaces = 2, Value = Clamp(Options.PartingMm ?? 0, -5000, 5000) };
            _rotate = new CheckBox { Left = 322, Top = 72, Width = 110, Text = "pode girar 90°", Checked = Options.AllowRotation };
            disp.Controls.AddRange(new Control[] { _autoParting, _parting, _rotate });
            y += 108;

            // Números da casa
            var house = Group("Regras da casa (mm)", x, y, w, 104);
            _margin = Num(house, "Margem lateral", 0, 0, Options.SideMarginMm);
            _floor = Num(house, "Fundo mínimo P1/P2", 0, 1, Options.FloorMm);
            _clearance = Num(house, "Folga do curso", 0, 2, Options.StrokeClearanceMm);
            _extract = Num(house, "Altura a extrair (0 = medir)", 1, 0, Options.ProductHeightMm);
            _channel = Num(house, "Ø do canal", 1, 1, Options.CoolingDiameterMm);
            y += 108;

            // Polimold (e as espessuras, que valem para os três tipos)
            _poli = Group("Polimold · espessuras", x, y, w, 132);
            _series = Combo(_poli, "Série", 0, new[] { "Automática" }.Concat(PolimoldCatalog.All.Select(s => s.Name)).ToArray(), half: true);
            _series.SelectedItem = Options.ForcedSeries != null && PolimoldCatalog.Find(Options.ForcedSeries) != null ? PolimoldCatalog.Find(Options.ForcedSeries).Name : "Automática";
            string[] thick = new[] { "Auto" }.Concat(PolimoldCatalog.All.SelectMany(s => s.Thicknesses)
                .Concat(new[] { MoldBaseStandards.MudFixedThickness, MoldBaseStandards.MudCavityPlate, MoldBaseStandards.BabyFixedHalf,
                                MoldBaseStandards.BabyMovingHalf, MoldBaseStandards.ZamakFixed, MoldBaseStandards.ZamakMoving, 20.0, 25, 30, 35, 40, 45, 50 })
                .Distinct().OrderBy(t => t).Select(F)).ToArray();
            _p1 = Combo(_poli, "P1", 1, thick, half: true);
            _p2 = Combo(_poli, "P2", 1, thick, half: true, right: true);
            _p1.SelectedItem = Options.ForcedP1 > 0 && thick.Contains(F(Options.ForcedP1)) ? F(Options.ForcedP1) : "Auto";
            _p2.SelectedItem = Options.ForcedP2 > 0 && thick.Contains(F(Options.ForcedP2)) ? F(Options.ForcedP2) : "Auto";
            _stroke = Combo(_poli, "Curso", 2, new[] { "Auto", "1", "2", "3" }, half: true);
            _stroke.SelectedIndex = Math.Max(0, Math.Min(3, Options.ForcedStroke));
            _type = Combo(_poli, "Tipo", 2, new[] { "2 (rasgo)", "4 (aba)" }, half: true, right: true);
            _type.SelectedIndex = Options.Type == 2 ? 0 : 1;
            _mounting = Combo(_poli, "Montagem", 3, new[] { "1", "2", "3", "4", "5", "6" }, half: true);
            _mounting.SelectedIndex = Math.Max(0, Math.Min(5, Options.Mounting - 1));
            _guiding = Combo(_poli, "Colunamento", 3, new[] { "A", "B", "C" }, half: true, right: true);
            _guiding.SelectedIndex = Math.Max(0, Math.Min(2, char.ToUpperInvariant(Options.Guiding) - 'A'));
            _steel = Combo(_poli, "Aço", 0, new[] { "1 (SAE 1045)", "3 (P20)" }, half: true, right: true);
            _steel.SelectedIndex = Options.Steel == 3 ? 1 : 0;
            y += 136;
            _auto = new Button { Left = x, Top = y, Width = 220, Height = 26, Text = "Série, espessuras e curso automáticos" };
            _auto.Click += (s, e) => { _loading = true; _series.SelectedIndex = 0; _p1.SelectedIndex = 0; _p2.SelectedIndex = 0; _stroke.SelectedIndex = 0; _loading = false; Recalc(); };
            Controls.Add(_auto);

            // Desenho e plano
            _view = new MoldBaseView { Left = 464, Top = 8, Width = 784, Height = 392 };
            Controls.Add(_view);
            _report = new TextBox
            {
                Left = 464, Top = 406, Width = 784, Height = 254, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                WordWrap = true, Font = new Font("Consolas", 8.5F), BackColor = SystemColors.Window,
            };
            Controls.Add(_report);

            bool existing = scan.Existing != null;
            _ok = new Button { Text = existing ? "Atualizar porta-molde" : "Criar porta-molde", Left = 980, Top = 674, Width = 170, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancelar", Left = 1158, Top = 674, Width = 90, DialogResult = DialogResult.Cancel };
            Controls.Add(_ok);
            Controls.Add(cancel);
            AcceptButton = _ok;
            CancelButton = cancel;
            Controls.Add(new Label
            {
                Left = 12, Top = 676, Width = 950, Height = 34, ForeColor = Color.DimGray,
                Text = existing ? "Atualizar troca os valores das mesmas peças, os componentes pelos códigos novos e as peças de biblioteca, e reempilha. Não salva a montagem ativa."
                                : "Cria a pasta \"Porta Molde - <montagem>\" com uma peça por placa e por componente, as peças de biblioteca do padrão e a submontagem. Não salva a montagem ativa.",
            });

            _series.SelectedIndexChanged += (s, e) =>
            {
                // Espessuras e curso forçados eram da série anterior — podem nem existir na nova.
                if (_loading) return;
                _loading = true;
                _p1.SelectedIndex = 0; _p2.SelectedIndex = 0; _stroke.SelectedIndex = 0;
                _loading = false;
                Recalc();
            };
            _family.SelectedIndexChanged += (s, e) =>
            {
                // Cada tipo tem as suas espessuras: as forçadas do tipo anterior voltam ao automático.
                if (_loading) return;
                _loading = true;
                _series.SelectedIndex = 0; _p1.SelectedIndex = 0; _p2.SelectedIndex = 0; _stroke.SelectedIndex = 0;
                _loading = false;
                Recalc();
            };
            foreach (Control c in new Control[] { _center, _fixedSide, _p1, _p2, _stroke, _type, _steel, _mounting, _guiding })
                ((ComboBox)c).SelectedIndexChanged += (s, e) => Recalc();
            foreach (NumericUpDown n in new[] { _parting, _margin, _floor, _clearance, _extract, _channel }) n.ValueChanged += (s, e) => Recalc();
            foreach (CheckBox c in new[] { _rotate, _guides, _returnPins }) c.CheckedChanged += (s, e) => Recalc();
            _autoParting.CheckedChanged += (s, e) => { _parting.Enabled = !_autoParting.Checked; Recalc(); };
            _ok.Click += (s, e) =>
            {
                if (Plan != null && Plan.HasFailures &&
                    MessageBox.Show("O plano tem reprovações (✗). Seguir assim mesmo?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    DialogResult = DialogResult.None;
            };
            _parting.Enabled = !_autoParting.Checked;
            _loading = false;
            Recalc();
        }

        /// <summary>Reacende só quando a marcação da grade muda (o resto do Recalc não mexe no que é aceso).</summary>
        private void RefreshHighlight()
        {
            if (_highlight == null) return;
            string key = string.Concat(_scan.Items.Select(i => !i.Include ? "0" : i.IsProduct ? "P" : "C"));
            if (key == _highlightKey) return;
            _highlightKey = key;
            _highlight.Clear();
            _highlight.Add(_scan.Items.Where(i => i.Include && !i.IsProduct).Select(i => i.Occurrence), OccurrenceHighlight.Orange);
            _highlight.Add(_scan.Items.Where(i => i.Include && i.IsProduct).Select(i => i.Occurrence), OccurrenceHighlight.Green);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _highlight?.Dispose();
            base.OnFormClosed(e);
        }

        private void Recalc()
        {
            if (_loading) return;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (!(row.Tag is MoldBaseItem it)) continue;
                it.Include = Convert.ToBoolean(row.Cells[0].Value);
                it.IsProduct = Convert.ToBoolean(row.Cells[1].Value);
            }
            RefreshHighlight();
            MoldBaseOptions o = Options;
            o.Family = (MoldBaseFamily)(_family.SelectedIndex + 1);
            o.WithGuides = _guides.Checked;
            o.WithReturnPins = _returnPins.Checked;
            o.Centering = _center.SelectedIndex == 1 ? MoldBaseCentering.ContentCenter : MoldBaseCentering.AssemblyOrigin;
            o.FixedSideNegative = _fixedSide.SelectedIndex == 1;
            o.PartingMm = _autoParting.Checked ? (double?)null : (double)_parting.Value;
            o.AllowRotation = _rotate.Checked;
            o.SideMarginMm = (double)_margin.Value;
            o.FloorMm = (double)_floor.Value;
            o.StrokeClearanceMm = (double)_clearance.Value;
            o.ProductHeightMm = (double)_extract.Value;
            o.CoolingDiameterMm = (double)_channel.Value;
            o.ForcedSeries = _series.SelectedIndex <= 0 ? null : (string)_series.SelectedItem;
            o.ForcedP1 = _p1.SelectedIndex <= 0 ? 0 : double.Parse((string)_p1.SelectedItem, CultureInfo.InvariantCulture);
            o.ForcedP2 = _p2.SelectedIndex <= 0 ? 0 : double.Parse((string)_p2.SelectedItem, CultureInfo.InvariantCulture);
            o.ForcedStroke = _stroke.SelectedIndex;
            o.Type = _type.SelectedIndex == 0 ? 2 : 4;
            o.Steel = _steel.SelectedIndex == 1 ? 3 : 1;
            o.Mounting = _mounting.SelectedIndex + 1;
            o.Guiding = (char)('A' + _guiding.SelectedIndex);

            // O que vale para o tipo escolhido.
            bool poli = o.Family == MoldBaseFamily.Polimold;
            foreach (Control c in new Control[] { _series, _stroke, _type, _steel, _mounting, _guiding }) c.Enabled = poli;
            // Polimold: componentes gerados. Gaveta e Babyplast: as peças de biblioteca do padrão (os padrões não têm
            // coluna nem pino de retorno — a gaveta corre nas guias do quadro, o Babyplast trava pelos cantos).
            bool zamak = o.Family == MoldBaseFamily.Zamak;
            _guides.Text = poli ? "Colunas e buchas" : o.Family == MoldBaseFamily.Mud ? "Quadro 7160, guias e puxadores (biblioteca)"
                         : zamak ? "Guias Ø12: só os furos (padrão)" : "Engate de extração (biblioteca)";
            _guides.Enabled = !zamak;
            _returnPins.Enabled = poli || zamak;
            _poli.Text = poli ? "Polimold · espessuras" : "Espessuras (" + MoldBaseStandards.FamilyLabel(o.Family) + ")";

            try { Plan = _planner(o.Clone()); }
            catch (Exception e) { Plan = null; _report.Text = "Falha ao planejar: " + e.GetBaseException().Message; _ok.Enabled = false; _view.Show(null, null); return; }
            _report.Text = MoldBaseReport.Text(Plan, _scan);
            _view.Show(Plan, MoldBaseBuilder.Content(_scan, o, out _));
            if (Plan.Ready && _autoParting.Checked)
            {
                _loading = true;   // mostra a partição que a escolha automática usou
                _parting.Value = Clamp(Plan.PartingMm, (double)_parting.Minimum, (double)_parting.Maximum);
                _loading = false;
            }
            _ok.Enabled = Plan.Ready;
        }

        private static decimal Clamp(double v, double min, double max) => (decimal)Math.Max(min, Math.Min(max, v));

        private GroupBox Group(string text, int x, int y, int w, int h)
        {
            var g = new GroupBox { Left = x, Top = y, Width = w, Height = h, Text = text };
            Controls.Add(g);
            return g;
        }

        private ComboBox Combo(GroupBox g, string label, int row, string[] items, bool half = false, bool right = false)
        {
            int left = right ? 225 : 10, top = 20 + row * 27;
            g.Controls.Add(new Label { Left = left, Top = top + 3, Width = half ? 80 : 120, Text = label });
            var c = new ComboBox { Left = left + (half ? 82 : 125), Top = top, Width = half ? 120 : 300, DropDownStyle = ComboBoxStyle.DropDownList };
            c.Items.AddRange(items);
            if (c.Items.Count > 0) c.SelectedIndex = 0;
            g.Controls.Add(c);
            return c;
        }

        private NumericUpDown Num(GroupBox g, string label, int col, int row, double value)
        {
            int left = col == 0 ? 10 : 225, top = 20 + row * 27;
            g.Controls.Add(new Label { Left = left, Top = top + 3, Width = 150, Text = label });
            var n = new NumericUpDown { Left = left + 152, Top = top, Width = 55, Minimum = 0, Maximum = 1000, DecimalPlaces = 1, Value = Clamp(value, 0, 1000) };
            g.Controls.Add(n);
            return n;
        }

        private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
