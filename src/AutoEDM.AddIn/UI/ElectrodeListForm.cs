using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AutoEDM.Electrode;

namespace AutoEDM.AddIn.UI
{
    /// <summary>
    /// Janela "Coordenadas" (Carlos, 2026-08-04): lista os eletrodos SELECIONADOS na
    /// montagem (não detecção automática por cor) com a posição — a mesma que aparece
    /// em "Propriedades de Ocorrência" no SE — e o GAP/Ra gravados em cada peça. Só
    /// leitura; a grade é ordenável por coluna (clique no cabeçalho) e o conteúdo pode
    /// ser selecionado/copiado (Ctrl+C) para colar direto numa planilha.
    /// </summary>
    public sealed class ElectrodeListForm : Form
    {
        public ElectrodeListForm(IReadOnlyList<ElectrodeListItem> items)
        {
            Text = $"AutoEDM — Coordenadas ({(items?.Count ?? 0)} eletrodo(s))";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(760, 420);
            MinimumSize = new Size(560, 260);

            var grid = new DataGridView
            {
                Left = 0, Top = 0, Width = ClientSize.Width, Height = ClientSize.Height - 44,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithAutoHeaderText,
                BackgroundColor = SystemColors.Window,
            };
            grid.Columns.Add("Name", "Eletrodo");
            grid.Columns.Add("X", "X (mm)");
            grid.Columns.Add("Y", "Y (mm)");
            grid.Columns.Add("Z", "Z (mm)");
            grid.Columns.Add("Az", "Rot. Z (°)");
            grid.Columns.Add("Gap", "GAP (mm)");
            grid.Columns.Add("Ra", "Ra (µm)");
            grid.Columns.Add("Notes", "Observações");
            foreach (DataGridViewColumn c in grid.Columns)
                if (c.Name != "Name" && c.Name != "Notes")
                    c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            foreach (var it in items ?? Array.Empty<ElectrodeListItem>())
            {
                int row = grid.Rows.Add(
                    it.Name ?? "—",
                    it.PositionKnown ? it.X.ToString("0.000") : "—",
                    it.PositionKnown ? it.Y.ToString("0.000") : "—",
                    it.PositionKnown ? it.Z.ToString("0.000") : "—",
                    it.PositionKnown ? it.AzDeg.ToString("0.0") : "—",
                    it.GapMm.HasValue ? it.GapMm.Value.ToString("0.00") : "—",
                    it.Ra.HasValue ? it.Ra.Value.ToString("0.0") : "—",
                    string.Join(" | ", it.Notes));
                if (!it.PositionKnown || !it.GapMm.HasValue || !it.Ra.HasValue)
                    grid.Rows[row].DefaultCellStyle.ForeColor = Color.FromArgb(160, 90, 0);
            }
            Controls.Add(grid);

            var btnClose = new Button
            {
                Text = "Fechar", DialogResult = DialogResult.OK,
                Width = 100, Height = 28,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            };
            btnClose.Left = ClientSize.Width - btnClose.Width - 8;
            btnClose.Top = ClientSize.Height - btnClose.Height - 8;
            Controls.Add(btnClose);

            AcceptButton = btnClose;
            CancelButton = btnClose;
        }
    }
}
