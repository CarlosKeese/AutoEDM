using AutoEDM.Electrode;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// O que a variável devolve, medido ao vivo em 2026-10-23 (SE 2023): Formula VAZIA para constante;
    /// Value em metros quando a variável é de distância (o Add antigo, sem UnitsType), cru quando escalar.
    /// </summary>
    public class RaVariableStoreTests
    {
        [Fact]
        public void TryDecode_LegacyDistanceVariable_ReadsValueAsTypedNumber()
        {
            Assert.True(RaVariableStore.TryDecode("", 0.0016, RaVariableStore.UnitDistance, out double ra));
            Assert.Equal(1.6, ra);
        }

        [Fact]
        public void TryDecode_ScalarVariable_ReadsValueRaw()
        {
            Assert.True(RaVariableStore.TryDecode("", 3.2, RaVariableStore.UnitScalar, out double ra));
            Assert.Equal(3.2, ra);
        }

        [Fact]
        public void TryDecode_FormulaWins_WhenPresent()
        {
            Assert.True(RaVariableStore.TryDecode("0.8", 999, RaVariableStore.UnitScalar, out double ra));
            Assert.Equal(0.8, ra);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        [InlineData(double.NaN)]
        public void TryDecode_NoValue_Fails(double value)
        {
            Assert.False(RaVariableStore.TryDecode("", value, RaVariableStore.UnitScalar, out _));
        }
    }
}
