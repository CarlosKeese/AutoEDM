using System.Collections.Generic;
using System.Linq;
using AutoEDM.Electrode;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// Testes do encadeamento das arestas abertas em contornos — o que decide QUAIS arestas
    /// entram em cada "Limite" (<c>SurfaceByBoundaries.Add</c>) no fechamento automático dos vãos
    /// X,Y. Só o núcleo PURO: as arestas são montadas à mão (mm), sem Solid Edge.
    /// </summary>
    public class OpenEdgeLoopsTests
    {
        private const double Tol = OpenEdgeLoops.DefaultJoinToleranceMm;

        /// <summary>Aresta reta de A a B (mm). Vertical = Z varia.</summary>
        private static OpenEdgeSegment Seg(double x1, double y1, double z1, double x2, double y2, double z2)
        {
            return new OpenEdgeSegment
            {
                StartMm = new[] { x1, y1, z1 },
                EndMm = new[] { x2, y2, z2 },
                ZMinMm = z1 < z2 ? z1 : z2,
                ZMaxMm = z1 > z2 ? z1 : z2,
                IsVertical = (z1 - z2) > 0.05 || (z2 - z1) > 0.05
            };
        }

        /// <summary>Vão lateral típico: retângulo em pé no plano Y=0 (2 verticais + 2 horizontais).</summary>
        private static List<OpenEdgeSegment> SideGapRectangle()
        {
            return new List<OpenEdgeSegment>
            {
                Seg(0, 0, 0,   10, 0, 0),    // base (horizontal)
                Seg(10, 0, 0,  10, 0, 5),    // subida (vertical)
                Seg(10, 0, 5,  0, 0, 5),     // topo (horizontal)
                Seg(0, 0, 5,   0, 0, 0),     // descida (vertical)
            };
        }

        [Fact]
        public void Vao_lateral_fecha_e_conta_as_verticais()
        {
            List<OpenEdgeLoop> loops = OpenEdgeLoops.Chain(SideGapRectangle(), Tol);

            OpenEdgeLoop loop = Assert.Single(loops);
            Assert.True(loop.Closed);
            Assert.Equal(4, loop.Segments.Count);
            Assert.Equal(2, loop.VerticalCount);
            Assert.True(loop.IsSideGap);
            Assert.Equal(0, loop.ZMinMm, 6);
            Assert.Equal(5, loop.ZMaxMm, 6);
            Assert.Equal(0, loop.XMinMm, 6);
            Assert.Equal(10, loop.XMaxMm, 6);
        }

        [Fact]
        public void Ordem_embaralhada_e_arestas_invertidas_ainda_encadeiam()
        {
            // A ordem em que as arestas saem do COM é arbitrária, e a orientação de cada uma
            // também — o encadeamento tem que virar a aresta quando é o FIM dela que encosta.
            var scrambled = new List<OpenEdgeSegment>
            {
                Seg(10, 0, 5,  0, 0, 5),
                Seg(0, 0, 0,   0, 0, 5),   // invertida em relação ao contorno
                Seg(10, 0, 0,  0, 0, 0),   // invertida
                Seg(10, 0, 0,  10, 0, 5),
            };

            OpenEdgeLoop loop = Assert.Single(OpenEdgeLoops.Chain(scrambled, Tol));
            Assert.True(loop.Closed);
            Assert.Equal(4, loop.Segments.Count);
            Assert.True(loop.IsSideGap);
        }

        [Fact]
        public void Rim_horizontal_fecha_mas_nao_e_vao_lateral()
        {
            // O rim de topo/fundo também é um contorno FECHADO — mas fechá-lo taparia a
            // superfície de queima, então não pode entrar como vão.
            var rim = new List<OpenEdgeSegment>
            {
                Seg(0, 0, 3,   10, 0, 3),
                Seg(10, 0, 3,  10, 8, 3),
                Seg(10, 8, 3,  0, 8, 3),
                Seg(0, 8, 3,   0, 0, 3),
            };

            OpenEdgeLoop loop = Assert.Single(OpenEdgeLoops.Chain(rim, Tol));
            Assert.True(loop.Closed);
            Assert.Equal(0, loop.VerticalCount);
            Assert.False(loop.IsSideGap);
        }

        [Fact]
        public void Contorno_que_nao_fecha_nao_vira_patch()
        {
            List<OpenEdgeSegment> incomplete = SideGapRectangle();
            incomplete.RemoveAt(3); // falta um lado

            OpenEdgeLoop loop = Assert.Single(OpenEdgeLoops.Chain(incomplete, Tol));
            Assert.False(loop.Closed);
            Assert.False(loop.IsSideGap);
            Assert.Equal(3, loop.Segments.Count);
        }

        [Fact]
        public void Dois_vaos_separados_viram_dois_contornos()
        {
            var segs = new List<OpenEdgeSegment>(SideGapRectangle());
            foreach (OpenEdgeSegment s in SideGapRectangle())
            {
                // Segundo vão, deslocado 50 mm em Y — nenhuma ponta encosta no primeiro.
                s.StartMm[1] += 50; s.EndMm[1] += 50;
                segs.Add(s);
            }

            List<OpenEdgeLoop> loops = OpenEdgeLoops.Chain(segs, Tol);

            Assert.Equal(2, loops.Count);
            Assert.All(loops, l => Assert.True(l.IsSideGap));
            Assert.All(loops, l => Assert.Equal(4, l.Segments.Count));
        }

        [Fact]
        public void Pontas_dentro_da_tolerancia_sao_o_mesmo_ponto()
        {
            // A malha real não fecha em ponto exato: as pontas chegam com folga de µm.
            var segs = new List<OpenEdgeSegment>
            {
                Seg(0, 0, 0,      10, 0, 0),
                Seg(10.000004, 0, 0,  10, 0, 5),
                Seg(10, 0, 5,     0.000003, 0, 5),
                Seg(0, 0, 5.000002,   0, 0, 0),
            };

            OpenEdgeLoop loop = Assert.Single(OpenEdgeLoops.Chain(segs, Tol));
            Assert.True(loop.Closed);
            Assert.Equal(4, loop.Segments.Count);
        }

        [Fact]
        public void Aresta_fechada_em_si_e_um_contorno()
        {
            // Furo circular na superfície: UMA aresta cujo início é o próprio fim.
            var circle = new List<OpenEdgeSegment> { Seg(0, 0, 0, 0, 0, 0) };
            circle[0].ZMaxMm = 4; // arco que sobe: o bbox diz que é vertical
            circle[0].IsVertical = true;

            OpenEdgeLoop loop = Assert.Single(OpenEdgeLoops.Chain(circle, Tol));
            Assert.True(loop.Closed);
            Assert.True(loop.IsSideGap);
        }

        [Fact]
        public void Lista_vazia_nao_gera_contorno()
        {
            Assert.Empty(OpenEdgeLoops.Chain(new List<OpenEdgeSegment>(), Tol));
            Assert.Empty(OpenEdgeLoops.Chain(null, Tol));
        }

        [Fact]
        public void Toda_aresta_entra_em_exatamente_um_contorno()
        {
            var segs = new List<OpenEdgeSegment>(SideGapRectangle());
            segs.Add(Seg(30, 30, 0, 40, 30, 0)); // aresta solta, sem par

            List<OpenEdgeLoop> loops = OpenEdgeLoops.Chain(segs, Tol);

            Assert.Equal(segs.Count, loops.Sum(l => l.Segments.Count));
            Assert.Equal(1, loops.Count(l => l.IsSideGap));
        }

        // --- pontas soltas: é o que separa "corte reto" de "encadeamento quebrado" ---

        [Fact]
        public void Contorno_fechado_nao_tem_folga_entre_as_pontas()
        {
            OpenEdgeLoop loop = OpenEdgeLoops.Chain(SideGapRectangle(), Tol).Single();

            Assert.True(loop.Closed);
            Assert.Equal(0, loop.GapMm);
        }

        [Fact]
        public void Corte_reto_deixa_as_pontas_longe_uma_da_outra()
        {
            // Perfil que atravessa a peça: começa numa borda e termina na outra, 40 mm adiante.
            var segs = new List<OpenEdgeSegment>
            {
                Seg(0, 0, 0,   20, 0, 0),
                Seg(20, 0, 0,  20, 15, 0),
                Seg(20, 15, 0, 40, 15, 0),
            };

            OpenEdgeLoop loop = OpenEdgeLoops.Chain(segs, Tol).Single();

            Assert.False(loop.Closed);
            Assert.Equal(new double[] { 0, 0, 0 }, loop.StartMm);
            Assert.Equal(new double[] { 40, 15, 0 }, loop.EndMm);
            Assert.True(loop.GapMm > 40, $"folga {loop.GapMm} mm");
        }

        [Fact]
        public void Folga_maior_que_a_tolerancia_parte_um_perfil_so_em_dois_contornos()
        {
            // Quadrado com um vão de 0,05 mm num canto: passa da tolerância de 0,01 mm e quebra.
            var segs = new List<OpenEdgeSegment>
            {
                Seg(0, 0, 0,    10, 0, 0),
                Seg(10, 0, 0,   10, 10, 0),
                Seg(10, 10, 0,  0, 10, 0),
                Seg(0, 10, 0,   0, 0.05, 0),   // não encosta no início por 0,05 mm
            };

            List<OpenEdgeLoop> loops = OpenEdgeLoops.Chain(segs, Tol);

            OpenEdgeLoop loop = Assert.Single(loops);
            Assert.False(loop.Closed);
            Assert.Equal(0.05, loop.GapMm, 6);   // a folga denuncia: devia ter fechado
        }

        // ------------------------------------------------ classificação (EE01 do 14973.102, 2026-10-19)

        /// <summary>O rim real do EE01: 8 arestas em Z=3 (paredes retas e chanfros dos cantos).</summary>
        private static List<OpenEdgeSegment> RimEe01() => new List<OpenEdgeSegment>
        {
            Seg(7.65, -2.6, 3, 6.25, -4, 3), Seg(6.25, -4, 3, -6.25, -4, 3), Seg(-6.25, -4, 3, -7.65, -2.6, 3),
            Seg(-7.65, -2.6, 3, -7.65, 2.6, 3), Seg(-7.65, 2.6, 3, -6.25, 4, 3), Seg(-6.25, 4, 3, 6.25, 4, 3),
            Seg(6.25, 4, 3, 7.65, 2.6, 3), Seg(7.65, 2.6, 3, 7.65, -2.6, 3),
        };

        /// <summary>
        /// O caso que travou a união: um furo de pino no PISO (aresta circular fechada, horizontal,
        /// Z=1,011) era lido como "rim" porque não tem aresta vertical — e ficava aberto.
        /// </summary>
        [Fact]
        public void FuroNoPiso_EhRegiaoAFechar_ENaoRim()
        {
            var segs = RimEe01();
            var circle = Seg(-3.7407, 1.004, 1.011, -3.7407, 1.004, 1.011);
            circle.MinMm = new[] { -5.7407, 0.004, 1.011 }; circle.MaxMm = new[] { -3.7407, 2.004, 1.011 };
            segs.Add(circle);

            OpenEdgeAnalysis a = OpenEdgeLoops.Analyze(segs);

            Assert.NotNull(a.Rim);
            Assert.Equal(8, a.Rim.Segments.Count);
            Assert.Equal(3.0, a.Rim.ZMinMm, 6);
            Assert.Single(a.Holes);
            Assert.Same(circle, a.Holes[0].Segments.Single());
            Assert.Empty(a.SeamEdges);
            Assert.Empty(a.OpenLoops);
        }

        /// <summary>
        /// A leitura anterior do mesmo EE01: o recorte da parede Y=+4 encostava no piso e nos
        /// degraus com as pontas iguais até 0,0001 mm, mas a costura não juntou — 3 pares de arestas
        /// coincidentes. Não é furo (o "Limite" sobre fenda de largura zero dava E_FAIL).
        /// </summary>
        [Fact]
        public void ArestasCoincidentes_SaoCosturaNaoFechada_ENaoFuro()
        {
            var segs = RimEe01();
            var pairs = new[]
            {
                Seg(-5.2572, 4, 1.011, 5.2572, 4, 1.011), Seg(5.2572, 4, 1.011, -5.2572, 4, 1.011),
                Seg(5.2572, 4, 1.011, 5.2748, 4, 0), Seg(5.2572, 4, 1.011, 5.2748, 4, 0),
                Seg(-5.2748, 4, 0, -5.2572, 4, 1.011), Seg(-5.2748, 4, 0, -5.2572, 4, 1.011),
            };
            segs.AddRange(pairs);

            OpenEdgeAnalysis a = OpenEdgeLoops.Analyze(segs);

            Assert.Equal(6, a.SeamEdges.Count);
            Assert.Empty(a.Holes);
            Assert.Empty(a.OpenLoops);
            Assert.Equal(8, a.Rim.Segments.Count);
        }

        /// <summary>Furo em "D" (reta + arco com as MESMAS pontas): a caixa diferente impede que
        /// os dois lados sejam tomados por um par coincidente.</summary>
        [Fact]
        public void FuroEmD_NaoEhParCoincidente()
        {
            var segs = RimEe01();
            var line = Seg(0, 0, 1, 2, 0, 1); line.MinMm = new[] { 0.0, 0, 1 }; line.MaxMm = new[] { 2.0, 0, 1 };
            var arc = Seg(2, 0, 1, 0, 0, 1); arc.MinMm = new[] { 0.0, 0, 1 }; arc.MaxMm = new[] { 2.0, 1, 1 };
            segs.Add(line); segs.Add(arc);

            OpenEdgeAnalysis a = OpenEdgeLoops.Analyze(segs);

            Assert.Empty(a.SeamEdges);
            Assert.Single(a.Holes);
            Assert.Equal(2, a.Holes[0].Segments.Count);
        }

        /// <summary>Vão lateral (o caso antigo, com arestas verticais) continua sendo região a fechar.</summary>
        [Fact]
        public void VaoLateral_ContinuaSendoFechado()
        {
            var segs = RimEe01();
            segs.AddRange(new[] { Seg(-2, -4, 0, 2, -4, 0), Seg(2, -4, 0, 2, -4, 1), Seg(2, -4, 1, -2, -4, 1), Seg(-2, -4, 1, -2, -4, 0) });

            OpenEdgeAnalysis a = OpenEdgeLoops.Analyze(segs);

            Assert.Single(a.Holes);
            Assert.Equal(2, a.Holes[0].VerticalCount);
            Assert.Equal(3.0, a.Rim.ZMaxMm, 6);
        }
    }
}
