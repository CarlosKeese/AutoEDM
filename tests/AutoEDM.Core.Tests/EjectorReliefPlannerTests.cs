using System.Collections.Generic;
using System.Linq;
using AutoEDM.Mold.Ejector;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// Alívio de extratores (Carlos, 2026-11-09): uma placa de 100 × 100 × 40 com a face de trás em
    /// Z = 0 (material para +Z) e um furo de cada tipo. Só o furo liso, inteiro e passante é aliviado.
    /// </summary>
    public class EjectorReliefPlannerTests
    {
        private static readonly double[] Z = { 0, 0, 1 };

        private static ReliefFace BackFace() => new ReliefFace
        {
            Axis = 2, PlaneMm = 0, MinMm = new double[] { 0, 0, 0 }, MaxMm = new double[] { 100, 100, 0 }, IntoSignHint = 1
        };

        private static double[][] Box(double x0, double y0, double z0, double x1, double y1, double z1) =>
            new[] { new[] { x0, y0, z0 }, new[] { x1, y1, z1 } };

        /// <summary>Cilindro inteiro de eixo Z, com as duas arestas circulares das pontas.</summary>
        private static HoleSurface Cyl(double x, double y, double r, double z0, double z1, bool threaded = false)
        {
            var s = new HoleSurface
            {
                Kind = HoleSurfaceKind.Cylinder, AxisPointMm = new[] { x, y, 0.0 }, AxisDir = Z, RadiusMm = r,
                MinMm = new[] { x - r, y - r, z0 }, MaxMm = new[] { x + r, y + r, z1 }, Threaded = threaded,
            };
            s.EdgeBoxesMm.Add(Box(x - r, y - r, z0, x + r, y + r, z0));
            s.EdgeBoxesMm.Add(Box(x - r, y - r, z1, x + r, y + r, z1));
            return s;
        }

        private static HoleSurface Cone(double x, double y, double r, double z0, double z1) => new HoleSurface
        {
            Kind = HoleSurfaceKind.Cone, AxisPointMm = new[] { x, y, 0.0 }, AxisDir = Z,
            MinMm = new[] { x - r, y - r, z0 }, MaxMm = new[] { x + r, y + r, z1 },
        };

        private static HoleSurface Disk(double x, double y, double r, double z) => new HoleSurface
        {
            Kind = HoleSurfaceKind.Plane, AxisPointMm = new[] { x, y, z }, AxisDir = Z,
            MinMm = new[] { x - r, y - r, z }, MaxMm = new[] { x + r, y + r, z },
        };

        private static EjectorHole At(EjectorReliefPlan p, double x, double y) =>
            p.Holes.Single(h => System.Math.Abs(h.CenterMm[0] - x) < 0.01 && System.Math.Abs(h.CenterMm[1] - y) < 0.01);

        private static EjectorReliefPlan PlanOf(params HoleSurface[] s) =>
            EjectorReliefPlanner.Plan(BackFace(), new List<HoleSurface>(s), new EjectorReliefOptions());

        [Theory]
        [InlineData(3.0, 10.0)]    // 2 × 3 = 6 → mínimo 10
        [InlineData(6.0, 12.0)]
        [InlineData(7.5, 15.0)]
        [InlineData(12.0, 20.0)]   // 24 → máximo 20
        [InlineData(5.2, 10.5)]    // 10,4 → para cima em 0,5
        public void Guia_eh_2xD_entre_10_e_20(double d, double guide) =>
            Assert.Equal(guide, new EjectorReliefOptions().GuideLengthMm(d), 6);

        [Theory]
        [InlineData(2.5, 3.0)]
        [InlineData(6.0, 6.5)]     // até Ø6: +0,5
        [InlineData(8.0, 9.0)]     // acima: +1,0
        [InlineData(6.01, 7.1)]
        public void Alivio_folga_pelo_diametro(double d, double relief) =>
            Assert.Equal(relief, new EjectorReliefOptions().ReliefDiameterMm(d), 6);

        [Fact]
        public void FuroPassanteLiso_eh_aliviado_ate_deixar_a_guia()
        {
            EjectorReliefPlan p = PlanOf(Cyl(20, 20, 3, 0, 40));
            Assert.Equal(1, p.IntoSign);
            EjectorHole h = Assert.Single(p.Holes);
            Assert.True(h.Relieve, h.SkipReason);
            Assert.Equal(6.0, h.DiameterMm, 6);
            Assert.Equal(12.0, h.GuideMm, 6);
            Assert.Equal(6.5, h.ReliefDiameterMm, 6);
            Assert.Equal(28.0, h.ReliefDepthMm, 6);
            Assert.Equal(new[] { 0.0, 0.0, 1.0 }, p.IntoDir);
        }

        [Fact]
        public void Roscado_rebaixo_chanfro_cego_curto_ficam_de_fora()
        {
            EjectorReliefPlan p = PlanOf(
                Cyl(40, 20, 2.5, 0, 40, threaded: true),                 // roscado (recurso na árvore)
                Cyl(60, 20, 5.5, 0, 6), Cyl(60, 20, 3.3, 6, 40),         // rebaixo de cabeça
                Cone(80, 20, 2.5, 0, 0.5), Cyl(80, 20, 2, 0.5, 40),      // chanfro na boca
                Cyl(20, 60, 4, 0, 15), Disk(20, 60, 4, 15),              // cego, fundo plano
                Cyl(40, 60, 1.5, 0, 10));                                // curto: 10 mm para 10 de guia

            Assert.Equal(5, p.Holes.Count);
            Assert.Empty(p.ToRelieve);
            Assert.Equal("roscado", At(p, 40, 20).SkipReason);
            Assert.StartsWith("dois Ø", At(p, 60, 20).SkipReason);
            Assert.StartsWith("chanfro", At(p, 80, 20).SkipReason);
            Assert.StartsWith("furo cego", At(p, 20, 60).SkipReason);
            Assert.StartsWith("curto", At(p, 40, 60).SkipReason);
        }

        [Fact]
        public void SaidaInclinada_guia_medida_da_ponta_mais_curta()
        {
            HoleSurface c = Cyl(60, 60, 2, 0, 40);
            c.EdgeBoxesMm[1] = Box(58, 58, 32, 62, 62, 40);              // saída numa rampa: de 32 a 40
            EjectorHole h = Assert.Single(PlanOf(c).Holes);
            Assert.Equal(32.0, h.LengthMm, 6);
            Assert.Equal(10.0, h.GuideMm, 6);
            Assert.Equal(22.0, h.ReliefDepthMm, 6);
            Assert.Equal(4.5, h.ReliefDiameterMm, 6);
        }

        [Fact]
        public void CilindroEmDuasMetades_eh_um_furo_so_e_as_retas_da_divisao_nao_contam()
        {
            var a = new HoleSurface
            {
                Kind = HoleSurfaceKind.Cylinder, AxisPointMm = new[] { 80.0, 60, 0 }, AxisDir = Z, RadiusMm = 5,
                MinMm = new[] { 75.0, 55, 0 }, MaxMm = new[] { 80.0, 65, 40 },
            };
            var b = new HoleSurface
            {
                Kind = HoleSurfaceKind.Cylinder, AxisPointMm = new[] { 80.0, 60, 0 }, AxisDir = Z, RadiusMm = 5,
                MinMm = new[] { 80.0, 55, 0 }, MaxMm = new[] { 85.0, 65, 40 },
            };
            foreach (HoleSurface s in new[] { a, b })
            {
                s.EdgeBoxesMm.Add(Box(80, 55, 0, 80, 55, 40));           // reta da divisão
                s.EdgeBoxesMm.Add(Box(80, 65, 0, 80, 65, 40));
                s.EdgeBoxesMm.Add(Box(s.MinMm[0], 55, 40, s.MaxMm[0], 65, 40));
            }
            EjectorHole h = Assert.Single(PlanOf(a, b).Holes);
            Assert.True(h.Relieve, h.SkipReason);
            Assert.Equal(40.0, h.LengthMm, 6);
            Assert.Equal(11.0, h.ReliefDiameterMm, 6);
            Assert.Equal(20.0, h.ReliefDepthMm, 6);
        }

        [Fact]
        public void MeiaCana_ressalto_e_furo_fora_da_face_sao_ignorados_ou_recusados()
        {
            var half = new HoleSurface
            {
                Kind = HoleSurfaceKind.Cylinder, AxisPointMm = new[] { 0.0, 50, 0 }, AxisDir = Z, RadiusMm = 3,
                MinMm = new[] { 0.0, 47, 0 }, MaxMm = new[] { 3.0, 53, 40 },
            };
            EjectorReliefPlan p = PlanOf(
                Cyl(20, 20, 3, 0, 40),
                half,                                                     // meia-cana na borda
                Cyl(50, 50, 4, -10, 0),                                   // ressalto do lado de fora
                Cyl(150, 150, 3, 0, 40));                                 // abre em outra face
            Assert.Equal(2, p.Holes.Count);
            Assert.StartsWith("furo incompleto", At(p, 0, 50).SkipReason);
            Assert.True(At(p, 20, 20).Relieve);
        }

        /// <summary>MD-15428 (2026-11-09): Ø8 de Z 35 a 4 com cone abrindo até Z 0 — era recusado.</summary>
        [Fact]
        public void ConeQueAlargaNaSaida_eh_aliviado_ate_o_comeco_do_cone()
        {
            var face = new ReliefFace { Axis = 2, PlaneMm = 35, MinMm = new double[] { -97, -97, 35 }, MaxMm = new double[] { 97, 97, 35 } };
            EjectorReliefPlan p = EjectorReliefPlanner.Plan(face, new List<HoleSurface>
            {
                Cyl(32, 0, 4, 4, 35), Cone(32, 0, 6, 0, 4),              // alarga: Ø8 → Ø12
                Cyl(32, 21, 2.5, 2.5, 35), Cone(32, 21, 2.5, 0, 2.5),    // estreita: ponta de broca
            }, new EjectorReliefOptions());
            EjectorHole h = At(p, 32, 0);
            Assert.True(h.Relieve, h.SkipReason);
            Assert.Equal(31.0, h.LengthMm, 6);
            Assert.Equal(16.0, h.GuideMm, 6);
            Assert.Equal(15.0, h.ReliefDepthMm, 6);
            Assert.StartsWith("ponta de broca", At(p, 32, 21).SkipReason);
        }

        private static HoleSurface Torus(double x, double y, double outerR, double z0, double z1) => new HoleSurface
        {
            Kind = HoleSurfaceKind.Torus, AxisPointMm = new[] { x, y, 0.0 }, AxisDir = Z,
            MinMm = new[] { x - outerR, y - outerR, z0 }, MaxMm = new[] { x + outerR, y + outerR, z1 },
        };

        /// <summary>
        /// MD-15428 (2026-11-09, 2º run): raio da superfície da moldagem em volta da saída, coaxial ao
        /// furo, tirava o extrator como "raio na borda". Raio na saída (que alarga) é da moldagem;
        /// raio na boca (face de trás) continua tirando o furo.
        /// </summary>
        [Fact]
        public void RaioDaMoldagemNaSaida_nao_tira_o_furo_mas_raio_na_boca_tira()
        {
            EjectorReliefPlan p = PlanOf(
                Cyl(20, 20, 4, 0, 31), Cone(20, 20, 6, 31, 35), Torus(20, 20, 9, 35, 37),   // saída: cone + raio da moldagem
                Cyl(60, 20, 2.5, 0, 40), Torus(60, 20, 9, 38, 42),                            // raio coaxial mais adiante
                Torus(20, 60, 3.5, 0, 1), Cyl(20, 60, 2.5, 1, 40),                            // raio na boca
                Cyl(60, 60, 2.5, 0, 30), Torus(60, 60, 2.5, 30, 32.5));                      // fundo arredondado
            EjectorHole a = At(p, 20, 20);
            Assert.True(a.Relieve, a.SkipReason);
            Assert.Equal(31.0, a.LengthMm, 6);
            Assert.True(At(p, 60, 20).Relieve, At(p, 60, 20).SkipReason);
            Assert.StartsWith("raio (arredondamento) na boca", At(p, 20, 60).SkipReason);
            Assert.StartsWith("fundo arredondado", At(p, 60, 60).SkipReason);
        }

        /// <summary>
        /// MD-15428 (2026-11-09, 3º run): a regra da saída tem de olhar só o que ENCOSTA no fim do furo.
        /// Parafuso Ø9 com rebaixo Ø15 do lado da moldagem e Ø14 com raio direto na borda da saída
        /// tinham sido aliviados; chanfro pequeno na saída também fica de fora.
        /// </summary>
        [Fact]
        public void Saida_rebaixo_raio_encostado_e_chanfro_tiram_o_furo()
        {
            EjectorReliefPlan p = PlanOf(
                Cyl(20, 20, 4.5, 0, 26), Cyl(20, 20, 7.5, 26, 35),                           // Ø9 + rebaixo Ø15 do outro lado
                Cyl(60, 20, 7, 0, 33), Torus(60, 20, 9, 33, 35),                              // Ø14 com raio R2 na borda da saída
                Cyl(20, 60, 2.5, 0, 34.5), Cone(20, 60, 3, 34.5, 35),                         // chanfro 0,5 na saída
                Cyl(60, 60, 2.5, 0, 32.5), Cone(60, 60, 5, 32.5, 35), Torus(60, 60, 8, 35, 36)); // sede cônica + raio além dela
            Assert.StartsWith("dois Ø", At(p, 20, 20).SkipReason);
            Assert.Equal(9.0, At(p, 20, 20).DiameterMm, 6);
            Assert.StartsWith("raio (arredondamento) na saída", At(p, 60, 20).SkipReason);
            Assert.StartsWith("chanfro na saída", At(p, 20, 60).SkipReason);
            EjectorHole seat = At(p, 60, 60);
            Assert.True(seat.Relieve, seat.SkipReason);
            Assert.Equal(22.5, seat.ReliefDepthMm, 6);
        }

        /// <summary>Carlos, 2026-11-09: forçar o alívio num reprovado, pular um aprovado; curto nem forçando.</summary>
        [Fact]
        public void Escolha_do_usuario_forca_reprovado_e_pula_aprovado_mas_curto_nao_sai()
        {
            var surfaces = new List<HoleSurface>
            {
                Cyl(20, 20, 3, 0, 40),                                    // aprovado
                Cyl(40, 20, 2.5, 0, 40, threaded: true),                  // reprovado: roscado
                Cyl(60, 20, 1.5, 0, 10),                                  // curto
            };
            EjectorReliefPlan auto = EjectorReliefPlanner.Plan(BackFace(), surfaces, new EjectorReliefOptions());
            Assert.Equal(30.0, At(auto, 40, 20).ReliefDepthMm, 6);        // medidas saem mesmo reprovado (Ø5: guia 10)
            var choices = new Dictionary<string, bool>
            {
                { At(auto, 20, 20).Key, false }, { At(auto, 40, 20).Key, true }, { At(auto, 60, 20).Key, true },
            };
            EjectorReliefPlan p = EjectorReliefPlanner.Plan(BackFace(), surfaces, new EjectorReliefOptions(), choices);
            Assert.False(At(p, 20, 20).Relieve);
            EjectorHole forced = At(p, 40, 20);
            Assert.True(forced.Relieve);
            Assert.True(forced.Forced);
            Assert.Equal("roscado", forced.SkipReason);
            Assert.False(At(p, 60, 20).Relieve);
            Assert.False(At(p, 60, 20).CanRelieve);
        }

        [Fact]
        public void Grupos_juntam_furos_de_mesmo_alivio()
        {
            EjectorReliefPlan p = PlanOf(
                Cyl(20, 20, 2.5, 0, 31.5), Cyl(20, 60, 2.5, 0, 31.5), Cyl(40, 40, 2.5, 0, 31.5),   // Ø5,5 × 21,5 ×3
                Cyl(60, 20, 4.05, 0, 35), Cyl(60, 60, 4.05, 0, 35),                                // Ø9,1 × 18,5 ×2
                Cyl(80, 40, 2.5, 0, 32.5));                                                        // Ø5,5 × 22,5
            List<ReliefGroup> g = p.Groups();
            Assert.Equal(3, g.Count);
            Assert.Equal(new[] { 3, 2, 1 }, g.Select(x => x.Holes.Count).OrderByDescending(x => x).ToArray());
            ReliefGroup three = g.Single(x => x.Holes.Count == 3);
            Assert.Equal(5.5, three.DiameterMm, 6);
            Assert.Equal(21.5, three.DepthMm, 6);
        }

        /// <summary>
        /// Faces reais do log (MD-15428, 2026-11-09): nos extratores E29/E30 o toro encostado no fim do
        /// furo é a concordância (0,3 mm) de uma SEDE cônica que vai até a face — alivia; no Ø14 do
        /// canto (E6) o toro vai até a face sem cone — raio na borda, fica fora.
        /// </summary>
        [Fact]
        public void ToroQueConcordaComSedeConica_passa_mas_raio_na_borda_nao()
        {
            EjectorReliefPlan p = PlanOf(
                Cyl(20, 20, 2.5, 0, 32.5), Torus(20, 20, 4.15, 32.5, 32.87), Cone(20, 20, 4.72, 32.87, 35),   // E29
                Cyl(60, 20, 4, 0, 31), Torus(60, 20, 6.665, 31, 31.33), Cone(60, 20, 8, 31.33, 35),          // E30
                Cyl(20, 60, 7, 0, 33), Torus(20, 60, 9, 33, 35));                                              // E6
            Assert.True(At(p, 20, 20).Relieve, At(p, 20, 20).SkipReason);
            Assert.Equal(22.5, At(p, 20, 20).ReliefDepthMm, 6);
            Assert.True(At(p, 60, 20).Relieve, At(p, 60, 20).SkipReason);
            Assert.Equal(15.0, At(p, 60, 20).ReliefDepthMm, 6);
            Assert.StartsWith("raio (arredondamento) na saída", At(p, 20, 60).SkipReason);
        }

        [Fact]
        public void JaAliviado_nao_eh_aliviado_de_novo()
        {
            EjectorReliefPlan p = PlanOf(Cyl(20, 20, 3.25, 0, 28), Cyl(20, 20, 3, 28, 40));
            Assert.StartsWith("dois Ø", Assert.Single(p.Holes).SkipReason);
        }

        [Fact]
        public void FaceDoOutroLado_material_para_menosZ()
        {
            var face = new ReliefFace { Axis = 2, PlaneMm = 40, MinMm = new double[] { 0, 0, 40 }, MaxMm = new double[] { 100, 100, 40 } };
            EjectorReliefPlan p = EjectorReliefPlanner.Plan(face, new List<HoleSurface> { Cyl(20, 20, 3, 0, 40) }, new EjectorReliefOptions());
            Assert.Equal(-1, p.IntoSign);
            EjectorHole h = Assert.Single(p.Holes);
            Assert.Equal(28.0, h.ReliefDepthMm, 6);
            Assert.Equal(new[] { 0.0, 0.0, -1.0 }, p.IntoDir);
        }

        [Fact]
        public void AliviosQueSeCruzam_geram_aviso()
        {
            EjectorReliefPlan p = PlanOf(Cyl(20, 20, 3, 0, 40), Cyl(26.5, 20, 3, 0, 40));
            Assert.Equal(2, p.ToRelieve.Count());
            Assert.Single(p.Warnings);
        }
    }
}
