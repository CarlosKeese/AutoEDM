using System.Linq;
using AutoEDM.Drawing;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// Regras puras do "Des. 2D" › Chaparia, sem Solid Edge: a escala do A3 e a ordem dos cliques das cotas
    /// entre linhas paralelas (medidas reais da 502.02 e da 501.02 do SaltSpray, 2026-11-12).
    /// </summary>
    public class SheetMetalDrawingRulesTests
    {
        private const double A3W = 0.420, A3H = 0.297;

        private static SheetLine L(double x1, double y1, double x2, double y2) => new SheetLine { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2 };

        [Fact]
        public void Escala_502_02_e_meio()
        {
            // planificada 192,43 × 30; dobrada 118,4 × 41,8 × 30 → 1:2 (o que o desenho validado usou)
            Assert.Equal(0.5, SheetMetalDrawingRules.ChooseScale(0.19243, 0.030, 0.1184, 0.0418, A3W, A3H));
        }

        [Fact]
        public void Escala_peca_pequena_sobe()
        {
            // 502.03: 30 × 20 → cabe em 2:1 (5:1 estoura a altura da planificada)
            Assert.Equal(2, SheetMetalDrawingRules.ChooseScale(0.030, 0.020, 0.030, 0.020, A3W, A3H));
        }

        [Fact]
        public void Escala_chapa_grande_desce_ate_caber()
        {
            // 801.00: 730 × 680 → nenhuma escala cabe nos 22% de altura do A3; fica a menor da lista (1:10)
            Assert.Equal(0.1, SheetMetalDrawingRules.ChooseScale(0.730, 0.680, 0.730, 0.680, A3W, A3H));
        }

        [Fact]
        public void Linhas_verticais_clicadas_na_mesma_altura()
        {
            // ponta da planificada (x = 82,1 mm) e linha de dobra (x = 112,9 mm), ambas de 208,8 a 218,8 mm
            var ponta = L(0.0821, 0.2088, 0.0821, 0.2188);
            var dobra = L(0.1129, 0.2213, 0.1129, 0.2063);
            DimPick p = SheetMetalDrawingRules.PickOrder(ponta, dobra).First();
            Assert.Equal(p.AY, p.BY, 9);
            Assert.InRange(p.AY, 0.2088, 0.2188);
            Assert.False(p.AKey || p.BKey);
        }

        [Fact]
        public void Linhas_horizontais_clicadas_na_mesma_abscissa_da_sobreposicao()
        {
            var baixo = L(0.040, 0.10, 0.100, 0.10);
            var cima = L(0.060, 0.14, 0.120, 0.14);
            DimPick p = SheetMetalDrawingRules.PickOrder(baixo, cima).First();
            Assert.Equal(p.AX, p.BX, 9);
            Assert.InRange(p.AX, 0.060, 0.100);
        }

        [Fact]
        public void Reserva_linha_ate_ponto_nunca_ponto_com_ponto()
        {
            var a = L(0, 0, 0, 0.01);
            var b = L(0.05, 0, 0.05, 0.01);
            var pares = SheetMetalDrawingRules.PickOrder(a, b);
            Assert.True(pares.Count >= 5);
            Assert.DoesNotContain(pares, p => p.AKey && p.BKey);
            Assert.Contains(pares, p => p.BKey && !p.AKey);
            Assert.Contains(pares, p => p.AKey && !p.BKey);
        }

        [Fact]
        public void Abas_em_U_nao_se_sobrepoem_a_base()
        {
            // topo das abas fora do vão da base: a altura vai para a lateral
            var base_ = L(0.050, 0.10, 0.090, 0.10);
            var aba = L(0.030, 0.14, 0.048, 0.14);
            Assert.False(SheetMetalDrawingRules.OverlapAcross(base_, aba));
            Assert.True(SheetMetalDrawingRules.OverlapAcross(base_, L(0.060, 0.14, 0.080, 0.14)));
        }
    }
}
