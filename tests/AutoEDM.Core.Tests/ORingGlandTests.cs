using System.Linq;
using AutoEDM.Sealing;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// O alojamento visto com a peça CONJUGADA: furo que recebe o eixo, eixo que entra no furo,
    /// tampa que fecha a face — e com as tolerâncias. Tudo REGIME DA TABELA (seção 1,78 / 2,62,
    /// estático), salvo onde o teste diz que é o modo "pelo anel".
    /// </summary>
    public class ORingGlandTests
    {
        private static ORingSize Ring(double d1, double d2, string code = null) =>
            new ORingSize { InnerDiameter = d1, CrossSection = d2, Series = "G", Code = code, Verified = true };

        private static ORingGlandContext Ctx(double? mating = null, GrooveSizing sizing = GrooveSizing.Table,
            double faceGap = 0, string fit = IsoFits.DefaultFit) =>
            new ORingGlandContext { MatingDiameterMm = mating, Sizing = sizing, FaceGapMm = faceGap, Fit = fit };

        // ------------------------------------------------------------- o caso de 2026-10-21

        /// <summary>
        /// Eixo Ø6 com o 2-009 (d1 5,28) — o anel "maior" que o Carlos escolheu. Pela tabela o
        /// fundo fica no Ø3,40 e o anel sobra 35,6 %: antes saía só como aviso e foi cortado.
        /// </summary>
        [Fact]
        public void AnelMuitoFolgado_noEixo_eReprovado()
        {
            var spec = ORingGrooveCalculator.Compute(Ring(5.28, 1.78, "2-009"), GrooveKind.RadialExternal,
                6.0, SealMotion.Static, Elastomer.Nbr);

            Assert.Equal(3.40, spec.GrooveBottomDiameter, 3);
            Assert.False(spec.IsWithinStandard);
            Assert.Contains(spec.Issues, i => i.Level == GrooveIssueLevel.Error && i.Message.Contains("FOLGADO"));
        }

        /// <summary>
        /// Pelo anel, o fundo sobe até onde o 2-009 aceita (5 % de folga) — o canal fica raso,
        /// mas aí o eixo e o furo no mesmo Ø esmagam o anel demais. O cálculo mostra isso em vez
        /// de cortar o canal da tabela calado.
        /// </summary>
        [Fact]
        public void PeloAnel_fundoSegueOAnel_dentroDaFolgaMaxima()
        {
            var spec = ORingGrooveCalculator.Compute(Ring(5.28, 1.78), GrooveKind.RadialExternal,
                6.0, SealMotion.Static, Elastomer.Nbr, context: Ctx(sizing: GrooveSizing.FromRing));

            Assert.Equal(5.28 * (1 - ORingGrooveRules.MaxSlack), spec.GrooveBottomDiameter, 4);
            Assert.Equal((6.0 - spec.GrooveBottomDiameter) / 2, spec.Depth, 4);
            Assert.True(spec.Depth < 1.30);                       // mais raso que o da tabela
            Assert.True(spec.Squeeze > 0.33);                    // e o esmagamento acusa
            Assert.Contains("PELO ANEL", spec.Source);
        }

        [Fact]
        public void PeloAnel_comAnelQueJaServe_daOMesmoCanalDaTabela()
        {
            // Eixo Ø25, cordão 2,62: fundo da tabela 20,80; o d1 20,24 estica 2,8 % — já serve.
            var table = ORingGrooveCalculator.Compute(Ring(20.24, 2.62), GrooveKind.RadialExternal, 25.0,
                SealMotion.Static, Elastomer.Nbr);
            var fromRing = ORingGrooveCalculator.Compute(Ring(20.24, 2.62), GrooveKind.RadialExternal, 25.0,
                SealMotion.Static, Elastomer.Nbr, context: Ctx(sizing: GrooveSizing.FromRing));

            Assert.Equal(table.GrooveBottomDiameter, fromRing.GrooveBottomDiameter, 4);
            Assert.Equal(table.Width, fromRing.Width, 4);
        }

        /// <summary>
        /// Eixo Ø6, cordão 1,78: pela tabela nenhum anel serve (o 2-007 sobra 7,6 %). O ranking
        /// tenta então o canal PELO ANEL: o fundo sobe para o 2-007 assentar e o esmagamento
        /// continua dentro da faixa — e é esse que vem recomendado.
        /// </summary>
        [Fact]
        public void Rank_semAnelNaTabela_recomendaOCanalPeloAnel()
        {
            var ranked = ORingGrooveCalculator.Rank(ORingCatalog.BuiltInSeriesG(), GrooveKind.RadialExternal, 6.0,
                SealMotion.Static, Elastomer.Nbr, 1.78);
            var best = ranked.First();

            Assert.True(best.Spec.IsWithinStandard, best.Spec.Describe());
            Assert.Equal(GrooveSizing.FromRing, best.Spec.Sizing);
            Assert.Equal(3.68 * (1 - ORingGrooveRules.MaxSlack), best.Spec.GrooveBottomDiameter, 3);
            Assert.InRange(best.Spec.Squeeze, 0.20, 0.33);
        }

        // ------------------------------------------------------------- o conjugado

        /// <summary>
        /// Furo conjugado MAIOR que o eixo: a altura do alojamento é medida a partir do furo, então
        /// o fundo sobe e o canal cortado no eixo fica raso — o esmagamento é o mesmo da tabela.
        /// </summary>
        [Fact]
        public void FuroConjugadoMaior_canalFicaRaso_eEsmagamentoSeMantem()
        {
            var same = ORingGrooveCalculator.Compute(Ring(4.47, 1.78), GrooveKind.RadialExternal, 6.0,
                SealMotion.Static, Elastomer.Nbr);
            var loose = ORingGrooveCalculator.Compute(Ring(4.47, 1.78), GrooveKind.RadialExternal, 6.0,
                SealMotion.Static, Elastomer.Nbr, context: Ctx(mating: 7.0));

            Assert.Equal(7.0 - 2 * 1.30, loose.GrooveBottomDiameter, 3);   // 4,40
            Assert.Equal(0.80, loose.Depth, 3);                            // cortado no eixo
            Assert.Equal(1.30, loose.GlandDepth, 3);                       // até o furo
            Assert.Equal(same.Squeeze, loose.Squeeze, 2);
            Assert.Equal(1.0, loose.NominalClearance, 3);
            Assert.Contains(loose.Issues, i => i.Level == GrooveIssueLevel.Warning && i.Message.Contains("jogo diametral"));
        }

        /// <summary>Seção métrica fora da tabela (2,40): o limite da folga vem interpolado — antes não avisava.</summary>
        [Fact]
        public void FolgaGrande_secaoForaDaTabela_avisaPeloLimiteInterpolado()
        {
            var spec = ORingGrooveCalculator.Compute(Ring(18.3, 2.40), GrooveKind.RadialExternal, 22.0,
                SealMotion.Static, Elastomer.Nbr, context: Ctx(mating: 22.5));
            Assert.Contains(spec.Issues, i => i.Level == GrooveIssueLevel.Warning &&
                                              i.Message.Contains("jogo diametral") && i.Message.Contains("estimado pela seção"));
        }

        /// <summary>Vedação dinâmica: folga acima do limite é ERRO (o anel morde), não só aviso.</summary>
        [Fact]
        public void FolgaGrande_dinamica_eErro()
        {
            var spec = ORingGrooveCalculator.Compute(Ring(18.72, 2.62), GrooveKind.RadialExternal, 22.0,
                SealMotion.Reciprocating, Elastomer.Nbr, context: Ctx(mating: 22.5));
            Assert.Contains(spec.Issues, i => i.Level == GrooveIssueLevel.Error && i.Message.Contains("DINÂMICA"));
        }

        [Fact]
        public void LimiteDeFolga_interpolaEntreSecoes()
        {
            Assert.Equal(0.13, ORingHousingTable.ClearanceLimit(2.40), 3);
            Assert.Equal(0.145, ORingHousingTable.ClearanceLimit((2.62 + 3.53) / 2), 3);
            Assert.Equal(0.20, ORingHousingTable.ClearanceLimit(8.0), 3);
        }

        [Fact]
        public void FuroConjugadoMaior_rankEscolheAnelParaOFundoReal()
        {
            var catalog = ORingCatalog.BuiltInSeriesG();
            var ranked = ORingGrooveCalculator.Rank(catalog, GrooveKind.RadialExternal, 6.0,
                SealMotion.Static, Elastomer.Nbr, 1.78, context: Ctx(mating: 7.0));

            var best = ranked.First();
            Assert.Equal(4.40, best.Spec.GrooveBottomDiameter, 3);
            Assert.True(best.Spec.IsWithinStandard);
            Assert.InRange(best.Ring.InnerDiameter, 4.40 / 1.05, 4.40 * 1.05);
        }

        [Fact]
        public void FolgaMaiorQueOAlojamento_naoSobraCanal()
        {
            var spec = ORingGrooveCalculator.Compute(Ring(3.68, 1.78), GrooveKind.RadialExternal, 6.0,
                SealMotion.Static, Elastomer.Nbr, context: Ctx(mating: 8.8));

            Assert.True(spec.Depth <= 0);
            Assert.Contains(spec.Issues, i => i.Level == GrooveIssueLevel.Error && i.Message.Contains("não sobra canal"));
        }

        [Fact]
        public void ConjugadoQueInterfere_eErro()
        {
            var spec = ORingGrooveCalculator.Compute(Ring(3.68, 1.78), GrooveKind.RadialExternal, 6.0,
                SealMotion.Static, Elastomer.Nbr, context: Ctx(mating: 5.9));
            Assert.Contains(spec.Issues, i => i.Level == GrooveIssueLevel.Error && i.Message.Contains("INTERFERE"));
        }

        [Fact]
        public void CanalNoFuro_eixoConjugadoMenor_fundoMedidoDoEixo()
        {
            // Furo Ø30 na peça, eixo conjugado Ø29,6: fundo = 29,6 + 2·2,10 = 33,80.
            var spec = ORingGrooveCalculator.Compute(Ring(29.0, 2.62), GrooveKind.RadialInternal, 30.0,
                SealMotion.Static, Elastomer.Nbr, context: Ctx(mating: 29.6));

            Assert.Equal(33.80, spec.GrooveBottomDiameter, 3);
            Assert.Equal(1.90, spec.Depth, 3);
            Assert.Equal(2.10, spec.GlandDepth, 3);
            Assert.Equal(0.40, spec.NominalClearance, 3);
        }

        [Fact]
        public void CanalDeFace_vaoEntreAsFaces_cortaMenos()
        {
            var shut = ORingGrooveCalculator.Compute(Ring(20.0, 2.62), GrooveKind.AxialFace, 25.0,
                SealMotion.Static, Elastomer.Nbr);
            var gap = ORingGrooveCalculator.Compute(Ring(20.0, 2.62), GrooveKind.AxialFace, 25.0,
                SealMotion.Static, Elastomer.Nbr, context: Ctx(faceGap: 0.3));

            Assert.Equal(shut.Depth - 0.3, gap.Depth, 4);
            Assert.Equal(shut.GlandDepth, gap.GlandDepth, 4);
            Assert.Equal(shut.Squeeze, gap.Squeeze, 4);
        }

        // ------------------------------------------------------------- pior caso

        [Theory]
        [InlineData("H7", 6.0, 0.0, 0.012)]
        [InlineData("g6", 6.0, -0.012, -0.004)]      // Ø6 é da faixa 3–6 ("até e inclusive")
        [InlineData("g6", 6.01, -0.014, -0.005)]
        [InlineData("H8", 25.0, 0.0, 0.033)]
        [InlineData("f7", 25.0, -0.041, -0.020)]
        [InlineData("h6", 100.0, -0.022, 0.0)]
        public void Iso286_afastamentosDaTabela(string zone, double d, double lo, double hi)
        {
            Assert.True(IsoFits.TryDeviations(zone, d, out double l, out double h));
            Assert.Equal(lo, l, 4);
            Assert.Equal(hi, h, 4);
        }

        [Fact]
        public void PiorCaso_abreAFaixaEmVoltaDoNominal()
        {
            var spec = ORingGrooveCalculator.Compute(Ring(3.68, 1.78), GrooveKind.RadialExternal, 6.0,
                SealMotion.Static, Elastomer.Nbr);
            var wc = spec.WorstCase;

            Assert.NotNull(wc);
            Assert.Equal("H7/g6", wc.Fit);
            Assert.True(wc.SqueezeMin < spec.Squeeze && spec.Squeeze < wc.SqueezeMax);
            Assert.True(wc.StretchMin < spec.Stretch && spec.Stretch < wc.StretchMax);
            Assert.True(wc.FillMax > spec.Fill);
            Assert.Equal(0.004, wc.ClearanceMin, 4);    // 6 H7/g6
            Assert.Equal(0.024, wc.ClearanceMax, 4);
        }

        [Fact]
        public void PiorCaso_semAjuste_naoCalcula()
        {
            var spec = ORingGrooveCalculator.Compute(Ring(3.68, 1.78), GrooveKind.RadialExternal, 6.0,
                SealMotion.Static, Elastomer.Nbr, context: Ctx(fit: ""));
            Assert.Null(spec.WorstCase);
        }

        // ------------------------------------------------------------- conjugado lido em contexto

        private static MatingCylinder Cyl(double d, double offset = 0, double cos = 1, bool covers = true, string owner = "P") =>
            new MatingCylinder { DiameterMm = d, AxisOffsetMm = offset, AxisCos = cos, CoversGroove = covers, Owner = owner };

        [Fact]
        public void Conjugado_eixo_pegaOMenorFuroQueEnvolve()
        {
            var pick = ORingMatingReader.Pick(GrooveKind.RadialExternal, 6.0, new[]
            {
                Cyl(5.0, owner: "pino"),          // dentro do eixo: não é conjugado
                Cyl(6.05, owner: "bucha"),        // o furo
                Cyl(6.60, owner: "tampa"),        // mais longe
                Cyl(6.02, offset: 0.3),           // não coaxial
                Cyl(6.01, covers: false),         // não cobre o canal
                Cyl(6.03, cos: 0.99)              // inclinado
            });
            Assert.Equal("bucha", pick.Owner);
        }

        [Fact]
        public void Conjugado_furo_pegaOMaiorEixoQueCabe()
        {
            var pick = ORingMatingReader.Pick(GrooveKind.RadialInternal, 30.0, new[]
            {
                Cyl(29.95, owner: "eixo"), Cyl(28.0, owner: "ressalto"), Cyl(31.0, owner: "camisa")
            });
            Assert.Equal("eixo", pick.Owner);
        }

        [Fact]
        public void Conjugado_longeDemais_naoEConjugado()
        {
            // Ø6 → alcance de 1 mm no diâmetro: um furo Ø8 é outra coisa (uma câmara, um rebaixo).
            Assert.Null(ORingMatingReader.Pick(GrooveKind.RadialExternal, 6.0, new[] { Cyl(8.0) }));
        }

        // ------------------------------------------------------------- o canal cabe na face?

        private static ORingTarget Shaft(double faceLength) => new ORingTarget
        {
            Ok = true, EdgeDiameterMm = 6.0, SealingDiameterMm = 6.0, EdgeAxialMm = 0.0, MaterialDirection = 1,
            FaceAxialMinMm = 0.0, FaceAxialMaxMm = faceLength
        };

        /// <summary>O canal de 2,5 mm a 1 mm da aresta — corte de 2026-10-21, saiu aberto na ponta.</summary>
        [Fact]
        public void Afastamento_menorQueMeiaLargura_abreOCanalNaAresta()
        {
            var spec = ORingGrooveCalculator.Compute(Ring(3.68, 1.78), GrooveKind.RadialExternal, 6.0,
                SealMotion.Static, Elastomer.Nbr);
            ORingGrooveCalculator.CheckPlacement(spec, Shaft(10.0), 1.0);

            Assert.Contains(spec.Issues, i => i.Level == GrooveIssueLevel.Error && i.Message.Contains("ABERTO na ponta"));
        }

        [Fact]
        public void Canal_alemDoFimDaFace_eErro()
        {
            var spec = ORingGrooveCalculator.Compute(Ring(3.68, 1.78), GrooveKind.RadialExternal, 6.0,
                SealMotion.Static, Elastomer.Nbr);
            ORingGrooveCalculator.CheckPlacement(spec, Shaft(5.0), 5.0);

            Assert.Contains(spec.Issues, i => i.Level == GrooveIssueLevel.Error && i.Message.Contains("fim da face"));
        }

        [Fact]
        public void Canal_inteiroNaFace_semApontamento()
        {
            var spec = ORingGrooveCalculator.Compute(Ring(3.68, 1.78), GrooveKind.RadialExternal, 6.0,
                SealMotion.Static, Elastomer.Nbr);
            int before = spec.Issues.Count;
            ORingGrooveCalculator.CheckPlacement(spec, Shaft(10.0), 5.0);
            Assert.Equal(before, spec.Issues.Count);
        }
    }
}
