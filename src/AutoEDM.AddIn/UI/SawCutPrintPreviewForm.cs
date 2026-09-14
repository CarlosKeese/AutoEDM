using System;
using System.Drawing;
using System.Windows.Forms;
using AutoEDM.Diagnostics;

namespace AutoEDM.AddIn.UI
{
    /// <summary>
    /// Visualização da Lista de corte antes de ir para a impressora (Carlos, 2026-09-14): mostra as
    /// páginas exatamente como sairão (miniaturas incluídas) e só imprime pelo botão daqui, que abre
    /// a escolha de impressora. DialogResult.OK = foi enviado para a impressora.
    /// </summary>
    public sealed class SawCutPrintPreviewForm : Form
    {
        private readonly SawCutPrintJob _job;
        private readonly PrintPreviewControl _preview;
        private readonly Label _pageLabel;
        private readonly Button _prev;
        private readonly Button _next;

        public SawCutPrintPreviewForm(SawCutPrintJob job)
        {
            _job = job ?? throw new ArgumentNullException(nameof(job));

            Text = "AutoEDM — Visualizar impressão (Lista de corte)";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(820, 900);
            MinimumSize = new Size(520, 420);

            _preview = new PrintPreviewControl
            {
                Dock = DockStyle.Fill,
                Document = job.Document,
                AutoZoom = true,
                UseAntiAlias = true,
            };
            var bar = new Panel { Dock = DockStyle.Bottom, Height = 44, Width = ClientSize.Width };
            // Ordem importa no Dock: o Fill entra primeiro para ocupar o que sobrar da barra.
            Controls.Add(_preview);
            Controls.Add(bar);

            _prev = new Button { Text = "◀ Anterior", Left = 8, Top = 8, Width = 100, Height = 28 };
            _next = new Button { Text = "Próxima ▶", Left = 114, Top = 8, Width = 100, Height = 28 };
            _pageLabel = new Label { AutoSize = false, Left = 222, Top = 8, Width = 160, Height = 28, TextAlign = ContentAlignment.MiddleLeft, Text = "Gerando páginas..." };
            _prev.Click += (s, e) => GoTo(_preview.StartPage - 1);
            _next.Click += (s, e) => GoTo(_preview.StartPage + 1);

            var btnClose = new Button
            {
                Text = "Fechar", DialogResult = DialogResult.Cancel,
                Width = 100, Height = 28, Top = 8,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            btnClose.Left = bar.Width - btnClose.Width - 8;
            var btnPrint = new Button
            {
                Text = "Imprimir...",
                Width = 110, Height = 28, Top = 8,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            btnPrint.Left = btnClose.Left - btnPrint.Width - 8;
            btnPrint.Click += (s, e) => PrintNow();

            bar.Controls.AddRange(new Control[] { _prev, _next, _pageLabel, btnPrint, btnClose });
            CancelButton = btnClose;

            // A visualização é calculada ao pintar; o nº de páginas só existe quando ela termina.
            _job.Document.EndPrint += OnJobEnded;
        }

        private void OnJobEnded(object sender, System.Drawing.Printing.PrintEventArgs e)
        {
            if (IsHandleCreated && !IsDisposed) BeginInvoke((Action)UpdatePager);
        }

        private void GoTo(int page)
        {
            int last = Math.Max(0, _job.PageCount - 1);
            _preview.StartPage = Math.Max(0, Math.Min(last, page));
            UpdatePager();
        }

        private void UpdatePager()
        {
            int pages = Math.Max(1, _job.PageCount);
            _pageLabel.Text = $"Página {_preview.StartPage + 1} de {pages}";
            _prev.Enabled = _preview.StartPage > 0;
            _next.Enabled = _preview.StartPage < pages - 1;
        }

        private void PrintNow()
        {
            // UseEXDialog: sem ele o diálogo clássico não abre em processo 64 bits.
            using (var dialog = new PrintDialog { Document = _job.Document, UseEXDialog = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    _job.Document.Print();
                    DialogResult = DialogResult.OK;
                }
                catch (Exception ex)
                {
                    Log.Warn("Lista de corte: impressão falhou — " + ex.GetBaseException().Message);
                    MessageBox.Show(this, "A impressão falhou:\n\n" + ex.GetBaseException().Message,
                        "AutoEDM — Lista de corte", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _job.Document.EndPrint -= OnJobEnded;
            base.Dispose(disposing);
        }
    }
}
