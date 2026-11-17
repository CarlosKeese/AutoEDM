using System.Collections.Generic;
using System.Linq;
using AutoEDM.Electrode;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// T5 do "Criar Eletrodos": separar por REGIÃO. Lógica pura sobre faces sintéticas (mm, referencial do eletrodo).
    /// Pegada inteira x −20…20, y −20…20; fechamento em Z 7.
    /// </summary>
    public class RegionFinderTests
    {
        private const int Plane = -1909484335, Cyl = -114972029;
        private const double RefZ = 7.0;
        private static readonly BoundingBox Foot = new BoundingBox { MinX = -20, MaxX = 20, MinY = -20, MaxY = 20, MinZ = 0, MaxZ = RefZ };

        private static FaceRec F(int type, double x0, double x1, double y0, double y1, double z0, double z1, double area, int[] edges,
            double[] n = null, bool finding = false, double r = double.NaN, bool? convex = null) =>
            new FaceRec { Type = type, Min = new[] { x0, y0, z0 }, Max = new[] { x1, y1, z1 }, AreaMm2 = area, Edges = edges, Normal = n, Finding = finding, R = r, Convex = convex };

        /// <summary>Um raio côncavo R0,2 que a fresa não faz, ao longo de Y em x = <paramref name="x"/>, com as duas paredes vizinhas.</summary>
        private static List<FaceRec> Fillet(int e0, double x, double y0, double y1)
        {
            return new List<FaceRec>
            {
                F(Cyl, x, x + 0.2, y0, y1, 1, 1.2, 0.3 * (y1 - y0), new[] { e0, e0 + 1 }, finding: true, r: 0.2, convex: false),
                F(Plane, x + 0.2, x + 0.2, y0, y1, 1.2, 3, 1.8 * (y1 - y0), new[] { e0, e0 + 2 }, new[] { -1.0, 0, 0 }),
                F(Plane, x - 1, x, y0, y1, 1, 1, 1.0 * (y1 - y0), new[] { e0 + 1, e0 + 3 }, new[] { 0, 0, -1.0 })
            };
        }

        [Fact]
        public void TwoFindingsTouchingTheSameSmallFace_AreOneRegion_FarOnesAreTwo()
        {
            var f = Fillet(100, 0, 0, 5);
            f.AddRange(Fillet(200, 0, 5, 9));
            f[4].Edges = f[4].Edges.Concat(new[] { 100 }).ToArray();   // o 2º raio encosta no 1º
            List<EdmRegion> one = RegionFinder.Find(f, RefZ, Foot);
            Assert.Single(one);
            Assert.Equal(2, one[0].Radii);

            var g = Fillet(100, 0, 0, 5);
            g.AddRange(Fillet(200, 12, 0, 5));
            Assert.Equal(2, RegionFinder.Find(g, RefZ, Foot).Count);
        }

        [Fact]
        public void BigFloorBetweenFindings_DoesNotJoinThem()
        {
            var f = Fillet(100, 0, 0, 5);
            f.AddRange(Fillet(200, 8, 0, 5));
            // um piso grande (40 × 20) vizinho dos dois raios
            f.Add(F(Plane, -20, 20, -10, 10, 1, 1, 800, new[] { 101, 201 }, new[] { 0, 0, -1.0 }));
            List<EdmRegion> regs = RegionFinder.Find(f, RefZ, Foot);
            Assert.Equal(2, regs.Count);
            Assert.All(regs, r => Assert.True(r.Box.MaxX - r.Box.MinX < 3));
        }

        [Fact]
        public void TabAgainstAWall_IsFoundByTheJudge_AndTheWallIsNotPartOfTheRegion()
        {
            // Ressalto (como o B1 do 15428.100): x −20…−17, y 0…6, z 6…7, preso na parede x = −17 que corre de y −5 a 11.
            var f = new List<FaceRec>
            {
                F(Plane, -20, -17, 0, 0, 6, 7, 3, new[] { 1, 3 }, new[] { 0, -1.0, 0 }),          // lado −y do ressalto
                F(Plane, -20, -17, 6, 6, 6, 7, 3, new[] { 2, 4 }, new[] { 0, 1.0, 0 }),           // lado +y
                F(Plane, -20, -17, 0, 6, 6, 6, 18, new[] { 3, 4, 5 }, new[] { 0, 0, -1.0 }),     // fundo do ressalto
                F(Plane, -17, -17, -5, 11, 2, 7, 80, new[] { 1, 2, 5, 6 }, new[] { -1.0, 0, 0 }) // parede (grande)
            };
            // as duas arestas verticais ressalto × parede, com 3/4 de material em volta (canto de 90°)
            var edges = new List<EdgeSample>
            {
                new EdgeSample { Id = 1, FaceA = 0, FaceB = 3, Mid = new[] { -17.0, 0, 6.5 }, Tangent = new[] { 0, 0, 1.0 }, LengthMm = 1, MaterialFraction = 0.75 },
                new EdgeSample { Id = 2, FaceA = 1, FaceB = 3, Mid = new[] { -17.0, 6, 6.5 }, Tangent = new[] { 0, 0, 1.0 }, LengthMm = 1, MaterialFraction = 0.75 }
            };
            var corners = RegionFinder.CornersOf(f, MillJudge.Judge(f, edges, RefZ));
            Assert.Equal(2, corners.Count);
            Assert.All(corners, c => Assert.Equal(3, c.big));
            EdmRegion r = Assert.Single(RegionFinder.Find(f, RefZ, Foot, null, corners));
            Assert.Equal(2, r.Corners);
            Assert.DoesNotContain(3, r.Faces);
            Assert.Equal(0.0, r.Box.MinY, 6);
            Assert.Equal(6.0, r.Box.MaxY, 6);
        }

        [Fact]
        public void ConvexEdgeBetweenTwoWalls_IsNotACorner()
        {
            // quina externa de um ressalto: as duas paredes estão uma do lado de DENTRO da outra
            var f = new List<FaceRec>
            {
                F(Plane, 0, 0, 0, 5, 1, 4, 15, new[] { 1 }, new[] { 1.0, 0, 0 }),
                F(Plane, -5, 0, 5, 5, 1, 4, 15, new[] { 1 }, new[] { 0, 1.0, 0 })
            };
            var edges = new List<EdgeSample> { new EdgeSample { Id = 1, FaceA = 0, FaceB = 1, Mid = new[] { 0, 5.0, 2.5 }, Tangent = new[] { 0, 0, 1.0 }, LengthMm = 3, MaterialFraction = 0.25 } };
            Assert.Empty(RegionFinder.CornersOf(f, MillJudge.Judge(f, edges, RefZ)));
        }

        [Fact]
        public void Footprint_HasMinimumWidth_AndSnapsToTheBorderInsteadOfLeavingAThinWall()
        {
            var o = new RegionOptions { MarginMm = 0.25, MinWidthMm = 2 };
            BoundingBox box = new BoundingBox { MinX = -18.9, MaxX = -18.7, MinY = 0, MaxY = 6 };
            BoundingBox fp = RegionFinder.FootprintOf(box, Foot, o);
            Assert.Equal(-20, fp.MinX, 6);           // a 1,1 mm da borda: encosta nela
            Assert.True(fp.MaxX - fp.MinX >= 2 - 1e-9);
            Assert.Equal(-0.25, fp.MinY, 6);
            Assert.Equal(6.25, fp.MaxY, 6);
        }

        [Fact]
        public void ThinWallBetweenTwoFootprints_IsClosedByTheSmallerOne()
        {
            var regs = new List<EdmRegion>
            {
                new EdmRegion { Footprint = new BoundingBox { MinX = 0, MaxX = 2, MinY = 0, MaxY = 10 } },
                new EdmRegion { Footprint = new BoundingBox { MinX = 3, MaxX = 13, MinY = 0, MaxY = 10 } }
            };
            RegionFinder.CloseThinGaps(regs, Foot, new RegionOptions { MinWidthMm = 2 });
            Assert.Equal(3.0, regs[0].Footprint.MaxX, 6);
            Assert.Equal(3.0, regs[1].Footprint.MinX, 6);
        }

        [Fact]
        public void Compare_RotationIsTheSameElectrode_MirrorIsAnother()
        {
            // região assimétrica: um raio comprido e uma parede curta deslocada
            List<FaceRec> f = new List<FaceRec>
            {
                F(Cyl, 0, 4, 0, 0.2, 1, 1.2, 1.2, new[] { 1 }, finding: true, r: 0.2, convex: false),
                F(Plane, 0, 1, 0.2, 0.2, 1.2, 3, 1.8, new[] { 1 }, new[] { 0, -1.0, 0 }),
                // a mesma coisa girada 180° em volta de (10, 10)
                F(Cyl, 16, 20, 19.8, 20, 1, 1.2, 1.2, new[] { 2 }, finding: true, r: 0.2, convex: false),
                F(Plane, 19, 20, 19.8, 19.8, 1.2, 3, 1.8, new[] { 2 }, new[] { 0, 1.0, 0 }),
                // a mesma coisa espelhada em X
                F(Cyl, -14, -10, 0, 0.2, 1, 1.2, 1.2, new[] { 3 }, finding: true, r: 0.2, convex: false),
                F(Plane, -11, -10, 0.2, 0.2, 1.2, 3, 1.8, new[] { 3 }, new[] { 0, -1.0, 0 })
            };
            EdmRegion R(int a, int b) => new EdmRegion { Box = new BoundingBox { MinX = f.Skip(a).Take(2).Min(x => x.Min[0]), MaxX = f.Skip(a).Take(2).Max(x => x.Max[0]), MinY = f.Skip(a).Take(2).Min(x => x.Min[1]), MaxY = f.Skip(a).Take(2).Max(x => x.Max[1]) } }.With(a, b);
            EdmRegion r0 = R(0, 1), r1 = R(2, 3), r2 = R(4, 5);
            Assert.Equal("idêntica, giro 180°", RegionFinder.Compare(f, r0, r1));
            Assert.Equal("espelhada", RegionFinder.Compare(f, r0, r2));
        }
    }

    internal static class EdmRegionTestExtensions
    {
        public static EdmRegion With(this EdmRegion r, params int[] faces) { r.Faces.AddRange(faces); return r; }
    }
}
