using System.Linq;
using AutoEDM.Mold.MoldBase;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// Escolha do porta-molde (Carlos, 2026-10-23): série pela área útil, sub-série pela altura das peças +
    /// fundo, curso pela altura a extrair + folga; a pilha de placas e as conferências.
    /// </summary>
    public class MoldBasePlannerTests
    {
        private static MoldBaseContent Box(double x, double y, double up, double down, double product = 0) => new MoldBaseContent
        {
            Min1 = -x / 2, Max1 = x / 2, Min2 = -y / 2, Max2 = y / 2, MinW = -down, MaxW = up, Count = 1, ProductHeight = product,
        };

        [Fact]
        public void EscolheAMenorSerieQueCabe()
        {
            // 150 × 150 + 20 por lado = 190 × 190 → 25.25 tem X 156: não; a primeira com X ≥ 190 e Y ≥ 190 pela área.
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(150, 150, 30, 30, 20), new MoldBaseOptions());
            Assert.True(plan.Ready, plan.Problem);
            Assert.True(plan.Series.X >= 190 && plan.Series.Y >= 190);
            double best = PolimoldCatalog.All.Where(s => s.X >= 190 && s.Y >= 190).Min(s => s.PlateArea);
            Assert.Equal(best, plan.Series.PlateArea);
            Assert.False(plan.HasFailures, string.Join("\n", plan.Checks));
        }

        [Fact]
        public void PecasCompridas_noPrimeiroEixo_giramOPortaMolde()
        {
            // 300 no 1º eixo, 100 no 2º: sem girar, A teria de comportar 340 (só a 45.xx+); girado, C comporta.
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(300, 100, 20, 20, 15), new MoldBaseOptions());
            Assert.True(plan.Rotated);
            MoldBasePlan straight = MoldBasePlanner.Plan(Box(300, 100, 20, 20, 15), new MoldBaseOptions { AllowRotation = false });
            Assert.False(straight.Rotated);
            Assert.True(plan.Series.PlateArea < straight.Series.PlateArea);
        }

        [Fact]
        public void Espessuras_eSubSerie()
        {
            // 30.35 (faixa 27…96): 26 acima + 20 de fundo = 46; 50 abaixo + 20 = 70 → 76.
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(150, 150, 26, 50, 20), new MoldBaseOptions { ForcedSeries = "30.35" });
            Assert.Equal(46, plan.P1);
            Assert.Equal(76, plan.P2);
            Assert.Equal(PolimoldCatalog.SubSeries(plan.Series, 46, 76), plan.SubSeries);
        }

        [Fact]
        public void Curso_oMenorQueAtende()
        {
            // 30.35: cursos 13 / 33 / 53. Produto 20 + folga 10 = 30 → curso 2 (33), espaçadores 76.
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(150, 150, 26, 26, 20), new MoldBaseOptions { ForcedSeries = "30.35" });
            Assert.Equal(2, plan.Stroke.Index);
            Assert.Equal(76, plan.Stroke.SpacerHeight);
            Assert.Equal("3035 4 " + plan.SubSeries.ToString("00") + " 1 2 5B", plan.OrderCode);
        }

        [Fact]
        public void CursoMaiorQueOsDaSerie_subirDeSerie()
        {
            // Produto de 60 + 10 = 70: a 30.35 (máx. 53) não serve; a escolha automática sobe.
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(150, 150, 30, 30, 60), new MoldBaseOptions());
            Assert.True(plan.Stroke.Stroke >= 70);
        }

        [Fact]
        public void SerieForcadaPequena_reprovaAArea()
        {
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(250, 250, 20, 20, 10), new MoldBaseOptions { ForcedSeries = "20.20" });
            Assert.True(plan.Ready);
            Assert.Contains(plan.Checks, c => c.Name == "Área útil" && c.Level == CheckLevel.Fail);
        }

        [Fact]
        public void Pilha_encostadaNaPartição_eEspacadoresComOCurso()
        {
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(150, 150, 26, 26, 20), new MoldBaseOptions { ForcedSeries = "30.35", ForcedP1 = 46, ForcedP2 = 46, ForcedStroke = 2 });
            PlateSpec P(MoldPlate p) => plan.Plates.Single(x => x.Plate == p);
            PolimoldSeries s = plan.Series;
            Assert.Equal(0, P(MoldPlate.P1).BottomW);
            Assert.Equal(0, P(MoldPlate.P2).TopW);
            Assert.Equal(P(MoldPlate.P1).TopW, P(MoldPlate.PBS).BottomW);
            Assert.Equal(P(MoldPlate.PS).TopW, P(MoldPlate.P2).BottomW);
            Assert.Equal(P(MoldPlate.ESP).TopW, P(MoldPlate.PS).BottomW);
            Assert.Equal(P(MoldPlate.PBI).TopW, P(MoldPlate.ESP).BottomW);
            // Curso = vão entre a porta-extratores e a placa suporte.
            Assert.Equal(plan.Stroke.Stroke, P(MoldPlate.PS).BottomW - P(MoldPlate.CPE).TopW, 3);
            Assert.Equal(MoldBasePlanner.StopButtonHeight, P(MoldPlate.PE).BottomW - P(MoldPlate.PBI).TopW, 3);
            // Tipo 4: placas base com a largura B (aba).
            Assert.Equal(s.B, P(MoldPlate.PBS).Width);
            Assert.Equal(s.P, P(MoldPlate.CPE).Width);
            Assert.Equal(2, P(MoldPlate.ESP).Count);
            Assert.Equal(46 + 46 + s.PBS + s.PS + 76 + s.PBI, plan.TotalHeight, 3);
        }

        [Fact]
        public void Montagem5B_furosNasPlacasCertas()
        {
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(150, 150, 26, 26, 20), new MoldBaseOptions { ForcedSeries = "30.35" });
            HoleGroup[] Groups(MoldPlate p) => plan.Plates.Single(x => x.Plate == p).Holes.Select(h => h.Group).ToArray();
            Assert.Contains(HoleGroup.Colunas, Groups(MoldPlate.PBS));
            Assert.Contains(HoleGroup.Colunas, Groups(MoldPlate.PS));
            Assert.DoesNotContain(HoleGroup.Colunas, Groups(MoldPlate.PBI));
            Assert.Contains(HoleGroup.PinosRetorno, Groups(MoldPlate.P2));
            Assert.DoesNotContain(HoleGroup.PinosRetorno, Groups(MoldPlate.P1));
            Assert.Contains(HoleGroup.GuiasExtratora, Groups(MoldPlate.PS));      // montagem 5: guias saem da PS
            Assert.DoesNotContain(HoleGroup.GuiasExtratora, Groups(MoldPlate.PBI));
        }

        [Fact]
        public void Montagem1A_semPinosNemGuias()
        {
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(150, 150, 26, 26, 20), new MoldBaseOptions { ForcedSeries = "30.35", Mounting = 1, Guiding = 'A' });
            Assert.DoesNotContain(plan.Plates.SelectMany(p => p.Holes), h => h.Group == HoleGroup.PinosRetorno || h.Group == HoleGroup.GuiasExtratora);
            Assert.DoesNotContain(plan.Plates.Single(p => p.Plate == MoldPlate.PBS).Holes, h => h.Group == HoleGroup.Colunas);
        }

        [Fact]
        public void Refrigeracao_acusaQuandoNaoHaCorredor()
        {
            // Peças quase do tamanho da área útil da 30.35 (206 × 196): o canal não passa entre elas e as colunas.
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(200, 190, 26, 26, 20), new MoldBaseOptions { ForcedSeries = "30.35", SideMarginMm = 0 });
            Assert.Contains(plan.Checks, c => c.Name.StartsWith("Refrigeração") && c.Level == CheckLevel.Warning);
            MoldBasePlan roomy = MoldBasePlanner.Plan(Box(80, 80, 26, 26, 20), new MoldBaseOptions { ForcedSeries = "30.35" });
            Assert.All(roomy.Checks.Where(c => c.Name.StartsWith("Refrigeração")), c => Assert.Equal(CheckLevel.Ok, c.Level));
        }

        [Fact]
        public void CentroNaOrigem_contaODeslocamentoDasPecas()
        {
            // Peças de 100 deslocadas +60 no 1º eixo: centradas na origem pedem 2·(110 + 20) = 260 em A.
            var content = new MoldBaseContent { Min1 = 10, Max1 = 110, Min2 = -50, Max2 = 50, MinW = -20, MaxW = 20, Count = 1, ProductHeight = 10 };
            MoldBasePlan origin = MoldBasePlanner.Plan(content, new MoldBaseOptions { AllowRotation = false });
            MoldBasePlan centered = MoldBasePlanner.Plan(content, new MoldBaseOptions { AllowRotation = false, Centering = MoldBaseCentering.ContentCenter });
            Assert.Equal(260, origin.NeededX, 3);
            Assert.Equal(140, centered.NeededX, 3);
            Assert.Equal(60, centered.Center1, 3);
        }

        [Fact]
        public void SemProduto_avisaQueOCursoFoiEstimado()
        {
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(100, 100, 25, 25), new MoldBaseOptions());
            Assert.Contains(plan.Notes, n => n.Contains("estimada"));
            Assert.Equal(25 + 10, plan.NeededStroke, 3);
        }

        [Fact]
        public void Espacador_furadoNoPf2_eNasColunasSoNoColunamentoC()
        {
            MoldBasePlan b = MoldBasePlanner.Plan(Box(150, 150, 26, 26, 20), new MoldBaseOptions { ForcedSeries = "25.35" });
            PlateSpec esp = b.Plates.Single(p => p.Plate == MoldPlate.ESP);
            HoleSpec pf2 = esp.Holes.Single();
            Assert.Equal(HoleGroup.ParafusosMolde, pf2.Group);
            // 25.35: F/2 = 97, espaçador centrado em A/2 − ESP/2 = 101,5 → furo a −4,5 do centro da peça.
            Assert.Equal(-1, pf2.Side);
            Assert.Equal(9, pf2.AcrossA, 3);
            Assert.Equal(2, pf2.Count);
            MoldBasePlan c = MoldBasePlanner.Plan(Box(150, 150, 26, 26, 20), new MoldBaseOptions { ForcedSeries = "25.35", Guiding = 'C' });
            Assert.Contains(c.Plates.Single(p => p.Plate == MoldPlate.ESP).Holes, h => h.Group == HoleGroup.Colunas);
        }

        [Fact]
        public void Pf3_passanteQuandoOCegoNaoCabeNaPortaExtratores()
        {
            // 15.15: CPE 9, M8 → 1,5·8 = 12 > 9 − 2.
            MoldBasePlan p = MoldBasePlanner.Plan(Box(40, 20, 10, 10, 5), new MoldBaseOptions { ForcedSeries = "15.15", SideMarginMm = 0 });
            Assert.Equal(HoleDepthKind.Through, p.Plates.Single(x => x.Plate == MoldPlate.CPE).Holes.Single(h => h.Group == HoleGroup.ParafusosExtratora).Depth);
        }

        [Fact]
        public void CursoForcado_comSerieAutomatica_sobeDeSerie()
        {
            // Curso 1 forçado e 30 mm pedidos: a série escolhida tem de ter curso 1 ≥ 30.
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(150, 150, 20, 20, 20), new MoldBaseOptions { ForcedStroke = 1 });
            Assert.Equal(1, plan.Stroke.Index);
            Assert.True(plan.Stroke.Stroke >= 30);
        }

        [Fact]
        public void EspessuraForaDoCatalogo_reprova_eCodigoNaoInventaSubSerie()
        {
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(150, 150, 20, 20, 20), new MoldBaseOptions { ForcedSeries = "35.40", ForcedP1 = 27 });
            Assert.Contains(plan.Checks, c => c.Name == "Sub-série" && c.Level == CheckLevel.Fail);
            Assert.Contains(" ?? ", plan.OrderCode);
        }

        [Fact]
        public void PastaDoPortaMolde_eDaMontagem()
        {
            Assert.Equal(@"W:\MD-15335\Porta Molde - 15335", MoldBaseBuilder.FolderFor(@"W:\MD-15335\15335.asm"));
            Assert.NotEqual(MoldBaseBuilder.FolderFor(@"W:\MD-15335\15335.asm"), MoldBaseBuilder.FolderFor(@"W:\MD-15335\15335_EDM.asm"));
        }

        [Fact]
        public void EscolhasDoPortaMolde_IdaEVoltaNoJsonDoProjeto()
        {
            string store = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "autoedm-moldbase-" + System.Guid.NewGuid() + ".json");
            try
            {
                var choices = new MoldBaseOptions { Guiding = 'C', PartingMm = -12.5, ForcedSeries = "35.40", Type = 2, FixedSideNegative = true };
                new AutoEDM.Mold.MoldProjectSettings { MoldBaseChoices = choices }.Save(@"W:\MD-15335\15335.asm", store);
                MoldBaseOptions back = AutoEDM.Mold.MoldProjectSettings.Load(@"W:\MD-15335\15335.asm", store).MoldBaseChoices;
                Assert.NotNull(back);
                Assert.Equal('C', back.Guiding);
                Assert.Equal(-12.5, back.PartingMm);
                Assert.Equal("35.40", back.ForcedSeries);
                Assert.Equal(2, back.Type);
                Assert.True(back.FixedSideNegative);
                Assert.Null(AutoEDM.Mold.MoldProjectSettings.Load(@"W:\MD-1\1.asm", store).MoldBaseChoices);
            }
            finally { if (System.IO.File.Exists(store)) System.IO.File.Delete(store); }
        }

        [Fact]
        public void NadaCabe_explica()
        {
            MoldBasePlan plan = MoldBasePlanner.Plan(Box(900, 900, 20, 20, 10), new MoldBaseOptions());
            Assert.False(plan.Ready);
            Assert.Contains("Nenhuma série", plan.Problem);
        }
    }
}
