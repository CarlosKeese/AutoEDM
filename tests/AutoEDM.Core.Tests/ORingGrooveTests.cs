using System;
using System.Linq;
using AutoEDM.Sealing;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// Testes do dimensionamento do alojamento de O'ring. Toda a engenharia do canal vive em
    /// código puro (o Solid Edge não participa de decidir cota), então ela é conferível aqui —
    /// o que sobra para a máquina do Carlos é só o corte.
    ///
    /// O dimensionamento tem DOIS REGIMES, e os testes estão separados por isso. Confundir os
    /// dois foi exatamente o que deixou 7 testes vermelhos quando a tabela real do catálogo
    /// entrou no lugar da estimativa por coeficientes:
    ///
    ///   * REGIME DA TABELA — seção tabelada (1,78 / 2,62 / 3,53 / 5,33 / 6,99 mm) em vedação
    ///     ESTÁTICA ou DINÂMICA. A cota vem LIDA da <see cref="ORingHousingTable"/> (catálogo
    ///     Parker 001-5 BR, p. 5). A âncora externa é a PÁGINA IMPRESSA, e o elastômero não
    ///     entra na conta do canal.
    ///   * REGIME DO CÁLCULO — seção fora da tabela (1,02 / 1,27 / 1,52 mm) ou vedação
    ///     ROTATIVA, que o catálogo não cobre. Aí valem os coeficientes de
    ///     <see cref="ORingGrooveRules"/> (esmagamento, preenchimento alvo, estiramento), e é
    ///     este regime — só ele — que os testes de coeficiente devem exercitar.
    ///
    /// Antes de mexer num teste daqui, pergunte DE QUAL REGIME é o caso. Um assert de
    /// coeficiente colado num caso tabelado vira âncora falsa: fica preso num número que o
    /// outro regime nunca vai produzir.
    /// </summary>
    public class ORingGrooveTests
    {
        private static ORingSize Ring(double d1, double d2, bool verified = true) =>
            new ORingSize { InnerDiameter = d1, CrossSection = d2, Series = "G", Verified = verified };

        // ---------------------------------------------------------------- geometria do canal

        /// <summary>
        /// REGIME DA TABELA. Os números abaixo são a transcrição da PÁGINA IMPRESSA (Parker
        /// 001-5 BR, p. 5): profundidade L (mín/máx) e corte do alojamento G (mín/máx). Estão
        /// repetidos aqui à mão DE PROPÓSITO — se o teste lesse a própria
        /// <see cref="ORingHousingTable"/>, só confirmaria a tabela contra ela mesma e um erro
        /// de digitação passaria batido.
        /// </summary>
        [Theory]
        //          seção  movimento                     L mín  L máx   G mín  G máx
        [InlineData(1.78, SealMotion.Static,             1.25,  1.35,   2.4,   2.6)]
        [InlineData(2.62, SealMotion.Static,             2.05,  2.15,   3.6,   3.8)]
        [InlineData(3.53, SealMotion.Static,             2.80,  2.95,   4.8,   5.0)]
        [InlineData(5.33, SealMotion.Static,             4.30,  4.50,   7.2,   7.4)]
        [InlineData(6.99, SealMotion.Static,             5.75,  5.95,   9.6,   9.8)]
        [InlineData(1.78, SealMotion.Reciprocating,      1.40,  1.45,   2.4,   2.6)]
        [InlineData(2.62, SealMotion.Reciprocating,      2.25,  2.30,   3.6,   3.8)]
        [InlineData(3.53, SealMotion.Reciprocating,      3.05,  3.10,   4.8,   5.0)]
        [InlineData(5.33, SealMotion.Reciprocating,      4.65,  4.75,   7.2,   7.4)]
        [InlineData(6.99, SealMotion.Reciprocating,      6.00,  6.10,   9.6,   9.8)]
        public void CanalTabelado_saiDaTabelaDeAlojamentoImpressa(double d2, SealMotion motion,
            double depthMin, double depthMax, double widthMin, double widthMax)
        {
            var spec = ORingGrooveCalculator.Compute(Ring(20.0, d2), GrooveKind.RadialExternal,
                25.0, motion, Elastomer.Nbr);

            // A cota de projeto é o MEIO da faixa publicada; a faixa inteira vai no relatório.
            Assert.Equal((depthMin + depthMax) / 2.0, spec.Depth, 3);
            Assert.Equal((widthMin + widthMax) / 2.0, spec.Width, 3);
            Assert.Equal(depthMin, spec.DepthMin, 3);
            Assert.Equal(depthMax, spec.DepthMax, 3);
            Assert.Equal(widthMin, spec.WidthMin, 3);
            Assert.Equal(widthMax, spec.WidthMax, 3);
            Assert.Contains("tabela de alojamento", spec.Source);

            // O canal real é ~1,40·W — NÃO os ~1,31·W que a estimativa antiga previa. É esta
            // linha que segura a diferença entre o catálogo e a fórmula que ele aposentou.
            Assert.InRange(spec.Width / d2, 1.36, 1.42);
        }

        /// <summary>
        /// REGIME DO CÁLCULO. As seções 1,02 / 1,27 / 1,52 mm existem no catálogo de ANÉIS
        /// (códigos 2-001 a 2-003) mas NÃO na tabela de alojamento; e vedação rotativa não é
        /// tabelada em seção nenhuma. Nesses casos manda o coeficiente — e o relatório tem de
        /// DIZER que foi cálculo, senão o operador acha que a cota veio de catálogo.
        /// </summary>
        [Theory]
        [InlineData(1.02, SealMotion.Static)]           // seção fora da tabela de alojamento
        [InlineData(1.52, SealMotion.Reciprocating)]
        [InlineData(2.62, SealMotion.Rotary)]           // seção tabelada, movimento que não é
        public void CanalNaoTabelado_seguerOsCoeficientesEAvisaQueFoiCalculo(double d2, SealMotion motion)
        {
            double sqMin, sqTarget, sqMax;
            ORingGrooveRules.Squeeze(motion, out sqMin, out sqTarget, out sqMax);

            var spec = ORingGrooveCalculator.Compute(Ring(20.0, d2), GrooveKind.RadialExternal,
                25.0, motion, Elastomer.Nbr);

            Assert.Equal(d2 * (1.0 - sqTarget), spec.Depth, 6);                                 // profundidade <- esmagamento
            Assert.Equal(ORingGrooveRules.TargetFill(motion, Elastomer.Nbr), spec.Fill, 6);     // largura <- preenchimento
            Assert.StartsWith("cálculo", spec.Source);
            Assert.Equal(0.0, spec.DepthMin, 6);   // sem faixa publicada para reportar
        }

        [Fact]
        public void Profundidade_caiComOMovimento_estaticaMaiorQueReciprocaMaiorQueRotativa()
        {
            double Depth(SealMotion m) => ORingGrooveCalculator
                .Compute(Ring(20.0, 2.62), GrooveKind.RadialExternal, 25.0, m, Elastomer.Nbr).Depth;

            // Menos esmagamento = canal mais raso "come" menos = profundidade MAIOR.
            Assert.True(Depth(SealMotion.Static) < Depth(SealMotion.Reciprocating));
            Assert.True(Depth(SealMotion.Reciprocating) < Depth(SealMotion.Rotary));
        }

        /// <summary>
        /// No regime da tabela o preenchimento é CONSEQUÊNCIA da cota publicada, não um alvo que
        /// o código persegue — então o que se confere é que sobrou folga em toda a tabela.
        /// Borracha é incompressível: canal cheio demais e o anel arrebenta o alojamento quando
        /// dilata. (O alvo de preenchimento continua exercitado, mas no regime do cálculo — ver
        /// <see cref="CanalNaoTabelado_seguerOsCoeficientesEAvisaQueFoiCalculo"/>.)
        /// </summary>
        [Theory]
        [InlineData(1.78, SealMotion.Static)]
        [InlineData(2.62, SealMotion.Static)]
        [InlineData(3.53, SealMotion.Static)]
        [InlineData(5.33, SealMotion.Static)]
        [InlineData(6.99, SealMotion.Static)]
        [InlineData(1.78, SealMotion.Reciprocating)]
        [InlineData(2.62, SealMotion.Reciprocating)]
        [InlineData(3.53, SealMotion.Reciprocating)]
        [InlineData(5.33, SealMotion.Reciprocating)]
        [InlineData(6.99, SealMotion.Reciprocating)]
        public void PreenchimentoTabelado_deixaFolgaParaABorrachaEscoar(double d2, SealMotion motion)
        {
            var spec = ORingGrooveCalculator.Compute(Ring(20.0, d2), GrooveKind.RadialExternal,
                25.0, motion, Elastomer.Nbr);

            Assert.True(spec.Fill < ORingGrooveRules.MaxFill,
                $"seção {d2} em {motion}: preenchimento {spec.Fill:P1} acima do limite");
            Assert.InRange(spec.Fill, 0.60, 0.80);
        }

        /// <summary>
        /// REGIME DO CÁLCULO: aqui o elastômero pesa na largura — o FKM dilata mais que a NBR,
        /// então pede mais folga para escoar. Seção 1,02 mm de propósito, que é fora da tabela.
        /// </summary>
        [Fact]
        public void Fkm_deixaOCanalMaisFolgadoQueNbr_noRegimeDeCalculo()
        {
            var nbr = ORingGrooveCalculator.Compute(Ring(20.0, 1.02), GrooveKind.RadialExternal, 25.0, SealMotion.Static, Elastomer.Nbr);
            var fkm = ORingGrooveCalculator.Compute(Ring(20.0, 1.02), GrooveKind.RadialExternal, 25.0, SealMotion.Static, Elastomer.Fkm);

            Assert.True(fkm.Width > nbr.Width);   // mais largo => mais espaço p/ a borracha escoar
            Assert.True(fkm.Fill < nbr.Fill);
            Assert.Equal(nbr.Depth, fkm.Depth, 6); // o elastômero NÃO muda o esmagamento
        }

        /// <summary>
        /// REGIME DA TABELA: o catálogo publica UMA cota de alojamento por seção, sem coluna de
        /// elastômero — então NBR e FKM cortam o mesmo canal. Onde o elastômero continua
        /// mandando é na escolha do ANEL (o limite de estiramento), não na cota do canal. Este
        /// teste existe para que a diferença entre os dois regimes fique escrita.
        /// </summary>
        [Fact]
        public void CanalTabelado_naoMudaComOElastomero_porqueACotaVemDaTabela()
        {
            var nbr = ORingGrooveCalculator.Compute(Ring(20.0, 2.62), GrooveKind.RadialExternal, 25.0, SealMotion.Static, Elastomer.Nbr);
            var fkm = ORingGrooveCalculator.Compute(Ring(20.0, 2.62), GrooveKind.RadialExternal, 25.0, SealMotion.Static, Elastomer.Fkm);

            Assert.Equal(nbr.Width, fkm.Width, 6);
            Assert.Equal(nbr.Depth, fkm.Depth, 6);
            Assert.True(ORingGrooveRules.MaxStretch(SealMotion.Static, Elastomer.Fkm)
                      < ORingGrooveRules.MaxStretch(SealMotion.Static, Elastomer.Nbr));
        }

        // ---------------------------------------------------------------- diâmetros por tipo

        [Fact]
        public void CanalNoEixo_oFundoFicaAbaixoDoDiametroVedante()
        {
            var spec = ORingGrooveCalculator.Compute(Ring(20.0, 2.62), GrooveKind.RadialExternal,
                25.0, SealMotion.Static, Elastomer.Nbr);

            Assert.Equal(25.0 - 2.0 * spec.Depth, spec.GrooveBottomDiameter, 6);
            Assert.True(spec.GrooveBottomDiameter < 25.0);
        }

        [Fact]
        public void CanalNoFuro_oFundoFicaAcimaDoDiametroVedante()
        {
            var spec = ORingGrooveCalculator.Compute(Ring(25.0, 2.62), GrooveKind.RadialInternal,
                30.0, SealMotion.Static, Elastomer.Nbr);

            Assert.Equal(30.0 + 2.0 * spec.Depth, spec.GrooveBottomDiameter, 6);
            Assert.True(spec.GrooveBottomDiameter > 30.0);
        }

        [Fact]
        public void CanalDeFace_ecentradoNoDiametroMedioDoAnel_semEsticar()
        {
            var ring = Ring(30.0, 3.53);
            var spec = ORingGrooveCalculator.Compute(ring, GrooveKind.AxialFace, 40.0, SealMotion.Static, Elastomer.Nbr);

            Assert.Equal(0.0, spec.Stretch, 6);
            Assert.Equal(ring.MeanDiameter, (spec.GrooveInnerDiameter + spec.GrooveOuterDiameter) / 2.0, 6);
            // largura RADIAL do anel anular = (OD − ID)/2
            Assert.Equal(spec.Width, (spec.GrooveOuterDiameter - spec.GrooveInnerDiameter) / 2.0, 6);
        }

        [Theory]
        [InlineData(7.0, 1.0, 2.62)]    // o caso do Carlos: furo Ø7 numa face, 1 mm de parede
        [InlineData(7.0, 1.0, 1.78)]
        [InlineData(30.0, 2.5, 3.53)]
        public void CanalDeFace_aParedePidaFicaEntreOFuroEOCanal(double holeMm, double wallMm, double d2)
        {
            // O anel ideal para o alvo é o que tem exatamente esse Ø médio — assim o teste mede
            // a REGRA, sem o arredondamento de "o que o catálogo tinha".
            double target = ORingGrooveCalculator.FaceSealingDiameter(
                holeMm, wallMm, d2, SealMotion.Static, Elastomer.Nbr);
            var ring = Ring(target - d2, d2);

            var spec = ORingGrooveCalculator.Compute(ring, GrooveKind.AxialFace, target,
                SealMotion.Static, Elastomer.Nbr);

            double land = (spec.GrooveInnerDiameter - holeMm) / 2.0;
            Assert.Equal(wallMm, land, 6);
            Assert.True(spec.GrooveInnerDiameter > holeMm, "o canal não pode invadir o furo");
        }

        [Theory]
        [InlineData(30.0, 1.0, 2.62)]   // topo de um eixo Ø30: o canal tem de ir para DENTRO
        [InlineData(12.0, 1.5, 1.78)]
        public void CanalDeFaceParaDentro_aParedeFicaEntreOContornoEOCanal(double outerMm, double wallMm, double d2)
        {
            double target = ORingGrooveCalculator.FaceSealingDiameter(
                outerMm, wallMm, d2, SealMotion.Static, Elastomer.Nbr, outward: false);
            var ring = Ring(target - d2, d2);

            var spec = ORingGrooveCalculator.Compute(ring, GrooveKind.AxialFace, target,
                SealMotion.Static, Elastomer.Nbr);

            Assert.Equal(wallMm, ORingGrooveCalculator.FaceGrooveWall(spec, outerMm, outward: false), 6);
            Assert.True(spec.GrooveOuterDiameter < outerMm, "o canal não pode passar do contorno da face");
        }

        [Theory]
        [InlineData(2.62)]   // tabelada
        [InlineData(4.20)]   // fora da tabela: cai no cálculo
        public void LarguraDoCanal_eAMesmaVindaDoAtalhoOuDoDimensionamento(double d2)
        {
            var spec = ORingGrooveCalculator.Compute(Ring(20.0, d2), GrooveKind.AxialFace,
                25.0, SealMotion.Static, Elastomer.Nbr);

            Assert.Equal(spec.Width, ORingGrooveCalculator.GrooveWidth(d2, SealMotion.Static, Elastomer.Nbr), 6);
        }

        // ---------------------------------------------------------------- limites da norma

        [Fact]
        public void Rotativa_comAnelEsticado_eReprovada_efeitoGowJoule()
        {
            // Anel bem menor que o fundo do canal => estiramento positivo.
            var spec = ORingGrooveCalculator.Compute(Ring(18.0, 2.62), GrooveKind.RadialExternal,
                25.0, SealMotion.Rotary, Elastomer.Nbr);

            Assert.True(spec.Stretch > 0);
            Assert.False(spec.IsWithinStandard);
            Assert.Contains(spec.Issues, i => i.Level == GrooveIssueLevel.Error && i.Message.Contains("Gow-Joule"));
        }

        [Fact]
        public void Rotativa_comAnelUmPoucoFolgado_passa()
        {
            double d1 = ORingGrooveCalculator.IdealInnerDiameter(GrooveKind.RadialExternal, 25.0, 2.62, SealMotion.Rotary);
            var spec = ORingGrooveCalculator.Compute(Ring(d1, 2.62), GrooveKind.RadialExternal,
                25.0, SealMotion.Rotary, Elastomer.Nbr);

            Assert.True(spec.Stretch <= 0);
            Assert.True(spec.IsWithinStandard, string.Join(" | ", spec.Issues.Select(i => i.ToString())));
        }

        [Fact]
        public void Fkm_temLimiteDeEstiramentoMenorQueNbr()
        {
            Assert.True(ORingGrooveRules.MaxStretch(SealMotion.Static, Elastomer.Fkm)
                      < ORingGrooveRules.MaxStretch(SealMotion.Static, Elastomer.Nbr));

            // 4% estica: passa em NBR (limite 5%), reprova em FKM (limite 3%).
            var ring = Ring(20.0, 2.62);
            double bottom = 20.0 * 1.04;
            double sealingDia = bottom + 2.0 * (ORingGrooveCalculator.GrooveDepth(2.62, SealMotion.Static));

            var nbr = ORingGrooveCalculator.Compute(ring, GrooveKind.RadialExternal, sealingDia, SealMotion.Static, Elastomer.Nbr);
            var fkm = ORingGrooveCalculator.Compute(ring, GrooveKind.RadialExternal, sealingDia, SealMotion.Static, Elastomer.Fkm);

            Assert.InRange(nbr.Stretch, 0.039, 0.041);
            Assert.True(nbr.IsWithinStandard);
            Assert.False(fkm.IsWithinStandard);
        }

        [Fact]
        public void EsticarOAnel_derrubaOEsmagamentoEfetivo()
        {
            var folgado = ORingGrooveCalculator.Compute(Ring(20.0, 2.62), GrooveKind.RadialExternal,
                20.0 + 2 * ORingGrooveCalculator.GrooveDepth(2.62, SealMotion.Static), SealMotion.Static, Elastomer.Nbr);   // estiramento ~0
            var esticado = ORingGrooveCalculator.Compute(Ring(19.0, 2.62), GrooveKind.RadialExternal,
                20.0 + 2 * ORingGrooveCalculator.GrooveDepth(2.62, SealMotion.Static), SealMotion.Static, Elastomer.Nbr);   // estiramento ~5%

            Assert.True(esticado.Stretch > folgado.Stretch);
            Assert.True(esticado.Squeeze < folgado.Squeeze); // cordão afina => esmaga menos
        }

        [Fact]
        public void AnelNaoConferido_geraApontamentoInformativo_masNaoReprova()
        {
            var spec = ORingGrooveCalculator.Compute(Ring(20.0, 2.62, verified: false), GrooveKind.RadialExternal,
                20.0 + 2 * ORingGrooveCalculator.GrooveDepth(2.62, SealMotion.Static), SealMotion.Static, Elastomer.Nbr);

            Assert.True(spec.IsWithinStandard);
            Assert.Contains(spec.Issues, i => i.Level == GrooveIssueLevel.Note && i.Message.Contains("não foi conferida"));
        }

        // ---------------------------------------------------------------- anel ideal / seleção

        [Theory]
        [InlineData(GrooveKind.RadialExternal, SealMotion.Static)]
        [InlineData(GrooveKind.RadialExternal, SealMotion.Reciprocating)]
        [InlineData(GrooveKind.RadialInternal, SealMotion.Static)]
        [InlineData(GrooveKind.AxialFace, SealMotion.Static)]
        public void AnelIdeal_realimentadoNoCalculo_caiNoAlvo(GrooveKind kind, SealMotion motion)
        {
            const double dia = 30.0, d2 = 2.62;
            double d1 = ORingGrooveCalculator.IdealInnerDiameter(kind, dia, d2, motion);
            var spec = ORingGrooveCalculator.Compute(Ring(d1, d2), kind, dia, motion, Elastomer.Nbr);

            double expected = kind == GrooveKind.AxialFace ? 0.0
                            : kind == GrooveKind.RadialInternal ? ORingGrooveRules.TargetOuterCompression
                            : ORingGrooveRules.TargetStretch(motion);
            Assert.Equal(expected, spec.Stretch, 3);
            Assert.True(spec.IsWithinStandard, string.Join(" | ", spec.Issues.Select(i => i.ToString())));
        }

        [Fact]
        public void Rank_poeOsAprovadosNaFrenteDosReprovados()
        {
            var catalog = ORingCatalog.BuiltInSeriesG();
            var ranked = ORingGrooveCalculator.Rank(catalog, GrooveKind.RadialExternal, 25.0,
                SealMotion.Static, Elastomer.Nbr, 2.62);

            Assert.NotEmpty(ranked);
            int lastOk = ranked.Select((c, i) => new { c, i }).Where(x => x.c.Spec.IsWithinStandard).Select(x => x.i).DefaultIfEmpty(-1).Max();
            int firstBad = ranked.Select((c, i) => new { c, i }).Where(x => !x.c.Spec.IsWithinStandard).Select(x => x.i).DefaultIfEmpty(int.MaxValue).Min();
            Assert.True(lastOk < firstBad);
        }

        [Fact]
        public void Rank_escolheOAnelMaisProximoDoIdeal()
        {
            var catalog = ORingCatalog.BuiltInSeriesG();
            var melhor = ORingGrooveCalculator.Rank(catalog, GrooveKind.RadialExternal, 25.0,
                SealMotion.Static, Elastomer.Nbr, 2.62).First();

            double ideal = ORingGrooveCalculator.IdealInnerDiameter(GrooveKind.RadialExternal, 25.0, 2.62, SealMotion.Static);
            var maisProximo = catalog.WithCrossSection(2.62).OrderBy(s => Math.Abs(s.InnerDiameter - ideal)).First();

            Assert.Equal(maisProximo.InnerDiameter, melhor.Ring.InnerDiameter, 3);
            Assert.True(melhor.Spec.IsWithinStandard);
        }

        [Fact]
        public void Rank_semCatalogo_devolveListaVazia_semExplodir()
        {
            Assert.Empty(ORingGrooveCalculator.Rank(new ORingCatalog(new ORingSize[0]),
                GrooveKind.RadialExternal, 25.0, SealMotion.Static, Elastomer.Nbr));
        }

        [Fact]
        public void SecaoSugerida_cresceComODiametro()
        {
            var disponiveis = ORingCatalog.BuiltInSeriesG().CrossSections;
            Assert.True(ORingGrooveCalculator.SuggestCrossSection(8.0, disponiveis)
                     <= ORingGrooveCalculator.SuggestCrossSection(30.0, disponiveis));
            Assert.True(ORingGrooveCalculator.SuggestCrossSection(30.0, disponiveis)
                     <= ORingGrooveCalculator.SuggestCrossSection(80.0, disponiveis));
        }

        // ---------------------------------------------------------------- catálogo

        /// <summary>
        /// O catálogo embutido é a TRANSCRIÇÃO literal da série G (Parker 001-5 BR, p. 6-7),
        /// extraída do PDF por script. Por ser transcrição — e não estimativa — toda linha nasce
        /// marcada como conferida, ao contrário da tabela gerada que ela substituiu.
        /// </summary>
        [Fact]
        public void CatalogoEmbutido_eATranscricaoLiteralDaSerieG()
        {
            var c = ORingCatalog.BuiltInSeriesG();

            Assert.Equal(349, c.Count);
            Assert.Equal(new[] { 1.02, 1.27, 1.52, 1.78, 2.62, 3.53, 5.33, 6.99 }, c.CrossSections.ToArray());
            Assert.All(c.Sizes, s => Assert.Equal("G", s.Series));
            Assert.All(c.Sizes, s => Assert.True(s.Verified));
            Assert.All(c.Sizes, s => Assert.False(string.IsNullOrWhiteSpace(s.Code)));
        }

        /// <summary>
        /// A ponte entre os dois catálogos: toda seção que a tabela de ALOJAMENTO cobre tem de
        /// existir no catálogo de ANÉIS. Se não existisse, haveria cota de canal para um anel
        /// que ninguém consegue comprar.
        /// </summary>
        [Fact]
        public void SecoesTabeladas_todasExistemNoCatalogoDeAneis()
        {
            var doCatalogo = ORingCatalog.BuiltInSeriesG().CrossSections;
            Assert.All(ORingHousingTable.TabulatedCrossSections,
                d2 => Assert.Contains(doCatalogo, v => Math.Abs(v - d2) < 0.01));
        }

        [Fact]
        public void CatalogoEmbutido_cobreAFaixaUtilDeMolde()
        {
            var c = ORingCatalog.BuiltInSeriesG();
            Assert.True(c.Sizes.Min(s => s.InnerDiameter) < 3.0);
            Assert.True(c.Sizes.Max(s => s.InnerDiameter) > 130.0);
        }

        /// <summary>
        /// Amostragem contra a página impressa (d1 em mm, como o catálogo publica) — uma linha
        /// por faixa de código, para pegar erro de transcrição no meio da tabela.
        /// </summary>
        [Fact]
        public void CatalogoEmbutido_bateComAPaginaImpressa_nosPontosDeAmostragem()
        {
            var c = ORingCatalog.BuiltInSeriesG();
            ORingSize Code(string code) => c.Sizes.Single(s => s.Code == code);

            Assert.Equal(0.74,   Code("2-001").InnerDiameter, 2);
            Assert.Equal(6.07,   Code("2-010").InnerDiameter, 2);
            Assert.Equal(34.65,  Code("2-028").InnerDiameter, 2);
            Assert.Equal(75.92,  Code("2-041").InnerDiameter, 2);
            Assert.Equal(133.07, Code("2-050").InnerDiameter, 2);
            Assert.Equal(9.19,   Code("2-110").InnerDiameter, 2);
            Assert.Equal(18.64,  Code("2-210").InnerDiameter, 2);
            Assert.Equal(37.47,  Code("2-325").InnerDiameter, 2);
            Assert.Equal(658.88, Code("2-475").InnerDiameter, 2);
        }

        /// <summary>
        /// O caso que matou a versão anterior do catálogo: os d1 eram GERADOS pelo passo da
        /// série AS568 (1/32", 1/16", 1/8", 1/4"), e onde o passo muda sem aviso a conta
        /// desandava — o 2-246 saía Ø75,79 contra os Ø113,89 publicados. São 38 mm de erro num
        /// número que vira canal usinado. Este teste existe para que ninguém tente "economizar
        /// linhas" gerando a tabela por progressão de novo.
        /// </summary>
        [Fact]
        public void CatalogoEmbutido_naoRegridiuParaProgressaoGerada_oCaso2_246()
        {
            var r = ORingCatalog.BuiltInSeriesG().Sizes.Single(s => s.Code == "2-246");

            Assert.Equal(113.89, r.InnerDiameter, 2);
            Assert.Equal(3.53, r.CrossSection, 2);
        }

        [Fact]
        public void Catalogo_leVirgulaEPontoDecimal_ePulaLinhaRuim()
        {
            var c = ORingCatalog.Parse(new[]
            {
                "\\\\ comentário",
                "# outro comentário",
                "",
                "  20,00 ; 2,62 ; 214 ; 1",     // vírgula decimal, conferido
                "25.00 ; 2.62 ; 216 ; 0",       // ponto decimal
                "isto não é medida nenhuma",    // ruim: pulada
                "0 ; 2.62 ; zero ; 1",          // d1 zero: pulada
                "30 ; 3.53"                     // sem código nem flag
            });

            Assert.Equal(3, c.Count);
            Assert.Equal(20.00, c.Sizes[0].InnerDiameter, 3);
            Assert.True(c.Sizes[0].Verified);
            Assert.False(c.Sizes[1].Verified);
            Assert.Equal("214", c.Sizes[0].Code);
        }

        [Fact]
        public void Catalogo_sobreviveAoIdaEVoltaPeloArquivo()
        {
            var original = ORingCatalog.BuiltInSeriesG();
            var voltou = ORingCatalog.Parse(ORingCatalog.Render(original).Split('\n'));

            Assert.Equal(original.Count, voltou.Count);
            for (int i = 0; i < original.Count; i++)
            {
                Assert.Equal(original.Sizes[i].InnerDiameter, voltou.Sizes[i].InnerDiameter, 2);
                Assert.Equal(original.Sizes[i].CrossSection, voltou.Sizes[i].CrossSection, 2);
                Assert.Equal(original.Sizes[i].Code, voltou.Sizes[i].Code);
            }
        }

        [Fact]
        public void ORingSize_derivaExternoMedioEArea()
        {
            var r = Ring(20.0, 2.62);
            Assert.Equal(25.24, r.OuterDiameter, 3);
            Assert.Equal(22.62, r.MeanDiameter, 3);
            Assert.Equal(Math.PI / 4 * 2.62 * 2.62, r.SectionArea, 6);
        }
    }
}
