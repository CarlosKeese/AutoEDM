using System;
using System.Collections.Generic;
using System.Linq;
using AutoEDM.Reverse;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// Correções de fronteira, enrolamento e desvio — tudo contra sólidos GERADOS com geometria
    /// conhecida (arruela com chanfro no furo, cunha com rampa), fechados e com as normais para
    /// fora, como sai um STL de CAD.
    /// </summary>
    public class BoundaryCorrectionsTests
    {
        // ------------------------------------------------------------------ enrolamento

        [Fact]
        public void Enrolamento_e_um_dentro_da_caixa_e_zero_fora()
        {
            double[] box = Lathe(new[] { new[] { 0.0, 0.0 }, new[] { 10.0, 0.0 }, new[] { 10.0, 5.0 }, new[] { 0.0, 5.0 } }, 48);

            Assert.True(MeshSolid.Contains(box, new[] { 1.0, 2.0, 2.5 }));
            Assert.True(MeshSolid.Contains(box, new[] { 9.5, 0.0, 0.1 }));
            Assert.False(MeshSolid.Contains(box, new[] { 11.0, 0.0, 2.5 }));
            Assert.False(MeshSolid.Contains(box, new[] { 0.0, 0.0, 5.5 }));
        }

        // ------------------------------------------------------------------ desvio

        [Fact]
        public void Distancia_de_ponto_a_triangulo_cobre_face_aresta_e_vertice()
        {
            double[] t = { 0, 0, 0, 10, 0, 0, 0, 10, 0 };
            Assert.Equal(3.0, MeshDeviation.PointTriangle(2, 2, 3, t, 0), 9);          // sobre a face
            Assert.Equal(5.0, MeshDeviation.PointTriangle(5, -4, 3, t, 0), 9);         // aresta y = 0
            Assert.Equal(Math.Sqrt(2), MeshDeviation.PointTriangle(-1, -1, 0, t, 0), 9); // vértice
        }

        [Fact]
        public void Malha_contra_ela_mesma_tem_desvio_zero_e_contra_outra_mede_a_diferenca()
        {
            double[] a = Lathe(new[] { new[] { 0.0, 0.0 }, new[] { 10.0, 0.0 }, new[] { 10.0, 5.0 }, new[] { 0.0, 5.0 } }, 48);
            double[] b = Lathe(new[] { new[] { 0.0, 0.0 }, new[] { 10.0, 0.0 }, new[] { 10.0, 5.3 }, new[] { 0.0, 5.3 } }, 48);

            Assert.Equal(0, MeshDeviation.Compute(a, a, 0.05).MaxMm, 9);
            DeviationResult d = MeshDeviation.Compute(b, a, 0.05);
            Assert.Equal(0.3, d.MaxMm, 6);
            Assert.True(d.Above > 0);
        }

        // ------------------------------------------------------------------ cone

        [Fact]
        public void Chanfro_do_furo_vira_um_recorte_revolvido_com_o_volume_do_triangulo()
        {
            // Arruela Ø20 × 3 com furo Ø4 e chanfro 1×45° na boca de cima (Ø6 em z = 3).
            double[] mesh = Lathe(new[]
            {
                new[] { 2.0, 0.0 }, new[] { 10.0, 0.0 }, new[] { 10.0, 3.0 }, new[] { 3.0, 3.0 }, new[] { 2.0, 2.0 }
            }, 72);

            RecognitionResult r = SurfaceRecognizer.Recognize(mesh, null);
            PrismaticPlan plan = PrismaticSection.Plan(mesh, r, null);
            CorrectionPlan c = BoundaryCorrections.Plan(mesh, r, plan);

            CorrectionRegion cut = Assert.Single(c.Regions);
            Assert.Equal("revolucao", cut.Kind);
            Assert.False(cut.Add);
            // Triângulo (2,2)-(3,3)-(2,3) revolvido: π·∫₀¹((2+t)² − 4)dt = 7π/3.
            Assert.Equal(7 * Math.PI / 3, cut.VolumeMm3, 6);
            Assert.Equal(1, c.AlreadyRight); // o triângulo de baixo é material nos dois
        }

        // ------------------------------------------------------------------ plano inclinado

        [Fact]
        public void Rampa_vira_um_recorte_e_uma_protrusao_ao_longo_da_horizontal_da_face()
        {
            // Cunha extrudada 30 mm em X: perfil (y, z) com a rampa de (0, 0) a (5, 10).
            double[] mesh = PrismX(new[]
            {
                new[] { 0.0, 0.0 }, new[] { 20.0, 0.0 }, new[] { 20.0, 10.0 }, new[] { 5.0, 10.0 }
            }, 30);

            RecognitionResult r = SurfaceRecognizer.Recognize(mesh, null);
            PrismaticPlan plan = PrismaticSection.Plan(mesh, r, null);
            CorrectionPlan c = BoundaryCorrections.Plan(mesh, r, plan);

            Assert.Equal(2, c.Regions.Count);
            Assert.All(c.Regions, g => Assert.Equal("extrusao", g.Kind));
            Assert.Single(c.Regions, g => g.Add);
            Assert.Single(c.Regions, g => !g.Add);
            Assert.All(c.Regions, g =>
            {
                Assert.Equal(30, g.DepthMm, 6);
                Assert.Equal(0.5 * 5 * 10 * 30, g.VolumeMm3, 6);
                Assert.Equal(1.0, Math.Abs(g.W[0]), 9); // extrudado ao longo de X
            });
        }

        [Fact]
        public void Peca_prismatica_nao_pede_correcao_nenhuma()
        {
            double[] mesh = Lathe(new[] { new[] { 2.0, 0.0 }, new[] { 10.0, 0.0 }, new[] { 10.0, 3.0 }, new[] { 2.0, 3.0 } }, 72);

            RecognitionResult r = SurfaceRecognizer.Recognize(mesh, null);
            CorrectionPlan c = BoundaryCorrections.Plan(mesh, r, PrismaticSection.Plan(mesh, r, null));

            Assert.Empty(c.Regions);
            Assert.Empty(c.Warnings);
        }

        // ------------------------------------------------------------------ sólidos de teste

        /// <summary>Sólido de revolução em volta de Z: perfil (r, z) FECHADO e anti-horário no
        /// plano (r, z); ponto com r = 0 vira o polo.</summary>
        private static double[] Lathe(double[][] profile, int segments)
        {
            var pts = new List<double>();
            int n = profile.Length;
            for (int i = 0; i < n; i++)
            {
                double[] a = profile[i], b = profile[(i + 1) % n];
                for (int k = 0; k < segments; k++)
                {
                    double t0 = 2 * Math.PI * k / segments, t1 = 2 * Math.PI * (k + 1) / segments;
                    double[] a0 = At(a, t0), a1 = At(a, t1), b0 = At(b, t0), b1 = At(b, t1);
                    // Perfil anti-horário em (r, z) + varredura anti-horária em Z = normal para fora.
                    if (a[0] > 0) Push(pts, a0, a1, b1);
                    if (b[0] > 0) Push(pts, a0, b1, b0);
                }
            }
            return Fix(pts.ToArray());
        }

        private static double[] At(double[] rz, double t) => new[] { rz[0] * Math.Cos(t), rz[0] * Math.Sin(t), rz[1] };

        /// <summary>Prisma ao longo de X (de 0 a len) sobre um polígono CONVEXO anti-horário em (y, z).</summary>
        private static double[] PrismX(double[][] poly, double len)
        {
            var pts = new List<double>();
            int n = poly.Length;
            for (int i = 0; i < n; i++)
            {
                double[] a = poly[i], b = poly[(i + 1) % n];
                double[] a0 = { 0, a[0], a[1] }, b0 = { 0, b[0], b[1] }, a1 = { len, a[0], a[1] }, b1 = { len, b[0], b[1] };
                Push(pts, a0, b1, b0);
                Push(pts, a0, a1, b1);
            }
            for (int i = 1; i + 1 < n; i++)
            {
                Push(pts, new[] { 0, poly[0][0], poly[0][1] }, new[] { 0, poly[i][0], poly[i][1] }, new[] { 0, poly[i + 1][0], poly[i + 1][1] });
                Push(pts, new[] { len, poly[0][0], poly[0][1] }, new[] { len, poly[i + 1][0], poly[i + 1][1] }, new[] { len, poly[i][0], poly[i][1] });
            }
            return Fix(pts.ToArray());
        }

        /// <summary>Garante normais para FORA: se o volume com sinal sair negativo, inverte tudo.
        /// O gerador acerta o sentido; isto só impede um teste de depender disso.</summary>
        private static double[] Fix(double[] m)
        {
            double vol = 0;
            for (int b = 0; b < m.Length; b += 9)
                vol += m[b] * (m[b + 4] * m[b + 8] - m[b + 5] * m[b + 7])
                     - m[b + 1] * (m[b + 3] * m[b + 8] - m[b + 5] * m[b + 6])
                     + m[b + 2] * (m[b + 3] * m[b + 7] - m[b + 4] * m[b + 6]);
            if (vol >= 0) return m;
            var o = (double[])m.Clone();
            for (int b = 0; b < o.Length; b += 9)
                for (int k = 0; k < 3; k++) { double tmp = o[b + 3 + k]; o[b + 3 + k] = o[b + 6 + k]; o[b + 6 + k] = tmp; }
            return o;
        }

        private static void Push(List<double> pts, double[] a, double[] b, double[] c)
        {
            pts.AddRange(a);
            pts.AddRange(b);
            pts.AddRange(c);
        }
    }
}
