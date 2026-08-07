using AutoEDM.Model;
using Xunit;

namespace AutoEDM.Core.Tests
{
    public class UnitsTests
    {
        [Theory]
        [InlineData(1, 0.001)]
        [InlineData(0.3, 0.0003)]
        [InlineData(-0.05, -0.00005)]
        public void MmToM_ConvertsCorrectly(double mm, double expectedM)
        {
            Assert.Equal(expectedM, Units.MmToM(mm), 9);
        }

        [Theory]
        [InlineData(0.001, 1)]
        [InlineData(0.0003, 0.3)]
        public void MToMm_ConvertsCorrectly(double m, double expectedMm)
        {
            Assert.Equal(expectedMm, Units.MToMm(m), 9);
        }

        [Fact]
        public void MmToM_And_MToMm_AreInverses()
        {
            double mm = 12.7;
            Assert.Equal(mm, Units.MToMm(Units.MmToM(mm)), 9);
        }
    }
}
