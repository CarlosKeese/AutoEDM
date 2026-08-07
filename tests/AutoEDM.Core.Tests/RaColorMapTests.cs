using System.Drawing;
using AutoEDM.Electrode;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>Cores confirmadas pelo Carlos (ver [[autoedm-decisions]]): RGB x255. Trava o
    /// mapa real de detecção de queima contra regressão.</summary>
    public class RaColorMapTests
    {
        private readonly RaColorMap _map = new RaColorMap();

        [Theory]
        [InlineData(0, 128, 255, 6.3)]   // azul
        [InlineData(255, 89, 99, 3.2)]   // vermelho
        [InlineData(128, 64, 0, 1.6)]    // marrom
        [InlineData(59, 128, 0, 0.8)]    // verde
        [InlineData(255, 255, 128, 0.1)] // amarelo
        public void TryGetRa_MatchesKnownColors(int r, int g, int b, double expectedRa)
        {
            bool ok = _map.TryGetRa(Color.FromArgb(r, g, b), out double ra, out _);
            Assert.True(ok);
            Assert.Equal(expectedRa, ra);
        }

        [Fact]
        public void TryGetRa_ReturnsFalse_ForUnmappedColor()
        {
            bool ok = _map.TryGetRa(Color.FromArgb(10, 200, 10), out _, out _);
            Assert.False(ok);
        }

        [Theory]
        [InlineData(0.1, 0.8)]
        [InlineData(0.8, 1.6)]
        [InlineData(6.3, 6.3)] // topo da escada: sem "mais grosso", mantém o mesmo
        public void RoughingRaFor_PicksNextCoarserBand(double finishRa, double expectedRoughingRa)
        {
            Assert.Equal(expectedRoughingRa, _map.RoughingRaFor(finishRa));
        }
    }
}
