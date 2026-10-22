using System;
using System.Windows.Forms;

namespace AutoEDM.AddIn.UI
{
    /// <summary>
    /// O Ø final do eletrodo de interpolação, dentro da <see cref="FacePickForm"/>. O ponto mais fundo
    /// do perfil fica a Ø/2 do eixo. O último valor vale para a sessão inteira do SE.
    /// </summary>
    public sealed class InterpolationOptionsPanel : Panel
    {
        private static decimal _last = 20m;
        private readonly NumericUpDown _diameter;

        public InterpolationOptionsPanel()
        {
            Height = 30;
            Controls.Add(new Label { Left = 0, Top = 4, Width = 150, Text = "Ø do eletrodo (mm):" });
            _diameter = new NumericUpDown
            {
                Left = 150, Top = 0, Width = 90,
                DecimalPlaces = 2, Increment = 0.5m, Minimum = 0.1m, Maximum = 1000m, Value = _last
            };
            _diameter.ValueChanged += (s, e) => _last = _diameter.Value;
            Controls.Add(_diameter);

            new ToolTip().SetToolTip(_diameter,
                "Diâmetro FINAL do eletrodo: o eixo fica a Ø/2 do ponto mais fundo do perfil, do lado vazio. " +
                "Tem de ser maior que o dobro da profundidade do perfil.");
        }

        public double DiameterMm => (double)_diameter.Value;
    }
}
