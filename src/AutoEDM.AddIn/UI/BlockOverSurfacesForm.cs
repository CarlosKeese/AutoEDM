using System;
using System.Drawing;
using System.Windows.Forms;
using AutoEDM.Diagnostics;
using AutoEDM.Electrode;

namespace AutoEDM.AddIn.UI
{
    /// <summary>
    /// Janela do botão "Bloco sobre superfícies" (ambiente de PEÇA). Configura os
    /// parâmetros (material, base/blank, gap, altura, fixação), mostra um resumo ao
    /// vivo (pegada + bloco escolhido) e permite PREVIEW: constrói de verdade e, ao
    /// re-visualizar ou cancelar, apaga a geometria do preview anterior.
    ///
    /// Roda na thread STA do Solid Edge (a ribbon abre esta janela modal), então as
    /// chamadas COM do <see cref="SurfaceBlockBuilder"/> acontecem no contexto certo —
    /// sem thread extra.
    /// </summary>
    public sealed class BlockOverSurfacesForm : Form
    {
        private readonly SurfaceBlockBuilder _builder;
        private readonly dynamic _partDoc;

        private ComboBox _cboMaterial;
        private ComboBox _cboBase;
        private NumericUpDown _numGap;
        private NumericUpDown _numHeight;
        private CheckBox _chkFix;
        private TextBox _txtSummary;
        private Button _btnPreview, _btnOk, _btnCancel;

        private BlockOverSurfacesResult _preview; // geometria atual do preview (null = nada aplicado)
        private bool _loading;

        public BlockOverSurfacesForm(SurfaceBlockBuilder builder, object partDoc)
        {
            _builder = builder ?? new SurfaceBlockBuilder();
            _partDoc = partDoc;
            BuildUi();
            Load += (s, e) => FullRecompute();
            FormClosing += OnClosing;
        }

        // ------------------------------------------------------------------ UI

        private void BuildUi()
        {
            Text = "AutoEDM — Bloco sobre superfícies";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false; MinimizeBox = false;
            ClientSize = new Size(440, 340);
            Font = new Font("Segoe UI", 9f);

            int lx = 14, cx = 150, cw = 270, y = 16, dy = 34;

            AddLabel("Material:", lx, y + 3);
            _cboMaterial = new ComboBox { Left = cx, Top = y, Width = cw, DropDownStyle = ComboBoxStyle.DropDownList };
            _cboMaterial.Items.AddRange(new object[] { "Cobre", "CuW80" });
            _cboMaterial.SelectedIndex = 0;
            _cboMaterial.SelectedIndexChanged += (s, e) => { if (!_loading) FullRecompute(); };
            Controls.Add(_cboMaterial);
            y += dy;

            AddLabel("Base (blank):", lx, y + 3);
            _cboBase = new ComboBox { Left = cx, Top = y, Width = cw, DropDownStyle = ComboBoxStyle.DropDownList };
            _cboBase.SelectedIndexChanged += (s, e) => { if (!_loading) UpdateSummary(); };
            Controls.Add(_cboBase);
            y += dy;

            AddLabel("Gap bloco↔superfície (mm):", lx, y + 3);
            _numGap = new NumericUpDown { Left = cx, Top = y, Width = 90, DecimalPlaces = 1, Increment = 0.5M, Minimum = 0, Maximum = 100, Value = 1M };
            _numGap.ValueChanged += (s, e) => { if (!_loading) UpdateSummary(); };
            Controls.Add(_numGap);
            y += dy;

            AddLabel("Altura do bloco (mm):", lx, y + 3);
            _numHeight = new NumericUpDown { Left = cx, Top = y, Width = 90, DecimalPlaces = 1, Increment = 1M, Minimum = 3, Maximum = 200, Value = 15 };
            _numHeight.ValueChanged += (s, e) => { if (!_loading) UpdateSummary(); };
            Controls.Add(_numHeight);
            y += dy;

            _chkFix = new CheckBox { Left = cx, Top = y, Width = cw, Text = "Aplicar fixação (furos / eixo)", Checked = true };
            Controls.Add(_chkFix);
            y += dy;

            _txtSummary = new TextBox
            {
                Left = lx, Top = y, Width = 412, Height = 92,
                Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(245, 245, 245), Font = new Font("Consolas", 8.5f)
            };
            Controls.Add(_txtSummary);
            y += 100;

            _btnPreview = new Button { Text = "Preview", Left = lx, Top = y, Width = 100, Height = 30 };
            _btnPreview.Click += (s, e) => DoPreview();
            _btnOk = new Button { Text = "OK (manter)", Left = 232, Top = y, Width = 100, Height = 30 };
            _btnOk.Click += (s, e) => DoOk();
            _btnCancel = new Button { Text = "Cancelar", Left = 338, Top = y, Width = 88, Height = 30 };
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(_btnPreview);
            Controls.Add(_btnOk);
            Controls.Add(_btnCancel);

            AcceptButton = _btnPreview;
            CancelButton = _btnCancel;
        }

