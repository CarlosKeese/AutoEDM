using System.Collections.Generic;
using AutoEDM.Electrode;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>T3 do "Criar Eletrodos": quais faces recuam o GAP, reencontrar as faces depois da troca e medir a nervura.</summary>
    public class ElectrodeGapTests
    {
        private const double RefZ = 7.0, HalfX = 10.0, HalfY = 5.0;
        private static double[] P(double x, double y, double z) => new[] { x, y, z };

        [Fact]
        public void BurnFace_IsBelowTheClosing_AndNotACutOfThePrism()
        {
            // parede da cavidade: recua
            Assert.True(ElectrodeGap.IsBurnFace(true, P(2, -3, 1), P(2, 3, 6), RefZ, HalfX, HalfY));
            // raio (não plano) encostado na lateral: recua
            Assert.True(ElectrodeGap.IsBurnFace(false, P(9.8, -3, 1), P(10, 3, 1.2), RefZ, HalfX, HalfY));
            // lateral da pegada (x = +10) e divisa da T2c 0,002 mm para dentro (y = −4,998): não recuam
            Assert.False(ElectrodeGap.IsBurnFace(true, P(10, -5, 0), P(10, 5, 7), RefZ, HalfX, HalfY));
            Assert.False(ElectrodeGap.IsBurnFace(true, P(-10, -4.998, 0), P(10, -4.998, 7), RefZ, HalfX, HalfY));
            // pescoço e acima (a partir do fechamento): não recuam
            Assert.False(ElectrodeGap.IsBurnFace(true, P(2, -3, 7), P(2, 3, 8), RefZ, HalfX, HalfY));
        }

        [Fact]
        public void Match_FindsTheSameFacesByGeometry()
        {
            var burn = new List<FaceSig>
            {
                new FaceSig { Type = 1, Min = P(0, 0, 0), Max = P(1, 1, 0) },
                new FaceSig { Type = 2, Min = P(0, 0, 0), Max = P(0.2, 0.2, 3) }
            };
            var after = new List<FaceSig>
            {
                new FaceSig { Type = 9, Min = P(0, 0, 9), Max = P(1, 1, 9) },                  // bloco: não
                new FaceSig { Type = 2, Min = P(0, 0, 0.00005), Max = P(0.2, 0.2, 3) },        // mesmo raio, ruído de 0,05 µm
                new FaceSig { Type = 1, Min = P(0, 0, 0), Max = P(1, 1, 0) },
                new FaceSig { Type = 1, Min = P(0, 0, 0.01), Max = P(1, 1, 0.01) }             // outro plano: não
            };
            Assert.Equal(new[] { 1, 2 }, ElectrodeGap.Match(burn, after));
        }

        private static FaceSig Wall(double x, int nx, double y0 = -2, double y1 = 2, double z0 = 0, double z1 = 3) =>
            new FaceSig { Type = -1909484335, Min = P(x, y0, z0), Max = P(x, y1, z1), OutwardNormal = new[] { (double)nx, 0, 0 } };

        [Fact]
        public void NarrowestRib_PicksTwoOppositeWallsWithCopperBetween_AndFollowsItAfterTheGap()
        {
            // nervura de x 1 a 3 (normal +x em 3, −x em 1); outra de 5 a 9; e um rasgo (normais para dentro) que não conta
            var faces = new List<FaceSig> { Wall(3, +1), Wall(1, -1), Wall(9, +1), Wall(5, -1), Wall(12, -1), Wall(10, +1) };
            RibMeasure rib = ElectrodeGap.NarrowestRib(faces);
            Assert.Equal('X', rib.Axis);
            Assert.Equal(2.0, rib.Width, 9);

            var after = new List<FaceSig> { Wall(2.9, +1), Wall(1.1, -1), Wall(8.9, +1), Wall(5.1, -1) };
            RibMeasure same = ElectrodeGap.NarrowestRib(after, rib, 0.15);
            Assert.Equal(1.8, same.Width, 9);   // 2 × GAP de 0,1

            // paredes que não se sobrepõem em Y não formam nervura
            Assert.Null(ElectrodeGap.NarrowestRib(new List<FaceSig> { Wall(3, +1, 0, 1), Wall(1, -1, 2, 3) }));
        }
    }
}
