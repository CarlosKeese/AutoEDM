using System;
using System.Drawing;
using System.Windows.Forms;
using AutoEDM.Electrode;

namespace AutoEDM.AddIn.UI
{
    /// <summary>
    /// Janela mínima do "Unir superfícies" (Carlos, 2026-07-17): escolher o Ra numa lista
    /// pronta já aplica o GAP e a cor correspondentes (<see cref="RaGapPresets"/>) — sem
    /// reinventar a tabela nem pedir o GAP na mão.
    /// </summary>
    public sealed class RaGapPickerForm : Form
    {
        private readonly ComboBox _cbo;
        private readonly Panel _swatch;

        public RaGapPresets.Choice Chosen { get; private set; }

        /// <summary>
        /// Janela do "Aplicar GAP" (Carlos, 2026-07-21): <paramref name="preselectRa"/> vem da
        /// variável Ra gravada na peça (<see cref="RaVariableStore"/> — escrita por "Criar
        /// eletrodo (manual)" a partir da cor detectada na seleção, ou por um "Aplicar GAP"
        /// anterior) para já abrir a lista na combinação certa em vez de sempre no topo (mais
        /// grosso). Null = comportamento antigo (1º item da lista).
        /// </summary>
        public RaGapPickerForm(double? preselectRa = null)
        {
            Text = "AutoEDM — Aplicar GAP (GAP/Ra)";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(360, 140);

            var lbl = new Label { Text = "Ra da região (define GAP e cor):", Left = 12, Top = 14, Width = 330 };
            Controls.Add(lbl);

            _cbo = new ComboBox { Left = 12, Top = 36, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var choice in RaGapPresets.All()) _cbo.Items.Add(choice);
            int preselectIndex = 0;
            if (preselectRa.HasValue)
            {
                for (int i = 0; i < _cbo.Items.Count; i++)
                    if (Math.Abs(((RaGapPresets.Choice)_cbo.Items[i]).Ra - preselectRa.Value) < 0.05) { preselectIndex = i; break; }
            }
            if (_cbo.Items.Count > 0) _cbo.SelectedIndex = preselectIndex;
            _cbo.SelectedIndexChanged += (s, e) => UpdateSwatch();
            Controls.Add(_cbo);

            _swatch = new Panel { Left = 282, Top = 36, Width = 24, Height = 24, BorderStyle = BorderStyle.FixedSingle };
            Controls.Add(_swatch);

            var btnOk = new Button { Text = "Aplicar (GAP + cor)", Left = 12, Top = 80, Width = 200, DialogResult = DialogResult.OK };
            btnOk.Click += (s, e) => { Chosen = (RaGapPresets.Choice)_cbo.SelectedItem; };
            Controls.Add(btnOk);

            var btnCancel = new Button { Text = "Cancelar", Left = 220, Top = 80, Width = 128, DialogResult = DialogResult.Cancel };
            Controls.Add(btnCancel);

            AcceptButton = btnOk; CancelButton = btnCancel;
            UpdateSwatch();
        }

        private void UpdateSwatch()
        {
            var choice = _cbo.SelectedItem as RaGapPresets.Choice;
            _swatch.BackColor = choice?.Color ?? SystemColors.Control;
        }
    }
}
