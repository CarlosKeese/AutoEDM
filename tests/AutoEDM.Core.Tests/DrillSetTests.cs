using AutoEDM.Machinability;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// O jogo do Carlos: DIN 338 de Ø1 a Ø19 mm (informado em 2026-09-11). O que estes testes
    /// travam não é a tabela da norma em si — é a REGRA que ela serve: furo fundo não é EDM.
    /// O caso que deu origem a tudo isto está em <see cref="OCasoDaFace73_Ø8A25mm"/>.
    /// </summary>
    public class DrillSetTests
    {
        private readonly DrillSet _set = DrillSet.Din338();

        [Fact]
        public void OCasoDaFace73_Ø8A25mm()
        {
            // 1º run ao vivo (log AutoEDM_20260911_150209): um Ø8 a 25 mm de profundidade saiu
            // como "só EDM" porque a fresa mais longa da ferramentaria para em 20 mm. É um furo
            // comum — a broca de Ø8 tem 75 mm de canal e sobra folga de 50 mm.
            Drill drill;
            Assert.True(_set.CanDrill(8.0, 25.0, out drill));
            Assert.Equal(8.0, drill.DiameterMm, 6);
            Assert.Equal(75.0, drill.FluteLengthMm, 6);
        }

        [Theory]
        [InlineData(1.0, 12.0)]
        [InlineData(3.0, 33.0)]
        [InlineData(6.0, 57.0)]
        [InlineData(10.0, 87.0)]
        [InlineData(19.0, 135.0)]
        public void CanalSegueATabelaDaNorma(double diameterMm, double expectedFluteMm)
        {
            Assert.Equal(expectedFluteMm, _set.For(diameterMm).FluteLengthMm, 6);
        }

        [Fact]
        public void DiametroIntermediario_UsaOTabeladoABAIXO_ErrandoParaOConservador()
        {
            // Ø7,5 existe na norma, mas não está na tabela: a consulta cai no Ø7 (canal 69 mm) em
            // vez do Ø8 (75 mm). Subestimar o alcance nunca aprova um furo que não dá; superestimar
            // aprovaria.
            Drill drill = _set.For(7.5);
            Assert.Equal(7.0, drill.DiameterMm, 6);
            Assert.Equal(69.0, drill.FluteLengthMm, 6);
        }

        [Fact]
        public void FuroMenorQueAMenorBroca_NaoTemBroca()
        {
            // Ø0,554 — o caso real das faces R0,277 do mesmo log. Nem broca nem fresa: é EDM.
            Drill drill;
            Assert.False(_set.CanDrill(0.554, 5.0, out drill));
            Assert.Null(_set.For(0.554));
        }

        [Fact]
        public void FuroMaiorQueAMaiorBroca_CaiNaBrocaMaior_QuemAbreEhAFresa()
        {
            // Ø25 não tem broca no jogo, mas isso não reprova nada: fura-se Ø19 e abre-se por
            // interpolação. A classe devolve a maior broca útil; quem julga a interpolação é a
            // ToolLadder, e para R12,5 ela tem fresa de sobra.
            Assert.Equal(19.0, _set.For(25.0).DiameterMm, 6);

            MillingTool tool;
            Assert.Equal(MachinabilityVerdict.Millable,
                ToolLadder.Shop().Classify(12.5, 10.0, out tool));
        }

        [Fact]
        public void FuroFundoDemaisParaOCanal_NaoPassa()
        {
            // Ø3 tem 33 mm de canal: 30 mm passa, 40 mm não.
            Drill drill;
            Assert.True(_set.CanDrill(3.0, 30.0, out drill));
            Assert.False(_set.CanDrill(3.0, 40.0, out drill));
        }

        [Fact]
        public void OCanalEncurtaEmRelacaoAoDiametroConformeABrocaEngrossa()
        {
            // Propriedade da norma que justifica a tabela existir em vez de uma razão fixa:
            // ~12x o diâmetro no Ø1, ~9x no Ø6, ~7x no Ø19.
            Assert.True(_set.For(1.0).FluteLengthMm / 1.0 > 11.0);
            Assert.True(_set.For(19.0).FluteLengthMm / 19.0 < 8.0);
        }

        [Fact]
        public void JogoCustomizado_MandaSobreANorma()
        {
            var shortSeries = new DrillSet(new[] { new Drill(8.0, 20.0) });
            Drill drill;
            Assert.False(shortSeries.CanDrill(8.0, 25.0, out drill)); // série curta não chega
        }
    }
}
