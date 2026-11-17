using AutoEDM.Electrode;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// T2b: o furo da fixação pode entrar no pescoço e na ponta, mas não pode estourar. Números medidos no eletrodo
    /// unido do 15428.100 (2026-11-13) com o M6 forçado a 16 mm: estourou por baixo e foi encurtado até 14,5 mm.
    /// </summary>
    public class HoleIntegrityTests
    {
        private static HoleMeasure Measure(double cone, int cones, params double[][] walls)
        {
            var m = new HoleMeasure { ConeAreaMm2 = cone, Cones = cones };
            m.Walls.AddRange(walls);
            return m;
        }

        [Fact]
        public void TheoreticalCone_MatchesWhatTheSolidMeasures()
        {
            Assert.Equal(22.907, HoleIntegrity.ConeAreaMm2(2.5, 118.0), 3);   // M6, broca Ø5
            Assert.Equal(14.660, HoleIntegrity.ConeAreaMm2(2.0, 118.0), 3);   // pino Ø4
        }

        [Fact]
        public void IntactHole_Passes()
        {
            // M6 com 10 mm: cone 22,907 e parede Ø5 cheia de Z 9 a 18,5.
            Assert.True(HoleIntegrity.IsIntact(Measure(22.907, 1, new[] { 149.23, 2.5, 9.0, 18.5 }), 2.5, 118.0, out _));
        }

        [Fact]
        public void TipOutOfTheCopper_IsBroken()
        {
            Assert.False(HoleIntegrity.IsIntact(Measure(5.06, 1, new[] { 243.47, 2.5, 3.0, 18.5 }), 2.5, 118.0, out string why));
            Assert.Contains("ponta da broca", why);
            Assert.False(HoleIntegrity.IsIntact(Measure(0, 0), 2.5, 118.0, out _));
        }

        [Fact]
        public void WindowInTheWall_IsBroken()
        {
            // Parede Ø4 de 6,6 mm de altura com 10 % da área faltando: o furo saiu pelo lado.
            double full = 2 * System.Math.PI * 2.0 * 6.6;
            Assert.False(HoleIntegrity.IsIntact(Measure(14.66, 1, new[] { 0.9 * full, 2.0, 12.0, 18.6 }), 2.0, 118.0, out string why));
            Assert.Contains("parede", why);
        }

        [Fact]
        public void Shortening_StepsDownToTheMinimum_ThenGivesUp()
        {
            // O caso medido: 16 → 15,5 → 15 → 14,5 (inteiro). Abaixo do mínimo (rosca 6 + 0,5) não há próxima.
            Assert.Equal(15.5, HoleIntegrity.Shorten(16.0, 0.5, 6.5));
            Assert.Equal(6.5, HoleIntegrity.Shorten(7.0, 0.5, 6.5));
            Assert.Null(HoleIntegrity.Shorten(6.5, 0.5, 6.5));
        }
    }
}
