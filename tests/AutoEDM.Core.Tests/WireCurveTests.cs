using System;
using AutoEDM.Wedm;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>Curvas de perfil WEDM: arco normalizado anti-horário e avaliação de B-spline.</summary>
    public class WireCurveTests
    {
        private static readonly double[] Center = { 0, 0, 5 };
        private const double S = 7.0710678118654755;

        [Fact]
        public void Arc_KeepsEndsWhenMidIsOnTheCounterClockwisePath()
        {
            WireCurve arc = WireCurve.HorizontalArc(Center, 10, new double[] { 10, 0, 5 }, new double[] { 0, 10, 5 }, new[] { S, S, 5 });
            Assert.Equal(new double[] { 10, 0, 5 }, arc.Start);
            Assert.Equal(new double[] { 0, 10, 5 }, arc.End);
            Assert.False(arc.FullCircle);
        }

        [Fact]
        public void Arc_SwapsEndsWhenItRunsClockwise()
        {
            // O arco de 270° que passa por 225°: de (10,0) até (0,10) só dá para passar lá indo no sentido horário.
            WireCurve arc = WireCurve.HorizontalArc(Center, 10, new double[] { 10, 0, 5 }, new double[] { 0, 10, 5 }, new[] { -S, -S, 5 });
            Assert.Equal(new double[] { 0, 10, 5 }, arc.Start);
            Assert.Equal(new double[] { 10, 0, 5 }, arc.End);
        }

        [Fact]
        public void Arc_CoincidentEndsIsFullCircle()
        {
            WireCurve arc = WireCurve.HorizontalArc(Center, 10, new double[] { 10, 0, 5 }, new double[] { 10, 0, 5 }, new double[] { 10, 0, 5 });
            Assert.True(arc.FullCircle);
            Assert.Equal(arc.Start, arc.End);
        }

        [Fact]
        public void BSpline_Degree1_EvaluatesAlongThePolygon()
        {
            WireCurve sp = WireCurve.BSpline(1, new double[] { 0, 0, 1, 2, 2 }, null,
                new[] { new double[] { 0, 0, 0 }, new double[] { 10, 0, 0 }, new double[] { 10, 10, 0 } }, 0, 2);
            Assert.Equal(new double[] { 5, 0, 0 }, sp.Evaluate(0.5));
            Assert.Equal(new double[] { 10, 5, 0 }, sp.Evaluate(1.5));
            Assert.Equal(new double[] { 10, 10, 0 }, sp.Evaluate(2));
        }

        [Fact]
        public void BSpline_RationalQuarterCircle_StaysOnTheCircle()
        {
            WireCurve sp = WireCurve.BSpline(2, new double[] { 0, 0, 0, 1, 1, 1 }, new[] { 1, Math.Sqrt(0.5), 1 },
                new[] { new double[] { 10, 0, 0 }, new double[] { 10, 10, 0 }, new double[] { 0, 10, 0 } }, 0, 1);
            foreach (double t in new[] { 0.0, 0.1, 0.37, 0.5, 0.9, 1.0 })
            {
                double[] p = sp.Evaluate(t);
                Assert.Equal(10.0, Math.Sqrt(p[0] * p[0] + p[1] * p[1]), 9);
            }
        }

        [Fact]
        public void BSpline_WrongKnotCount_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => WireCurve.BSpline(2, new double[] { 0, 0, 1, 1 }, null,
                new[] { new double[] { 0, 0, 0 }, new double[] { 1, 0, 0 }, new double[] { 2, 0, 0 } }, 0, 1));
        }

        [Fact]
        public void ZRange_OfSplineComesFromPoles()
        {
            WireCurve sp = WireCurve.BSpline(1, new double[] { 0, 0, 1, 1 }, null,
                new[] { new double[] { 0, 0, 3 }, new double[] { 10, 0, 3.5 } }, 0, 1);
            sp.GetZRange(out double min, out double max);
            Assert.Equal(3, min);
            Assert.Equal(3.5, max);
        }
    }
}
