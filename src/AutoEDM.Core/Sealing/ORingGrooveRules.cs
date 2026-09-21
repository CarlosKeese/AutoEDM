using System;

namespace AutoEDM.Sealing
{
    /// <summary>
    /// TODOS os coeficientes de projeto do alojamento num lugar só — para que dê para
    /// auditar, discutir e ajustar sem caçar número solto pelo código.
    ///
    /// DE ONDE VÊM (importa saber, para não tratar tudo como "a norma manda"):
    ///   * A ISO 3601-1 define as MEDIDAS DOS ANÉIS (d1 × d2). Quem tabela as COTAS DO
    ///     ALOJAMENTO é a ISO 3601-2. Eu não reproduzo a tabela da 3601-2 aqui: em vez disso
    ///     o canal é CALCULADO pelos três critérios de projeto que essa tabela materializa —
    ///     ESMAGAMENTO, PREENCHIMENTO e ESTIRAMENTO — que são consenso de engenharia de
    ///     vedação (manuais Parker/Trelleborg/Freudenberg batem entre si nas faixas abaixo).
    ///   * Conferência de sanidade: com estes números, um canal estático dá largura ≈ 1,31·d2
    ///     e profundidade ≈ 0,80·d2 — que é exatamente a ordem de grandeza tabelada nos
    ///     manuais (ex.: d2 = 2,62 → canal 3,43 × 2,10 mm; d2 = 3,53 → 4,62 × 2,82 mm).
    ///
    /// Se a sua norma interna pedir outra faixa, é AQUI que se muda, e o efeito aparece
    /// inteiro no relatório do <see cref="ORingGrooveSpec"/>.
    /// </summary>
    public static class ORingGrooveRules
    {
        /// <summary>
        /// ESMAGAMENTO (fração de d2 que o canal "come" do cordão). É o que gera a vedação.
        /// Muito pouco: vaza. Muito: o anel deforma permanente e, se houver movimento, arrasta,
        /// esquenta e rasga — por isso cai conforme o movimento aumenta.
        /// </summary>
        public static void Squeeze(SealMotion motion, out double min, out double target, out double max)
        {
            switch (motion)
            {
                case SealMotion.Reciprocating: min = 0.10; target = 0.15; max = 0.20; break;
                case SealMotion.Rotary:        min = 0.02; target = 0.05; max = 0.08; break;
                default:                       min = 0.15; target = 0.20; max = 0.30; break; // estática
            }
        }

        /// <summary>
        /// PREENCHIMENTO alvo: quanto da área do canal a borracha ocupa. Borracha é
        /// praticamente incompressível — ela não some, ela ESCOA. Se o canal ficar cheio
        /// demais, o anel não tem para onde ir quando dilatar com o calor ou inchar no fluido,
        /// e ele mesmo arrebenta o alojamento. Daí a folga de projeto.
        /// O FKM dilata mais que a NBR, então pede um pouco mais de folga.
        /// </summary>
        public static double TargetFill(SealMotion motion, Elastomer elastomer)
        {
            double baseFill;
            switch (motion)
            {
                case SealMotion.Reciprocating: baseFill = 0.72; break;
                case SealMotion.Rotary:        baseFill = 0.65; break;
                default:                       baseFill = 0.75; break; // estática
            }
            return elastomer == Elastomer.Fkm ? baseFill - 0.03 : baseFill;
        }

        /// <summary>Preenchimento MÁXIMO admissível — acima disso o canal é reprovado.</summary>
        public const double MaxFill = 0.90;

        /// <summary>
        /// ESTIRAMENTO máximo do d1 num canal de EIXO. A NBR alonga mais que o FKM, daí o
        /// limite maior. Em vedação ROTATIVA o limite é ZERO: borracha esticada CONTRAI ao
        /// esquentar (efeito Gow-Joule), então um anel esticado num eixo girando vai apertando
        /// sozinho até queimar — nesse caso o anel tem de ser igual ou maior que o fundo do canal.
        /// </summary>
        public static double MaxStretch(SealMotion motion, Elastomer elastomer)
        {
            if (motion == SealMotion.Rotary) return 0.0;
            return elastomer == Elastomer.Fkm ? 0.03 : 0.05;
        }

        /// <summary>Estiramento ALVO (o que a seleção persegue). Negativo em rotativa: o anel
        /// deve entrar com uma folga pequena, nunca esticado.</summary>
        public static double TargetStretch(SealMotion motion) =>
            motion == SealMotion.Rotary ? -0.02 : 0.02;

        /// <summary>Folga máxima tolerável (estiramento negativo) antes de o anel ficar solto
        /// no canal e correr risco de torcer na montagem.</summary>
        public const double MaxSlack = 0.05;

        /// <summary>
        /// COMPRESSÃO máxima do diâmetro EXTERNO num canal de FURO: ali o anel entra apertado
        /// e abre dentro do canal. Comprimir demais enruga o anel (ele ondula em vez de
        /// encostar liso) e a vedação falha.
        /// </summary>
        public static double MaxOuterCompression(Elastomer elastomer) =>
            elastomer == Elastomer.Fkm ? 0.02 : 0.03;

        /// <summary>Compressão ALVO do diâmetro externo no canal de furo.</summary>
        public const double TargetOuterCompression = 0.01;

        /// <summary>
        /// Canal de FACE: quanto o anel entra APERTADO contra a parede que o apoia (ver
        /// <see cref="FacePressure"/>). Pressão interna: o Ø externo do canal fica 1 % menor que
        /// o Ø externo do anel. Pressão externa: o Ø interno do canal fica 1 % maior que o d1 (o
        /// anel estica 1 %). Com o anel já encostado, a pressão não tem folga para arrastá-lo.
        /// Fica dentro dos limites de <see cref="MaxStretch"/>/<see cref="MaxOuterCompression"/>.
        /// </summary>
        public const double FaceSeatFit = 0.01;

        /// <summary>Limites de largura do canal, em múltiplos de d2 — trava de sanidade para o
        /// cálculo por preenchimento não gerar um canal absurdo em caso extremo.</summary>
        public const double MinWidthFactor = 1.10;
        public const double MaxWidthFactor = 1.70;

        /// <summary>Raio do FUNDO do canal (mm): canto vivo no fundo concentra tensão e corta o
        /// anel. ~0,1·d2, limitado a [0,20; 0,50] mm.</summary>
        public static double BottomRadius(double crossSectionMm) =>
            Clamp(0.10 * crossSectionMm, 0.20, 0.50);

        /// <summary>Quebra de canto na ENTRADA do canal (mm): sem ela a aresta viva raspa o
        /// anel na montagem. ~0,05·d2, limitada a [0,10; 0,30] mm.</summary>
        public static double EdgeBreak(double crossSectionMm) =>
            Clamp(0.05 * crossSectionMm, 0.10, 0.30);

        /// <summary>
        /// Ao ser esticado, o cordão AFINA (o volume de borracha é o mesmo). Um anel esticado
        /// 5 % perde ~2,5 % de seção — e com isso perde esmagamento, que é justamente o que
        /// veda. Aproximação de 1ª ordem usada em manual de vedação: Δd2/d2 ≈ −0,5·estiramento.
        /// Só vale para estiramento pequeno (é o único que a norma admite mesmo).
        /// </summary>
        public static double EffectiveCrossSection(double crossSectionMm, double stretch) =>
            crossSectionMm * (1.0 - 0.5 * Math.Max(0.0, stretch));

        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
