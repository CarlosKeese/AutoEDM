using System.Linq;
using AutoEDM.Electrode;
using AutoEDM.Wedm;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// Extremidades paralelas ao plano XY de uma superfície: só o Z mínimo e o Z máximo viram
    /// curva; aresta que sobe em Z (parede) e nível horizontal intermediário ficam de fora.
    /// </summary>
    public class SurfaceRimsTests
    {
        private static OpenEdgeSegment Edge(double z0, double z1) => new OpenEdgeSegment
        {
            StartMm = new[] { 0.0, 0.0, z0 },
            EndMm = new[] { 10.0, 0.0, z1 },
            ZMinMm = z0 < z1 ? z0 : z1,
            ZMaxMm = z0 < z1 ? z1 : z0,
            IsVertical = z0 != z1,
        };

        [Fact]
        public void Pick_KeepsOnlyTheLowestAndHighestHorizontalLevels()
        {
            var pick = SurfaceRims.Pick(new[]
            {
                Edge(0, 0), Edge(0, 0),      // fundo
                Edge(5, 5),                  // degrau no meio — fora
                Edge(12.5, 12.5),            // topo
                Edge(0, 12.5),               // parede
            });

            Assert.Equal(3, pick.LevelCount);
            Assert.False(pick.Flat);
            Assert.Equal(new[] { "0.00", "12.50" }, pick.Rims.Select(r => r.Label));
            Assert.False(pick.Rims[0].IsTop);
            Assert.True(pick.Rims[1].IsTop);
            Assert.Equal(2, pick.Rims[0].Edges.Count);
            Assert.Equal(1, pick.EdgesDropped);
            Assert.Equal(1, pick.EdgesNotHorizontal);
        }

        [Fact]
        public void Pick_GroupsEdgesThatAreWithinTheLevelTolerance()
        {
            var pick = SurfaceRims.Pick(new[] { Edge(0, 0), Edge(0.004, 0.004), Edge(8, 8) });

            Assert.Equal(2, pick.LevelCount);
            Assert.Equal(2, pick.Rims[0].Edges.Count); // 0 e 0,004 são o mesmo rim
            Assert.Equal("0.00", pick.Rims[0].Label);
        }

        [Fact]
        public void Pick_FlatSurfaceGivesOneRimThatIsBothBottomAndTop()
        {
            var pick = SurfaceRims.Pick(new[] { Edge(-3.456, -3.456), Edge(-3.456, -3.456) });

            Assert.True(pick.Flat);
            Assert.Single(pick.Rims);
            Assert.Equal("-3.46", pick.Rims[0].Label);
            Assert.True(pick.Rims[0].IsTop);
            Assert.Equal(0, pick.EdgesDropped);
        }

        [Fact]
        public void Pick_SurfaceWithoutHorizontalEdgesGivesNoRim()
        {
            var pick = SurfaceRims.Pick(new[] { Edge(0, 10), Edge(2, 12) });

            Assert.Empty(pick.Rims);
            Assert.Equal(2, pick.EdgesNotHorizontal);
            Assert.Equal(0, pick.LevelCount);
        }
    }
}
