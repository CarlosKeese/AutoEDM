using System.Collections.Generic;
using AutoEDM.Electrode;
using Xunit;

namespace AutoEDM.Core.Tests
{
    public class InterpolationGeometryTests
    {
        private static SectionEdge E(double x1, double z1, double x2, double z2) => new SectionEdge
        {
            StartMm = new[] { x1, 0.0, z1 },
            EndMm = new[] { x2, 0.0, z2 },
            MinMm = new[] { System.Math.Min(x1, x2), 0.0, System.Math.Min(z1, z2) },
            MaxMm = new[] { System.Math.Max(x1, x2), 0.0, System.Math.Max(z1, z2) },
        };

        [Fact]
        public void HorizontalAxis_IsTheNormalProjectedOnXY()
        {
            Assert.True(InterpolationGeometry.TryHorizontalAxis(new[] { 0.0, -3.0, 4.0 }, out double[] x));
            Assert.Equal(0.0, x[0], 9);
            Assert.Equal(-1.0, x[1], 9);
            Assert.Equal(0.0, x[2], 9);
        }

        [Fact]
        public void HorizontalAxis_RejectsNearlyHorizontalFace()
        {
            Assert.False(InterpolationGeometry.TryHorizontalAxis(new[] { 0.1, 0.0, 1.0 }, out _));
        }

        [Fact]
        public void Rotation_HasXAlongNormal_AndZUp()
        {
            double[] r = InterpolationGeometry.RotationRows(new[] { 0.0, 1.0, 0.0 });
            // colunas: X = (0,1,0), Y = Z × X = (-1,0,0), Z = (0,0,1)
            Assert.Equal(new[] { 0.0, -1.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0 }, r);
        }

        [Fact]
        public void Axis_SitsHalfDiameterFromDeepestPoint()
        {
            Assert.Equal(7.0, InterpolationGeometry.AxisXmm(-3.0, 20.0), 9);
        }

        [Fact]
        public void Diameter_MustKeepProfileOnOneSideOfAxis()
        {
            Assert.Null(InterpolationGeometry.CheckDiameter(-3.0, 0.0, 20.0));
            Assert.NotNull(InterpolationGeometry.CheckDiameter(-3.0, 0.0, 6.0));
            Assert.NotNull(InterpolationGeometry.CheckDiameter(-3.0, 0.0, 0.0));
        }

        [Fact]
        public void Chains_SeparateBothSidesOfABore_AndNearestWins()
        {
            // rebaixo retangular dos dois lados de um furo, cortado por um plano que passa pelo eixo
            var edges = new List<SectionEdge>
            {
                E(0, 10, -2, 10), E(-2, 10, -2, 4), E(-2, 4, 0, 4),          // lado da face escolhida
                E(40, 10, 42, 10), E(42, 10, 42, 4), E(42, 4, 40, 4),        // lado oposto
            };
            List<List<int>> chains = InterpolationGeometry.Chains(edges);
            Assert.Equal(2, chains.Count);

            List<int> near = InterpolationGeometry.NearestChain(chains, edges, new[] { -2.0, 0.0, 7.0 });
            Assert.Equal(new[] { 0, 1, 2 }, near.ToArray());

            Assert.True(InterpolationGeometry.TryOpenEnds(edges, near, out double[] top, out double[] bottom));
            Assert.Equal(10.0, top[2], 9);
            Assert.Equal(4.0, bottom[2], 9);

            double[][] box = InterpolationGeometry.ChainBox(edges, near);
            Assert.Equal(-2.0, box[0][0], 9);
            Assert.Equal(0.0, box[1][0], 9);
        }

        [Fact]
        public void OpenEnds_RefuseClosedLoop()
        {
            var edges = new List<SectionEdge> { E(0, 0, 1, 0), E(1, 0, 1, 1), E(1, 1, 0, 1), E(0, 1, 0, 0) };
            Assert.False(InterpolationGeometry.TryOpenEnds(edges, new[] { 0, 1, 2, 3 }, out _, out _));
        }

        [Fact]
        public void Closing_GoesHorizontallyToAxisAndAlongIt()
        {
            double[][] c = InterpolationGeometry.ClosingPoints(new[] { -7.0, 0.0, 10.0 }, new[] { -7.0, 0.0, 4.0 });
            Assert.Equal(4, c.Length);
            Assert.Equal(new[] { 0.0, 0.0, 10.0 }, c[1]);
            Assert.Equal(new[] { 0.0, 0.0, 4.0 }, c[2]);
        }
    }
}
