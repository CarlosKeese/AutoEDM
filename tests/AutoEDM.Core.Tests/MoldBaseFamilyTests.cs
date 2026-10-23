using System.Linq;
using AutoEDM.Mold.MoldBase;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// Tipos de porta-molde e componentes (Carlos, 2026-10-23). Os números vêm dos arquivos da casa lidos nesse
    /// dia: o 15354 (Polimold 3035) da biblioteca e os padrões TIPO GAVETA e BABY PLAST - 75 X 75 de
    /// W:\01 - Biblioteca\Padrões\Moldes.
    /// </summary>
    public class MoldBaseFamilyTests
    {
        private static MoldBaseContent Box(double x, double y, double up, double down, double product) => new MoldBaseContent
        {
            Min1 = -x / 2, Max1 = x / 2, Min2 = -y / 2, Max2 = y / 2, MinW = -down, MaxW = up, Count = 1, ProductHeight = product,
        };

        private static ComponentSpec Comp(MoldBasePlan p, string prefix) => p.Components.First(c => c.Code.StartsWith(prefix + " "));
        private static PlateSpec Plate(MoldBasePlan p, string code) => p.Plates.Single(x => x.Code == code);

        [Fact]
        public void Polimold3035_componentesComoNaBiblioteca()
        {
            // O 15354 da biblioteca: 30.35, P1 46, P2 36, curso 2, montagem 5B.
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(150, 150, 20, 20, 20),
                new MoldBaseOptions { ForcedSeries = "30.35", ForcedP1 = 46, ForcedP2 = 36, ForcedStroke = 2 });
            ComponentSpec col = plan.Components.First(c => c.Code == "CG 24 046 046");
            Assert.Equal(3, col.Positions.Count);
            Assert.Equal(17 + 46 + 46, col.MaxZ - col.MinZ, 3);               // S 17 + P1 46 + ponta 46
            Assert.Equal(35, col.MaxDiameter);
            Assert.Contains(plan.Components, c => c.Code == "CG 22 046 046" && c.Positions.Single()[0] > 0 && c.Positions.Single()[1] < 0);
            ComponentSpec bush = plan.Components.First(c => c.Code == "BG 24 036");
            Assert.Equal(24, bush.Bore.Diameter);
            Assert.Equal(36 + 17, bush.MaxZ - bush.MinZ, 3);                       // P2 + S na PS
            ComponentSpec pin = Comp(plan, "PR");
            Assert.Equal("PR 14 x 132", pin.Code);                                  // CPE 17 + curso 33 + PS 46 + P2 36
            Assert.Equal(4, pin.Positions.Count);
            Assert.Equal(22, pin.MaxDiameter);
            // Rebaixos das cabeças nas placas.
            Assert.Contains(plan.Plates.Single(p => p.Plate == MoldPlate.P1).Holes, h => h.Group == HoleGroup.ColunasCabeca && h.Depth == HoleDepthKind.BlindFromTop);
            Assert.Contains(plan.Plates.Single(p => p.Plate == MoldPlate.P2).Holes, h => h.Group == HoleGroup.BuchasCabeca && h.Depth == HoleDepthKind.BlindFromBottom);
            Assert.Contains(plan.Plates.Single(p => p.Plate == MoldPlate.CPE).Holes, h => h.Group == HoleGroup.PinosRetornoCabeca);
        }

        [Fact]
        public void Polimold_semComponentes_quandoDesmarcado()
        {
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(150, 150, 20, 20, 20),
                new MoldBaseOptions { ForcedSeries = "30.35", WithGuides = false, WithReturnPins = false });
            Assert.Empty(plan.Components);
            Assert.DoesNotContain(plan.Plates.SelectMany(p => p.Holes), h => h.Group == HoleGroup.ColunasCabeca || h.Group == HoleGroup.PinosRetornoCabeca);
        }

        [Fact]
        public void Polimold_colunamentoA_colunaCsemGuiaNaPBS()
        {
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(150, 150, 20, 20, 20),
                new MoldBaseOptions { ForcedSeries = "30.35", ForcedP1 = 46, ForcedP2 = 36, Guiding = 'A' });
            ComponentSpec col = plan.Components.First(c => c.Code.StartsWith("C 24"));
            Assert.Equal(0, col.MaxZ, 3);
            Assert.StartsWith("B 24", plan.Components.First(c => c.Kind == "Bucha guia").Code);
        }

        [Fact]
        public void Mud_gavetaComoNoPadrao()
        {
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(100, 180, 30, 10, 12), new MoldBaseOptions { Family = MoldBaseFamily.Mud });
            Assert.True(plan.Ready, plan.Problem);
            Assert.DoesNotContain(plan.Checks, c => c.Level == CheckLevel.Fail);
            Assert.Equal(46, plan.P1);
            Assert.Equal(32, plan.P2);
            Assert.Equal(87 - 32 - 5 - 24 - 0.7, plan.Stroke.Stroke, 3);
            Assert.Empty(plan.Components);                                   // o padrão não tem pino de retorno

            PlateSpec p1 = Plate(plan, "100"), p2 = Plate(plan, "200"), ps = Plate(plan, "201"), cpe = Plate(plan, "301"), pe = Plate(plan, "302");
            Assert.Equal("100 - Placa Fixa.par", p1.FileName);
            Assert.Equal(170, p2.Width);
            Assert.Equal(246, p2.Length);
            Assert.Equal(55, ps.Thickness);
            Assert.Equal(-87, ps.BottomW, 3);
            Assert.Equal(55, ps.Pocket.Depth, 3);                            // alojamento passante
            Assert.Equal(186, ps.Pocket.Length);
            Assert.Equal(5, p2.Boss.Height);
            Assert.True(p2.Boss.OnBottom);
            Assert.Equal(-74.3, cpe.BottomW, 3);
            Assert.Equal(-86.3, pe.BottomW, 3);
            Assert.Equal(72, pe.Pocket.Width);

            Assert.Equal(6, p2.Holes.Single().Count);                        // os 6 M8
            Assert.Equal(HoleDepthKind.BlindFromBottom, p2.Holes.Single().Depth);
            Assert.Contains(ps.Holes, h => h.Group == HoleGroup.ParafusosMoldeCabeca && h.Diameter == 15 && h.DepthMm == 9);
            Assert.Equal(12, p1.Groove.Z0);
            Assert.Equal(35, p1.Groove.Z1);
            Assert.Equal(32, ps.Groove.Z0, 3);
            Assert.Equal(55, ps.Groove.Z1, 3);
            Assert.Null(p2.Groove);

            Assert.Equal(16, plan.LibraryParts.Count);
            Assert.Single(plan.LibraryParts, p => p.FileName.StartsWith("7160"));
            // Guia do padrão em (0,45; 78; 23,5) com o comprimento em X → na submontagem (−78; 0,45; 23,5).
            Assert.Contains(plan.LibraryParts, p => p.FileName == "8760.001v3.par" && System.Math.Abs(p.T[0] + 78) < 1e-9 && System.Math.Abs(p.T[2] - 23.5) < 1e-9);
        }

        [Fact]
        public void Mud_200MaisGrossaComeOCurso()
        {
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(100, 180, 10, 30, 20), new MoldBaseOptions { Family = MoldBaseFamily.Mud });
            Assert.Equal(50, plan.P2);
            Assert.Contains(plan.Checks, c => c.Name == "Curso de extração" && c.Level == CheckLevel.Fail);
            PlateSpec p2 = Plate(plan, "200"), ps = Plate(plan, "201");
            Assert.Equal(37, ps.Thickness);
            Assert.Equal(0, p2.Groove.Z0, 3);                                // a faixa das guias passa para a 200
            Assert.Equal(18, p2.Groove.Z1, 3);
            Assert.Equal(32, ps.Groove.Z0, 3);
            Assert.Equal(37, ps.Groove.Z1, 3);
        }

        [Fact]
        public void Mud_pecasCompridas_giramAUnidade()
        {
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(200, 100, 20, 10, 10), new MoldBaseOptions { Family = MoldBaseFamily.Mud });
            Assert.True(plan.Rotated);
            Assert.DoesNotContain(plan.Checks, c => c.Level == CheckLevel.Fail);
        }

        [Fact]
        public void Mud_100e200Alteraveis_semAviso()
        {
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(100, 180, 30, 10, 12), new MoldBaseOptions { Family = MoldBaseFamily.Mud, ForcedP1 = 56 });
            Assert.Equal(56, plan.P1);
            Assert.Equal(CheckLevel.Ok, plan.Checks.Single(c => c.Name == "Placa fixa (100)").Level);
        }

        [Fact]
        public void Babyplast_metades40e44_travasDeCanto()
        {
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(30, 30, 12, 10, 8), new MoldBaseOptions { Family = MoldBaseFamily.Babyplast });
            Assert.True(plan.Ready, plan.Problem);
            Assert.DoesNotContain(plan.Checks, c => c.Level == CheckLevel.Fail);
            Assert.Equal(40, plan.P1);
            Assert.Equal(44, plan.P2);
            Assert.Empty(plan.Components);
            PlateSpec p1 = Plate(plan, "100"), p2 = Plate(plan, "200"), pbs = Plate(plan, "101"), ps = Plate(plan, "201");
            PlateSpec cpe = Plate(plan, "300"), pe = Plate(plan, "301");
            Assert.Equal(76, p1.Width);                                      // 76 perto da partição…
            Assert.Equal(0.5, p1.Step.Inset);                                // …75 nos 21 de trás
            Assert.Equal(21, p1.Step.Depth);
            Assert.False(p1.Step.FromBottom);
            Assert.True(p2.Step.FromBottom);
            Assert.Equal(12, p1.Corners.Size);
            Assert.Equal(74, pbs.Width);
            Assert.Equal(6, pbs.Thickness);
            Assert.Equal(-50, ps.BottomW, 3);
            Assert.Equal(70, cpe.Width);
            Assert.Equal(-50 - 18 - 12, cpe.BottomW, 3);                     // curso 8 + 10
            Assert.False(p1.Corners.Recess);
            Assert.True(p1.Corners.OnBottom);
            Assert.True(p2.Corners.Recess);
            Assert.False(p2.Corners.OnBottom);
            HoleSpec backing = p1.Holes.Single(h => h.Group == HoleGroup.ParafusosMolde);
            Assert.Equal(63, backing.AcrossA);
            Assert.Equal(HoleDepthKind.BlindFromTop, backing.Depth);
            Assert.Equal(28.2, p2.Holes.Single(h => h.Group == HoleGroup.ParafusosFixacao).AcrossA);
            Assert.Equal(6.2, ps.Holes.Single().Diameter);
            Assert.Contains(pe.Holes, h => h.Group == HoleGroup.ParafusosExtratoraCabeca);

            LibraryPart coupling = plan.LibraryParts.Single();
            Assert.Equal("8760.019.par", coupling.FileName);
            Assert.Equal(pe.TopW, coupling.T[2], 3);
            Assert.Equal(1, coupling.R[8], 6);                               // o eixo do engate no Z do molde
        }

        [Fact]
        public void Babyplast_pecaMaiorQue75_reprova()
        {
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(60, 60, 10, 10, 5), new MoldBaseOptions { Family = MoldBaseFamily.Babyplast });
            Assert.Contains(plan.Checks, c => c.Name == "Área" && c.Level == CheckLevel.Fail);
        }

        [Fact]
        public void Zamak_comoNoPadraoMini()
        {
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(80, 60, 15, 15, 3), new MoldBaseOptions { Family = MoldBaseFamily.Zamak });
            Assert.True(plan.Ready, plan.Problem);
            Assert.DoesNotContain(plan.Checks, c => c.Level == CheckLevel.Fail);
            Assert.Equal(39, plan.P1);
            Assert.Equal(40, plan.P2);
            Assert.Equal(13, plan.Stroke.Stroke);                            // 3 + 10 → o mínimo do padrão
            PlateSpec esp = plan.Plates.Single(p => p.Plate == MoldPlate.ESP), pbs = plan.Plates.Single(p => p.Plate == MoldPlate.PBS);
            PlateSpec cpe = plan.Plates.Single(p => p.Plate == MoldPlate.CPE), pbi = plan.Plates.Single(p => p.Plate == MoldPlate.PBI);
            Assert.Equal(45, esp.Thickness);
            Assert.Equal(2, esp.Count);
            Assert.Equal(220, pbs.Width);
            Assert.Equal(200, pbs.Length);
            Assert.Equal(20, pbs.Pocket.Depth);                              // janela passante
            Assert.Equal(-85, pbi.TopW, 3);                                  // extratoras sobre o cavalete
            Assert.Equal(-53, cpe.TopW, 3);

            HoleSpec guides = plan.Plates.Single(p => p.Plate == MoldPlate.P1).Holes.Single(h => h.Group == HoleGroup.Colunas);
            Assert.Equal(36.4, guides.DepthMm, 3);
            Assert.Contains(guides.Points, q => q[1] == 57.5);               // 1 mm fora de centro: anti-inversão
            Assert.Equal(3, esp.Holes.Single(h => h.Group == HoleGroup.ParafusosBase).Count);
            Assert.Equal(1, esp.Holes.Single(h => h.Group == HoleGroup.ParafusosMolde).Side);

            ComponentSpec pin = Comp(plan, "PR");
            Assert.Equal("PR 08 x 66.4", pin.Code);                          // MÓVEL 40 + curso 13 + 13,4 cravados
            Assert.Equal(4, pin.Positions.Count);
        }

        [Fact]
        public void FurosDadosUmAUm_formaMudaComOsCentros()
        {
            var a = new HoleSpec { Points = new System.Collections.Generic.List<double[]> { new[] { -51.0, -55.5 }, new[] { 51.0, 57.5 } }, Diameter = 12 };
            var b = new HoleSpec { Points = new System.Collections.Generic.List<double[]> { new[] { -51.0, -56.5 }, new[] { 51.0, 56.5 } }, Diameter = 12 };
            Assert.Equal(2, a.Count);
            Assert.False(a.OnCenterLine);
            Assert.NotEqual(MoldBasePlateModeler.ShapeCode(a), MoldBasePlateModeler.ShapeCode(b));
            var line = new HoleSpec { AcrossA = 0, AlongC = 100, Diameter = 8, Middle = true };
            Assert.Equal(3, MoldBasePlateModeler.HolePositions(line).Count);
        }

        [Fact]
        public void Degrau_volume()
        {
            var p = new PlateSpec { Width = 76, Length = 76, Thickness = 40, Step = new StepSpec { Inset = 0.5, Depth = 21 } };
            Assert.Equal(-(76 * 76 - 75 * 75) * 21.0, MoldBasePlateModeler.FeatureVolume(p), 3);
        }

        [Fact]
        public void SemBiblioteca_quandoDesmarcado()
        {
            foreach (MoldBaseFamily f in new[] { MoldBaseFamily.Mud, MoldBaseFamily.Babyplast })
                Assert.Empty(MoldBasePlanner.Plan(Box(30, 30, 12, 10, 8), new MoldBaseOptions { Family = f, WithGuides = false }).LibraryParts);
        }

        [Fact]
        public void RecursosDaPlaca_volumeECaixa()
        {
            var p = new PlateSpec
            {
                Width = 170, Length = 246, Thickness = 32,
                Boss = new BossSpec { Width = 110, Length = 186, Height = 5, OnBottom = true },
                Groove = new GrooveSpec { Depth = 7, Z0 = 12, Z1 = 40 },
            };
            Assert.Equal(110 * 186 * 5 - 2 * 7 * (32 - 12) * 246, MoldBasePlateModeler.FeatureVolume(p), 3);
            Assert.Equal(5, MoldBasePlateModeler.BelowBottom(p));
            Assert.Equal(0, MoldBasePlateModeler.AboveTop(p));
            var q = new PlateSpec { Width = 75, Length = 75, Thickness = 40, Corners = new CornerSpec { Size = 11.5, Height = 4.1, OnBottom = true } };
            Assert.Equal(4 * 11.5 * 11.5 * 4.1, MoldBasePlateModeler.FeatureVolume(q), 3);
            Assert.Equal(4.1, MoldBasePlateModeler.BelowBottom(q), 6);
        }

        [Fact]
        public void FurosNoMeioDosLadosDeC()
        {
            var h = new HoleSpec { AcrossA = 144, AlongC = 184, Diameter = 8, Middle = true };
            var pos = MoldBasePlateModeler.HolePositions(h);
            Assert.Equal(6, pos.Count);
            Assert.Equal(6, h.Count);
            Assert.Contains(pos, q => q[0] == 72 && q[1] == 0);
            Assert.Contains(pos, q => q[0] == -72 && q[1] == 0);
        }

        [Fact]
        public void ComponenteRedondo_volumeDaUniao()
        {
            var bush = new ComponentSpec { Code = "BG 24 036" };
            bush.Solids.Add(new RoundSegment(30, -53, 0));
            bush.Solids.Add(new RoundSegment(35, -36, -30));
            bush.Bore = new RoundSegment(24, -53, 0);
            double want = System.Math.PI / 4 * ((30 * 30 - 24 * 24) * 53 + (35 * 35 - 30 * 30) * 6);
            Assert.Equal(want, MoldBaseComponentModeler.ExpectedVolume(bush), 3);
        }

        [Fact]
        public void BolsaoDescontaOsFurosQueCaemDentro()
        {
            var plate = new PlateSpec { Width = 170, Length = 246, Thickness = 87, Pocket = new PocketSpec { Width = 112, Length = 188, Depth = 44 } };
            var h = new HoleSpec { AcrossA = 88, AlongC = 154, Diameter = 10, Depth = HoleDepthKind.Through };
            Assert.Equal(4 * System.Math.PI * 25 * 44, MoldBasePlateModeler.PocketOverlap(plate, h), 3);
        }

        [Fact]
        public void ReconheceOQueEDoBotao()
        {
            Assert.True(MoldBaseBuilder.IsGeneratedComponent("CG 24 046 046.par"));
            Assert.True(MoldBaseBuilder.IsGeneratedComponent("PR 14 x 132.par"));
            Assert.False(MoldBaseBuilder.IsGeneratedComponent("Olhal M12.par"));
            Assert.True(MoldBaseBuilder.IsGeneratedPlate("P1 - Placa Cavidade Fixa.par"));
            Assert.True(MoldBaseBuilder.IsGeneratedPlate("201 - Placa Suporte.par"));
            Assert.False(MoldBaseBuilder.IsGeneratedPlate("15335.101.par"));
            Assert.True(MoldBaseBuilder.IsStandardBase(@"\\servidor02\ferramentaria$\09 - Projetos\Projetos Atuais\01 - PORTA MOLDES\9220 - PORTA MOLDE 1525\9220R0.asm"));
            Assert.True(MoldBaseBuilder.IsStandardBase(@"W:\01 - Biblioteca\Componentes\Guias Moldes de Gaveta\8760.001v3.par"));
            Assert.False(MoldBaseBuilder.IsStandardBase(@"U:\Moldes\MD-15335\15335.100.par"));
        }
    }

    internal static class PlanExtensions
    {
        /// <summary>Todas as posições dos componentes (A, C).</summary>
        public static System.Collections.Generic.IEnumerable<double[]> Positions(this MoldBasePlan p) =>
            p.Components.SelectMany(c => c.Positions);
    }
}
