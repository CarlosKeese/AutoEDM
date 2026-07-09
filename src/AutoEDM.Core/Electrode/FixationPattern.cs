namespace AutoEDM.Electrode
{
    /// <summary>
    /// Padrão de furação de fixação do eletrodo na base/holder:
    ///   - 1 furo central roscado M6;
    ///   - 2 furos Ø4 (pinos/localização) com 15 mm entre centros.
    ///
    /// Usado ao gerar o holder (etapa de geometria). Diâmetros em mm.
    /// </summary>
    public sealed class FixationPattern
    {
        /// <summary>Rosca do furo central.</summary>
        public string CenterThread { get; set; } = "M6";

        /// <summary>Diâmetro do furo-guia da rosca central (broca de M6 ≈ 5,0 mm).</summary>
        public double CenterTapDrillDiameter { get; set; } = 5.0;

        /// <summary>Profundidade do furo roscado central M6 (mm) — confirmado Carlos.</summary>
        public double CenterHoleDepth { get; set; } = 11.0;

        /// <summary>Diâmetro dos dois furos de pino.</summary>
        public double DowelDiameter { get; set; } = 4.0;

        /// <summary>Profundidade dos furos de pino Ø4 (mm) — confirmado Carlos.</summary>
        public double DowelDepth { get; set; } = 9.0;

        /// <summary>Distância entre os centros dos dois furos Ø4 (mm) — 7,5 mm do centro.</summary>
        public double DowelCenterDistance { get; set; } = 15.0;
    }
}
