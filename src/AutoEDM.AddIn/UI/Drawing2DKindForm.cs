using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AutoEDM.AddIn.UI
{
    /// <summary>Que desenho o "Des. 2D" faz.</summary>
    public enum Drawing2DKind { Turned, SheetMetal }

    /// <summary>
    /// A escolha do botão "Des. 2D" (Carlos, 2026-11-12): Torno (peça de revolução, cotada pelas cores das
    /// faces) ou Chaparia (chapa dobrada: planificada com a posição das dobras e tabela de dobras). Vem
    /// marcada a opção do tipo do documento ativo (.par → torno, .psm → chaparia); a que não serve para
    /// o documento fica desabilitada com o motivo.
    /// </summary>
    public sealed class Drawing2DKindForm : Form
    {
        private readonly RadioButton _turned, _sheet;

        public Drawing2DKind Kind => _sheet.Checked ? Drawing2DKind.SheetMetal : Drawing2DKind.Turned;

        public Drawing2DKindForm(string documentName, bool isPart, bool isSheetMetal)
        {
            Text = "AutoEDM — Des. 2D";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = MaximizeBox = false;
            ShowInTaskbar = false;
            Font = new Font("Segoe UI", 9F);
            ClientSize = new Size(470, 230);

            Controls.Add(new Label
            {
                Left = 12, Top = 10, Width = 446, Height = 36,
                Text = "Documento: " + Path.GetFileName(documentName ?? "") + Environment.NewLine + "Que desenho criar?",
            });

            _turned = new RadioButton { Left = 24, Top = 52, Width = 420, Text = "Torno — peça de revolução, cotada pelas cores das faces", Enabled = isPart };
            Controls.Add(_turned);
            Controls.Add(new Label
            {
                Left = 42, Top = 74, Width = 410, Height = 32, ForeColor = isPart ? SystemColors.GrayText : Color.DarkRed,
                Text = isPart ? "Vista lateral como no torno, face zero escolhida na próxima janela, tolerâncias e GD&T pela cor."
                              : "Precisa de uma PEÇA (.par) ativa.",
            });

            _sheet = new RadioButton { Left = 24, Top = 110, Width = 420, Text = "Chaparia (dobra) — chapa dobrada", Enabled = isSheetMetal };
            Controls.Add(_sheet);
            Controls.Add(new Label
            {
                Left = 42, Top = 132, Width = 410, Height = 46, ForeColor = isSheetMetal ? SystemColors.GrayText : Color.DarkRed,
                Text = isSheetMetal
                    ? "Planificada com comprimento, largura e posição de cada dobra, tabela de dobras (raio, ângulo, sentido), vistas dobradas e isométrica. Sem padrão plano, ele é criado."
                    : "Precisa de uma CHAPA (.psm) ativa.",
            });

            _turned.Checked = isPart;
            _sheet.Checked = isSheetMetal && !isPart;

            var ok = new Button { Text = "Continuar", Left = 282, Top = 192, Width = 85, DialogResult = DialogResult.OK, Enabled = isPart || isSheetMetal };
            var cancel = new Button { Text = "Cancelar", Left = 373, Top = 192, Width = 85, DialogResult = DialogResult.Cancel };
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
