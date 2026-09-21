using System;

namespace AutoEDM.Sealing
{
    /// <summary>
    /// Uma medida de anel O'ring: diâmetro INTERNO (d1) × seção (d2), em MILÍMETROS — a
    /// designação da ISO 3601-1. Tudo o mais (externo, médio, área) é derivado, para não
    /// existirem dois números do mesmo fato podendo divergir.
    /// </summary>
    public sealed class ORingSize
    {
        /// <summary>d1 — diâmetro INTERNO do anel, em mm.</summary>
        public double InnerDiameter { get; set; }

        /// <summary>d2 — diâmetro do CORDÃO (seção), em mm.</summary>
        public double CrossSection { get; set; }

        /// <summary>Série da ISO 3601-1 ("G" = polegada/AS568, "A" = métrica).</summary>
        public string Series { get; set; } = "G";

        /// <summary>Código do fornecedor/AS568 quando existir (ex.: "2-214"). Só rótulo.</summary>
        public string Code { get; set; }

        /// <summary>Tolerância do d1 publicada no catálogo (± mm); 0 = não informada. Entra no
        /// relatório porque a folga do anel some dentro dela: um 2-214 pode chegar Ø24,74 ou
        /// Ø25,24, e num estiramento de 2 % isso é metade da margem.</summary>
        public double Tolerance { get; set; }

        /// <summary>
        /// FALSE = medida ainda não conferida contra o catálogo do fornecedor. A tabela
        /// embutida sai toda como não-conferida DE PROPÓSITO: as seções (d2) da série G são
        /// certas, mas os d1 foram reconstruídos pela progressão das séries AS568 e ninguém
        /// deve comprar anel por eles sem olhar o catálogo. Ver <see cref="ORingCatalog"/>.
        /// </summary>
        public bool Verified { get; set; }

        /// <summary>Composto em que o fornecedor faz ESTA medida; null = qualquer (a tabela
        /// AS568 é só dimensional). O catálogo métrico da DL Seals é por composto: a mesma
        /// medida pode existir em NBR e não em Viton — e oferecer um anel que não se compra no
        /// elastômero escolhido não serve para nada.</summary>
        public Elastomer? Material { get; set; }

        /// <summary>d1 + 2·d2 — diâmetro EXTERNO do anel livre (mm).</summary>
        public double OuterDiameter => InnerDiameter + 2.0 * CrossSection;

        /// <summary>d1 + d2 — diâmetro MÉDIO do anel livre (mm); é por ele que se posiciona
        /// um canal de face, onde o anel só deita.</summary>
        public double MeanDiameter => InnerDiameter + CrossSection;

        /// <summary>Área da seção do cordão (mm²) — o volume de borracha que tem de caber no
        /// canal; base da taxa de preenchimento.</summary>
        public double SectionArea => Math.PI / 4.0 * CrossSection * CrossSection;

        public string Designation =>
            string.IsNullOrEmpty(Code)
                ? $"{Series} {InnerDiameter:0.00} × {CrossSection:0.00}"
                : $"{Series} {InnerDiameter:0.00} × {CrossSection:0.00} ({Code})";

        public override string ToString() => Designation + (Verified ? "" : "  ⚠ conferir");
    }
}
