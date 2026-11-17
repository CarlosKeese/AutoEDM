using System.Collections.Generic;
using System.Linq;
using AutoEDM.Electrode;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// T2 do "Criar Eletrodos": dividir a pegada do eletrodo que a fresa não faz. Lógica pura, sem Solid Edge.
    /// Pegada de teste: 20 × 10 mm (x 0…20, y 0…10).
    /// </summary>
    public class ElectrodeSplitterTests
    {
        private static readonly BoundingBox Foot = new BoundingBox { MinX = 0, MaxX = 20, MinY = 0, MaxY = 10, MinZ = -5, MaxZ = 0 };
        private static readonly List<BoundingBox> AllBurn = new List<BoundingBox> { Foot };

        /// <summary>Pé de degrau R0,1 de eixo ao longo de Y; a parede fica em x = <paramref name="wallX"/> (eixo 0,1 antes).</summary>
        private static SplitFinding StepFoot(double wallX, double y0 = 0, double y1 = 10) =>
            SplitFinding.From(1, false, 0.1, 0, new[] { 0.0, 1.0, 0.0 }, new[] { wallX - 0.1, 0.0, -2.0 },
                new[] { wallX - 0.1, y0, -2.1 }, new[] { wallX, y1, -2.0 });

        /// <summary>Pé de degrau de eixo ao longo de X; a parede fica em y = <paramref name="wallY"/>.</summary>
        private static SplitFinding StepFootX(double wallY, double x0, double x1) =>
            SplitFinding.From(2, false, 0.1, 0, new[] { 1.0, 0.0, 0.0 }, new[] { 0.0, wallY - 0.1, -2.0 },
                new[] { x0, wallY - 0.1, -2.1 }, new[] { x1, wallY, -2.0 });

        [Fact]
        public void HorizontalAxis_CutsOnTheTangentWall_NotOnTheAxis()
        {
            SplitFinding f = StepFoot(10.0);
            Assert.Equal("pé de degrau", f.Kind);
            Assert.Single(f.Options);
            Assert.Equal('X', f.Options[0].Axis);
            Assert.Equal(10.0, f.Options[0].At, 9);
        }

        [Fact]
        public void VerticalCorner_QuarterArc_OffersBothTangentWalls()
        {
            // Arco de centro (10, 5), R0,2, no quadrante (+x, +y): paredes em x = 10,2 e y = 5,2.
            SplitFinding f = SplitFinding.From(3, false, 0.2, 0, new[] { 0.0, 0.0, 1.0 }, new[] { 10.0, 5.0, -3.0 },
                new[] { 10.0, 5.0, -3.0 }, new[] { 10.2, 5.2, -1.0 });
            Assert.Equal("canto", f.Kind);
            Assert.Equal(2, f.Options.Count);
            Assert.Contains(f.Options, l => l.Axis == 'X' && System.Math.Abs(l.At - 10.2) < 1e-9);
            Assert.Contains(f.Options, l => l.Axis == 'Y' && System.Math.Abs(l.At - 5.2) < 1e-9);
        }

        [Fact]
        public void InclinedAxis_HasNoCut()
        {
            SplitFinding f = SplitFinding.From(4, false, 0.1, 0, new[] { 0.6, 0.0, 0.8 }, new[] { 10.0, 5.0, -3.0 },
                new[] { 9.9, 4.9, -3.0 }, new[] { 10.1, 5.1, -2.0 });
            Assert.Empty(f.Options);
            Assert.Equal("eixo inclinado", f.NoCut);
        }

        [Fact]
        public void OneFinding_GivesTwoPieces()
        {
            SplitPlan plan = ElectrodeSplitter.Plan(Foot, new List<SplitFinding> { StepFoot(10.0) }, AllBurn, null);

            Assert.Single(plan.Cuts);
            Assert.Single(plan.Cuts[0].Resolves);
            Assert.Equal(2, plan.Pieces.Count);
            Assert.Contains(plan.Pieces, p => p.Footprint.MinX == 0 && p.Footprint.MaxX == 10.0);
            Assert.Contains(plan.Pieces, p => p.Footprint.MinX == 10.0 && p.Footprint.MaxX == 20);
            Assert.All(plan.Pieces, p => Assert.Single(p.OnBorder));
            Assert.Empty(plan.Unresolved);

            // O enchimento do raio fica do lado do eixo (x < 10): só o pedaço da esquerda passa 0,001 da divisa.
            SplitPiece left = plan.Pieces.Single(p => p.Footprint.MinX == 0), right = plan.Pieces.Single(p => p.Footprint.MinX == 10.0);
            Assert.Equal(10.001, left.BuildFootprint.MaxX, 9);
            Assert.Equal(10.0, right.BuildFootprint.MinX, 9);
        }

        [Fact]
        public void CollinearFindings_ShareOneCut()
        {
            var findings = new List<SplitFinding> { StepFoot(10.0, 0, 4), StepFoot(10.03, 6, 10) };
            SplitPlan plan = ElectrodeSplitter.Plan(Foot, findings, AllBurn, null);

            Assert.Single(plan.Cuts);
            Assert.Equal(2, plan.Cuts[0].Resolves.Count);
            Assert.Equal(2, plan.Pieces.Count);
        }

        [Fact]
        public void CellsWithNoFindingBetweenThem_AreJoined()
        {
            // x = 10 só proíbe atravessar em y 0…3; y = 5 só proíbe atravessar em x 12…14. Quatro células → três pedaços.
            var findings = new List<SplitFinding> { StepFoot(10.0, 0, 3), StepFootX(5.0, 12, 14) };
            SplitPlan plan = ElectrodeSplitter.Plan(Foot, findings, AllBurn, null);

            Assert.Equal(2, plan.Cuts.Count);
            Assert.Equal(3, plan.Pieces.Count);
            Assert.Equal(200.0, plan.Pieces.Sum(p => p.Footprint.SizeX * p.Footprint.SizeY), 6);
            Assert.All(plan.Pieces, p => Assert.False(ElectrodeSplitter.HasInteriorFinding(p.Footprint, findings, 0.05)));
        }

        [Fact]
        public void CellWithoutBurn_IsDropped()
        {
            var burn = new List<BoundingBox> { new BoundingBox { MinX = 1, MaxX = 9.5, MinY = 1, MaxY = 9, MinZ = -5, MaxZ = 0 } };
            SplitPlan plan = ElectrodeSplitter.Plan(Foot, new List<SplitFinding> { StepFoot(10.0) }, burn, null);

            Assert.Single(plan.Pieces);
            Assert.Equal(1, plan.DroppedCells);
            Assert.Equal(10.0, plan.Pieces[0].Footprint.MaxX, 9);
        }

        [Fact]
        public void CutCloserThanMinimumWidth_DoesNotSplit_AndIsReported()
        {
            SplitFinding f = StepFoot(1.5);   // pedaço de 1,5 mm < 2 mm
            SplitPlan plan = ElectrodeSplitter.Plan(Foot, new List<SplitFinding> { f }, AllBurn, null, new SplitOptions { MinPieceMm = 2.0 });

            Assert.False(plan.IsSplit);
            Assert.Single(plan.Pieces);
            Assert.Single(plan.Unresolved);
            Assert.Contains("largura mínima", f.Unresolved);
            Assert.Single(plan.Pieces[0].Unresolved);
        }

        [Fact]
        public void TwoParallelCutsTooClose_KeepOnlyOne()
        {
            var findings = new List<SplitFinding> { StepFoot(10.0), StepFoot(11.0) };
            SplitPlan plan = ElectrodeSplitter.Plan(Foot, findings, AllBurn, null, new SplitOptions { MinPieceMm = 2.0 });

            Assert.Single(plan.Cuts);
            Assert.Single(plan.Unresolved);
            Assert.Equal(2, plan.Pieces.Count);
        }

        [Fact]
        public void PieceThatDoesNotFitABar_IsNotJoined()
        {
            // O caso das quatro células (três pedaços quando tudo cabe), agora sem barra maior que 12 × 6 mm:
            // as células se juntariam sem achado entre elas, mas o pedaço reunido não caberia numa barra.
            var findings = new List<SplitFinding> { StepFoot(10.0, 0, 3), StepFootX(5.0, 12, 14) };
            SplitPlan plan = ElectrodeSplitter.Plan(Foot, findings, AllBurn, r => r.SizeX <= 12.0 && r.SizeY <= 6.0);

            Assert.Equal(4, plan.Pieces.Count);
        }

        [Fact]
        public void TooManyPieces_AbandonsTheSplit()
        {
            var findings = new List<SplitFinding> { StepFoot(5.0), StepFoot(10.0), StepFoot(15.0) };
            SplitPlan plan = ElectrodeSplitter.Plan(Foot, findings, AllBurn, null, new SplitOptions { MaxPieces = 3 });

            Assert.False(plan.IsSplit);
            Assert.NotNull(plan.Abandoned);
        }
    }
}
