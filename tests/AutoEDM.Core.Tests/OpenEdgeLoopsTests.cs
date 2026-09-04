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
    }
}
