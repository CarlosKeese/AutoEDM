using AutoEDM.Electrode;
using AutoEDM.Model;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>Tabela confirmada pelo Carlos (ver [[autoedm-decisions]]): 0,2-0,8um->0,05mm;
    /// 1,6->0,10; 3,2->0,20; 6,3->0,30. Trava a tabela real do fluxo contra regressão.</summary>
    public class RaOffsetTablePolicyTests
    {
        private readonly RaOffsetTablePolicy _policy = new RaOffsetTablePolicy();

        [Theory]
        [InlineData(0.2, 0.05)]
        [InlineData(0.8, 0.05)]
        [InlineData(1.6, 0.10)]
        [InlineData(3.2, 0.20)]
        [InlineData(6.3, 0.30)]
        [InlineData(10.0, 0.30)] // acima da última faixa: usa a maior
        public void GetInwardOffsetMm_FollowsShopTable(double ra, double expectedOffset)
        {
            var pass = new ElectrodePass("ACAB", ra);
            Assert.Equal(expectedOffset, _policy.GetInwardOffsetMm(pass, "Cobre"), 6);
        }

        [Fact]
        public void GetInwardOffsetMm_OverrideWinsOverTable()
        {
            var pass = new ElectrodePass("ACAB", 0.8, offsetOverrideMm: 0.12);
            Assert.Equal(0.12, _policy.GetInwardOffsetMm(pass, "Cobre"), 6);
        }
    }
}
