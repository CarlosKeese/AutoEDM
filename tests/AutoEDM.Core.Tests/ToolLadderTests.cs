using System.Linq;
using AutoEDM.Machinability;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// Trava a ferramentaria REAL do Carlos (informada em 2026-09-11) e as três perguntas que a
    /// escada responde. O teste que mais importa é o do ALCANCE: o pedido original partia de
    /// "Ø1 × 3 mm", e é a existência do longneck Ø1 × 10 que faz o teto a raio 0,5 mm ser 10 mm.
    /// Se alguém reescrever a escada e esse número cair para 3, a análise volta a condenar bolsão
    /// que se fresa hoje — e é isso que este arquivo existe para impedir.
    /// </summary>
    public class ToolLadderTests
    {
        private readonly ToolLadder _shop = ToolLadder.Shop();

        [Fact]
        public void Shop_TemAsDezoitoFresasDasDuasFamilias()
        {
            Assert.Equal(18, _shop.Tools.Count);
            Assert.Equal(9, _shop.Tools.Count(t => t.Shape == ToolShape.Flat));
            Assert.Equal(9, _shop.Tools.Count(t => t.Shape == ToolShape.Ball));
        }

        [Fact]
        public void MenorRaio_EhMeioMilimetro()
        {
            Assert.Equal(0.5, _shop.SmallestRadiusMm, 6);
        }

        [Theory]
        [InlineData(0.49, 0.0)]  // abaixo da Ø1: nenhuma fresa serve, em profundidade nenhuma
        [InlineData(0.50, 10.0)] // o longneck Ø1 × 10 — NÃO 3 mm
        [InlineData(0.75, 10.0)] // a Ø1,5 × 4 não ajuda: mais curta que o longneck
        [InlineData(1.00, 20.0)] // entra a esférica Ø2 × 20
        [InlineData(3.00, 20.0)] // acima de R1 o teto continua sendo o da Ø2 × 20
        public void Alcance_SegueOEnvelopeDaFerramentaria(double requiredRadiusMm, double expectedReachMm)
        {
            Assert.Equal(expectedReachMm, _shop.ReachMm(requiredRadiusMm), 6);
        }

        [Theory]
        // Topo reto: o longneck segura até R2, e só aí as fresas grossas passam dele.
        [InlineData(0.50, ToolShape.Flat, 10.0)]
        [InlineData(1.50, ToolShape.Flat, 10.0)]
        [InlineData(2.00, ToolShape.Flat, 11.0)]
        [InlineData(2.50, ToolShape.Flat, 12.0)]
        [InlineData(3.00, ToolShape.Flat, 15.0)]
        // Esférica: dois degraus e pronto.
        [InlineData(0.50, ToolShape.Ball, 10.0)]
        [InlineData(1.00, ToolShape.Ball, 20.0)]
        [InlineData(3.00, ToolShape.Ball, 20.0)]
        public void AlcancePorFamilia_NaoEhOCombinado(double requiredRadiusMm, ToolShape shape, double expectedReachMm)
        {
            Assert.Equal(expectedReachMm, _shop.ReachMm(requiredRadiusMm, shape), 6);
        }

        [Theory]
        [InlineData(0.30, 2.0, MachinabilityVerdict.BelowMinimumRadius)] // canto fechado demais
        [InlineData(0.50, 8.0, MachinabilityVerdict.Millable)]           // longneck alcança
        [InlineData(0.50, 12.0, MachinabilityVerdict.BeyondReach)]       // raio serve, profundidade não
        [InlineData(1.00, 18.0, MachinabilityVerdict.Millable)]          // esférica Ø2 × 20
        [InlineData(1.00, 25.0, MachinabilityVerdict.BeyondReach)]
        [InlineData(3.00, 5.0, MachinabilityVerdict.Millable)]
        public void Classify_SeparaRaioDeAlcance(double radiusMm, double depthMm, MachinabilityVerdict expected)
        {
            MillingTool tool;
            Assert.Equal(expected, _shop.Classify(radiusMm, depthMm, out tool));
            Assert.Equal(expected == MachinabilityVerdict.Millable, tool != null);
        }

        [Fact]
        public void Classify_RaioExatoDaFresaEhAceito()
        {
            // Um canto modelado em R0,5 tem de aceitar a fresa de R0,5: é o raio que ela produz.
            MillingTool tool;
            Assert.Equal(MachinabilityVerdict.Millable, _shop.Classify(0.5, 3.0, out tool));
            Assert.NotNull(tool);
        }

        [Fact]
        public void BestFor_PrefereAMaiorFresaQueAindaAlcanca()
        {
            // Quem pode usar a Ø4 não usa a Ø1: mais rígida, mais avanço, não quebra na peça.
            MillingTool tool = _shop.BestFor(2.0, 9.0);
            Assert.NotNull(tool);
            Assert.Equal(4.0, tool.DiameterMm, 6);
            Assert.Equal(ToolShape.Flat, tool.Shape);
        }

        [Fact]
        public void BestFor_SemAlcance_DevolveNulo()
        {
            Assert.Null(_shop.BestFor(0.5, 11.0)); // o longneck Ø1 para em 10 mm
        }

        [Fact]
        public void SeisFresasDecidem_ODemaisEhRigidezETempo()
        {
            var deciding = _shop.DecidingTools();
            Assert.Equal(6, deciding.Count);
            Assert.Equal(12, _shop.DominatedTools().Count);

            // Topo reto: o longneck e as três que passam dos 10 mm.
            Assert.Contains(deciding, t => t.Shape == ToolShape.Flat && t.DiameterMm == 1.0 && t.FluteLengthMm == 10.0);
            Assert.Contains(deciding, t => t.Shape == ToolShape.Flat && t.DiameterMm == 4.0);
            Assert.Contains(deciding, t => t.Shape == ToolShape.Flat && t.DiameterMm == 5.0);
            Assert.Contains(deciding, t => t.Shape == ToolShape.Flat && t.DiameterMm == 6.0);
            // Esférica: só os dois longnecks.
            Assert.Contains(deciding, t => t.Shape == ToolShape.Ball && t.DiameterMm == 1.0 && t.FluteLengthMm == 10.0);
            Assert.Contains(deciding, t => t.Shape == ToolShape.Ball && t.DiameterMm == 2.0 && t.FluteLengthMm == 20.0);
        }

        [Fact]
        public void AFresaDoPedidoOriginal_EhDominadaPeloLongneck()
        {
            // A Ø1 × 3 topo reto — a "menor ferramenta" do enunciado — serve o mesmo raio que o
            // longneck e vai 7 mm menos fundo. Ela não decide nada na análise geométrica.
            var dominated = _shop.DominatedTools();
            Assert.Contains(dominated, t => t.Shape == ToolShape.Flat && t.DiameterMm == 1.0 && t.FluteLengthMm == 3.0);
        }

        [Fact]
        public void EscadaCustomizada_MandaSobreADeFabrica()
        {
            // Uma oficina sem longneck: o teto a R0,5 volta a ser 3 mm.
            var ladder = new ToolLadder(new[]
            {
                new MillingTool(1.0, 3.0, ToolShape.Flat),
                new MillingTool(6.0, 15.0, ToolShape.Flat),
            });
            Assert.Equal(3.0, ladder.ReachMm(0.5), 6);

            MillingTool tool;
            Assert.Equal(MachinabilityVerdict.BeyondReach, ladder.Classify(0.5, 8.0, out tool));
        }

        [Fact]
        public void FuroFundo_NaoEhVereditoDaEscada_EhDeQuemChama()
        {
            // A escada só sabe de FRESA: um Ø8 (R4) a 25 mm continua sendo BeyondReach aqui.
            // Quem reclassifica como furo é a sonda, que enxerga a volta completa da face — este
            // teste trava a fronteira, para ninguém enfiar regra de broca dentro da escada.
            MillingTool tool;
            Assert.Equal(MachinabilityVerdict.BeyondReach, _shop.Classify(4.0, 25.0, out tool));
            Assert.Null(tool);

            // E o que o raio ACEITARIA, ignorando profundidade, é a maior fresa da casa.
            MillingTool wouldAccept = _shop.BestFor(4.0, 0);
            Assert.NotNull(wouldAccept);
            Assert.Equal(6.0, wouldAccept.DiameterMm, 6);
        }

        [Fact]
        public void OCasoRealDoPrimeiroRun_R0277A26Milimetros()
        {
            // Log AutoEDM_20260911_150209: duas faces de R0,277 mm a 26,4 mm de profundidade.
            // Abaixo do raio mínimo -> EDM, e nenhuma profundidade muda isso.
            MillingTool tool;
            Assert.Equal(MachinabilityVerdict.BelowMinimumRadius, _shop.Classify(0.277, 26.4, out tool));
            Assert.Equal(MachinabilityVerdict.BelowMinimumRadius, _shop.Classify(0.277, 0.5, out tool));
        }

        [Fact]
        public void Rotulo_SaiEmPortuguesComVirgulaDecimal()
        {
            var tool = new MillingTool(1.5, 4.0, ToolShape.Ball);
            var ptBr = new System.Globalization.CultureInfo("pt-BR");
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = ptBr;
                Assert.Equal("Ø1,5 × 4 mm esférica", tool.Label);
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = previous;
            }
        }
    }
}
