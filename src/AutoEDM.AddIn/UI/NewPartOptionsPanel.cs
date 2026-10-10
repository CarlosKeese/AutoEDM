using System;
using System.Drawing;
using System.Windows.Forms;
using AutoEDM.Diagnostics;
using AutoEDM.Mold;

namespace AutoEDM.AddIn.UI
{
    /// <summary>
    /// As opções do "Nova peça", dentro da <see cref="FacePickForm"/>: de que parte do molde a
    /// peça é (define a série do código), qual eixo da montagem é a altura e se a origem vai no
    /// ponto mais baixo ou mais alto das faces. Mostra o nome que vai sair ANTES de criar.
    ///
    /// Eixo e lado são do PROJETO (cada montagem pode estar orientada de um jeito) e ficam
    /// gravados por montagem em <see cref="MoldProjectSettings"/> — junto com a última parte usada.
    /// </summary>
    public sealed class NewPartOptionsPanel : Panel
    {
        private static readonly MoldSection[] Sections = { MoldSection.Fixed, MoldSection.Moving, MoldSection.Ejection };

        private readonly Func<object> _doc;          // montagem FRESCA a cada leitura
        private readonly string _asmPath;
        private readonly MoldProjectSettings _prefs;
        private readonly ComboBox _cboSection, _cboAxis;
        private readonly RadioButton _rdoBottom, _rdoTop;
        private readonly Label _lblName;
        private bool _loading;

        public NewPartOptionsPanel(Func<object> doc, string asmPath)
        {
            _doc = doc;
            _asmPath = asmPath;
            _prefs = MoldProjectSettings.Load(asmPath);
            Height = 104;

            _loading = true;
            Controls.Add(new Label { Left = 0, Top = 4, Width = 150, Text = "Parte do molde:" });
            _cboSection = new ComboBox { Left = 150, Top = 0, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (MoldSection s in Sections) _cboSection.Items.Add(MoldPartNaming.SectionLabel(s));
            _cboSection.SelectedIndex = Math.Max(0, Array.IndexOf(Sections, _prefs.LastSection));
            Controls.Add(_cboSection);

            Controls.Add(new Label { Left = 0, Top = 34, Width = 150, Text = "Eixo da altura (montagem):" });
            _cboAxis = new ComboBox { Left = 150, Top = 30, Width = 60, DropDownStyle = ComboBoxStyle.DropDownList };
            _cboAxis.Items.AddRange(new object[] { "X", "Y", "Z" });
            _cboAxis.SelectedIndex = (int)_prefs.HeightAxis;
            Controls.Add(_cboAxis);

            _rdoBottom = new RadioButton { Left = 222, Top = 31, Width = 110, Text = "ponto mais baixo", Checked = !_prefs.OriginAtTop };
            _rdoTop = new RadioButton { Left = 336, Top = 31, Width = 120, Text = "ponto mais alto", Checked = _prefs.OriginAtTop };
            Controls.Add(_rdoBottom);
            Controls.Add(_rdoTop);

            _lblName = new Label { Left = 0, Top = 62, Width = 456, Height = 40, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            Controls.Add(_lblName);
            _loading = false;

            var tips = new ToolTip();
            tips.SetToolTip(_cboSection, "Define a série do código: fixa = XXXXX.100 em diante, móvel = .200, extração = .300. " +
                                         "O número é o próximo livre na pasta da montagem.");
            tips.SetToolTip(_cboAxis, "Qual eixo da MONTAGEM é a altura do molde neste projeto. Fica gravado para o projeto.");

            _cboSection.SelectedIndexChanged += (s, e) => { SaveChoice(); RefreshName(); };
            _cboAxis.SelectedIndexChanged += (s, e) => SaveChoice();
            _rdoTop.CheckedChanged += (s, e) => SaveChoice();

            RefreshName();
        }

        public NewPartOptions Options => new NewPartOptions
        {
            Section = Sections[Math.Max(0, _cboSection.SelectedIndex)],
            HeightAxis = (HeightAxis)Math.Max(0, _cboAxis.SelectedIndex),
            OriginAtTop = _rdoTop.Checked,
        };

        /// <summary>Relê pasta + montagem e mostra o nome que sai no próximo "Criar".</summary>
        public void RefreshName()
        {
            try
            {
                object doc = _doc();
                if (doc == null) { _lblName.Text = "— sem montagem ativa —"; return; }
                NewPartNamePlan plan = NewPartBuilder.PlanName(doc, Options.Section);
                _lblName.ForeColor = plan.Problem == null ? Color.DarkGreen : Color.Firebrick;
                _lblName.Text = plan.Problem ?? $"Próxima peça: {plan.FileName}";
            }
            catch (Exception e)
            {
                _lblName.Text = "— não consegui calcular o nome (veja o log) —";
                Log.Warn("Nova peça: prévia do nome falhou — " + e.GetBaseException().Message);
            }
        }

        private void SaveChoice()
        {
            if (_loading) return;
            NewPartOptions o = Options;
            _prefs.LastSection = o.Section;
            _prefs.HeightAxis = o.HeightAxis;
            _prefs.OriginAtTop = o.OriginAtTop;
            _prefs.Save(_asmPath);
        }
    }
}
