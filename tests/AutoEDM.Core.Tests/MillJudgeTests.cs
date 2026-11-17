using System;
using System.Collections.Generic;
using System.Linq;
using AutoEDM.Electrode;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// T6: o juiz único de fresabilidade (lógica pura) e a divisão NA QUINA. Faces e arestas sintéticas, mm, referencial
    /// do eletrodo (ponta de Z 0 ao fechamento em Z 7; a fresa vem de −Z).
    /// </summary>
    public class MillJudgeTests
    {
        private const int Plane = MillJudge.igPlane, Cyl = MillJudge.igCyl;
        private const double RefZ = 7.0;

        private static FaceRec F(int type, double x0, double x1, double y0, double y1, double z0, double z1, double area,
            double[] n = null, double r = double.NaN, bool? convex = null) =>
            new FaceRec { Type = type, Min = new[] { x0, y0, z0 }, Max = new[] { x1, y1, z1 }, AreaMm2 = area, Normal = n, R = r, Convex = convex };

        private static EdgeSample E(int a, int b, double[] mid, double[] t, double len, double frac) =>
            new EdgeSample { FaceA = a, FaceB = b, Mid = mid, Tangent = t, LengthMm = len, MaterialFraction = frac };

        // um bloco de referência para a pegada: piso grande no fundo
        private static List<FaceRec> Base() => new List<FaceRec> { F(Plane, -20, 20, -20, 20, 0, 0, 1600, new[] { 0, 0, -1.0 }) };

        [Fact]
        public void VerticalCorner_CylinderTimesWall_IsRejected_TheT5Escape()
        {
            var f = Base();
            f.Add(F(Cyl, -17.8, -16.7, -20, -19.5, 0.3, 7.1, 10, r: 1.006, convex: true));     // 1
            f.Add(F(Plane, -16.7, -10, -19.6, -19.5, 0.3, 7.1, 40, new[] { 0.052, -0.998, -0.018 })); // 2
            var r = MillJudge.Judge(f, new[] { E(1, 2, new[] { -16.727, -19.522, 3.7 }, new[] { 0, 0, 1.0 }, 6.854, 0.75) }, RefZ);
            MillHit h = Assert.Single(r.Failed);
            Assert.Equal(MillHitKind.SharpVertical, h.Kind);
            Assert.Equal(90, h.EmptyDeg, 6);
            Assert.False(r.Millable);
        }

        [Fact]
        public void WallTimesFloor_FloorFacingTheMill_Passes()
        {
            var f = Base();
            f.Add(F(Plane, 0, 25, -8.2, -8.1, 2.2, 5, 70, new[] { 0, -1.0, 0 }));   // 1 parede
            f.Add(F(Plane, 0, 25, -10, -8.1, 2.2, 2.2, 50, new[] { 0, 0, -1.0 }));  // 2 piso voltado para a fresa
            var r = MillJudge.Judge(f, new[] { E(1, 2, new[] { 12.5, -8.15, 2.2 }, new[] { 1.0, 0, 0 }, 25, 0.75) }, RefZ);
            Assert.True(r.Millable);
            Assert.Equal(MillHitKind.WallFloor, Assert.Single(r.Passed).Kind);
        }

        [Fact]
        public void InclinedAndObtuseCorners_AreRejected_ConvexEdgesIgnored()
        {
            var f = Base();
            f.Add(F(Cyl, 16, 17, -9, -8, 2, 3, 1, r: 0.201, convex: true));
            f.Add(F(Cyl, 16, 17, -8.5, -7.5, 2, 3, 1, r: 0.201, convex: true));
            double s = Math.Sin(55 * Math.PI / 180), c = Math.Cos(55 * Math.PI / 180);
            var edges = new[]
            {
                E(1, 2, new[] { 16.62, -8.088, 2.277 }, new[] { s, 0, c }, 0.34, 2.0 / 3),   // vão 120°, 55° de Z
                E(1, 2, new[] { 16.0, -8.0, 2.0 }, new[] { 0, 0, 1.0 }, 1, 0.25)            // convexa
            };
            var r = MillJudge.Judge(f, edges, RefZ);
            MillHit h = Assert.Single(r.Failed);
            Assert.Equal(MillHitKind.SharpInclined, h.Kind);
            Assert.Equal(120, h.EmptyDeg, 6);
        }

        [Fact]
        public void ShortEdges_AreSlivers_NotRejections()
        {
            var f = Base();
            f.Add(F(Plane, 0, 0, 0, 5, 0.1, 0.25, 1, new[] { 1.0, 0, 0 }));
            f.Add(F(Plane, 0, 5, 0, 0, 0.1, 0.25, 1, new[] { 0, 1.0, 0 }));
            var r = MillJudge.Judge(f, new[] { E(1, 2, new[] { 0, 0, 0.2 }, new[] { 0, 0, 1.0 }, 0.14, 0.75) }, RefZ);
            Assert.True(r.Millable);
            Assert.True(Assert.Single(r.Slivers).Sliver);
            Assert.Contains("lasca", r.Summary());
        }

        [Fact]
        public void ConcaveRadius_BelowTheSmallestMill_IsRejected_AtTheMill_Passes()
        {
            var f = Base();
            f.Add(F(Cyl, 0, 0.2, 0, 5, 1, 1.2, 1, r: 0.2, convex: false));
            f.Add(F(Cyl, 5, 5.5, 0, 5, 1, 1.5, 3, r: 0.5, convex: false));
            f.Add(F(Cyl, 9, 9.1, 0, 0.1, 1, 1.1, 0.01, r: 0.1, convex: false));   // 0,1 mm: lasca
            var r = MillJudge.Judge(f, new List<EdgeSample>(), RefZ);
            Assert.Equal(MillHitKind.SmallRadius, Assert.Single(r.Failed).Kind);
            Assert.Single(r.Passed);
            Assert.Single(r.Slivers);
        }

        [Fact]
        public void ReliefCorner_Counts_AndUndercutFace_IsRejected()
        {
            var f = Base();
            // canto de alívio: duas paredes de um bolso fechado (nenhuma exceção para alívio)
            f.Add(F(Plane, -14.55, -14.55, -31.6, -25.1, 1, 7, 30, new[] { 1.0, 0, 0 }));
            f.Add(F(Plane, -14.55, -10, -25.1, -25.1, 1, 7, 30, new[] { 0, -1.0, 0 }));
            f.Add(F(Plane, 3, 4, 3, 4, 1, 2, 1, new[] { 0.999, 0, 0.035 }));   // contra-saída
            var r = MillJudge.Judge(f, new[] { E(1, 2, new[] { -14.55, -25.141, 4 }, new[] { 0, 0, 1.0 }, 6, 0.75) }, RefZ);
            Assert.Equal(2, r.Failed.Count);
            Assert.Contains(r.Failed, h => h.Kind == MillHitKind.SharpVertical);
            Assert.Contains(r.Failed, h => h.Kind == MillHitKind.Undercut);
        }

        [Fact]
        public void UndercutFaceOnADivisionPlane_IsClassifiedApart_ButStillRejects()
        {
            // divisão pela parede com 2° de saída: a face do outro lado fica com a saída ao contrário
            double t = 2 * Math.PI / 180;
            var plane = new CutPlane { Point = new[] { 0.0, 0, 3 }, Normal = new[] { -Math.Cos(t), 0, -Math.Sin(t) } };
            var f = Base();
            f.Add(F(Plane, -0.1, 0.1, -5, 5, 0, 6, 60, new[] { Math.Cos(t), 0, Math.Sin(t) }));
            var r = MillJudge.Judge(f, new List<EdgeSample>(), RefZ, null, new[] { plane });
            Assert.Equal(MillHitKind.DivisionUndercut, Assert.Single(r.Failed).Kind);
            Assert.Equal(0, r.FailedExceptDivision);
            Assert.False(r.Millable);
        }

        [Fact]
        public void UnreadMaterialSide_CountsAsRejected()
        {
            var f = Base();
            f.Add(F(Plane, 0, 0, 0, 5, 1, 4, 15, new[] { 1.0, 0, 0 }));
            f.Add(F(Plane, 0, 5, 0, 0, 1, 4, 15, new[] { 0, 1.0, 0 }));
            var r = MillJudge.Judge(f, new[] { E(1, 2, new[] { 0, 0, 2.5 }, new[] { 0, 0, 1.0 }, 3, double.NaN) }, RefZ);
            Assert.Equal(MillHitKind.Unknown, Assert.Single(r.Failed).Kind);
        }

        [Fact]
        public void Corner_IsSplitOnTheWallPlaneThatResolvesMost()
        {
            // parede comprida y = 0 (normal −y) com DOIS cantos vivos nela (x = 0 e x = 10); a outra parede de cada um é curta
            var f = Base();
            f.Add(F(Plane, -5, 15, 0, 0, 1, 6, 100, new[] { 0, -1.0, 0 }));   // 1
            f.Add(F(Plane, 0, 0, -3, 0, 1, 6, 15, new[] { 1.0, 0, 0 }));      // 2
            f.Add(F(Plane, 10, 10, -3, 0, 1, 6, 15, new[] { -1.0, 0, 0 }));   // 3
            var edges = new[]
            {
                E(1, 2, new[] { 0, 0, 3.5 }, new[] { 0, 0, 1.0 }, 5, 0.75),
                E(1, 3, new[] { 10.0, 0, 3.5 }, new[] { 0, 0, 1.0 }, 5, 0.75)
            };
            var r = MillJudge.Judge(f, edges, RefZ);
            Assert.Equal(2, r.Failed.Count);
            CornerCut c = CornerSplitPlanner.Choose(f, r, RefZ);
            Assert.NotNull(c);
            Assert.Equal(2, c.Resolved);
            Assert.Equal(-1.0, c.Plane.Normal[1], 6);   // o plano da parede comprida
            Assert.Contains("resolve 2", c.Choice);
        }

        [Fact]
        public void Candidates_NeverAFloor_AndAVerticalPlaneWhenNoWallIsPlanar()
        {
            var f = Base();
            f.Add(F(Cyl, 16, 17, -9, -8, 2, 3, 1, r: 0.201, convex: true));
            f.Add(F(Cyl, 16, 17, -8.5, -7.5, 2, 3, 1, r: 0.201, convex: true));
            double s = Math.Sin(55 * Math.PI / 180), c = Math.Cos(55 * Math.PI / 180);
            var h = new MillHit { Kind = MillHitKind.SharpInclined, FaceA = 1, FaceB = 2, Mid = new[] { 16.62, -8.088, 2.277 }, Tangent = new[] { s, 0, c } };
            CutPlane p = Assert.Single(CornerSplitPlanner.Candidates(f, h));
            Assert.Equal(0.0, p.Normal[2], 9);
            Assert.True(CornerSplitPlanner.OnPlane(p, h));

            var floorWall = new MillHit { Kind = MillHitKind.HorizontalNotFloor, FaceA = 0, FaceB = 1, Mid = new[] { 0, 0, 0.0 }, Tangent = new[] { 1.0, 0, 0 } };
            Assert.All(CornerSplitPlanner.Candidates(f, floorWall), q => Assert.True(Math.Abs(q.Normal[2]) < 0.2));
        }

        [Fact]
        public void ClipFootprint_KeepsEachSide_AndFollowsTheDraft()
        {
            var rect = new BoundingBox { MinX = -10, MaxX = 10, MinY = -5, MaxY = 5 };
            var cut = new CutPlane { Point = new[] { 2.0, 0, 0 }, Normal = new[] { 1.0, 0, 0 } };
            BoundingBox a = CornerSplitPlanner.ClipFootprint(rect, new[] { cut }, 0, 7, 0).Value;
            BoundingBox b = CornerSplitPlanner.ClipFootprint(rect, new[] { cut.Flipped("outro") }, 0, 7, 0).Value;
            Assert.Equal(2.0, a.MaxX, 9); Assert.Equal(-10.0, a.MinX, 9);
            Assert.Equal(2.0, b.MinX, 9); Assert.Equal(10.0, b.MaxX, 9);

            // parede com 2° de saída: o corte em Z 7 fica 7·tan2° mais para fora
            double t = 2 * Math.PI / 180;
            var draft = new CutPlane { Point = new[] { 2.0, 0, 0 }, Normal = new[] { Math.Cos(t), 0, -Math.Sin(t) } };
            BoundingBox d = CornerSplitPlanner.ClipFootprint(rect, new[] { draft }, 0, 7, 0).Value;
            Assert.Equal(2 + 7 * Math.Tan(t), d.MaxX, 6);
            Assert.Null(CornerSplitPlanner.ClipFootprint(rect, new[] { new CutPlane { Point = new[] { -20.0, 0, 0 }, Normal = new[] { 1.0, 0, 0 } } }, 0, 7));
        }
    }
}
