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

        /// <summary>
        /// Profundidade do FURO central (mm), medida até o OMBRO — a ponta cônica da broca
        /// fica ABAIXO disso (Carlos, 2026-09-03: "altura do furo 10 mm").
        /// </summary>
        public double CenterHoleDepth { get; set; } = 10.0;

        /// <summary>
        /// Profundidade da ROSCA (mm) — menor que <see cref="CenterHoleDepth"/> de propósito:
        /// o macho não chega ao fundo do furo (Carlos, 2026-09-03: "altura da rosca 6 mm").
        /// </summary>
        public double CenterThreadDepth { get; set; } = 6.0;

        /// <summary>
        /// Ângulo da PONTA DA BROCA no fundo do furo cego (graus, ângulo total). 118° é a
        /// broca helicoidal padrão. A API do SE recebe este ângulo em GRAUS.
        /// </summary>
        public double CenterBottomAngle { get; set; } = 118.0;

        /// <summary>Chanfro de entrada do furo central: recuo em mm (0,5 × 45°).</summary>
        public double CenterChamferSetback { get; set; } = 0.5;

        /// <summary>Chanfro de entrada do furo central: ângulo em GRAUS (0,5 × 45°).</summary>
        public double CenterChamferAngle { get; set; } = 45.0;

        /// <summary>Diâmetro dos dois furos de pino.</summary>
        public double DowelDiameter { get; set; } = 4.0;

        /// <summary>Profundidade dos furos de pino Ø4 (mm) — confirmado Carlos.</summary>
        public double DowelDepth { get; set; } = 9.0;

        /// <summary>Distância entre os centros dos dois furos Ø4 (mm) — 7,5 mm do centro.</summary>
        public double DowelCenterDistance { get; set; } = 15.0;

        // --- Fixação ALTERNATIVA por EIXO (Carlos): quando os furos M6+2×Ø4 NÃO cabem no
        //     bloco, modela-se um EIXO cilíndrico no topo, preso num suporte com furo +
        //     parafuso lateral. Usa-se o Ø maior quando cabe; o menor só quando não couber.

        /// <summary>Diâmetro do eixo de fixação PADRÃO (mm) — usado quando cabe no topo.</summary>
        public double ShaftDiameterLarge { get; set; } = 9.6;

        /// <summary>Diâmetro do eixo de fixação MENOR (mm) — só quando o Ø9,6 não cabe.</summary>
        public double ShaftDiameterSmall { get; set; } = 6.1;

        /// <summary>Altura do eixo de fixação (mm). CALIBRÁVEL — confirmar com o Carlos.</summary>
        public double ShaftHeight { get; set; } = 20.0;
    }
}
