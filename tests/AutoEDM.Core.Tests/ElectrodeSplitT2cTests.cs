using System;
using System.Collections.Generic;
using System.Linq;
using AutoEDM.Electrode;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// T2c do "Criar Eletrodos": tirar o enchimento do raio cortado (C1), dividir só quando resolve (C2) e a tolerância de
    /// alinhamento (C3). Lógica pura. Pegada de teste: 20 × 10 mm (x 0…20, y 0…10), fechamento em Z 0, fundo em −5.
    /// </summary>
    public class ElectrodeSplitT2cTests
    {
        private static readonly BoundingBox Foot = new BoundingBox { MinX = 0, MaxX = 20, MinY = 0, MaxY = 10, MinZ = -5, MaxZ = 0 };
        private static readonly List<BoundingBox> AllBurn = new List<BoundingBox> { Foot };

        /// <summary>Pé de degrau R0,1 de eixo ao longo de Y, parede em x = <paramref name="wallX"/>; enchimento do lado x &lt; parede.</summary>
        private static SplitFinding StepFoot(double wallX, double y0 = 0, double y1 = 10) =>
            SplitFinding.From(1, false, 0.1, 0, new[] { 0.0, 1.0, 0.0 }, new[] { wallX - 0.1, 0.0, -2.0 },
                new[] { wallX - 0.1, y0, -2.1 }, new[] { wallX, y1, -2.0 });

        /// <summary>Pé de degrau de eixo ao longo de X, parede em y = <paramref name="wallY"/>; enchimento do lado y &lt; parede.</summary>
        private static SplitFinding StepFootX(double wallY, double x0, double x1) =>
            SplitFinding.From(2, false, 0.1, 0, new[] { 1.0, 0.0, 0.0 }, new[] { 0.0, wallY - 0.1, -2.0 },
                new[] { x0, wallY - 0.1, -2.1 }, new[] { x1, wallY, -2.0 });

        /// <summary>
        /// Canto R0,2 de eixo quase vertical, 2° de saída em X: no topo (Z −1) o arco tem centro (10, 5) no quadrante
        /// (+x, +y); descendo até Z −3 o centro anda +2·tan 2° em X.
        /// </summary>
        private static SplitFinding DraftedCorner(double tolDeg)
        {
            double a = 2.0 * Math.PI / 180.0, k = Math.Tan(a);
            return SplitFinding.From(3, false, 0.2, 0, new[] { -Math.Sin(a), 0.0, Math.Cos(a) }, new[] { 10.0, 5.0, -1.0 },
                new[] { 10.0, 5.0, -3.0 }, new[] { 10.2 + 2 * k, 5.2, -1.0 }, tolDeg);
        }

        // ---------------------------------------------------------------- C2: dividir só quando resolve

        [Fact]
        public void Auto_SplitsOnlyWhenEveryFindingHasACut()
        {
            SplitPlan solved = ElectrodeSplitter.Plan(Foot, new List<SplitFinding> { StepFoot(10.0) }, AllBurn, null);
            Assert.True(SplitDecision.ApplyPlan(solved, SplitMode.Auto, out string none));
            Assert.Null(none);

            var inclined = SplitFinding.From(4, false, 0.1, 0, new[] { 0.6, 0.0, 0.8 }, new[] { 15.0, 5.0, -3.0 },
                new[] { 14.9, 4.9, -3.0 }, new[] { 15.1, 5.1, -2.0 });
            SplitPlan open = ElectrodeSplitter.Plan(Foot, new List<SplitFinding> { StepFoot(10.0), inclined }, AllBurn, null);
            Assert.True(open.IsSplit);
            Assert.False(SplitDecision.ApplyPlan(open, SplitMode.Auto, out string why));
            Assert.Equal("a divisão não resolve: 1 raio(s) sem corte possível", why);

            Assert.True(SplitDecision.ApplyPlan(open, SplitMode.Force, out _));
            Assert.False(SplitDecision.ApplyPlan(solved, SplitMode.Off, out _));
        }

        [Fact]
        public void Auto_PlanWithoutCut_IsNotApplied_EvenWhenForced()
        {
            var inclined = SplitFinding.From(4, false, 0.1, 0, new[] { 0.6, 0.0, 0.8 }, new[] { 15.0, 5.0, -3.0 },
                new[] { 14.9, 4.9, -3.0 }, new[] { 15.1, 5.1, -2.0 });
            SplitPlan plan = ElectrodeSplitter.Plan(Foot, new List<SplitFinding> { inclined }, AllBurn, null);
            Assert.False(SplitDecision.ApplyPlan(plan, SplitMode.Force, out string why));
            Assert.Contains("1 raio(s) sem corte", why);
        }

        [Fact]
        public void Auto_KeepsBuiltPiecesOnlyIfAllAreMillable()
        {
            Assert.True(SplitDecision.KeepPieces(SplitMode.Auto, new[] { 0, 0, 0 }, out _));
            Assert.False(SplitDecision.KeepPieces(SplitMode.Auto, new[] { 0, 2, 1 }, out string bad));
            Assert.Contains("2 pedaço(s)", bad);
            Assert.Contains("3 raio(s)", bad);
            Assert.False(SplitDecision.KeepPieces(SplitMode.Auto, new[] { 0, -1 }, out string failed));
            Assert.Contains("não saíram construídos", failed);
            Assert.True(SplitDecision.KeepPieces(SplitMode.Force, new[] { 0, 2 }, out _));
        }

        // ---------------------------------------------------------------- C1: caixa do enchimento

        [Fact]
        public void FillerBox_OnlyInThePieceThatHasIt_FlushToTheFace_AndOutOfThePieceAcrossTheBorder()
        {
            var o = new SplitOptions { CoveOverlapMm = -0.001 };
            SplitPlan plan = ElectrodeSplitter.Plan(Foot, new List<SplitFinding> { StepFoot(10.0) }, AllBurn, null, o);
            SplitPiece left = plan.Pieces.Single(p => p.Footprint.MinX == 0), right = plan.Pieces.Single(p => p.Footprint.MinX == 10.0);
            Assert.Equal(9.999, left.BuildFootprint.MaxX, 9);   // sobreposição negativa: o pedaço para antes da parede

            Assert.Empty(ElectrodeSplitter.FillerBoxes(right, null, -5, 0, 0.0, o));
            FillerCut c = Assert.Single(ElectrodeSplitter.FillerBoxes(left, null, -5, 0, 0.0, o));
            Assert.Same(plan.Pieces.SelectMany(p => p.OnBorder).First(), c.Finding);
            Assert.Equal(9.9, c.Box.MinX, 9);               // rente à face, do lado de longe da divisa
            Assert.Equal(9.999 + 1.0, c.Box.MaxX, 9);       // passa da divisa para fora do pedaço
            Assert.Equal(-1.0, c.Box.MinY, 9);              // a face atravessa o pedaço em Y: sai pelas duas bordas
            Assert.Equal(11.0, c.Box.MaxY, 9);
            Assert.Equal(-2.1, c.Box.MinZ, 9);
            Assert.Equal(-2.0, c.Box.MaxZ, 9);

            FillerCut clear = Assert.Single(ElectrodeSplitter.FillerBoxes(left, null, -5, 0, 0.001, o));
            Assert.Equal(9.899, clear.Box.MinX, 9);
            Assert.Equal(-2.0 + 0.001, clear.Box.MaxZ, 9);
        }

        [Fact]
        public void FillerBox_IsClippedToThePiece_AlongTheRadius()
        {
            // O pé de degrau em y = 5 atravessa a pegada inteira em X, e o corte x = 10 parte a pegada: cada pedaço de
            // baixo tira só a sua parte da caixa (até fora da própria borda em X), e o da esquerda ainda o enchimento de x = 10.
            var o = new SplitOptions { CoveOverlapMm = -0.001 };
            var findings = new List<SplitFinding> { StepFoot(10.0), StepFootX(5.0, 0, 20) };
            SplitPlan plan = ElectrodeSplitter.Plan(Foot, findings, AllBurn, null, o);
            Assert.Equal(2, plan.Cuts.Count);
            Assert.Equal(4, plan.Pieces.Count);

            SplitPiece lowRight = plan.Pieces.Single(p => p.Footprint.MinX == 10.0 && p.Footprint.MaxY <= 5.0);
            FillerCut c = Assert.Single(ElectrodeSplitter.FillerBoxes(lowRight, null, -5, 0, 0.0, o));
            Assert.Equal(10.0 - 1.0, c.Box.MinX, 9);      // a face vem de x 0: neste pedaço a caixa começa fora dele
            Assert.Equal(20.0 + 1.0, c.Box.MaxX, 9);
            Assert.Equal(4.9, c.Box.MinY, 9);
            Assert.Equal(4.999 + 1.0, c.Box.MaxY, 9);

            SplitPiece lowLeft = plan.Pieces.Single(p => p.Footprint.MaxX == 10.0 && p.Footprint.MaxY <= 5.0);
            Assert.Equal(2, ElectrodeSplitter.FillerBoxes(lowLeft, null, -5, 0, 0.0, o).Count);   // os dois enchimentos
            SplitPiece upLeft = plan.Pieces.Single(p => p.Footprint.MaxX == 10.0 && p.Footprint.MinY >= 5.0);
            FillerCut up = Assert.Single(ElectrodeSplitter.FillerBoxes(upLeft, null, -5, 0, 0.0, o));
            Assert.Equal(5.0 - 1.0, up.Box.MinY, 9);      // a face de x = 10 atravessa o pedaço: sai pelas duas bordas
            Assert.Equal(10.0 + 1.0, up.Box.MaxY, 9);
            Assert.Empty(ElectrodeSplitter.FillerBoxes(plan.Pieces.Single(p => p.Footprint.MinX == 10.0 && p.Footprint.MinY >= 5.0), null, -5, 0, 0.0, o));
        }

        [Fact]
        public void PositiveOverlap_AddsTheStripThatRemovesTheLamina()
        {
            var o = new SplitOptions { CoveOverlapMm = 0.001, StripInsetMm = 0.001 };
            SplitPlan plan = ElectrodeSplitter.Plan(Foot, new List<SplitFinding> { StepFoot(10.0) }, AllBurn, null, o);
            SplitPiece left = plan.Pieces.Single(p => p.Footprint.MinX == 0);
            List<FillerCut> cuts = ElectrodeSplitter.FillerBoxes(left, null, -5, 0, 0.0, o);
            Assert.Equal(2, cuts.Count);
            FillerCut strip = cuts.Single(c => c.Finding == null);
            Assert.Equal(9.999, strip.Box.MinX, 9);
            Assert.Equal(-6.0, strip.Box.MinZ, 9);
            Assert.Equal(1.0, strip.Box.MaxZ, 9);
        }

        [Fact]
        public void FillerReachingTheClosing_GoesAboveIt()
        {
            var f = SplitFinding.From(5, false, 0.2, 0, new[] { 0.0, 0.0, 1.0 }, new[] { 10.0, 5.0, -3.0 },
                new[] { 10.0, 5.0, -3.0 }, new[] { 10.2, 5.2, -0.005 });
            var o = new SplitOptions { CoveOverlapMm = -0.001 };
            SplitPlan plan = ElectrodeSplitter.Plan(Foot, new List<SplitFinding> { f }, AllBurn, null, o);
            SplitPiece holder = plan.Pieces.Single(p => p.Footprint.MaxX == 10.2);
            FillerCut c = Assert.Single(ElectrodeSplitter.FillerBoxes(holder, null, -5, 0, 0.0, o));
            Assert.Equal(1.0, c.Box.MaxZ, 9);
            Assert.Equal(5.0, c.Box.MinY, 9);
            Assert.Equal(5.2, c.Box.MaxY, 9);
        }

        [Fact]
        public void PocketCornerTorus_HasNoCut_ButItsEndLeavesThePieceBeyondAWall()
        {
            // Boca de um rebaixo arredondada R0,3 contornando o canto dele (toro de dentro, Rm 1,3, centro (10, 5)):
            // paredes em x = 9 e y = 4. O meio do arco fica entre as duas retas: corte reto nenhum o separa
            // (medido no 15428.100: com o corte x = −11,823 o arco seguia no pedaço do canto).
            var f = SplitFinding.From(7, true, 0.3, 1.3, new[] { 0.0, 0.0, -1.0 }, new[] { 10.0, 5.0, -2.3 },
                new[] { 8.7, 3.7, -2.3 }, new[] { 10.0, 5.0, -2.0 });
            Assert.Empty(f.Options);
            Assert.Contains("contorna o canto", f.NoCut);
            Assert.Contains(f.SideLines, l => l.Axis == 'X' && Math.Abs(l.At - 9.0) < 1e-9 && l.CoveSide == -1);
            Assert.Contains(f.SideLines, l => l.Axis == 'Y' && Math.Abs(l.At - 4.0) < 1e-9 && l.CoveSide == -1);

            var beyond = new BoundingBox { MinX = 0, MaxX = 9, MinY = 0, MaxY = 10, MinZ = -5, MaxZ = 0 };
            var corner = new BoundingBox { MinX = 9, MaxX = 20, MinY = 4, MaxY = 10, MinZ = -5, MaxZ = 0 };
            var list = new[] { f };
            FillerCut c = Assert.Single(ElectrodeSplitter.FillerBoxes(new SplitPiece { Footprint = beyond, BuildFootprint = beyond }, list, -5, 0, 0.0));
            Assert.Equal(8.7, c.Box.MinX, 9);
            Assert.Equal(9.0 + 1.0, c.Box.MaxX, 9);   // a face passa da borda x = 9: a caixa vai até 1 mm fora dela
            Assert.Equal(3.7, c.Box.MinY, 9);
            // no pedaço do canto a caixa pegaria o cobre do próprio rebaixo: nada sai
            Assert.Empty(ElectrodeSplitter.FillerBoxes(new SplitPiece { Footprint = corner, BuildFootprint = corner }, list, -5, 0, 0.0));
        }

        // ---------------------------------------------------------------- C3: tolerância de alinhamento

        [Fact]
        public void AlignmentTolerance_OffByDefault_TiltedCornerHasNoCut()
        {
            SplitFinding f = DraftedCorner(0.0);
            Assert.Empty(f.Options);
            Assert.Equal("eixo inclinado", f.NoCut);
            Assert.False(f.Tilted);
        }

        [Fact]
        public void AlignmentTolerance_TiltedCorner_CutsAtTheWallOnTop()
        {
            SplitFinding f = DraftedCorner(3.0);
            Assert.True(f.Tilted);
            Assert.Equal(2.0, f.TiltDeg, 6);
            Assert.Contains(f.Options, l => l.Axis == 'X' && Math.Abs(l.At - 10.2) < 1e-9);
            Assert.Contains(f.Options, l => l.Axis == 'Y' && Math.Abs(l.At - 5.2) < 1e-9);

            Assert.Empty(DraftedCorner(1.5).Options);   // 2° não cabe em 1,5°
        }

        [Fact]
        public void AlignmentTolerance_TiltedFiller_LeavesBothNeighbourPieces()
        {
            SplitFinding f = DraftedCorner(3.0);
            var o = new SplitOptions { CoveOverlapMm = -0.001 };
            SplitPlan plan = ElectrodeSplitter.Plan(Foot, new List<SplitFinding> { f }, AllBurn, null, o);
            Assert.Equal('X', plan.Cuts.Single().Line.Axis);
            SplitPiece left = plan.Pieces.Single(p => p.Footprint.MinX == 0), right = plan.Pieces.Single(p => p.Footprint.MaxX == 20);

            FillerCut l = Assert.Single(ElectrodeSplitter.FillerBoxes(left, null, -5, 0, 0.0, o));
            Assert.Equal(10.0, l.Box.MinX, 9);
            FillerCut r = Assert.Single(ElectrodeSplitter.FillerBoxes(right, null, -5, 0, 0.0, o));
            Assert.Equal(10.2 - 1.0, r.Box.MinX, 9);
            Assert.Equal(10.2 + 2 * Math.Tan(2.0 * Math.PI / 180.0), r.Box.MaxX, 9);
        }

        [Fact]
        public void AlignmentTolerance_StepFootRotatedInPlan_CutsThroughTheMiddleOfTheFace()
        {
            // Eixo a 2° de Y em planta, de y 0 a 10; no meio da face (y 5) o eixo passa em x 9,9.
            double a = 2.0 * Math.PI / 180.0;
            double xMid = 9.9, x0 = xMid - 5 * Math.Tan(a);
            var f = SplitFinding.From(6, false, 0.1, 0, new[] { Math.Sin(a), Math.Cos(a), 0.0 }, new[] { x0, 0.0, -2.0 },
                new[] { x0, 0.0, -2.1 }, new[] { xMid + 5 * Math.Tan(a) + 0.1, 10.0, -2.0 }, 3.0);
            Assert.True(f.Tilted);
            SplitLine l = Assert.Single(f.Options);
            Assert.Equal('X', l.Axis);
            Assert.Equal(10.0, l.At, 9);
        }
    }
}
