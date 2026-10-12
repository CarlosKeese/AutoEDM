using System;
using System.Collections.Generic;
using System.Linq;
using AutoEDM.Reverse;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// Direção de extrusão: o frame (permutação cíclica dos eixos) e a escolha da fachada. A peça
    /// que motivou (vector_v2-6, canal em U dobrado) é 2,5D em Y e não em Z — aqui o mesmo caso
    /// em miniatura: um perfil em V no plano XZ extrudado ao longo de Y.
    /// </summary>
    public class ExtrusionFrameTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void Frame_ida_e_volta_e_rotacao_propria(int axis)
        {
            var f = new ExtrusionFrame(axis);
            double[] a = { 1.5, -2, 7 }, b = { -3, 0.25, 4 };
            Assert.Equal(a, f.ToWorld(f.ToLocal(a)));

            // Z local é o eixo escolhido do mundo.
            double[] z = f.LocalAxisInWorld(2);
            Assert.Equal(1.0, z[axis]);

            // Rotação PRÓPRIA: o produto vetorial atravessa a troca (normal para fora continua).
            double[] crossLocal = Cross(f.ToLocal(a), f.ToLocal(b));
            Assert.Equal(f.ToLocal(Cross(a, b)), crossLocal);
        }

        [Fact]
        public void Malha_e_reconhecimento_vao_juntos_para_o_frame()
        {
            double[] mesh = PrismY(Chevron(), 0, 15, ChevronPieces);
            RecognitionResult r = SurfaceRecognizer.Recognize(mesh, new RecognizerOptions());
            var f = new ExtrusionFrame(1);
            RecognitionResult l = f.ToLocal(r);

            Assert.Same(r.RegionOfTriangle, l.RegionOfTriangle);
            // As tampas (normal ±Y no mundo) viram planos ±Z exatos no frame — é o que a secção exige.
            Assert.Equal(2, l.Surfaces.Count(s => s.Kind == "plano" && Math.Abs(s.Normal[2]) == 1.0));
            Assert.Equal(PrismaticSection.MeshVolume(mesh), PrismaticSection.MeshVolume(f.ToLocalPoints(mesh)), 6);
        }

        [Fact]
        public void Perfil_em_V_ao_longo_de_Y_sai_em_Y_e_exato()
        {
            double[] mesh = PrismY(Chevron(), 0, 15, ChevronPieces);
            // Sem arredondamento: a distância das faces inclinadas do V à origem não é redonda, e
            // levá-la ao centésimo mexeria no volume — não é isso que este teste mede.
            RecognitionResult r = SurfaceRecognizer.Recognize(mesh, new RecognizerOptions { RoundToMm = 0 });

            MeshToSolidPlan p = MeshToSolidPlanner.Plan(mesh, r, null);

            Assert.Equal(1, p.Frame.Axis);
            Assert.Single(p.Plan.Bands);
            Assert.Equal(ChevronArea() * 15, p.Plan.ExactVolumeMm3, 3);
            Assert.True(p.Evaluation.FarErrorMm3 < 1, "erro de forma previsto " + p.Evaluation.FarErrorMm3);
            // Z não serviu; X, Y e as normais das paredes inclinadas do V foram avaliadas.
            Assert.True(p.DirectionNotes.Count >= 3, string.Join(" | ", p.DirectionNotes));

            string report = PrismaticReport.Format(p, null, "V", r);
            Assert.Contains("faixas Y", report);
            Assert.Contains("Níveis Y (mm): 0.00 15.00", report);
        }

        [Fact]
        public void Peca_girada_no_espaco_sai_na_normal_das_tampas()
        {
            // O V extrudado em Y, girado 30° em volta de X — como as peças do
            // frame-fidget-mini-claw-machine: nenhum eixo do mundo é a direção de extrusão.
            double[] mesh = RotateX(PrismY(Chevron(), 0, 15, ChevronPieces), 30);
            RecognitionResult r = SurfaceRecognizer.Recognize(mesh, new RecognizerOptions { RoundToMm = 0 });

            MeshToSolidPlan p = MeshToSolidPlanner.Plan(mesh, r, null);

            Assert.Equal(-1, p.Frame.Axis);
            double[] d = p.Frame.Direction;
            Assert.Equal(Math.Cos(Math.PI / 6), Math.Abs(d[1]), 6); // a normal das tampas, girada junto
            Assert.Equal(0.5, Math.Abs(d[2]), 6);
            Assert.Single(p.Plan.Bands);
            Assert.Equal(ChevronArea() * 15, p.Plan.ExactVolumeMm3, 2);
            Assert.True(p.Evaluation.FarErrorMm3 < 1, "erro de forma previsto " + p.Evaluation.FarErrorMm3);
        }

        [Fact]
        public void Frame_de_direcao_qualquer_e_rotacao_propria()
        {
            ExtrusionFrame f = ExtrusionFrame.Along(new[] { 0.5, 0, Math.Sqrt(3) / 2 });
            double[] a = { 1.5, -2, 7 }, b = { -3, 0.25, 4 };
            double[] back = f.ToWorld(f.ToLocal(a));
            for (int k = 0; k < 3; k++) Assert.Equal(a[k], back[k], 12);
            double[] c1 = Cross(f.ToLocal(a), f.ToLocal(b)), c2 = f.ToLocal(Cross(a, b));
            for (int k = 0; k < 3; k++) Assert.Equal(c2[k], c1[k], 12);
            Assert.Equal(new[] { 0.0, 0, 1 }, f.ToLocal(new[] { 0.5, 0, Math.Sqrt(3) / 2 }).Select(v => Math.Round(v, 12)).ToArray());
            Assert.Equal(1, ExtrusionFrame.Along(new[] { 0.0, -2, 0 }).Axis); // eixo do mundo: volta ao caminho exato
        }

        [Fact]
        public void Placa_em_Z_continua_em_Z_sem_avaliar_as_outras()
        {
            double[] mesh = PrismY(new List<double[]> { new[] { 0.0, 0.0 }, new[] { 30.0, 0.0 }, new[] { 30.0, 5.0 }, new[] { 0.0, 5.0 } }, 0, 20);
            RecognitionResult r = SurfaceRecognizer.Recognize(mesh, new RecognizerOptions());

            MeshToSolidPlan p = MeshToSolidPlanner.Plan(mesh, r, null);

            Assert.Equal(2, p.Frame.Axis);
            Assert.Single(p.DirectionNotes);
            Assert.Equal(30 * 5 * 20, p.Plan.ExactVolumeMm3, 3);
        }

        [Fact]
        public void Direcao_imposta_vale_mesmo_quando_Z_bastaria()
        {
            double[] mesh = PrismY(new List<double[]> { new[] { 0.0, 0.0 }, new[] { 30.0, 0.0 }, new[] { 30.0, 5.0 }, new[] { 0.0, 5.0 } }, 0, 20);
            RecognitionResult r = SurfaceRecognizer.Recognize(mesh, new RecognizerOptions());

            MeshToSolidPlan p = MeshToSolidPlanner.Plan(mesh, r, "x");

            Assert.Equal(0, p.Frame.Axis);
            Assert.True(p.Forced);
            Assert.Equal(30 * 5 * 20, p.Plan.ExactVolumeMm3, 3);
        }

        [Fact]
        public void Contorno_cruzado_e_denunciado_e_o_quadrado_nao()
        {
            Func<double[][], SectionBand> band = pts =>
            {
                var b = new SectionBand { Z0 = 0, Z1 = 1 };
                var l = new SectionLoop();
                for (int i = 0; i < pts.Length; i++)
                    l.Elements.Add(new SectionElement { Kind = "reta", Start = pts[i], End = pts[(i + 1) % pts.Length], Exact = true });
                b.Loops.Add(l);
                return b;
            };
            double[][] square = { new[] { 0.0, 0 }, new[] { 10.0, 0 }, new[] { 10.0, 10 }, new[] { 0.0, 10 } };
            // O laço da ponta da perna da vector_v2-6, em miniatura: um trecho volta 0,02 mm e o
            // seguinte atravessa a parede de onde veio.
            double[][] bow = { new[] { 0.0, 0 }, new[] { 10.0, 0 }, new[] { 10.0, 10 }, new[] { 0.0, 10 }, new[] { 0.0, 0.02 }, new[] { -0.02, 0.04 }, new[] { 2.0, -1 } };

            Assert.Null(PrismaticSection.Crossing(band(square)));
            Assert.NotNull(PrismaticSection.Crossing(band(bow)));
        }

        [Fact]
        public void Degraus_de_faixa_cobrem_rampa_e_raio_deitados()
        {
            // A divisória do tool-organizer_v2 em miniatura (perfil em XZ, extrudado 15 em Y):
            // parede vertical, rampa de 45° e raio R10 tangente à rampa e ao topo. Em Z, a faixa
            // da rampa e a do raio ficam em degrau; o canto e o segmento do raio e os dois
            // triângulos da rampa não cobriam o degrau — sobrava um triângulo acima da tangente
            // e a rampa inteira ficava "igual" pelos pontos de teste.
            double c = 20, R = 10, zc = 10;
            double xe = c - R * Math.Sqrt(0.5), ze = zc + R * Math.Sqrt(0.5); // fim do arco, a 135°
            double xf = xe - (ze - 5);                                        // pé da rampa, em z = 5
            var profile = new List<double[]> { new[] { xf, 0.0 }, new[] { 30.0, 0.0 }, new[] { 30.0, 20.0 } };
            for (int k = 0; k <= 12; k++)
            {
                double t = Math.PI / 2 + Math.PI / 4 * k / 12;
                profile.Add(new[] { c + R * Math.Cos(t), zc + R * Math.Sin(t) });
            }
            profile.Add(new[] { xf, 5.0 });
            double[] mesh = PrismY(profile, 0, 15);
            RecognitionResult r = SurfaceRecognizer.Recognize(mesh, new RecognizerOptions { RoundToMm = 0 });

            MeshToSolidPlan z = MeshToSolidPlanner.Plan(mesh, r, "z");

            Assert.True(z.Plan.Bands.Count >= 3, "faixas " + z.Plan.Bands.Count);
            Assert.Contains(z.Corrections.Regions, x => x.BandStep);
            Assert.True(z.Evaluation.FarErrorMm3 < 1, "erro de forma previsto em Z " + z.Evaluation.FarErrorMm3);

            // Escolhendo sozinho: Z precisou de degrau, então X e Y são avaliados — e Y, onde o
            // perfil é exato numa faixa só, vence o empate pelo número de faixas.
            MeshToSolidPlan auto = MeshToSolidPlanner.Plan(mesh, r, null);
            Assert.Equal(1, auto.Frame.Axis);
            Assert.Single(auto.Plan.Bands);
        }

        [Fact]
        public void Cantos_arco_reta_chanfrados_em_5_centesimos()
        {
            // O enchimento do R20 das divisórias do tool-organizer_v2: segmento de 45° entre o
            // arco (pontas tangentes ao topo e à rampa) e a corda.
            double r = 20, a0 = Math.PI / 2, a1 = Math.PI / 2 + Math.PI / 4;
            double[] s0 = { r * Math.Cos(a0), r * Math.Sin(a0) }, e0 = { r * Math.Cos(a1), r * Math.Sin(a1) };
            double[] m0 = { r * Math.Cos((a0 + a1) / 2), r * Math.Sin((a0 + a1) / 2) };
            var c = new CorrectionRegion { Kind = "extrusao", Add = true, Label = "R20" };
            c.Edges.Add(new ProfileEdge { Kind = "arco", Start = e0, Mid = m0, End = s0 });
            c.Edges.Add(new ProfileEdge { Kind = "reta", Start = s0, End = e0 });

            CorrectionRegion t = PrismaticBuilder.WithArcTipsCut(c);
            Assert.NotNull(t);
            Assert.Equal(new[] { "arco", "reta", "reta", "reta" }, t.Edges.Select(e => e.Kind).ToArray());
            // Fechado, ponta com ponta.
            for (int k = 0; k < t.Edges.Count; k++)
                Assert.Equal(t.Edges[k].End, t.Edges[(k + 1) % t.Edges.Count].Start);
            // Nenhum vértice ficou nos pontos de tangência; os novos estão a 0,05 deles.
            foreach (ProfileEdge e in t.Edges)
            {
                Assert.True(Dist(e.Start, s0) > 0.04 && Dist(e.Start, e0) > 0.04);
                Assert.True(Math.Min(Dist(e.Start, s0), Dist(e.Start, e0)) < 0.051);
            }
            // O arco continua no mesmo círculo, com o meio entre as pontas novas.
            ProfileEdge arc = t.Edges[0];
            foreach (double[] p in new[] { arc.Start, arc.Mid, arc.End })
                Assert.Equal(r, Math.Sqrt(p[0] * p[0] + p[1] * p[1]), 9);
            double am = Math.Atan2(arc.Mid[1], arc.Mid[0]);
            Assert.Equal((a0 + a1) / 2, am, 9);
            Assert.True(Math.Atan2(arc.Start[1], arc.Start[0]) < a1 && Math.Atan2(arc.End[1], arc.End[0]) > a0);

            // Sem arco, nada a aparar.
            var square = new CorrectionRegion();
            square.Edges.Add(new ProfileEdge { Kind = "reta", Start = new double[] { 0, 0 }, End = new double[] { 1, 0 } });
            Assert.Null(PrismaticBuilder.WithArcTipsCut(square));
        }

        [Fact]
        public void Cilindro_deitado_da_niveis_no_alto_e_no_baixo()
        {
            // O pino Ø3 do elo do frame-fidget: eixo X, centro em z = 3.
            var pin = new RecognizedSurface { Kind = "cilindro", AxisMm = new double[] { 1, 0, 0 }, PointMm = new double[] { 0, 0, 3 }, RadiusMm = 1.5, SweepDeg = 360 };
            Assert.Equal(new[] { 1.5, 4.5 }, PrismaticSection.LyingCylinderExtremes(pin).OrderBy(z => z).ToArray());

            // Meia-cana virada para cima (arco de 180° de +Y, passando por +Z, até −Y): só o alto.
            var half = new RecognizedSurface
            {
                Kind = "cilindro", AxisMm = new double[] { 1, 0, 0 }, PointMm = new double[] { 0, 0, 3 }, RadiusMm = 1.5,
                SweepDeg = 180, ArcFrom = new double[] { 0, 1, 0 }, ArcTo = new double[] { 0, -1, 0 }
            };
            Assert.Equal(new[] { 4.5 }, PrismaticSection.LyingCylinderExtremes(half).ToArray());

            // Raio de borda de 90° não pede faixa; cilindro em pé (eixo Z) também não.
            var edge = new RecognizedSurface { Kind = "cilindro", AxisMm = new double[] { 1, 0, 0 }, PointMm = new double[] { 0, 0, 3 }, RadiusMm = 0.5, SweepDeg = 90, ArcFrom = new double[] { 0, 0, 1 }, ArcTo = new double[] { 0, -1, 0 } };
            var standing = new RecognizedSurface { Kind = "cilindro", AxisMm = new double[] { 0, 0, 1 }, PointMm = new double[] { 0, 0, 3 }, RadiusMm = 2, SweepDeg = 360 };
            Assert.Empty(PrismaticSection.LyingCylinderExtremes(edge));
            Assert.Empty(PrismaticSection.LyingCylinderExtremes(standing));
        }

        // ------------------------------------------------------------------ malhas

        /// <summary>Perfil em V invertido (duas pernas inclinadas, como a vector_v2-6 vista de lado)
        /// no plano (x, z).</summary>
        private static List<double[]> Chevron()
        {
            return new List<double[]>
            {
                new[] { 0.0, 0.0 }, new[] { 6.0, 0.0 }, new[] { 20.0, 14.0 }, new[] { 34.0, 0.0 },
                new[] { 40.0, 0.0 }, new[] { 20.0, 20.0 }
            };
        }

        private static double ChevronArea()
        {
            List<double[]> p = Chevron();
            double a = 0;
            for (int i = 0; i < p.Count; i++)
            {
                double[] u = p[i], v = p[(i + 1) % p.Count];
                a += u[0] * v[1] - v[0] * u[1];
            }
            return Math.Abs(a) / 2;
        }

        /// <summary>Prisma ao longo de Y (de y0 a y1) sobre um polígono em (x, z); as tampas são
        /// os pedaços CONVEXOS dados (índices do polígono), cada um em leque. Sem pedaços, a tampa
        /// inteira é um leque (só para polígono convexo). Normais corrigidas para fora.</summary>
        private static double[] PrismY(List<double[]> poly, double y0, double y1, params int[][] capPieces)
        {
            var pts = new List<double>();
            int n = poly.Count;
            Func<double[], double, double[]> at = (q, y) => new[] { q[0], y, q[1] };
            for (int i = 0; i < n; i++)
            {
                double[] a = poly[i], b = poly[(i + 1) % n];
                Tri(pts, at(a, y0), at(b, y0), at(b, y1));
                Tri(pts, at(a, y0), at(b, y1), at(a, y1));
            }
            if (capPieces.Length == 0) capPieces = new[] { Enumerable.Range(0, n).ToArray() };
            foreach (int[] piece in capPieces)
                for (int k = 1; k + 1 < piece.Length; k++)
                {
                    double[] a = poly[piece[0]], b = poly[piece[k]], c = poly[piece[k + 1]];
                    Tri(pts, at(a, y0), at(c, y0), at(b, y0));
                    Tri(pts, at(a, y1), at(b, y1), at(c, y1));
                }
            return Outward(pts.ToArray());
        }

        /// <summary>As duas pernas do V, convexas: (0, 1, 2, 5) e (2, 3, 4, 5).</summary>
        private static readonly int[][] ChevronPieces = { new[] { 0, 1, 2, 5 }, new[] { 2, 3, 4, 5 } };

        private static double[] RotateX(double[] m, double deg)
        {
            double c = Math.Cos(deg * Math.PI / 180), sn = Math.Sin(deg * Math.PI / 180);
            var o = new double[m.Length];
            for (int i = 0; i < m.Length; i += 3)
            {
                o[i] = m[i];
                o[i + 1] = c * m[i + 1] - sn * m[i + 2];
                o[i + 2] = sn * m[i + 1] + c * m[i + 2];
            }
            return o;
        }

        private static void Tri(List<double> pts, double[] a, double[] b, double[] c)
        {
            pts.AddRange(a); pts.AddRange(b); pts.AddRange(c);
        }

        private static double[] Outward(double[] m)
        {
            if (PrismaticSection.MeshVolume(m) >= 0) return m;
            var o = (double[])m.Clone();
            for (int b = 0; b < o.Length; b += 9)
                for (int k = 0; k < 3; k++) { double t = o[b + 3 + k]; o[b + 3 + k] = o[b + 6 + k]; o[b + 6 + k] = t; }
            return o;
        }

        private static double Dist(double[] a, double[] b) => Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]));

        private static double[] Cross(double[] a, double[] b)
        {
            return new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
        }
    }
}
