using System.Linq;
using AutoEDM.Mold.MoldBase;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// O catálogo Polimold de 2 placas (CATPM2P, jun/2013) transcrito em <see cref="PolimoldCatalog"/>. As
    /// relações R1–R3 não estão escritas no catálogo, mas fecham em todas as 53 linhas — se uma transcrição
    /// errar um número, é aqui que aparece.
    /// </summary>
    public class PolimoldCatalogTests
    {
        [Fact]
        public void Tem53Series_todasCompletas()
        {
            Assert.Equal(53, PolimoldCatalog.All.Count);
            foreach (PolimoldSeries s in PolimoldCatalog.All)
            {
                Assert.NotNull(s.Columns);
                Assert.NotNull(s.ReturnPins);
                Assert.Equal(3, s.Strokes.Count);
                Assert.NotEmpty(s.Thicknesses);
            }
        }

        [Fact]
        public void R1_alturaDoEspacador_eCursoMaisExtratorasMaisEncosto()
        {
            foreach (PolimoldSeries s in PolimoldCatalog.All)
                foreach (StrokeOption o in s.Strokes)
                    Assert.Equal(o.SpacerHeight, o.Stroke + s.CPE + s.PE + MoldBasePlanner.StopButtonHeight, 3);
        }

        [Fact]
        public void R2_e_R3_areaUtil()
        {
            foreach (PolimoldSeries s in PolimoldCatalog.All)
            {
                Assert.Equal(s.X, s.P - 2, 3);
                Assert.Equal(s.Y, s.EjectorGuides.AlongC - s.EjectorGuides.Diameter - 8, 3);
            }
        }

        [Fact]
        public void Extratora_cabeEntreOsEspacadores_eColunasSobreEles()
        {
            foreach (PolimoldSeries s in PolimoldCatalog.All)
            {
                double gap = (s.A - 2 * s.ESP - s.P) / 2;
                Assert.True(gap == 1 || gap == 2, s.Name + ": folga " + gap);
                Assert.InRange(s.Columns.AcrossA / 2, s.A / 2 - s.ESP, s.A / 2);
            }
        }

        [Theory]
        [InlineData("3035", 15, 46, 27)]   // "Porta Molde - 3035415125B.asm" do MD-15335
        [InlineData("3540", 6, 36, 96)]    // "Porta Molde - 3540406315B.asm"
        [InlineData("2535", 8, 36, 27)]    // exemplo do catálogo, p.4: 2535 2 08 3 2 5A
        public void SubSerie_decodificaOsCodigosReais(string code, int sub, double p1, double p2)
        {
            PolimoldSeries s = PolimoldCatalog.Find(code);
            Assert.NotNull(s);
            Assert.True(PolimoldCatalog.TryDecodeSubSeries(s, sub, out double a, out double b));
            Assert.Equal(p1, a);
            Assert.Equal(p2, b);
            Assert.Equal(sub, PolimoldCatalog.SubSeries(s, p1, p2));
        }

        [Fact]
        public void Find_aceitaNomeECodigo()
        {
            Assert.Same(PolimoldCatalog.Find("30.35"), PolimoldCatalog.Find("3035"));
            Assert.Equal("70.100", PolimoldCatalog.Find("70100").Name);
            Assert.Null(PolimoldCatalog.Find("99.99"));
        }

        [Fact]
        public void GuiasDaExtratora_sobreALinhaDeCentro_soNasPequenas()
        {
            string[] onCenter = PolimoldCatalog.All.Where(s => s.EjectorGuides.AcrossA == 0).Select(s => s.Name).ToArray();
            Assert.Equal(new[] { "15.15", "15.20", "15.25", "20.20" }, onCenter);
        }

        [Fact]
        public void Rosca_nominal()
        {
            Assert.Equal(12, PolimoldCatalog.ThreadNominal("M12"));
            Assert.Equal(12, PolimoldCatalog.Find("30.35").MoldScrews.Diameter);
            Assert.Equal("M12", PolimoldCatalog.Find("30.35").MoldScrews.Thread);
        }
    }
}
