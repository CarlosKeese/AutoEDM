using System;
using System.Collections.Generic;
using System.Linq;
using AutoEDM.Reverse;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// Secção 2,5D: de malha + superfícies reconhecidas para faixas Z com contorno EXATO.
    /// Todas as malhas são sólidos fechados gerados com geometria conhecida, normais para fora.
    /// </summary>
    public class PrismaticSectionTests
    {
        private static PrismaticPlan PlanOf(double[] mesh)
        {
            RecognitionResult r = SurfaceRecognizer.Recognize(mesh, null);
            return PrismaticSection.Plan(mesh, r, null);
        }

        [Fact]
        public void Bloco_vira_uma_faixa_com_quatro_retas_nos_cantos_certos()
        {
            PrismaticPlan p = PlanOf(Box(0, 0, 0, 20, 30, 40));

            SectionBand b = Assert.Single(p.Bands);
            Assert.Equal(0, b.Z0, 9);
            Assert.Equal(40, b.Z1, 9);

            SectionLoop l = Assert.Single(b.Loops);
            Assert.Equal(4, l.Elements.Count);
            Assert.All(l.Elements, e => { Assert.Equal("reta", e.Kind); Assert.True(e.Exact); });

            var cantos = l.Elements.Select(e => Tuple.Create(Math.Round(e.Start[0], 9), Math.Round(e.Start[1], 9)))
                                   .OrderBy(t => t.Item1).ThenBy(t => t.Item2).ToList();
            Assert.Equal(new[] { Tuple.Create(0d, 0d), Tuple.Create(0d, 30d), Tuple.Create(20d, 0d), Tuple.Create(20d, 30d) }, cantos);

            Assert.Equal(24000, p.MeshVolumeMm3, 6);
            Assert.Equal(24000, p.PlanVolumeMm3, 6);
            Assert.True(l.SignedAreaMm2 > 0, "contorno externo tem de sair anti-horário");
            Assert.Empty(p.Warnings);
        }

        [Fact]
        public void Degrau_vira_duas_faixas_com_contornos_diferentes()
        {
            // Base 40×20×10 e ressalto 20×20×10 por cima, de x = 10 a 30.
            double[] mesh = Step();

            PrismaticPlan p = PlanOf(mesh);

            Assert.Equal(new[] { 0d, 10d, 20d }, p.Levels.Select(z => Math.Round(z, 9)).ToArray());
            Assert.Equal(2, p.Bands.Count);
            Assert.Equal(800, Math.Abs(p.Bands[0].Loops.Single().SignedAreaMm2), 6);
            Assert.Equal(400, Math.Abs(p.Bands[1].Loops.Single().SignedAreaMm2), 6);

            var xs = p.Bands[1].Loops.Single().Elements.Select(e => Math.Round(e.Start[0], 9)).Distinct().OrderBy(x => x);
            Assert.Equal(new[] { 10d, 30d }, xs);
            Assert.Equal(12000, p.PlanVolumeMm3, 6);
        }

        [Fact]
        public void Furo_passante_vira_circulo_exato_dentro_do_contorno()
        {
            // Placa 40×40×10 com furo Ø10 no centro, tesselado em 64 lados.
            double[] mesh = PlateWithHole(side: 40, thickness: 10, holeRadius: 5, segments: 64);

            PrismaticPlan p = PlanOf(mesh);

            SectionBand b = Assert.Single(p.Bands);
            Assert.Equal(2, b.Loops.Count);

            SectionLoop furo = b.Loops.Single(l => l.Elements.Count == 1);
            SectionElement c = furo.Elements[0];
            Assert.Equal("circulo", c.Kind);
            Assert.Equal(10.00, 2 * c.RadiusMm, 9);
            Assert.Equal(20.00, c.Center[0], 9);
            Assert.Equal(20.00, c.Center[1], 9);
            Assert.True(furo.SignedAreaMm2 < 0, "furo tem de sair horário (subtrai área)");

            SectionLoop externo = b.Loops.Single(l => l.Elements.Count == 4);
            Assert.All(externo.Elements, e => Assert.Equal("reta", e.Kind));

            // O plano confere com a malha (os dois usam o polígono da tesselação)…
            Assert.Equal(p.MeshVolumeMm3, p.PlanVolumeMm3, 6);
            // …e o volume EXATO é o da peça de verdade: 40·40·10 − π·5²·10. A malha, com o furo
            // inscrito, tem material A MAIS — é por isso que o sólido não se confere contra ela.
            Assert.Equal(16000 - Math.PI * 25 * 10, p.ExactVolumeMm3, 6);
            Assert.True(p.MeshVolumeMm3 > p.ExactVolumeMm3);
        }

        [Fact]
        public void Rasgo_arredondado_da_retas_e_arcos_com_vertice_de_tangencia_exato()
        {
            double[] mesh = Prism(Stadium(centres: 30, radius: 7, segmentsPerEnd: 18), height: 6);

            PrismaticPlan p = PlanOf(mesh);

            SectionLoop l = Assert.Single(Assert.Single(p.Bands).Loops);
            Assert.Equal(2, l.Elements.Count(e => e.Kind == "reta"));
            Assert.Equal(2, l.Elements.Count(e => e.Kind == "arco"));

            // Área exata do oblongo: 30·14 + π·7².
            Assert.Equal(30 * 14 + Math.PI * 49, l.ExactAreaMm2, 6);

            // Vértices de tangência: (±15; ±7), no centésimo.
            var vertices = l.Elements.Select(e => Tuple.Create(Math.Round(e.Start[0], 6), Math.Round(e.Start[1], 6)))
                                     .OrderBy(t => t.Item1).ThenBy(t => t.Item2).ToList();
            Assert.Equal(new[] { Tuple.Create(-15d, -7d), Tuple.Create(-15d, 7d), Tuple.Create(15d, -7d), Tuple.Create(15d, 7d) }, vertices);

            // O ponto do meio (que fixa o sentido do arco por 3 pontos) está SOBRE o círculo e do
            // lado da ponta do rasgo — não do lado de dentro, que daria o arco complementar.
            Assert.All(l.Elements.Where(e => e.Kind == "arco"), e =>
            {
                double dx = e.Mid[0] - e.Center[0], dy = e.Mid[1] - e.Center[1];
                Assert.Equal(7.0, Math.Sqrt(dx * dx + dy * dy), 9);
                Assert.True(Math.Abs(e.Mid[0]) > 15 + 6, $"meio do arco em x = {e.Mid[0]}: lado errado");
            });
        }

        [Fact]
        public void Lacos_que_se_tocam_num_vertice_saem_separados()
        {
            // Dois blocos que só se tocam pela aresta vertical em (10; 10): a secção tem dois
            // quadrados com UM vértice em comum. É a geometria do furo tangente à parede do
            // bolsão (malha 6 do Rack_Anghinoni), em que o encadeamento ingênuo pulava de um
            // laço para o outro e a faixa saía com área negativa.
            double[] mesh = Box(0, 0, 0, 10, 10, 5).Concat(Box(10, 10, 0, 20, 20, 5)).ToArray();

            PrismaticPlan p = PlanOf(mesh);

            SectionBand b = Assert.Single(p.Bands);
            Assert.Equal(2, b.Loops.Count);
            Assert.All(b.Loops, l =>
            {
                Assert.Equal(4, l.Elements.Count);
                Assert.Equal(100, l.SignedAreaMm2, 6);
            });
            Assert.Equal(1000, p.PlanVolumeMm3, 6);
        }

        [Fact]
        public void Faixa_prismatica_nao_gera_aviso_de_forma()
        {
            PrismaticPlan p = PlanOf(PlateWithHole(side: 40, thickness: 10, holeRadius: 5, segments: 64));

            Assert.True(Assert.Single(p.Bands).AreaVariationMm2 < 1e-6);
            Assert.DoesNotContain(p.Warnings, w => w.Contains("NÃO é prismática"));
        }

        [Fact]
        public void Piramide_truncada_e_denunciada_como_nao_prismatica()
        {
            // Tronco de pirâmide: base 20×20, topo 10×10, altura 10 — o caso do ângulo de saída.
            // A secção do meio (15×15) faria uma extrusão reta com o volume ERRADO sem aviso
            // nenhum; a variação da área ao longo da faixa é o que denuncia.
            double[] mesh = Frustum(bottom: 20, top: 10, height: 10);

            PrismaticPlan p = PlanOf(mesh);

            Assert.Contains(p.Warnings, w => w.Contains("NÃO é prismática"));
            Assert.True(Assert.Single(p.Bands).AreaVariationMm2 > 50);
        }

        [Fact]
        public void Relatorio_poe_os_tres_volumes_lado_a_lado_e_os_avisos()
        {
            string limpo = PrismaticReport.Format(PlanOf(PlateWithHole(40, 10, 5, 64)), null, "placa");
            Assert.Contains("plano EXATO", limpo);
            Assert.Contains("Sem avisos", limpo);
            Assert.Contains("1 círculo(s)", limpo);

            string saida = PrismaticReport.Format(PlanOf(Frustum(20, 10, 10)), null, "tronco");
            Assert.Contains("NÃO é prismática", saida);
            Assert.DoesNotContain("Sem avisos", saida);
        }

        [Fact]
        public void Recusa_reconhecimento_de_outra_malha()
        {
            RecognitionResult r = SurfaceRecognizer.Recognize(Box(0, 0, 0, 10, 10, 10), null);
            Assert.Throws<ArgumentException>(() => PrismaticSection.Plan(Box(0, 0, 0, 10, 10, 10).Concat(Box(20, 0, 0, 30, 10, 10)).ToArray(), r, null));
        }

        // ------------------------------------------------------------------ malhas de teste

        private static double[] Box(double x0, double y0, double z0, double x1, double y1, double z1)
        {
            var pts = new List<double>();
            Quad(pts, new[] { x0, y0, z0 }, new[] { x0, y1, z0 }, new[] { x1, y1, z0 }, new[] { x1, y0, z0 }); // Z−
            Quad(pts, new[] { x0, y0, z1 }, new[] { x1, y0, z1 }, new[] { x1, y1, z1 }, new[] { x0, y1, z1 }); // Z+
            Quad(pts, new[] { x0, y0, z0 }, new[] { x1, y0, z0 }, new[] { x1, y0, z1 }, new[] { x0, y0, z1 }); // Y−
            Quad(pts, new[] { x1, y1, z0 }, new[] { x0, y1, z0 }, new[] { x0, y1, z1 }, new[] { x1, y1, z1 }); // Y+
            Quad(pts, new[] { x1, y0, z0 }, new[] { x1, y1, z0 }, new[] { x1, y1, z1 }, new[] { x1, y0, z1 }); // X+
            Quad(pts, new[] { x0, y1, z0 }, new[] { x0, y0, z0 }, new[] { x0, y0, z1 }, new[] { x0, y1, z1 }); // X−
            return pts.ToArray();
        }

        /// <summary>Base 40×20×10 + ressalto 20×20×10 (x 10..30) por cima, fechado e com
        /// normais para fora.</summary>
        private static double[] Step()
        {
            var pts = new List<double>();
            // base
            Quad(pts, new[] { 0.0, 0, 0 }, new[] { 0.0, 20, 0 }, new[] { 40.0, 20, 0 }, new[] { 40.0, 0, 0 });      // fundo
            Quad(pts, new[] { 0.0, 0, 10 }, new[] { 10.0, 0, 10 }, new[] { 10.0, 20, 10 }, new[] { 0.0, 20, 10 });  // topo esq.
            Quad(pts, new[] { 30.0, 0, 10 }, new[] { 40.0, 0, 10 }, new[] { 40.0, 20, 10 }, new[] { 30.0, 20, 10 }); // topo dir.
            Quad(pts, new[] { 0.0, 0, 0 }, new[] { 40.0, 0, 0 }, new[] { 40.0, 0, 10 }, new[] { 0.0, 0, 10 });      // Y−
            Quad(pts, new[] { 40.0, 20, 0 }, new[] { 0.0, 20, 0 }, new[] { 0.0, 20, 10 }, new[] { 40.0, 20, 10 });  // Y+
            Quad(pts, new[] { 40.0, 0, 0 }, new[] { 40.0, 20, 0 }, new[] { 40.0, 20, 10 }, new[] { 40.0, 0, 10 });  // X+
            Quad(pts, new[] { 0.0, 20, 0 }, new[] { 0.0, 0, 0 }, new[] { 0.0, 0, 10 }, new[] { 0.0, 20, 10 });      // X−
            // ressalto
            Quad(pts, new[] { 10.0, 0, 20 }, new[] { 30.0, 0, 20 }, new[] { 30.0, 20, 20 }, new[] { 10.0, 20, 20 }); // topo
            Quad(pts, new[] { 10.0, 0, 10 }, new[] { 30.0, 0, 10 }, new[] { 30.0, 0, 20 }, new[] { 10.0, 0, 20 });   // Y−
            Quad(pts, new[] { 30.0, 20, 10 }, new[] { 10.0, 20, 10 }, new[] { 10.0, 20, 20 }, new[] { 30.0, 20, 20 }); // Y+
            Quad(pts, new[] { 30.0, 0, 10 }, new[] { 30.0, 20, 10 }, new[] { 30.0, 20, 20 }, new[] { 30.0, 0, 20 });  // X+
            Quad(pts, new[] { 10.0, 20, 10 }, new[] { 10.0, 0, 10 }, new[] { 10.0, 0, 20 }, new[] { 10.0, 20, 20 });  // X−
            return pts.ToArray();
        }

        /// <summary>Placa quadrada com furo redondo no centro. As tampas são um anel entre o
        /// quadrado (amostrado nos mesmos ângulos do furo, com os cantos a 45°) e o círculo.</summary>
        private static double[] PlateWithHole(double side, double thickness, double holeRadius, int segments)
        {
            double c = side / 2;
            var outer = new List<double[]>();
            var inner = new List<double[]>();
            for (int i = 0; i < segments; i++)
            {
                double a = 2 * Math.PI * i / segments;
                double ca = Math.Cos(a), sa = Math.Sin(a);
                double k = c / Math.Max(Math.Abs(ca), Math.Abs(sa));
                outer.Add(new[] { c + k * ca, c + k * sa });
                inner.Add(new[] { c + holeRadius * ca, c + holeRadius * sa });
            }

            var pts = new List<double>();
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % segments;
                double[] o0 = outer[i], o1 = outer[j], i0 = inner[i], i1 = inner[j];
                // tampas (topo +Z, fundo −Z)
                Quad(pts, P3(i0, thickness), P3(o0, thickness), P3(o1, thickness), P3(i1, thickness));
                Quad(pts, P3(i0, 0), P3(i1, 0), P3(o1, 0), P3(o0, 0));
                // parede externa e parede do furo
                Quad(pts, P3(o0, 0), P3(o1, 0), P3(o1, thickness), P3(o0, thickness));
                Quad(pts, P3(i1, 0), P3(i0, 0), P3(i0, thickness), P3(i1, thickness));
            }
            return pts.ToArray();
        }

        private static double[] Prism(List<double[]> poly, double height)
        {
            var pts = new List<double>();
            int n = poly.Count;
            double cxp = poly.Average(q => q[0]), cyp = poly.Average(q => q[1]);
            for (int i = 0; i < n; i++)
            {
                double[] a = poly[i], b = poly[(i + 1) % n];
                Quad(pts, P3(a, 0), P3(b, 0), P3(b, height), P3(a, height));
                Tri(pts, new[] { cxp, cyp, 0 }, P3(b, 0), P3(a, 0));
                Tri(pts, new[] { cxp, cyp, height }, P3(a, height), P3(b, height));
            }
            return pts.ToArray();
        }

        private static List<double[]> Stadium(double centres, double radius, int segmentsPerEnd)
        {
            var poly = new List<double[]>();
            double h = centres / 2;
            for (int i = 0; i <= segmentsPerEnd; i++)
            {
                double a = -Math.PI / 2 + Math.PI * i / segmentsPerEnd;
                poly.Add(new[] { h + radius * Math.Cos(a), radius * Math.Sin(a) });
            }
            for (int i = 0; i <= segmentsPerEnd; i++)
            {
                double a = Math.PI / 2 + Math.PI * i / segmentsPerEnd;
                poly.Add(new[] { -h + radius * Math.Cos(a), radius * Math.Sin(a) });
            }
            return poly;
        }

        /// <summary>Tronco de pirâmide de base quadrada centrado na origem.</summary>
        private static double[] Frustum(double bottom, double top, double height)
        {
            double b = bottom / 2, t = top / 2;
            double[][] lo = { new[] { -b, -b, 0 }, new[] { b, -b, 0 }, new[] { b, b, 0 }, new[] { -b, b, 0 } };
            double[][] hi = { new[] { -t, -t, height }, new[] { t, -t, height }, new[] { t, t, height }, new[] { -t, t, height } };
            var pts = new List<double>();
            Quad(pts, lo[0], lo[3], lo[2], lo[1]); // fundo (Z−)
            Quad(pts, hi[0], hi[1], hi[2], hi[3]); // topo (Z+)
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                Quad(pts, lo[i], lo[j], hi[j], hi[i]);
            }
            return pts.ToArray();
        }

        private static double[] P3(double[] xy, double z) => new[] { xy[0], xy[1], z };

        /// <summary>Quadrilátero a-b-c-d anti-horário visto de fora → dois triângulos.</summary>
        private static void Quad(List<double> pts, double[] a, double[] b, double[] c, double[] d)
        {
            Tri(pts, a, b, c);
            Tri(pts, a, c, d);
        }

        private static void Tri(List<double> pts, double[] a, double[] b, double[] c)
        {
            pts.AddRange(a);
            pts.AddRange(b);
            pts.AddRange(c);
        }
    }
}
