using System;
using System.Collections.Generic;
using AutoEDM.Model;
using AutoEDM.Selection;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// Testes do núcleo geométrico da área da secção de queima (coluna nova do botão
    /// "Coordenadas"). Só o caminho SEM COM — as malhas são montadas à mão aqui, do
    /// mesmo jeito que <c>Face.GetFacetData</c> as devolve: 9 doubles por faceta
    /// (3 vértices × XYZ), em METROS.
    /// </summary>
    public class SectionAreaCalculatorTests
    {
        /// <summary>Parede vertical (quad = 2 triângulos) do ponto A ao B, de zBase a zTop (tudo mm).</summary>
        private static double[] VerticalWall(double x1, double y1, double x2, double y2, double zBase, double zTop)
        {
            double[] P(double x, double y, double z) => new[] { Units.MmToM(x), Units.MmToM(y), Units.MmToM(z) };
            var a = P(x1, y1, zBase); var b = P(x2, y2, zBase);
            var c = P(x2, y2, zTop); var d = P(x1, y1, zTop);
            var buf = new List<double>(18);
            buf.AddRange(a); buf.AddRange(b); buf.AddRange(c); // triângulo 1
            buf.AddRange(a); buf.AddRange(c); buf.AddRange(d); // triângulo 2
            return buf.ToArray();
        }

        /// <summary>Prisma reto de secção retangular w×h (mm), centrado na origem, de z=0 a z=alturaMm.</summary>
        private static List<double[]> RectPrism(double wMm, double hMm, double heightMm)
        {
            double x = wMm / 2, y = hMm / 2;
            return new List<double[]>
            {
                VerticalWall(-x, -y,  x, -y, 0, heightMm),
                VerticalWall( x, -y,  x,  y, 0, heightMm),
                VerticalWall( x,  y, -x,  y, 0, heightMm),
                VerticalWall(-x,  y, -x, -y, 0, heightMm),
            };
        }

        [Fact]
        public void RectangularSection_MatchesWidthTimesHeight()
        {
            // 40 × 25 mm = 1000 mm² = 10 cm²
            var mesh = RectPrism(40, 25, 30);

            Assert.True(SectionAreaCalculator.TryAreaFromMeshesCm2(
                mesh, zMidMm: 15, out double areaCm2, out int segs, out int bad, out string error), error);

            Assert.Equal(10.0, areaCm2, 2);
            Assert.Equal(0, bad);
            Assert.True(segs >= 4, $"esperava >= 4 segmentos, veio {segs}");
        }

        [Fact]
        public void SectionWithInnerHole_SubtractsTheHole()
        {
            // Quadro 40×40 (1600 mm²) com uma ilha 10×10 (100 mm²) dentro → 1500 mm² = 15 cm².
            // A regra par-ímpar da varredura tem de descontar o furo sozinha.
            var mesh = RectPrism(40, 40, 20);
            mesh.AddRange(RectPrism(10, 10, 20));

            Assert.True(SectionAreaCalculator.TryAreaFromMeshesCm2(
                mesh, zMidMm: 10, out double areaCm2, out _, out int bad, out string error), error);

            Assert.Equal(15.0, areaCm2, 2);
            Assert.Equal(0, bad);
        }

        [Fact]
        public void TaperedWalls_MeasuredAtMidHeight_NotAtTopOrBottom()
        {
            // Tronco de pirâmide: 40×40 na base (z=0), 20×20 no topo (z=20).
            // No meio (z=10) a secção é 30×30 = 900 mm² = 9 cm² — o valor que interessa,
            // e que difere tanto da base (16 cm²) quanto do topo (4 cm²).
            double[] Wall(double bx1, double by1, double bx2, double by2,
                          double tx1, double ty1, double tx2, double ty2)
            {
                double[] P(double x, double y, double z) => new[] { Units.MmToM(x), Units.MmToM(y), Units.MmToM(z) };
                var a = P(bx1, by1, 0); var b = P(bx2, by2, 0);
                var c = P(tx2, ty2, 20); var d = P(tx1, ty1, 20);
                var buf = new List<double>(18);
                buf.AddRange(a); buf.AddRange(b); buf.AddRange(c);
                buf.AddRange(a); buf.AddRange(c); buf.AddRange(d);
                return buf.ToArray();
            }

            var mesh = new List<double[]>
            {
                Wall(-20, -20,  20, -20, -10, -10,  10, -10),
                Wall( 20, -20,  20,  20,  10, -10,  10,  10),
                Wall( 20,  20, -20,  20,  10,  10, -10,  10),
                Wall(-20,  20, -20, -20, -10,  10, -10, -10),
            };

            Assert.True(SectionAreaCalculator.TryAreaFromMeshesCm2(
                mesh, zMidMm: 10, out double areaCm2, out _, out _, out string error), error);

            Assert.Equal(9.0, areaCm2, 2);
        }

        [Fact]
        public void CircularSection_ApproximatesPiRSquared()
        {
            // Cilindro Ø30 facetado em 180 lados: π·15² = 706,86 mm² ≈ 7,069 cm².
            const double r = 15.0;
            const int sides = 180;
            var mesh = new List<double[]>(sides);
            for (int i = 0; i < sides; i++)
            {
                double a0 = 2 * Math.PI * i / sides, a1 = 2 * Math.PI * (i + 1) / sides;
                mesh.Add(VerticalWall(r * Math.Cos(a0), r * Math.Sin(a0),
                                      r * Math.Cos(a1), r * Math.Sin(a1), 0, 10));
            }

            Assert.True(SectionAreaCalculator.TryAreaFromMeshesCm2(
                mesh, zMidMm: 5, out double areaCm2, out _, out _, out string error), error);

            // Tolerância de 1%: a malha é um polígono inscrito e a varredura discretiza em Y.
            Assert.InRange(areaCm2, 7.069 * 0.99, 7.069 * 1.01);
        }

        [Fact]
        public void ParedeFaltando_E_RECUSADA_emVezDeDevolverNumeroPlausivel()
        {
            // O caso real que sumia com a coluna (Carlos, 2026-09-02): as faces da feature de
            // GAP não davam a volta completa no eletrodo. Com 3 das 4 paredes, as linhas
            // horizontais ainda cruzam as duas laterais e "fecham" a conta sozinhas — daria
            // 10 cm² como se nada estivesse faltando. A varredura cruzada em X denuncia.
            var mesh = RectPrism(40, 25, 30);
            mesh.RemoveAt(2); // tira a parede de cima (y = +12,5)

            Assert.False(SectionAreaCalculator.TryAreaFromMeshesCm2(
                mesh, zMidMm: 15, out double areaCm2, out _, out _, out string error));

            Assert.Equal(0, areaCm2);
            Assert.Contains("ABERTO", error);
        }

        [Fact]
        public void ContornoFechado_TemAsDuasVarredurasIguais()
        {
            // Contra-prova do teste acima: num contorno fechado (e não quadrado, para as duas
            // direções não serem simétricas por acaso) a checagem cruzada não pode reprovar.
            var mesh = RectPrism(60, 12, 20);

            Assert.True(SectionAreaCalculator.TryAreaFromMeshesCm2(
                mesh, zMidMm: 10, out double areaCm2, out _, out int bad, out string error), error);

            Assert.Equal(7.2, areaCm2, 2); // 60 × 12 = 720 mm²
            Assert.Equal(0, bad);
        }

        [Fact]
        public void PlaneAboveTheGeometry_ReportsOpenContour_InsteadOfZeroArea()
        {
            // Corte fora da altura da malha: tem de FALHAR com motivo, nunca devolver 0 cm²
            // como se fosse uma medição válida.
            var mesh = RectPrism(40, 25, 30);

            Assert.False(SectionAreaCalculator.TryAreaFromMeshesCm2(
                mesh, zMidMm: 99, out _, out int segs, out _, out string error));

            Assert.Equal(0, segs);
            Assert.Contains("não fechou", error);
        }
    }
}
