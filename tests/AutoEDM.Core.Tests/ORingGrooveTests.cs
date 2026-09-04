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
    /// Vários testes checam contra os valores de MANUAL DE VEDAÇÃO (canal estático ≈ 1,31·d2 de
    /// largura por 0,80·d2 de profundidade): é a âncora externa que impede um coeficiente de
    /// <see cref="ORingGrooveRules"/> ser mexido por engano sem ninguém perceber.
    /// </summary>
    public class ORingGrooveTests
    {
        private static ORingSize Ring(double d1, double d2, bool verified = true) =>
            new ORingSize { InnerDiameter = d1, CrossSection = d2, Series = "G", Verified = verified };

        // ---------------------------------------------------------------- geometria do canal

        [Theory]
        [InlineData(1.78)]
        [InlineData(2.62)]
        [InlineData(3.53)]
        public void CanalEstatico_temProfundidade80PorCentoELargura131PorCentoDaSecao(double d2)
        {
            var spec = ORingGrooveCalculator.Compute(Ring(20.0, d2), GrooveKind.RadialExternal,
                25.0, SealMotion.Static, Elastomer.Nbr);

            Assert.Equal(0.80 * d2, spec.Depth, 3);          // esmagamento alvo de 20%
            Assert.InRange(spec.Width / d2, 1.28, 1.34);      // manual: ~1,3·d2
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

        [Fact]
        public void Preenchimento_ficaNoAlvoEAbaixoDoLimite()
        {
            var spec = ORingGrooveCalculator.Compute(Ring(20.0, 3.53), GrooveKind.RadialExternal,
                25.0, SealMotion.Static, Elastomer.Nbr);

            Assert.InRange(spec.Fill, 0.70, 0.80);
            Assert.True(spec.Fill < ORingGrooveRules.MaxFill);
        }

        [Fact]
        public void Fkm_deixaOCanalMaisFolgadoQueNbr_porCausaDaDilatacao()
        {
            var nbr = ORingGrooveCalculator.Compute(Ring(20.0, 2.62), GrooveKind.RadialExternal, 25.0, SealMotion.Static, Elastomer.Nbr);
            var fkm = ORingGrooveCalculator.Compute(Ring(20.0, 2.62), GrooveKind.RadialExternal, 25.0, SealMotion.Static, Elastomer.Fkm);

            Assert.True(fkm.Width > nbr.Width);   // mais largo => mais espaço p/ a borracha escoar
            Assert.True(fkm.Fill < nbr.Fill);
            Assert.Equal(nbr.Depth, fkm.Depth, 6); // o elastômero NÃO muda o esmagamento
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
            double sealingDia = bottom + 2.0 * (2.62 * 0.80);

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
                20.0 + 2 * 2.62 * 0.8, SealMotion.Static, Elastomer.Nbr);   // estiramento ~0
            var esticado = ORingGrooveCalculator.Compute(Ring(19.0, 2.62), GrooveKind.RadialExternal,
                20.0 + 2 * 2.62 * 0.8, SealMotion.Static, Elastomer.Nbr);   // estiramento ~5%

            Assert.True(esticado.Stretch > folgado.Stretch);
            Assert.True(esticado.Squeeze < folgado.Squeeze); // cordão afina => esmaga menos
        }

        [Fact]
        public void AnelNaoConferido_geraApontamentoInformativo_masNaoReprova()
        {
            var spec = ORingGrooveCalculator.Compute(Ring(20.0, 2.62, verified: false), GrooveKind.RadialExternal,
                20.0 + 2 * 2.62 * 0.8, SealMotion.Static, Elastomer.Nbr);

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

        [Fact]
        public void CatalogoEmbutido_temAsTresSecoesDeMoldeETodasAConferir()
        {
            var c = ORingCatalog.BuiltInSeriesG();

            Assert.Equal(new[] { 1.78, 2.62, 3.53 }, c.CrossSections.ToArray());
            Assert.All(c.Sizes, s => Assert.False(s.Verified));
            Assert.All(c.Sizes, s => Assert.Equal("G", s.Series));
            Assert.True(c.Count > 100);
        }

        [Fact]
        public void CatalogoEmbutido_cobreAFaixaUtilDeMolde()
        {
            var c = ORingCatalog.BuiltInSeriesG();
            Assert.True(c.Sizes.Min(s => s.InnerDiameter) < 3.0);
            Assert.True(c.Sizes.Max(s => s.InnerDiameter) > 130.0);
        }

        [Fact]
        public void CatalogoEmbutido_asProgressoesBatemComOsCodigosConhecidos()
        {
            var c = ORingCatalog.BuiltInSeriesG();
            ORingSize Code(string code) => c.Sizes.Single(s => s.Code == code);

            // Cada faixa AS568 sobe um passo fixo; o último código tem de bater com o primeiro
            // mais N passos. É esta conferência que sustenta a tabela embutida.
            //
            // Tolerância de 0,1 mm de propósito: as tabelas publicadas arredondam o d1 para 3
            // casas de POLEGADA, então a progressão exata e o valor publicado divergem em até
            // ~0,013 mm (ex.: 028 = 0,301 + 17 × 1/16" = 1,3635" contra 1,364" publicado).
            // É pequeno para o canal e grande o bastante para lembrar por que toda linha da
            // tabela embutida nasce marcada "a conferir".
            Assert.Equal(0.239 * 25.4, Code("010").InnerDiameter, 1);   // 006 + 4 × 1/32"
            Assert.Equal(1.364 * 25.4, Code("028").InnerDiameter, 1);   // 011 + 17 × 1/16"
            Assert.Equal(2.989 * 25.4, Code("041").InnerDiameter, 1);   // 029 + 12 × 1/8"
            Assert.Equal(5.239 * 25.4, Code("050").InnerDiameter, 1);   // 042 + 8 × 1/4"
            Assert.Equal(0.362 * 25.4, Code("110").InnerDiameter, 1);   // 109 + 1 × 1/16"
            Assert.Equal(0.734 * 25.4, Code("210").InnerDiameter, 1);   // 201 + 9 × 1/16"
            Assert.Equal(2.984 * 25.4, Code("246").InnerDiameter, 1);   // 201 + 45 × 1/16"
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
