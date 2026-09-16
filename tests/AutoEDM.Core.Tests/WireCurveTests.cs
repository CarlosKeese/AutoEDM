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

        // --- é arco circular? (o raio de canto que o SE devolve como "elipse") ---

        private static double[] OnCircle(double[] center, double r, double degrees)
        {
            double t = degrees * Math.PI / 180.0;
            return new[] { center[0] + r * Math.Cos(t), center[1] + r * Math.Sin(t), center[2] };
        }

        /// <summary>Amostras como o leitor monta: as duas pontas + 1/4, 1/2 e 3/4 da aresta.</summary>
        private static double[][] ArcSamples(double[] center, double r, double fromDeg, double toDeg)
        {
            double[] At(double f) => OnCircle(center, r, fromDeg + (toDeg - fromDeg) * f);
            return new[] { At(0), At(1), At(0.25), At(0.5), At(0.75) };
        }

        [Fact]
        public void CircularRadius_OfAQuarterRound_IsTheMeasuredRadius()
        {
            var c = new double[] { 10, 5, 23 };
            double r = WireCurve.TryCircularRadiusMm(c, ArcSamples(c, 2.5, 0, 90), 0.001, out double dev);
            Assert.Equal(2.5, r, 6);
            Assert.True(dev < 1e-9);
        }

        [Fact]
        public void CircularRadius_AcceptsTheRatioTheSolidEdgeReportsForACornerRound()
        {
            // O SE devolve MinorMajorRatio 0,9999 em raio de canto (log 112004). Num raio de
            // 2,5 mm isso é meio mícron de diferença: é círculo para o fio.
            var c = new double[] { 0, 0, 0 };
            var samples = ArcSamples(c, 2.5, 0, 90);
            foreach (double[] p in samples) p[1] *= 0.9999;   // achata o semieixo Y

            double r = WireCurve.TryCircularRadiusMm(c, samples, 0.001, out double dev);
            Assert.False(double.IsNaN(r));
            Assert.True(dev < 0.001, $"desvio {dev} mm");
        }

        [Fact]
        public void CircularRadius_RejectsARealEllipse_EvenWithEndsAtTheSameRadius()
        {
            // Pontas simétricas (mesmo raio), mas os pontos de dentro caem no semieixo MENOR.
            var c = new double[] { 0, 0, 0 };
            var samples = new[]
            {
                new double[] { 4 * Math.Cos(Math.PI / 4), 2 * Math.Sin(Math.PI / 4), 0 },
                new double[] { -4 * Math.Cos(Math.PI / 4), 2 * Math.Sin(Math.PI / 4), 0 },
                new double[] { 2, 1.73, 0 }, new double[] { 0, 2, 0 }, new double[] { -2, 1.73, 0 },
            };
            double r = WireCurve.TryCircularRadiusMm(c, samples, 0.001, out double dev);
            Assert.True(double.IsNaN(r));
            Assert.True(dev > 0.5, $"desvio {dev} mm");
        }

        [Fact]
        public void CircularRadius_IgnoresZ_TheArcIsMeasuredInXY()
        {
            var c = new double[] { 0, 0, 0 };
            var samples = ArcSamples(c, 3, 0, 180);
            foreach (double[] p in samples) p[2] = 12;

            double r = WireCurve.TryCircularRadiusMm(c, samples, 0.001, out double _);
            Assert.Equal(3, r, 6);
        }

        [Fact]
        public void CircularRadius_RejectsWhenOneSampleIsOffTheCircle()
        {
            var c = new double[] { 0, 0, 0 };
            var samples = ArcSamples(c, 3, 0, 90);
            samples[3] = new double[] { samples[3][0] * 1.01, samples[3][1] * 1.01, 0 };

            Assert.True(double.IsNaN(WireCurve.TryCircularRadiusMm(c, samples, 0.001, out double _)));
        }
    }
}
