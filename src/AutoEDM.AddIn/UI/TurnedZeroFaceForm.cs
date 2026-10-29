using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AutoEDM.Diagnostics;
using AutoEDM.Drawing;

namespace AutoEDM.AddIn.UI
{
    /// <summary>
    /// A escolha do "Des. 2D (Torno)" (Carlos, 2026-10-29): qual face plana é a face ZERO — as cotas de
    /// comprimento partem dela e, no desenho, ela fica virada para a direita, como a peça vista do
    /// torno. A face da linha marcada fica realçada no modelo, e o plano de cotas embaixo é refeito a
    /// cada troca. Abre com a face plana que estiver selecionada na peça, ou com a ponta oposta à cabeça.
    /// </summary>
    public sealed class TurnedZeroFaceForm : Form
    {
        private readonly object _doc;
        private readonly TurnedProfile _profile;
        private readonly TurnedDimensionPlan _plan;
        private readonly List<RevolvedFace> _faces;
        private readonly ListBox _list;
        private readonly TextBox _planText;
        private object _highlight;

        public RevolvedFace Zero => _list.SelectedIndex < 0 ? null : _faces[_list.SelectedIndex];

        public TurnedZeroFaceForm(object partDoc, TurnedProfile profile, TurnedDimensionPlan plan)
        {
            _doc = partDoc;
            _profile = profile;
            _plan = plan;
            _faces = TurnedZeroFace.Candidates(profile);

            Text = "AutoEDM — Des. 2D (Torno)";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = MaximizeBox = false;
            ShowInTaskbar = false;
            Font = new Font("Segoe UI", 9F);
            ClientSize = new Size(620, 560);

            Controls.Add(new Label
            {
                Left = 12, Top = 10, Width = 596, Height = 36,
                Text = "Qual é a face ZERO? As cotas de comprimento partem dela, e no desenho ela fica virada" +
                       Environment.NewLine + "para a DIREITA, como a peça vista do torno. A face marcada fica realçada no modelo.",
            });

            _list = new ListBox { Left = 12, Top = 50, Width = 596, Height = 150, IntegralHeight = false };
            foreach (RevolvedFace f in _faces) _list.Items.Add(TurnedZeroFace.Describe(profile, f));
            Controls.Add(_list);

            Controls.Add(new Label { Left = 12, Top = 208, Width = 596, Height = 18, Text = "Plano de cotas:" });
            _planText = new TextBox
            {
                Left = 12, Top = 228, Width = 596, Height = 284, Multiline = true, ReadOnly = true,
                ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9F), BackColor = SystemColors.Window,
            };
            Controls.Add(_planText);

            var ok = new Button { Text = "Criar desenho", Left = 412, Top = 522, Width = 105, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancelar", Left = 523, Top = 522, Width = 85, DialogResult = DialogResult.Cancel };
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;

            _list.SelectedIndexChanged += (s, e) => { ok.Enabled = Zero != null; ShowZero(Zero); };
            int start = _faces.IndexOf(SelectedInModel() ?? TurnedZeroFace.Default(profile));
            _list.SelectedIndex = start >= 0 ? start : (_faces.Count > 0 ? 0 : -1);
            ok.Enabled = Zero != null;
            if (_list.SelectedIndex < 0) ShowZero(null);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            ClearHighlight();
            base.OnFormClosed(e);
        }

        private void ShowZero(RevolvedFace zero)
        {
            _planText.Text = TurnedPlanFormatter.Describe(_profile, _plan, zero);
            Highlight(zero);
        }

        /// <summary>A face plana que o Carlos selecionou na peça antes de clicar, se for uma das candidatas.</summary>
        private RevolvedFace SelectedInModel()
        {
            try
            {
                dynamic ss = ((dynamic)_doc).SelectSet;
                int n = (int)ss.Count;
                for (int i = 1; i <= n; i++)
                {
                    dynamic item = ss.Item(i);
                    int id;
                    try { id = (int)item.ID; }
                    catch { id = (int)item.Object.ID; }   // item de SelectSet pode ser invólucro
                    RevolvedFace f = TurnedZeroFace.Find(_profile, id);
                    if (f != null) return f;
                }
            }
            catch { }
            return null;
        }

        private void Highlight(RevolvedFace zero)
        {
            ClearHighlight();
            if (zero == null) return;
            try
            {
                object face = FindFace(zero.Id);
                if (face == null) return;
                dynamic hs = ((dynamic)_doc).HighlightSets.Add();
                _highlight = (object)hs;
                hs.Color = 255 | (140 << 8);          // laranja
                hs.AddItem(face);
                hs.Draw();
            }
            catch (Exception e) { Log.Warn("Des. 2D (Torno): realce não aplicado — " + e.GetBaseException().Message); }
        }

        private object FindFace(int id)
        {
            dynamic faces = ((dynamic)_doc).Models.Item(1).Body.Faces[1];   // igQueryAll
            int n = (int)faces.Count;
            for (int i = 1; i <= n; i++)
            {
                dynamic f = faces.Item(i);
                try { if ((int)f.ID == id) return (object)f; } catch { }
            }
            return null;
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
