using System;
using System.Collections.Generic;
using System.Linq;
using AutoEDM.Reverse;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// Testes do reconhecimento de superfície sobre malha.
    ///
    /// Todas as malhas aqui são GERADAS com geometria conhecida (caixa 20×30×40, cilindro Ø20×30,
    /// esfera R25), então cada afirmação é contra a medida certa — não contra o que o próprio
    /// reconhecedor devolveu da última vez. É a única forma de um teste dizer algo sobre CAD sem
    /// a Solid Edge aberta.
    /// </summary>
    public class SurfaceRecognizerTests
    {
        // ------------------------------------------------------------------ caixa

        [Fact]
        public void Caixa_sai_com_seis_planos_e_nenhuma_sobra()
        {
            double[] mesh = Box(20, 30, 40);

            RecognitionResult r = SurfaceRecognizer.Recognize(mesh, null);

            Assert.Equal(12, r.TriangleCount);
            Assert.Equal(6, r.Surfaces.Count);
            Assert.All(r.Surfaces, s => Assert.Equal("plano", s.Kind));
            Assert.Equal(0, r.FreeFormAreaMm2);

            // Área da caixa: 2(ab + bc + ca) = 2(600 + 1200 + 800) = 5200 mm².
            Assert.Equal(5200, r.TotalAreaMm2, 6);
        }

        [Fact]
        public void Caixa_da_as_seis_normais_dos_eixos_e_as_areas_certas()
        {
            RecognitionResult r = SurfaceRecognizer.Recognize(Box(20, 30, 40), null);

            // Cada par de faces opostas tem a sua área, e cada face é plana até o bit.
            Assert.All(r.Surfaces, s => Assert.True(s.FitRmsMm < 1e-9,
                $"face com RMS {s.FitRmsMm} mm não é plana"));

            var areas = r.Surfaces.Select(s => Math.Round(s.AreaMm2, 6)).OrderBy(a => a).ToList();
            Assert.Equal(new[] { 600d, 600d, 800d, 800d, 1200d, 1200d }, areas);

            // As seis normais são os seis sentidos dos eixos, uma vez cada.
            var normals = r.Surfaces.Select(s => Round(s.Normal)).OrderBy(t => t.Item1)
                .ThenBy(t => t.Item2).ThenBy(t => t.Item3).ToList();
            Assert.Equal(6, normals.Distinct().Count());
            Assert.All(normals, n => Assert.Equal(1.0, Math.Abs(n.Item1) + Math.Abs(n.Item2) + Math.Abs(n.Item3), 6));
        }

        [Fact]
        public void Caixa_e_lida_como_prismatica_pelo_relatorio()
        {
            RecognitionResult r = SurfaceRecognizer.Recognize(Box(20, 30, 40), null);

            string texto = SurfaceReport.Format(r, "teste", "Caixa");

            Assert.Contains("6 plano(s)", texto);
            Assert.Contains("PRISMÁTICA", texto);
            Assert.DoesNotContain("MOSAICO", texto);
        }

        // ------------------------------------------------------------------ cilindro

        [Fact]
        public void Cilindro_sai_com_o_diametro_o_eixo_e_a_altura_certos()
        {
            // Ø20 × 30 de altura, eixo Z, base em z = 0, com as duas tampas.
            double[] mesh = Cylinder(radius: 10, height: 30, segments: 64, withCaps: true);

            RecognitionResult r = SurfaceRecognizer.Recognize(mesh, null);

            RecognizedSurface cyl = r.Surfaces.SingleOrDefault(s => s.Kind == "cilindro");
            Assert.NotNull(cyl);

            Assert.Equal(10.0, cyl.RadiusMm, 3);          // os vértices ficam SOBRE o raio real
            Assert.Equal(30.0, cyl.LengthMm, 6);
            Assert.Equal(1.0, Math.Abs(cyl.AxisMm[2]), 6); // eixo em Z
            Assert.True(cyl.SweepDeg > 350, $"a volta deu {cyl.SweepDeg}°, devia fechar 360°");
            Assert.True(cyl.FitRmsMm < 1e-9, $"RMS do ajuste: {cyl.FitRmsMm} mm");
        }

        [Fact]
        public void Cilindro_com_tampa_da_dois_planos_perpendiculares_ao_eixo()
        {
            RecognitionResult r = SurfaceRecognizer.Recognize(
                Cylinder(radius: 10, height: 30, segments: 64, withCaps: true), null);

            var caps = r.Surfaces.Where(s => s.Kind == "plano").ToList();
            Assert.Equal(2, caps.Count);
            Assert.All(caps, c => Assert.Equal(1.0, Math.Abs(c.Normal[2]), 6));

            // Uma tampa em z = 0 e a outra em z = 30.
            var alturas = caps.Select(c => Math.Round(c.PointMm[2], 6)).OrderBy(z => z).ToList();
            Assert.Equal(new[] { 0d, 30d }, alturas);
        }

        [Fact]
        public void Meio_cilindro_reporta_meia_volta_e_nao_uma_volta_inteira()
        {
            // O que distingue um furo passante de um raio de canto é justamente isto.
            double[] mesh = Cylinder(radius: 6, height: 10, segments: 48, withCaps: false, sweepDeg: 180);

            RecognitionResult r = SurfaceRecognizer.Recognize(mesh, null);
            RecognizedSurface cyl = r.Surfaces.Single(s => s.Kind == "cilindro");

            Assert.Equal(6.0, cyl.RadiusMm, 3);
            Assert.InRange(cyl.SweepDeg, 170, 190);
        }

        // ------------------------------------------------------------------ forma livre

        [Fact]
        public void Esfera_nao_e_vendida_como_peca_prismatica()
        {
            // Uma esfera CABE dentro da tolerância de planaridade em pedacinhos. O reconhecedor
            // pode até fatiá-la assim, mas o relatório tem de avisar que aquilo é mosaico —
            // senão o veredito chamaria de prismática uma peça sem um plano sequer.
            double[] mesh = Sphere(radius: 25, rings: 40, segments: 40);

            RecognitionResult r = SurfaceRecognizer.Recognize(mesh, null);
            string texto = SurfaceReport.Format(r, "teste", "Esfera");

            bool mosaico = texto.Contains("MOSAICO");
            bool livre = texto.Contains("FORMA LIVRE");
            Assert.True(mosaico || livre,
                "a esfera não foi marcada nem como mosaico nem como forma livre:\n" + texto);
            Assert.DoesNotContain("é PRISMÁTICA", texto);
        }

        // ------------------------------------------------------------------ casos de borda

        [Fact]
        public void Triangulo_degenerado_e_descartado_sem_derrubar_a_leitura()
        {
            var pts = new List<double>();
            pts.AddRange(Box(10, 10, 10));
            pts.AddRange(new double[] { 0, 0, 0, 1, 1, 1, 2, 2, 2 }); // três pontos colineares

            RecognitionResult r = SurfaceRecognizer.Recognize(pts.ToArray(), null);

            Assert.Equal(13, r.TriangleCount);
            Assert.Equal(1, r.DegenerateTriangles);
            Assert.Equal(6, r.Surfaces.Count);
            Assert.Equal(600, r.TotalAreaMm2, 6);
        }

        [Fact]
        public void Malha_vazia_devolve_resultado_vazio_em_vez_de_estourar()
        {
            RecognitionResult r = SurfaceRecognizer.Recognize(new double[0], null);

            Assert.Equal(0, r.TriangleCount);
            Assert.Empty(r.Surfaces);
            Assert.Contains("vazia", SurfaceReport.Format(r, null, null));
        }

        [Fact]
        public void Sopa_de_triangulos_incompleta_e_recusada()
        {
            Assert.Throws<ArgumentException>(() => SurfaceRecognizer.Recognize(new double[] { 1, 2, 3, 4 }, null));
        }

        [Fact]
        public void Eixo_do_cilindro_nao_depende_do_sinal_de_quem_gerou_a_malha()
        {
            // Dois runs têm de dar o MESMO eixo: um relatório que alternasse entre Z+ e Z− faria
            // qualquer comparação entre rodadas virar ruído.
            double[] mesh = Cylinder(radius: 8, height: 12, segments: 32, withCaps: false);

            double[] a = SurfaceRecognizer.Recognize(mesh, null).Surfaces.Single(s => s.Kind == "cilindro").AxisMm;
            double[] b = SurfaceRecognizer.Recognize(mesh, null).Surfaces.Single(s => s.Kind == "cilindro").AxisMm;

            Assert.Equal(a[0], b[0], 12);
            Assert.Equal(a[1], b[1], 12);
            Assert.Equal(a[2], b[2], 12);
        }

        [Fact]
        public void Autovetor_menor_de_uma_diagonal_e_a_direcao_de_menor_autovalor()
        {
            var m = new double[3, 3] { { 9, 0, 0 }, { 0, 4, 0 }, { 0, 0, 0.25 } };

            double[] e = SurfaceRecognizer.SmallestEigenvector(m);

            Assert.Equal(1.0, Math.Abs(e[2]), 9);
            Assert.Equal(0.0, Math.Abs(e[0]), 9);
            Assert.Equal(0.0, Math.Abs(e[1]), 9);
        }

        [Fact]
        public void Volta_medida_pelo_maior_vao_e_nao_pelo_intervalo_dos_angulos()
        {
            // Ângulos em torno de ±180°: um cálculo ingênuo (max − min) diria 340°, quando o arco
            // real é de 20° atravessando a descontinuidade do atan2.
            var angles = new List<double>();
            for (int i = 0; i <= 10; i++) angles.Add(Math.PI - 0.1745 + i * 0.0349); // ~20° em volta de 180°

            double sweep = SurfaceRecognizer.SweepDegrees(angles);

            Assert.InRange(sweep, 15, 25);
        }

        // ------------------------------------------------------------------ malhas de teste

        /// <summary>Caixa sólida com o canto em (0,0,0), duas faces triangulares por lado.</summary>
        private static double[] Box(double sx, double sy, double sz)
        {
            var v = new[]
            {
                new[] { 0.0, 0.0, 0.0 }, new[] { sx, 0.0, 0.0 }, new[] { sx, sy, 0.0 }, new[] { 0.0, sy, 0.0 },
                new[] { 0.0, 0.0, sz  }, new[] { sx, 0.0, sz  }, new[] { sx, sy, sz  }, new[] { 0.0, sy, sz  }
            };

            int[][] faces =
            {
                new[] { 0, 3, 2, 1 }, // base   (Z−)
                new[] { 4, 5, 6, 7 }, // topo   (Z+)
                new[] { 0, 1, 5, 4 }, // frente (Y−)
                new[] { 2, 3, 7, 6 }, // fundo  (Y+)
                new[] { 1, 2, 6, 5 }, // direita(X+)
                new[] { 3, 0, 4, 7 }  // esquerda(X−)
            };

            var pts = new List<double>();
            foreach (int[] f in faces)
            {
                Push(pts, v[f[0]], v[f[1]], v[f[2]]);
                Push(pts, v[f[0]], v[f[2]], v[f[3]]);
            }
            return pts.ToArray();
        }

        /// <summary>Casca cilíndrica de eixo Z, base em z = 0, opcionalmente com as tampas.</summary>
        private static double[] Cylinder(double radius, double height, int segments, bool withCaps,
            double sweepDeg = 360)
        {
            var pts = new List<double>();
            double total = sweepDeg * Math.PI / 180.0;
            double step = total / segments;
            bool closed = Math.Abs(sweepDeg - 360) < 1e-9;

            for (int i = 0; i < segments; i++)
            {
                double a0 = i * step, a1 = (i + 1) * step;
                double[] p00 = { radius * Math.Cos(a0), radius * Math.Sin(a0), 0 };
                double[] p10 = { radius * Math.Cos(a1), radius * Math.Sin(a1), 0 };
                double[] p01 = { radius * Math.Cos(a0), radius * Math.Sin(a0), height };
                double[] p11 = { radius * Math.Cos(a1), radius * Math.Sin(a1), height };

                Push(pts, p00, p10, p11);
                Push(pts, p00, p11, p01);
            }

            if (withCaps && closed)
            {
                double[] cBottom = { 0, 0, 0 };
                double[] cTop = { 0, 0, height };
                for (int i = 0; i < segments; i++)
                {
                    double a0 = i * step, a1 = (i + 1) * step;
                    double[] b0 = { radius * Math.Cos(a0), radius * Math.Sin(a0), 0 };
                    double[] b1 = { radius * Math.Cos(a1), radius * Math.Sin(a1), 0 };
                    double[] t0 = { radius * Math.Cos(a0), radius * Math.Sin(a0), height };
                    double[] t1 = { radius * Math.Cos(a1), radius * Math.Sin(a1), height };

                    Push(pts, cBottom, b1, b0);
                    Push(pts, cTop, t0, t1);
                }
            }
            return pts.ToArray();
        }

        /// <summary>Esfera centrada na origem, em anéis de latitude.</summary>
        private static double[] Sphere(double radius, int rings, int segments)
        {
            var pts = new List<double>();
            for (int i = 0; i < rings; i++)
            {
                double phi0 = Math.PI * i / rings;
                double phi1 = Math.PI * (i + 1) / rings;
                for (int j = 0; j < segments; j++)
                {
                    double th0 = 2 * Math.PI * j / segments;
                    double th1 = 2 * Math.PI * (j + 1) / segments;

                    double[] a = OnSphere(radius, phi0, th0);
                    double[] b = OnSphere(radius, phi1, th0);
                    double[] c = OnSphere(radius, phi1, th1);
                    double[] d = OnSphere(radius, phi0, th1);

                    Push(pts, a, b, c);
                    Push(pts, a, c, d);
                }
            }
            return pts.ToArray();
        }

        private static double[] OnSphere(double r, double phi, double theta) => new[]
        {
            r * Math.Sin(phi) * Math.Cos(theta),
            r * Math.Sin(phi) * Math.Sin(theta),
            r * Math.Cos(phi)
        };

        private static void Push(List<double> pts, double[] a, double[] b, double[] c)
        {
            pts.AddRange(a);
            pts.AddRange(b);
            pts.AddRange(c);
        }

        private static Tuple<double, double, double> Round(double[] v) =>
            Tuple.Create(Math.Round(v[0], 6), Math.Round(v[1], 6), Math.Round(v[2], 6));
    }
}