        private void AddLabel(string text, int x, int y)
            => Controls.Add(new Label { Text = text, Left = x, Top = y, Width = 134, ForeColor = Color.DimGray });

        // ------------------------------------------------------------ recompute

        private BlockOverSurfacesOptions OptionsFromUi()
        {
            return new BlockOverSurfacesOptions
            {
                Material = _cboMaterial.SelectedItem?.ToString() ?? "Cobre",
                ChosenBlank = _cboBase.SelectedItem is BlankItem bi ? bi.Blank : null,
                GapMm = (double)_numGap.Value,
                BlockHeightMm = (double)_numHeight.Value,
                ApplyFixation = _chkFix.Checked,
            };
        }

        /// <summary>Recalcula tudo (repopula o combo de blanks) — no load e ao trocar material.</summary>
        private void FullRecompute()
        {
            try
            {
                var plan = _builder.Plan(_partDoc, OptionsFromUi());
                RebuildBlankCombo(plan);
                _txtSummary.Text = plan.Summary();
                _btnPreview.Enabled = _btnOk.Enabled = plan.SurfacesFound;
            }
            catch (Exception ex) { ShowError("recalcular", ex); }
        }

        /// <summary>Só atualiza o resumo (gap/altura/blank/fixação não mudam a lista de blanks).</summary>
        private void UpdateSummary()
        {
            try
            {
                var plan = _builder.Plan(_partDoc, OptionsFromUi());
                _txtSummary.Text = plan.Summary();
            }
            catch (Exception ex) { ShowError("atualizar o resumo", ex); }
        }

        private void RebuildBlankCombo(BlockOverSurfacesPlan plan)
        {
            _loading = true;
            _cboBase.Items.Clear();
            _cboBase.Items.Add(new BlankItem(null,
                plan.EligibleBlanks.Count > 0 ? $"Auto — mais compacto ({plan.EligibleBlanks[0].Name})" : "Auto (nenhum blank serve)"));
            foreach (var b in plan.EligibleBlanks) _cboBase.Items.Add(new BlankItem(b, b.Describe()));
            _cboBase.SelectedIndex = 0;
            _loading = false;
        }

        /// <summary>Item do combo de blanks (Tag = BlankSpec; null = auto).</summary>
        private sealed class BlankItem
        {
            public readonly BlankSpec Blank;
            private readonly string _text;
            public BlankItem(BlankSpec blank, string text) { Blank = blank; _text = text; }
            public override string ToString() => _text;
        }

        // -------------------------------------------------------------- actions

        private void DoPreview()
        {
            SetBusy(true);
            try
            {
                CleanupPreview();                                   // apaga o preview anterior
                _preview = _builder.Build(_partDoc, OptionsFromUi());
                _txtSummary.Text = _preview.Plan.Summary() +
                    $"\r\n[preview: bloco={YesNo(_preview.BlockCreated)}, superfícies unidas={YesNo(_preview.SurfacesUnited)}, " +
                    $"fixação={YesNo(_preview.FixationApplied)} — confira no modelo]";
            }
            catch (Exception ex) { ShowError("gerar o preview", ex); }
            finally { SetBusy(false); }
        }

        private void DoOk()
        {
            SetBusy(true);
            try
            {
                if (_preview == null)                               // OK sem preview: constrói agora
                    _preview = _builder.Build(_partDoc, OptionsFromUi());
                _preview = null;                                    // "solta" — não apagar no closing
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) { ShowError("criar o bloco", ex); SetBusy(false); }
        }

        private void CleanupPreview()
        {
            if (_preview == null) return;
            try { _builder.Cleanup(_preview); } catch (Exception ex) { Log.Warn("Cleanup do preview: " + ex.GetBaseException().Message); }
            _preview = null;
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            // Fechou por Cancelar / X sem confirmar -> remove a geometria do preview.
            if (DialogResult != DialogResult.OK) CleanupPreview();
        }

        // ---------------------------------------------------------------- infra

        private void SetBusy(bool busy)
        {
            _btnPreview.Enabled = _btnOk.Enabled = _btnCancel.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            if (busy) { Update(); }
        }

        private static string YesNo(bool b) => b ? "sim" : "não";

        private void ShowError(string what, Exception ex)
        {
            Log.Error($"Falha ao {what} (bloco sobre superfícies).", ex);
            MessageBox.Show(this, ex.GetBaseException().Message, "AutoEDM — erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
