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

            // Desde o ajuste de superfície de revolução (2026-10-10) a esfera é RECONHECIDA como
            // esfera — o que importa continua: ninguém a chama de prismática.
            bool mosaico = texto.Contains("MOSAICO");
            bool livre = texto.Contains("FORMA LIVRE");
            bool esfera = r.Surfaces.Any(q => q.Kind == "esfera" && Math.Abs(q.RadiusMm - 25) < 0.05);
            Assert.True(mosaico || livre || esfera,
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

        // ------------------------------------- superfície curva TANGENTE (malha real, 2026-10-09)

        [Fact]
        public void Rasgo_arredondado_sai_com_duas_pontas_cilindricas_e_nao_como_mosaico()
        {
            // Oblongo: centros a 30 mm, R7 (largura 14) — a ponta do rasgo do Rack_Anghinoni.
            // A ponta é TANGENTE às paredes retas: sem quina entre elas, tudo cai num componente
            // só. A versão antiga descascava planos primeiro e cada faixa da ponta virava um
            // "plano" de dois triângulos (276 regiões falsas na malha real).
            double[] mesh = Prism(Stadium(centres: 30, radius: 7, segmentsPerEnd: 18), height: 6);

            RecognitionResult r = SurfaceRecognizer.Recognize(mesh, null);

            var cyl = r.Surfaces.Where(s => s.Kind == "cilindro").ToList();
            Assert.Equal(2, cyl.Count);
            Assert.All(cyl, c =>
            {
                Assert.Equal(14.0, 2 * c.RadiusMm, 6);
                Assert.InRange(c.SweepDeg, 175, 185);
                Assert.Equal(1.0, Math.Abs(c.AxisMm[2]), 9);
            });

            // Duas paredes retas + duas tampas, e nenhuma faixa solta.
            var planes = r.Surfaces.Where(s => s.Kind == "plano").ToList();
            Assert.Equal(4, planes.Count);
            Assert.Equal(0, r.FreeFormAreaMm2);

            // Os centros das pontas, nos seus lugares (±15 em X).
            var xs = cyl.Select(c => Math.Round(c.PointMm[0], 6)).OrderBy(x => x).ToList();
            Assert.Equal(new[] { -15d, 15d }, xs);
        }

        [Fact]
        public void Canto_com_raio_tangente_da_um_quarto_de_cilindro_entre_dois_planos()
        {
            // Retângulo 40×20 com UM canto arredondado R5, extrudado 10. O raio é tangente às
            // duas paredes vizinhas: é o "raio de concordância" de toda peça de molde.
            double[] mesh = Prism(RoundedCornerRect(40, 20, radius: 5, segments: 12), height: 10);

            RecognitionResult r = SurfaceRecognizer.Recognize(mesh, null);

            RecognizedSurface raio = r.Surfaces.Single(s => s.Kind == "cilindro");
            Assert.Equal(5.0, raio.RadiusMm, 6);
            Assert.InRange(raio.SweepDeg, 85, 95);
            Assert.Equal(6, r.Surfaces.Count(s => s.Kind == "plano")); // 4 paredes + 2 tampas
        }

        [Fact]
        public void Furo_inteiro_tesselado_da_volta_de_360_e_nao_360_menos_um_passo()
        {
            // 24 segmentos de 15°: todos os vãos iguais. A versão antiga dizia "345°" e o furo
            // parecia aberto.
            double[] mesh = Cylinder(radius: 1.75, height: 2, segments: 24, withCaps: false);

            RecognizedSurface cyl = SurfaceRecognizer.Recognize(mesh, null).Surfaces.Single(s => s.Kind == "cilindro");

            Assert.Equal(360.0, cyl.SweepDeg);
        }

        [Fact]
        public void Quase_plano_nao_vira_cilindro_de_raio_gigante()
        {
            // Casca com R20.000 e 0,3° de arco: a tolerância relativa antiga (2 % do raio)
            // compraria 400 mm de folga e chamaria isto de "cilindro Ø40000" — a malha real deu
            // exatamente esse defeito no fundo de uma peça.
            double[] mesh = Cylinder(radius: 20000, height: 30, segments: 10, withCaps: false, sweepDeg: 0.3);

            RecognitionResult r = SurfaceRecognizer.Recognize(mesh, null);

            Assert.DoesNotContain(r.Surfaces, s => s.Kind == "cilindro");
        }

        // ------------------------------------------------------------------ arredondamento

        [Fact]
        public void Medida_reconhecida_volta_ao_centesimo_e_ao_eixo_exato()
        {
            // Caixa 20×30×40 girada 0,01° em X e deslocada 0,002 mm em Z: o que um STL exportado
            // com float faz com uma peça cotada em centésimos.
            double[] mesh = Transform(Box(20, 30, 40), rotXDeg: 0.01, dz: 0.002);

            RecognitionResult r = SurfaceRecognizer.Recognize(mesh, null);

            // Toda normal é EXATAMENTE um eixo — nada de (0; 0,00017; 1).
            Assert.All(r.Surfaces, s =>
                Assert.Equal(1.0, Math.Abs(s.Normal[0]) + Math.Abs(s.Normal[1]) + Math.Abs(s.Normal[2])));

            var zs = r.Surfaces.Where(s => Math.Abs(s.Normal[2]) == 1)
                               .Select(s => s.PointMm[2]).OrderBy(z => z).ToList();
            Assert.Equal(0.00, zs[0], 9);
            Assert.Equal(40.00, zs[1], 9);
        }

        [Fact]
        public void Diametro_do_cilindro_sai_no_centesimo()
        {
            var s = new RecognizedSurface
            {
                Kind = "cilindro", AxisMm = new[] { 0.0, 0.00001, 1.0 },
                PointMm = new[] { 70.0042, 115.9961, 3.5 }, RadiusMm = 3.50021
            };

            SurfaceRecognizer.Snap(s, new RecognizerOptions());

            Assert.Equal(7.00, 2 * s.RadiusMm, 9);
            Assert.Equal(new[] { 0.0, 0.0, 1.0 }, s.AxisMm);
            Assert.Equal(70.00, s.PointMm[0], 9);
            Assert.Equal(116.00, s.PointMm[1], 9);
            Assert.Equal(3.5, s.PointMm[2], 9); // ao longo do eixo não se mexe
        }

        [Fact]
        public void Arredondamento_desligado_preserva_a_medida_crua()
        {
            var o = new RecognizerOptions { RoundToMm = 0, SnapAngleDeg = 0 };
            var s = new RecognizedSurface
            {
                Kind = "cilindro", AxisMm = new[] { 0.0, 0.00001, 1.0 },
                PointMm = new[] { 70.0042, 115.9961, 3.5 }, RadiusMm = 3.50021
            };

            SurfaceRecognizer.Snap(s, o);

            Assert.Equal(3.50021, s.RadiusMm);
            Assert.Equal(0.00001, s.AxisMm[1]);
        }

        // ------------------------------------------------------------------ cone

        [Fact]
        public void Chanfro_de_furo_sai_como_cone_de_45_graus_com_os_dois_diametros()
        {
            // Chanfro 1×45° na boca de um furo Ø4: tronco de cone de Ø4 (z = 0) a Ø6 (z = 1).
            double[] mesh = Frustum(2, 3, 1, 36);

            RecognitionResult r = SurfaceRecognizer.Recognize(mesh, null);

            RecognizedSurface cone = Assert.Single(r.Surfaces);
            Assert.Equal("cone", cone.Kind);
            Assert.Equal(45, cone.HalfAngleDeg);
            Assert.Equal(2.0, cone.RadiusMinMm, 6);
            Assert.Equal(3.0, cone.RadiusMaxMm, 6);
            Assert.Equal(360, cone.SweepDeg);
            Assert.Equal(1.0, Math.Abs(cone.AxisMm[2]), 9);
            // Ápice na extensão do cone: raio zero dois milímetros abaixo da boca do Ø4.
            Assert.Equal(-2.0, cone.ApexMm[2], 3);
        }

        [Fact]
        public void Ressalto_com_saida_de_um_grau_da_cone_de_um_grau()
        {
            double h = 30, rBase = 10, rTop = rBase - h * Math.Tan(Math.PI / 180);
            double[] mesh = Frustum(rBase, rTop, h, 72);

            RecognitionResult r = SurfaceRecognizer.Recognize(mesh, null);

            RecognizedSurface cone = Assert.Single(r.Surfaces);
            Assert.Equal("cone", cone.Kind);
            Assert.Equal(1, cone.HalfAngleDeg);
            Assert.Equal(rTop, cone.RadiusMinMm, 2);
            Assert.Equal(rBase, cone.RadiusMaxMm, 2);
            // O ápice fica acima do topo (o cone se fecha para cima), e o eixo aponta para baixo.
            Assert.True(cone.ApexMm[2] > h);
            Assert.Equal(-1.0, cone.AxisMm[2], 9);
        }

        [Fact]
        public void Parede_e_rampa_em_angulo_nao_viram_cilindro_de_tres_pontos()
        {
            // Malha real (corpo 1 do Rack_Anghinoni): parede Y = 60 de z −2 a 1 e rampa até
            // (Y 65, z 15,142). Três pontos de perfil cabem num círculo de RMS zero — era
            // reconhecido como "cilindro Ø53,57 de 39°".
            var pts = new List<double>();
            double[] a0 = { 0, 60, -2 }, a1 = { 30, 60, -2 };
            double[] b0 = { 0, 60, 1 }, b1 = { 30, 60, 1 };
            double[] c0 = { 0, 65, 15.142 }, c1 = { 30, 65, 15.142 };
            Push(pts, a0, b1, a1); Push(pts, a0, b0, b1);
            Push(pts, b0, c1, b1); Push(pts, b0, c0, c1);

            RecognitionResult r = SurfaceRecognizer.Recognize(pts.ToArray(), null);

            Assert.Equal(2, r.Surfaces.Count);
            Assert.All(r.Surfaces, s => Assert.Equal("plano", s.Kind));
            RecognizedSurface ramp = r.Surfaces.Single(s => Math.Abs(s.Normal[2]) > 0.1);
            Assert.Equal(Math.Atan2(5, 14.142) * 180 / Math.PI, Math.Acos(Math.Abs(ramp.Normal[1])) * 180 / Math.PI, 1);
        }

        // ------------------------------------------------------------------ toro

        [Fact]
        public void Raio_contornando_ponta_arredondada_sai_como_um_toro_e_nao_como_cilindros()
        {
            // A ponta da alça da malha 2: R2,5 de perfil (quarto de volta), a R0,5 do eixo Z,
            // meia volta em torno dele, tesselado a 15° nas duas direções.
            double[] mesh = TorusPatch(0.5, 2.5, -Math.PI / 2, Math.PI / 2, 12, 0, Math.PI / 2, 6);

            RecognitionResult r = SurfaceRecognizer.Recognize(mesh, null);

            RecognizedSurface t = Assert.Single(r.Surfaces);
            Assert.Equal("toro", t.Kind);
            Assert.Equal(2.5, t.RadiusMm, 6);
            Assert.Equal(0.5, t.MajorRadiusMm, 6);
            Assert.Equal(180, t.SweepDeg, 1);
            Assert.Equal(1.0, Math.Abs(t.AxisMm[2]), 9);
        }

        [Fact]
        public void Raio_de_noventa_graus_tesselado_sai_inteiro_e_com_as_pontas_nos_eixos()
        {
            // Quarto de cilindro R2,5 de eixo Y em 6 facetas: as duas de baixo saíam como "faces
            // inclinadas" de 7,5° e 22,5° (alça da malha 2).
            var pts = new List<double>();
            for (int i = 0; i < 6; i++)
            {
                double a0 = Math.PI / 2 * i / 6, a1 = Math.PI / 2 * (i + 1) / 6;
                double[] p00 = { 2.5 * Math.Cos(a0), 0, 2.5 * Math.Sin(a0) }, p10 = { 2.5 * Math.Cos(a1), 0, 2.5 * Math.Sin(a1) };
                double[] p01 = { p00[0], 40, p00[2] }, p11 = { p10[0], 40, p10[2] };
                Push(pts, p00, p11, p10);
                Push(pts, p00, p01, p11);
            }

            RecognitionResult r = SurfaceRecognizer.Recognize(pts.ToArray(), null);

            RecognizedSurface c = Assert.Single(r.Surfaces);
            Assert.Equal("cilindro", c.Kind);
            Assert.Equal(2.5, c.RadiusMm, 6);
            Assert.Equal(90, c.SweepDeg, 6);
        }

        // ------------------------------------------------------------------ malhas de teste

        /// <summary>Retalho de toro de eixo Z: raio maior R, do perfil r; φ em volta do eixo,
        /// θ no perfil (0 = para fora, π/2 = para cima).</summary>
        private static double[] TorusPatch(double R, double r, double phi0, double phi1, int nPhi,
            double th0, double th1, int nTh)
        {
            var pts = new List<double>();
            Func<double, double, double[]> at = (ph, th) => new[]
            {
                (R + r * Math.Cos(th)) * Math.Cos(ph), (R + r * Math.Cos(th)) * Math.Sin(ph), r * Math.Sin(th)
            };
            for (int i = 0; i < nPhi; i++)
                for (int j = 0; j < nTh; j++)
                {
                    double pa = phi0 + (phi1 - phi0) * i / nPhi, pb = phi0 + (phi1 - phi0) * (i + 1) / nPhi;
                    double ta = th0 + (th1 - th0) * j / nTh, tb = th0 + (th1 - th0) * (j + 1) / nTh;
                    Push(pts, at(pa, ta), at(pb, ta), at(pb, tb));
                    Push(pts, at(pa, ta), at(pb, tb), at(pa, tb));
                }
            return pts.ToArray();
        }

        /// <summary>Tronco de cone de eixo Z: raio r0 em z = 0 e r1 em z = h, sem tampas.</summary>
        private static double[] Frustum(double r0, double r1, double h, int segments)
        {
            var pts = new List<double>();
            for (int i = 0; i < segments; i++)
            {
                double a0 = 2 * Math.PI * i / segments, a1 = 2 * Math.PI * (i + 1) / segments;
                double[] p00 = { r0 * Math.Cos(a0), r0 * Math.Sin(a0), 0 };
                double[] p10 = { r0 * Math.Cos(a1), r0 * Math.Sin(a1), 0 };
                double[] p01 = { r1 * Math.Cos(a0), r1 * Math.Sin(a0), h };
                double[] p11 = { r1 * Math.Cos(a1), r1 * Math.Sin(a1), h };
                Push(pts, p00, p10, p11);
                Push(pts, p00, p11, p01);
            }
            return pts.ToArray();
        }

        /// <summary>Prisma de eixo Z sobre um polígono CONVEXO (anti-horário), base em z = 0:
        /// paredes de dois triângulos por aresta e tampas em leque a partir do centroide.</summary>
        private static double[] Prism(List<double[]> poly, double height)
        {
            var pts = new List<double>();
            int n = poly.Count;
            double cxp = poly.Average(q => q[0]), cyp = poly.Average(q => q[1]);
            for (int i = 0; i < n; i++)
            {
                double[] a = poly[i], b = poly[(i + 1) % n];
                double[] a0 = { a[0], a[1], 0 }, b0 = { b[0], b[1], 0 };
                double[] a1 = { a[0], a[1], height }, b1 = { b[0], b[1], height };
                Push(pts, a0, b0, b1);
                Push(pts, a0, b1, a1);
                Push(pts, new[] { cxp, cyp, 0 }, b0, a0);
                Push(pts, new[] { cxp, cyp, height }, a1, b1);
            }
            return pts.ToArray();
        }

        /// <summary>Oblongo (rasgo) centrado na origem, ao longo de X.</summary>
        private static List<double[]> Stadium(double centres, double radius, int segmentsPerEnd)
        {
            var poly = new List<double[]>();
            double h = centres / 2;
            for (int i = 0; i <= segmentsPerEnd; i++) // ponta direita: −90° → +90°
            {
                double a = -Math.PI / 2 + Math.PI * i / segmentsPerEnd;
                poly.Add(new[] { h + radius * Math.Cos(a), radius * Math.Sin(a) });
            }
            for (int i = 0; i <= segmentsPerEnd; i++) // ponta esquerda: +90° → +270°
            {
                double a = Math.PI / 2 + Math.PI * i / segmentsPerEnd;
                poly.Add(new[] { -h + radius * Math.Cos(a), radius * Math.Sin(a) });
            }
            return poly;
        }

        /// <summary>Retângulo [0,sx]×[0,sy] com o canto (sx,sy) arredondado.</summary>
        private static List<double[]> RoundedCornerRect(double sx, double sy, double radius, int segments)
        {
            var poly = new List<double[]> { new[] { 0.0, 0.0 }, new[] { sx, 0.0 } };
            double ccx = sx - radius, ccy = sy - radius;
            for (int i = 0; i <= segments; i++)
            {
                double a = Math.PI / 2 * i / segments;
                poly.Add(new[] { ccx + radius * Math.Cos(a), ccy + radius * Math.Sin(a) });
            }
            poly.Add(new[] { 0.0, sy });
            return poly;
        }

        private static double[] Transform(double[] mesh, double rotXDeg, double dz)
        {
            double a = rotXDeg * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
            var o = new double[mesh.Length];
            for (int i = 0; i < mesh.Length; i += 3)
            {
                double y = mesh[i + 1], z = mesh[i + 2];
                o[i] = mesh[i];
                o[i + 1] = c * y - s * z;
                o[i + 2] = s * y + c * z + dz;
            }
            return o;
        }

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
