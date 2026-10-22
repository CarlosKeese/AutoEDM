using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AutoEDM.Mold;

namespace AutoEDM.AddIn.UI
{
    /// <summary>
    /// A escolha do "Desenho de processo" (Carlos, 2026-10-22): em que máquina a peça vai ser trabalhada,
    /// CDU (centro de usinagem) e/ou CDT (centro de torneamento). Cada marcação vira uma peça
    /// &lt;peça&gt;_CDU / _CDT vinculada à original. O plano é refeito a cada clique e mostrado antes de
    /// criar — o que nasce, o que já existe e é reaproveitado, e se a montagem é nova ou acrescentada.
    /// </summary>
    public sealed class ProcessMachineForm : Form
    {
        private static ProcessMachine _last = ProcessMachine.Cdu;

        private readonly Func<ProcessMachine, ProcessDrawingPlan> _plan;
        private readonly CheckBox _cdu, _cdt;
        private readonly Label _summary;
        private readonly Button _ok;

        public ProcessDrawingPlan Plan { get; private set; }

        public ProcessMachineForm(string sourcePath, bool unsaved, Func<ProcessMachine, ProcessDrawingPlan> plan)
        {
            _plan = plan;
            Text = "AutoEDM — Desenho de processo";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = MaximizeBox = false;
            ShowInTaskbar = false;
            Font = new Font("Segoe UI", 9F);
            ClientSize = new Size(520, 330);

            Controls.Add(new Label
            {
                Left = 12, Top = 10, Width = 496, Height = 36,
                Text = "Peça: " + Path.GetFileName(sourcePath) + Environment.NewLine + "Em que máquina ela vai ser trabalhada?",
            });

            _cdu = new CheckBox { Left = 24, Top = 54, Width = 300, Text = "CDU — centro de usinagem (_CDU)", Checked = (_last & ProcessMachine.Cdu) != 0 };
            _cdt = new CheckBox { Left = 24, Top = 80, Width = 300, Text = "CDT — centro de torneamento (_CDT)", Checked = (_last & ProcessMachine.Cdt) != 0 };
            Controls.Add(_cdu);
            Controls.Add(_cdt);

            _summary = new Label { Left = 12, Top = 112, Width = 496, Height = 150, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(4) };
            Controls.Add(_summary);

            if (unsaved)
                Controls.Add(new Label
                {
                    Left = 12, Top = 266, Width = 496, Height = 30, ForeColor = Color.DarkRed,
                    Text = "A peça tem alterações NÃO SALVAS: a cópia é feita do arquivo — salve antes para levá-las.",
                });

            _ok = new Button { Text = "Criar", Left = 332, Top = 296, Width = 85, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancelar", Left = 423, Top = 296, Width = 85, DialogResult = DialogResult.Cancel };
            Controls.Add(_ok);
            Controls.Add(cancel);
            AcceptButton = _ok;
            CancelButton = cancel;

            _cdu.CheckedChanged += (s, e) => UpdatePlan();
            _cdt.CheckedChanged += (s, e) => UpdatePlan();
            FormClosing += (s, e) => { if (DialogResult == DialogResult.OK) _last = Selected; };
            UpdatePlan();
        }

        private ProcessMachine Selected =>
            (_cdu.Checked ? ProcessMachine.Cdu : ProcessMachine.None) | (_cdt.Checked ? ProcessMachine.Cdt : ProcessMachine.None);

        private void UpdatePlan()
        {
            Plan = _plan(Selected);
            _ok.Text = Plan.NothingToDo ? "Abrir" : "Criar";
            _ok.Enabled = Plan.Ready || Plan.NothingToDo;

            if (Plan.Error != null) { _summary.Text = Plan.Error; return; }

            string nl = Environment.NewLine;
            string text = "Em " + Plan.Folder + ":" + nl;
            foreach (ProcessCopy c in Plan.Copies)
                text += "  • " + Path.GetFileName(c.Path) + (c.Exists
                    ? " — já existe, é reaproveitada"
                    : " — peça nova com Cópia de peça VINCULADA à original (ordenado)") + nl;
            text += "  • " + Path.GetFileName(Plan.AssemblyPath) + (Plan.AssemblyExists
                ? " — já existe: é aberta e recebe só o que faltar"
                : " — montagem nova com a original e as peças acima, na origem") + nl + nl;
            text += Plan.NothingToDo
                ? "Tudo já existe. \"Abrir\" abre a montagem."
                : "Nada é sobrescrito. Alterou e salvou a original, as peças de processo recebem a mudança.";
            _summary.Text = text;
        }
    }
}
