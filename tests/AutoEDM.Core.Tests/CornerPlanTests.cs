using System;
using System.Collections.Generic;
using System.Linq;
using AutoEDM.Electrode;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>T7: o plano da divisão na quina (lógica pura): recuo vertical da frente, enchimento do raio, reunião, parte estreita.</summary>
    public class CornerPlanTests
    {
        private const int Plane = MillJudge.igPlane, Cyl = MillJudge.igCyl;
        private static readonly BoundingBox Rect = new BoundingBox { MinX = -10, MaxX = 10, MinY = -10, MaxY = 10 };

        private static FaceRec F(int type, double x0, double x1, double y0, double y1, double[] n = null, double r = double.NaN, bool? convex = null) =>
            new FaceRec { Type = type, Min = new[] { x0, y0, 0.0 }, Max = new[] { x1, y1, 6.0 }, AreaMm2 = 10, Normal = n, R = r, Convex = convex };

        private static MillHit Corner(int wall, int other, double x, double y) =>
            new MillHit { Kind = MillHitKind.SharpVertical, FaceA = wall, FaceB = other, Mid = new[] { x, y, 3.0 }, Tangent = new[] { 0, 0, 1.0 }, LengthMm = 6 };

        private static MillJudgeResult Judged(params MillHit[] hits) { var r = new MillJudgeResult(); r.Failed.AddRange(hits); return r; }

        [Fact]
        public void FrontSide_GetsAVerticalPlane_ThatOnlyGrows_SoNoLaminaIsLeft()
        {
            double t = 2 * Math.PI / 180;
            var wall = new CutPlane { Point = new[] { 0.0, 0, 3 }, Normal = new[] { -Math.Cos(t), 0, -Math.Sin(t) } };
            CutPlane back = CornerPlanner.SidePlane(wall, false, 0, 7), front = CornerPlanner.SidePlane(wall, true, 0, 7);
            Assert.Equal(wall.Normal[2], back.Normal[2], 9);
            Assert.Equal(0.0, front.Normal[2], 9);
            double maxOverlap = 0;
            // todo ponto da frente da parede fica na parte da frente (sem lâmina); a sobreposição é ≤ tan 2° × 7
            for (double z = 0; z <= 7; z += 0.5)
                for (double x = -1; x <= 1; x += 0.005)
                {
                    bool keptFront = front.Distance(x, 0, z) <= 1e-9, inFrontOfWall = wall.Distance(x, 0, z) <= 0 == false;
                    if (wall.Distance(x, 0, z) > 1e-9) Assert.True(keptFront, $"lâmina em x {x}, z {z}");
                    if (keptFront && back.Distance(x, 0, z) <= 0) maxOverlap = Math.Max(maxOverlap, -wall.Distance(x, 0, z));
                }
            Assert.InRange(maxOverlap, 0.2, 7 * Math.Tan(t) + 1e-6);
        }

        [Fact]
        public void GrowthStrip_SitsBetweenTheWallAndTheVertical_UnderTheWallFace()
        {
            double t = 2 * Math.PI / 180;
            var wall = new CutPlane { Point = new[] { 0.0, 0, 0 }, Normal = new[] { 0, Math.Cos(t), -Math.Sin(t) } };   // parede y ≈ 0, frente +y
            CutPlane front = CornerPlanner.SidePlane(wall, true, 0, 7);
            var face = new FaceRec { Type = Plane, Min = new[] { 0.0, -0.3, 0 }, Max = new[] { 5.0, 0.0, 7 }, Normal = wall.Normal };
            TipCut strip = CornerPlanner.GrowthStrip(wall, front, face, 0, 7);
            Assert.NotNull(strip);
            Assert.True(strip.IsStrip);
            Assert.Equal(5.02, strip.LengthMm, 6);
            Assert.Equal(1.0, Math.Abs(strip.Axis[0]), 9);
            // os pontos do polígono ficam entre a vertical (y = 0) e o plano inclinado (y = z·tan2°), na metade do comprimento
            Assert.All(strip.Prism, p => Assert.Equal(2.5, p[0], 6));
            Assert.All(strip.Prism, p => Assert.InRange(p[1], -0.01, 8 * Math.Tan(t) + 0.01));
        }

        [Fact]
        public void Judge_CountsTheGap_OnConcaveRadii()
        {
            var faces = new List<FaceRec>
            {
                new FaceRec { Type = Cyl, R = 0.4, Convex = false, Min = new[] { 0, 0, 1.0 }, Max = new[] { 0.4, 5, 1.4 }, AreaMm2 = 3 },
                new FaceRec { Type = Cyl, R = 0.302, Convex = false, Min = new[] { 2, 0, 1.0 }, Max = new[] { 2.3, 5, 1.3 }, AreaMm2 = 2 }
            };
            MillJudgeResult r = MillJudge.Judge(faces, new List<EdgeSample>(), 7, new MillJudgeOptions { GapMm = 0.1 });
            Assert.Single(r.Passed);    // 0,4 + 0,1 = 0,5
            Assert.Single(r.Failed);    // 0,302 + 0,1 < 0,5
            Assert.Contains("GAP", r.Failed[0].Why);
        }

        [Fact]
        public void SmallRadius_IsSplitOnTheTangentWall_AndTheFrontLosesTheFiller()
        {
            var faces = new List<FaceRec>
            {
                F(Cyl, -2, 2, 0, 0.2, r: 0.2, convex: false),            // 0 raio, na frente da parede y = 0
                F(Plane, -10, 10, 0, 0, new[] { 0, 1.0, 0 })             // 1 parede tangente (normal +y)
            };
            var edges = new List<EdgeSample> { new EdgeSample { FaceA = 0, FaceB = 1, Mid = new[] { 0, 0, 3.0 }, Tangent = new[] { 1.0, 0, 0 }, LengthMm = 4, MaterialFraction = 0.5 } };
            var radius = new MillHit { Kind = MillHitKind.SmallRadius, FaceA = 0, Mid = new[] { 0, 0.1, 3.0 }, LengthMm = 4 };
            List<CornerCell> cells = CornerPlanner.Plan(faces, edges, Judged(radius), Rect, 0, 7, null, null);
            Assert.Equal(2, cells.Count);
            Assert.All(cells, c => Assert.True(c.Millable));
            CornerCell front = cells.Single(c => c.Path[0].front);
            Assert.Single(front.Fillers);
            Assert.Empty(cells.Single(c => !c.Path[0].front).Fillers);
        }

        [Fact]
        public void FillerPrism_IsTheCornerTriangle_AlongTheAxis()
        {
            var w = new CutPlane { Point = new[] { 1.0, 0, 3 }, Normal = new[] { 0, 1.0, 0 } };
            var q = new CutPlane { Point = new[] { 0, 1.0, 3 }, Normal = new[] { 1.0, 0, 0 } };
            var face = new FaceRec { Type = Cyl, R = 0.2, Min = new[] { 0, 0, 0.0 }, Max = new[] { 0.2, 0.2, 6.0 } };
            var cut = new TipCut();
            CornerPlanner.FillerPrism(cut, w, q, face, new[] { 0.1, 0.1, 3.0 });
            Assert.NotNull(cut.Prism);
            Assert.Equal(new[] { 0.0, 0, 3 }, cut.Prism[0].Select(v => Math.Round(v, 9)).ToArray());
            Assert.Equal(0.31, cut.Prism[1][0], 6); Assert.Equal(0.0, cut.Prism[1][1], 6);
            Assert.Equal(0.0, cut.Prism[2][0], 6); Assert.Equal(0.31, cut.Prism[2][1], 6);
            Assert.Equal(1.0, Math.Abs(cut.Axis[2]), 9);
            Assert.Equal(6.02, cut.LengthMm, 6);
        }

        [Fact]
        public void RadiusWithoutAPlanarTangentWall_StaysForWire()
        {
            var faces = new List<FaceRec> { F(Cyl, -2, 2, 0, 0.2, r: 0.2, convex: false), F(Cyl, -2, 2, 0.2, 1, r: 1, convex: true) };
            var edges = new List<EdgeSample> { new EdgeSample { FaceA = 0, FaceB = 1, Mid = new[] { 0, 0.2, 3.0 }, Tangent = new[] { 1.0, 0, 0 }, LengthMm = 4, MaterialFraction = 0.5 } };
            var log = new List<string>();
            List<CornerCell> cells = CornerPlanner.Plan(faces, edges, Judged(new MillHit { Kind = MillHitKind.SmallRadius, FaceA = 0, Mid = new[] { 0, 0.1, 3.0 } }), Rect, 0, 7, null, log);
            Assert.Single(cells[0].Failed);
            Assert.Contains(log, l => l.Contains("sem parede tangente"));
        }

        [Fact]
        public void PartsAcrossTheSamePlane_AreMergedWhenTheUnionPasses()
        {
            // dois cantos na parede y = 0 (lado x < −7) e dois cantos no plano x = −7 (um de cada lado de y = 0):
            // o plano x = −7 aparece nos dois ramos; as duas partes x > −7 voltam a ser uma (nada reprovado nelas).
            var faces = new List<FaceRec>
            {
                F(Plane, -10, -7.5, 0, 0, new[] { 0, 1.0, 0 }),          // 0 parede y = 0
                F(Cyl, -9.5, -8.5, 0, 1, r: 1, convex: true),          // 1
                F(Plane, -7, -7, -10, 10, new[] { 1.0, 0, 0 }),          // 2 parede x = −7
                F(Cyl, -7, -6, -6, -4, r: 1, convex: true),            // 3
                F(Cyl, -7, -6, 4, 6, r: 1, convex: true)               // 4
            };
            var hits = new[] { Corner(0, 1, -9, 0), Corner(0, 1, -8, 0), Corner(0, 1, -7.8, 0), Corner(2, 3, -7, -5), Corner(2, 4, -7, 5) };
            var log = new List<string>();
            List<CornerCell> cells = CornerPlanner.Plan(faces, new List<EdgeSample>(), Judged(hits), Rect, 0, 7, null, log);
            Assert.True(log.Any(l => l.StartsWith("partes: 4 antes de reunir, 3 depois")), string.Join(" | ", log));
            CornerCell merged = Assert.Single(cells, c => c.MergeNote != null);
            Assert.True(merged.Millable);
            Assert.Equal(-7.0, merged.Footprint.MinX, 1);
            Assert.All(cells, c => Assert.True(c.Millable));
        }

        [Fact]
        public void NarrowPart_IsMergedToTheNeighbour_EvenIfTheUnionFails()
        {
            var faces = new List<FaceRec> { F(Plane, 9, 9, -10, 10, new[] { 1.0, 0, 0 }), F(Cyl, 9, 10, -1, 1, r: 1, convex: true) };
            var log = new List<string>();
            List<CornerCell> cells = CornerPlanner.Plan(faces, new List<EdgeSample>(), Judged(Corner(0, 1, 9, 0)), Rect, 0, 7, null, log);
            CornerCell c = Assert.Single(cells);
            Assert.Contains("estreita", c.MergeNote);
            Assert.False(c.Millable);
        }
    }
}
