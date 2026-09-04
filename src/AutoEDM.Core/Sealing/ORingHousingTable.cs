using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoEDM.Sealing
{
    /// <summary>Uma linha da tabela de alojamento: os limites que o catálogo publica.</summary>
    public sealed class ORingHousing
    {
        public double CrossSection { get; set; }   // W — seção do cordão (mm)
        public double DepthMin { get; set; }       // L mín (mm)
        public double DepthMax { get; set; }       // L máx (mm)
        public double SqueezeMinPct { get; set; }  // encosto mínimo (%)
        public double SqueezeMaxPct { get; set; }  // encosto máximo (%)
        public double WidthMin { get; set; }       // corte do alojamento G, sem Parbak (mm)
        public double WidthMax { get; set; }
        public double ClearanceMin { get; set; }   // jogo diametral E (mm)
        public double ClearanceMax { get; set; }
        public double RadiusMin { get; set; }      // raio do alojamento R (mm)
        public double RadiusMax { get; set; }
        public double Eccentricity { get; set; }   // excentricidade admissível (mm)

        /// <summary>Profundidade NOMINAL de projeto — o meio da faixa publicada.</summary>
        public double Depth => (DepthMin + DepthMax) / 2.0;

        /// <summary>Largura NOMINAL de projeto — o meio da faixa publicada.</summary>
        public double Width => (WidthMin + WidthMax) / 2.0;

        /// <summary>Raio de fundo nominal.</summary>
        public double Radius => (RadiusMin + RadiusMax) / 2.0;
    }

    /// <summary>
    /// A tabela REAL de alojamento — "Tabela para Desenho de Alojamento Estático e Dinâmico",
    /// catálogo Parker "O'Rings 001-5 BR" (junho/2009), página 5. Substitui a estimativa por
    /// coeficientes que este projeto usava antes.
    ///
    /// O QUE A TABELA MOSTROU QUE A ESTIMATIVA ERRAVA:
    ///   * o esmagamento NÃO é uma porcentagem só — ele CAI conforme a seção engrossa. No
    ///     estático vai de ~27 % no cordão de 1,78 mm a ~16 % no de 6,99 mm. A estimativa
    ///     usava 20 % para todos, o que deixava o canal do 1,78 raso demais;
    ///   * a LARGURA do canal não muda com o tipo de vedação: a coluna "corte do alojamento G"
    ///     é a MESMA nas tabelas estática e dinâmica. A estimativa fazia a largura variar com
    ///     o preenchimento alvo, que variava com o movimento;
    ///   * a largura real é ~1,40 × W, não ~1,31 × W.
    ///
    /// A tabela cobre ESTÁTICO e DINÂMICO (recíproco). Vedação ROTATIVA não está nela — para
    /// esse caso o AutoEDM continua calculando (ver <see cref="ORingGrooveRules"/>) e o
    /// relatório diz que a origem foi cálculo, não tabela.
    ///
    /// Os limites de ESTIRAMENTO (que dependem do elastômero) também não vêm daqui: a tabela
    /// dimensiona o canal, não escolhe o anel.
    /// </summary>
    public static class ORingHousingTable
    {
        /// <summary>Alojamento ESTÁTICO (catálogo Parker 001-5 BR, p. 5, bloco "Estático").</summary>
        private static readonly ORingHousing[] Static =
        {
            new ORingHousing { CrossSection = 1.78, DepthMin = 1.25, DepthMax = 1.35, SqueezeMinPct = 20, SqueezeMaxPct = 33, WidthMin = 2.4, WidthMax = 2.6, ClearanceMin = 0.05, ClearanceMax = 0.13, RadiusMin = 0.1, RadiusMax = 0.4, Eccentricity = 0.05 },
            new ORingHousing { CrossSection = 2.62, DepthMin = 2.05, DepthMax = 2.15, SqueezeMinPct = 15, SqueezeMaxPct = 25, WidthMin = 3.6, WidthMax = 3.8, ClearanceMin = 0.05, ClearanceMax = 0.13, RadiusMin = 0.1, RadiusMax = 0.4, Eccentricity = 0.05 },
            new ORingHousing { CrossSection = 3.53, DepthMin = 2.80, DepthMax = 2.95, SqueezeMinPct = 13, SqueezeMaxPct = 23, ClearanceMin = 0.08, ClearanceMax = 0.16, WidthMin = 4.8, WidthMax = 5.0, RadiusMin = 0.2, RadiusMax = 0.6, Eccentricity = 0.08 },
            new ORingHousing { CrossSection = 5.33, DepthMin = 4.30, DepthMax = 4.50, SqueezeMinPct = 13, SqueezeMaxPct = 22, ClearanceMin = 0.08, ClearanceMax = 0.18, WidthMin = 7.2, WidthMax = 7.4, RadiusMin = 0.5, RadiusMax = 1.0, Eccentricity = 0.10 },
            new ORingHousing { CrossSection = 6.99, DepthMin = 5.75, DepthMax = 5.95, SqueezeMinPct = 13, SqueezeMaxPct = 20, ClearanceMin = 0.10, ClearanceMax = 0.20, WidthMin = 9.6, WidthMax = 9.8, RadiusMin = 0.5, RadiusMax = 1.0, Eccentricity = 0.12 }
        };

        /// <summary>Alojamento DINÂMICO / recíproco (mesma página, bloco "Dinâmico").</summary>
        private static readonly ORingHousing[] Dynamic =
        {
            new ORingHousing { CrossSection = 1.78, DepthMin = 1.40, DepthMax = 1.45, SqueezeMinPct = 14, SqueezeMaxPct = 25, WidthMin = 2.4, WidthMax = 2.6, ClearanceMin = 0.05, ClearanceMax = 0.13, RadiusMin = 0.1, RadiusMax = 0.4, Eccentricity = 0.05 },
            new ORingHousing { CrossSection = 2.62, DepthMin = 2.25, DepthMax = 2.30, SqueezeMinPct =  9, SqueezeMaxPct = 19, WidthMin = 3.6, WidthMax = 3.8, ClearanceMin = 0.05, ClearanceMax = 0.13, RadiusMin = 0.1, RadiusMax = 0.4, Eccentricity = 0.05 },
            new ORingHousing { CrossSection = 3.53, DepthMin = 3.05, DepthMax = 3.10, SqueezeMinPct =  9, SqueezeMaxPct = 16, WidthMin = 4.8, WidthMax = 5.0, ClearanceMin = 0.08, ClearanceMax = 0.16, RadiusMin = 0.2, RadiusMax = 0.6, Eccentricity = 0.08 },
            new ORingHousing { CrossSection = 5.33, DepthMin = 4.65, DepthMax = 4.75, SqueezeMinPct =  8, SqueezeMaxPct = 15, WidthMin = 7.2, WidthMax = 7.4, ClearanceMin = 0.08, ClearanceMax = 0.18, RadiusMin = 0.5, RadiusMax = 1.0, Eccentricity = 0.10 },
            new ORingHousing { CrossSection = 6.99, DepthMin = 6.00, DepthMax = 6.10, SqueezeMinPct = 10, SqueezeMaxPct = 16, WidthMin = 9.6, WidthMax = 9.8, ClearanceMin = 0.10, ClearanceMax = 0.20, RadiusMin = 0.5, RadiusMax = 1.0, Eccentricity = 0.12 }
        };

        /// <summary>Seções que a tabela cobre (mm).</summary>
        public static IReadOnlyList<double> TabulatedCrossSections =>
            Static.Select(h => h.CrossSection).ToList();

        /// <summary>
        /// A linha da tabela para esta seção e este movimento, ou <c>null</c> quando não há —
        /// seção fora das cinco tabeladas, ou vedação ROTATIVA (que o catálogo não tabela).
        /// Devolver null é de propósito: quem chama decide cair no cálculo e DIZER que caiu.
        /// </summary>
        public static ORingHousing Find(double crossSectionMm, SealMotion motion)
        {
            if (motion == SealMotion.Rotary) return null;
            var table = motion == SealMotion.Reciprocating ? Dynamic : Static;
            return table.FirstOrDefault(h => Math.Abs(h.CrossSection - crossSectionMm) < 0.01);
        }
    }
}
