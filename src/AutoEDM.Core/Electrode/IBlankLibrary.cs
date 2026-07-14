using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AutoEDM.Electrode
{
    /// <summary>An axis-aligned bounding box in model space (mm).</summary>
    public struct BoundingBox
    {
        public double MinX, MinY, MinZ, MaxX, MaxY, MaxZ;

        public double SizeX => MaxX - MinX;
        public double SizeY => MaxY - MinY;
        public double SizeZ => MaxZ - MinZ;
    }

    /// <summary>Forma da seção do blank padrão.</summary>
    public enum BlankShape
    {
        /// <summary>QUAD — quadrado (lado x lado).</summary>
        Square,
        /// <summary>RED — redondo/cilíndrico (diâmetro).</summary>
        Round,
        /// <summary>RET — retangular (largura x altura).</summary>
        Rectangular
    }

    /// <summary>Um blank padrão do catálogo da empresa.</summary>
    public sealed class BlankSpec
    {
        /// <summary>Código interno (ex.: 8840).</summary>
        public string Code { get; set; }
        public BlankShape Shape { get; set; }
        /// <summary>Lado (quadrado), diâmetro (redondo) ou largura (retangular), mm.</summary>
        public double DimA { get; set; }
        /// <summary>Altura (só retangular), mm. Null para quadrado/redondo.</summary>
        public double? DimB { get; set; }
        /// <summary>Material especial, ex.: "CuW80". Null = estoque padrão.</summary>
        public string Material { get; set; }

        public string Name
        {
            get
            {
                switch (Shape)
                {
                    case BlankShape.Square:      return $"QUAD. {Fmt(DimA)} x {Fmt(DimA)}";
                    case BlankShape.Round:       return $"RED. {Fmt(DimA)}";
                    case BlankShape.Rectangular: return $"RET. {Fmt(DimA)} x {Fmt(DimB ?? 0)}";
                    default:                     return "?";
                }
            }
        }

        public string Describe() =>
            $"{Name}{(Material != null ? " - " + Material : "")} (cód {Code})";

        /// <summary>Maior dimensão da seção (mm) — usada p/ preferir o blank mais COMPACTO
        /// (ex.: QUAD 19 em vez de RET 25×13 p/ um detalhe pequeno; assim os furos caem no
        /// 45° como o Carlos observou). Quadrado/redondo = DimA; retangular = maior lado.</summary>
        public double MaxDim
        {
            get
            {
                switch (Shape)
                {
                    case BlankShape.Rectangular: return Math.Max(DimA, DimB ?? DimA);
                    default:                     return DimA;
                }
            }
        }

        /// <summary>Área da seção transversal (mm²), critério de desempate.</summary>
        public double SectionArea
        {
            get
            {
                switch (Shape)
                {
                    case BlankShape.Square:      return DimA * DimA;
                    case BlankShape.Round:       return Math.PI / 4.0 * DimA * DimA;
                    case BlankShape.Rectangular: return DimA * (DimB ?? 0);
                    default:                     return double.MaxValue;
                }
            }
        }

        /// <summary>A seção comporta uma pegada needX x needY (mm)?</summary>
        public bool Fits(double needX, double needY)
        {
            switch (Shape)
            {
                case BlankShape.Square:
                    return DimA >= needX && DimA >= needY;
                case BlankShape.Round:
                    // A pegada precisa caber no círculo -> diagonal <= diâmetro.
                    return DimA >= Math.Sqrt(needX * needX + needY * needY);
                case BlankShape.Rectangular:
                    double h = DimB ?? 0;
                    return (DimA >= needX && h >= needY) || (DimA >= needY && h >= needX);
                default:
                    return false;
            }
        }

        private static string Fmt(double v) =>
            v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>Escolhe um blank padrão que comporta a pegada da queima + margem.</summary>
    public interface IBlankLibrary
    {
        /// <summary>
        /// Menor blank (por área de seção) que comporta (pegada + 2·margem) e é
        /// compatível com o material pedido. Null se nada servir.
        /// </summary>
        BlankSpec SelectBlank(BoundingBox burnBox, double marginPerSide, string material);

        /// <summary>
        /// TODOS os blanks que comportam (pegada + 2·margem) e são compatíveis com o
        /// material, do mais compacto ao maior. Alimenta o pop-up "escolher a base"
        /// (o 1º é o mesmo que <see cref="SelectBlank"/> devolveria). Lista vazia se
        /// nada servir.
        /// </summary>
        IReadOnlyList<BlankSpec> EligibleBlanks(BoundingBox burnBox, double marginPerSide, string material);
    }

    /// <summary>
    /// Catálogo real de blanks de COBRE da empresa, conferido contra os nomes de
    /// arquivo em W:\...\BLANKS ELETRODOS\COBRE (2026-07-08; OBSOLETO excluído).
    /// CuW80 = cobre-tungstênio (detalhes finos); demais são cobre padrão.
    ///
    /// ATENÇÃO: a pasta GRAFITE NÃO tem blanks dimensionados — só templates de base
    /// (EE_BASE_*). Ou seja, para eletrodo de grafite este catálogo NÃO se aplica
    /// (o blank de grafite é cortado de barra / parte de um template base). Regra a
    /// definir com o Carlos. Ver [[electrode-anatomy]].
    ///
    /// As barras podem ser cortadas em COMPRIMENTOS maiores e giradas para encaixar;
    /// se nada servir, compra-se material. O <see cref="BlankSpec.Fits"/> hoje só
    /// testa a seção nas duas orientações (giro), NÃO o corte em comprimento — regra
    /// do comprimento livre ainda a modelar.
    /// </summary>
    public sealed class StandardBlankLibrary : IBlankLibrary
    {
        private readonly List<BlankSpec> _catalog;

        public StandardBlankLibrary(IEnumerable<BlankSpec> catalog = null)
        {
            _catalog = (catalog ?? DefaultCatalog()).ToList();
        }

        public BlankSpec SelectBlank(BoundingBox burnBox, double marginPerSide, string material)
            => EligibleBlanks(burnBox, marginPerSide, material).FirstOrDefault();

        public IReadOnlyList<BlankSpec> EligibleBlanks(BoundingBox burnBox, double marginPerSide, string material)
        {
            double needX = burnBox.SizeX + 2 * marginPerSide;
            double needY = burnBox.SizeY + 2 * marginPerSide;

            return _catalog
                .Where(b => IsMaterialCompatible(b, material))
                .Where(b => b.Fits(needX, needY))
                .OrderBy(b => b.MaxDim)       // preferir o mais COMPACTO (quadrado p/ detalhe pequeno)
                .ThenBy(b => b.SectionArea)   // desempate por menor área
                .ToList();
        }

        /// <summary>
        /// Blank de material especial (ex.: CuW80) só é elegível se o material
        /// pedido corresponder. Estoque padrão (Material null) é sempre elegível.
        /// </summary>
        private static bool IsMaterialCompatible(BlankSpec b, string requested)
        {
            if (b.Material == null) return true;
            if (string.IsNullOrWhiteSpace(requested)) return false;
            return requested.IndexOf(b.Material, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static IEnumerable<BlankSpec> DefaultCatalog() => new[]
        {
            // Quadrados
            new BlankSpec { Code = "11715", Shape = BlankShape.Square, DimA = 19 },
            new BlankSpec { Code = "8840",  Shape = BlankShape.Square, DimA = 32 },
            new BlankSpec { Code = "11717", Shape = BlankShape.Square, DimA = 50 },
            // Redondos
            new BlankSpec { Code = "12343", Shape = BlankShape.Round, DimA = 6,  Material = "CuW80" },
            new BlankSpec { Code = "8835",  Shape = BlankShape.Round, DimA = 10 },
            new BlankSpec { Code = "12344", Shape = BlankShape.Round, DimA = 12, Material = "CuW80" },
            new BlankSpec { Code = "8836",  Shape = BlankShape.Round, DimA = 13 },
            new BlankSpec { Code = "8837",  Shape = BlankShape.Round, DimA = 16 },
            new BlankSpec { Code = "12345", Shape = BlankShape.Round, DimA = 16, Material = "CuW80" },
            new BlankSpec { Code = "11718", Shape = BlankShape.Round, DimA = 25 },
            new BlankSpec { Code = "9223",  Shape = BlankShape.Round, DimA = 38 },
            // Retangulares
            new BlankSpec { Code = "8842",  Shape = BlankShape.Rectangular, DimA = 25,  DimB = 13 },
            new BlankSpec { Code = "8843",  Shape = BlankShape.Rectangular, DimA = 38,  DimB = 16 },
            new BlankSpec { Code = "8844",  Shape = BlankShape.Rectangular, DimA = 50,  DimB = 19 },
            new BlankSpec { Code = "11721", Shape = BlankShape.Rectangular, DimA = 102, DimB = 25 },
        };
    }
}
