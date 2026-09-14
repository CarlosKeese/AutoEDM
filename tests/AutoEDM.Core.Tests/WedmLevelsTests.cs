using System.Linq;
using AutoEDM.Wedm;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>Níveis de corte WEDM: um arquivo por Z, nome "peça Z = XX.XX.igs".</summary>
    public class WedmLevelsTests
    {
        private static WireCurve Line(double z0, double z1) =>
            WireCurve.Line(new[] { 0, 0, z0 }, new[] { 10, 0, z1 });

        [Fact]
        public void Group_SplitsByZ_AndSetsAsideNonHorizontalCurves()
        {
            var curves = new[] { Line(12.5, 12.5), Line(0, 0), Line(0.0004, 0.0004), Line(0, 5) };
            var levels = WedmLevels.Group(curves, out var notHorizontal);

            Assert.Equal(2, levels.Count);
            Assert.Equal("0.00", levels[0].Label);   // crescente
            Assert.Equal(2, levels[0].Curves.Count); // 0 e 0,0004 são o mesmo nível
            Assert.Equal("12.50", levels[1].Label);
            Assert.Single(notHorizontal);
        }

        [Fact]
        public void Group_KeepsLevelsThatAreApartMoreThanTheTolerance()
        {
            var levels = WedmLevels.Group(new[] { Line(10, 10), Line(10.02, 10.02) }, out _);
            Assert.Equal(new[] { "10.00", "10.02" }, levels.Select(l => l.Label));
        }

        [Fact]
        public void FileName_UsesPartNameAndTwoDecimals()
        {
            Assert.Equal("PUNCAO 01 Z = 12.50.igs", WedmLevels.FileName(@"W:\Moldes\PUNCAO 01.par", 12.5));
            Assert.Equal("PUNCAO 01 Z = -3.46.igs", WedmLevels.FileName("PUNCAO 01.par", -3.456));
        }

        [Fact]
        public void FormatZ_NeverWritesNegativeZero()
        {
            Assert.Equal("0.00", WedmLevels.FormatZ(-0.001));
        }
    }
}
