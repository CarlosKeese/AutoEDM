using System;
using System.Collections.Generic;
using System.Linq;
using AutoEDM.Reverse;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// O que a perna do corpo 3 do Rack_Anghinoni ensinou (2026-10-10): corte de secção passando
    /// exatamente por vértice, e raio de concordância com tesselação NÃO regrada.
    /// </summary>
    public class FilletAndSectionTests
    {
        [Fact]
        public void Corte_na_altura_de_um_vertice_nao_perde_o_trecho()
        {
            // Bloco 10×10, Z 0–20, com a parede X = 10 partida numa fileira de vértices em Z 10
            // — exatamente a meia altura da faixa, onde a secção é feita.
            var pts = new List<double>();
            double[] a0 = { 0, 0, 0 }, b0 = { 10, 0, 0 }, c0 = { 10, 10, 0 }, d0 = { 0, 10, 0 };
            double[] a1 = { 0, 0, 20 }, b1 = { 10, 0, 20 }, c1 = { 10, 10, 20 }, d1 = { 0, 10, 20 };
            double[] bm = { 10, 0, 10 }, cm = { 10, 10, 10 };
            Quad(pts, a0, d0, c0, b0);          // fundo
            Quad(pts, a1, b1, c1, d1);          // topo
            Quad(pts, a0, b0, b1, a1);          // Y−
            Quad(pts, c0, d0, d1, c1);          // Y+
            Quad(pts, d0, a0, a1, d1);          // X−
            Quad(pts, b0, c0, cm, bm);          // X+ embaixo
            Quad(pts, bm, cm, c1, b1);          // X+ em cima

            double[] mesh = pts.ToArray();
            RecognitionResult r = SurfaceRecognizer.Recognize(mesh, null);
            PrismaticPlan plan = PrismaticSection.Plan(mesh, r, null);

            SectionBand band = Assert.Single(plan.Bands);
            SectionLoop loop = Assert.Single(band.Loops);
            Assert.Equal(4, loop.Elements.Count);
            Assert.All(loop.Elements, e => Assert.True(e.Exact));
            Assert.Equal(100, Math.Abs(PrismaticSection.ExactArea(loop)), 6);
        }

        [Fact]
        public void Raio_de_concordancia_com_tesselacao_torcida_sai_como_cilindro_R5()
        {
            // Parede Y = 0 (Z 0–35, material em Y < 0), fundo do tampo Z = 40 (Y 5–20) e o raio
            // R5 entre eles, centro (Y 5, Z 35), ao longo de X 0–10. A tesselação do raio é
            // TORCIDA como no STL real: os vértices em X = 0 e em X = 10 ficam em ângulos
            // diferentes, então não há geratriz e o crescimento de cilindro não o pega.
            var pts = new List<double>();
            Func<double, double, double[]> arc = (x, th) => new[] { x, 5 - 5 * Math.Cos(th), 35 + 5 * Math.Sin(th) };
            int n = 12;
            double step = Math.PI / 2 / n;
            var left = new List<double>();
            var right = new List<double> { 0 };
            for (int k = 0; k <= n; k++) left.Add(k * step);
            for (int k = 0; k < n; k++) right.Add((k + 0.5) * step);
            right.Add(Math.PI / 2);

            // Zíper entre as duas fileiras, normal apontando para o centro do raio (o ar).
            int i = 0, j = 0;
            while (i < left.Count - 1 || j < right.Count - 1)
            {
                bool advanceLeft = j == right.Count - 1 || (i < left.Count - 1 && left[i + 1] <= right[j + 1]);
                double[] p0 = arc(0, left[i]), p1 = arc(10, right[j]);
                double[] p2 = advanceLeft ? arc(0, left[i + 1]) : arc(10, right[j + 1]);
                Tri(pts, p0, p1, p2, new[] { 5.0, 5, 35 }, true);
                if (advanceLeft) i++; else j++;
            }
            // Parede e fundo, com as normais para o ar (+Y e −Z).
            Quad(pts, new[] { 0.0, 0, 0 }, new[] { 10.0, 0, 0 }, new[] { 10.0, 0, 35 }, new[] { 0.0, 0, 35 }, new[] { 0.0, 1, 0 });
            Quad(pts, new[] { 0.0, 5, 40 }, new[] { 10.0, 5, 40 }, new[] { 10.0, 20, 40 }, new[] { 0.0, 20, 40 }, new[] { 0.0, 0, -1 });

            RecognitionResult r = SurfaceRecognizer.Recognize(pts.ToArray(), null);

            RecognizedSurface fillet = Assert.Single(r.Surfaces, s => s.Kind == "cilindro");
            Assert.Equal(5.0, fillet.RadiusMm, 6);
            Assert.Equal(1.0, Math.Abs(fillet.AxisMm[0]), 9);
            Assert.Equal(90, fillet.SweepDeg, 1);
            Assert.DoesNotContain(r.Surfaces, s => s.Kind == "livre");
        }

        // ------------------------------------------------------------------ malha

        private static void Quad(List<double> pts, double[] a, double[] b, double[] c, double[] d, double[] outward = null)
        {
            Tri(pts, a, b, c, outward, false);
            Tri(pts, a, c, d, outward, false);
        }

        /// <summary>Triângulo com a normal para o lado pedido: na direção 'toward' (vetor) ou,
        /// com <paramref name="towardPoint"/>, para o ponto dado.</summary>
        private static void Tri(List<double> pts, double[] a, double[] b, double[] c, double[] toward, bool towardPoint)
        {
            if (toward != null)
            {
                double[] u = { b[0] - a[0], b[1] - a[1], b[2] - a[2] }, v = { c[0] - a[0], c[1] - a[1], c[2] - a[2] };
                double[] n = { u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0] };
                double[] dir = towardPoint
                    ? new[] { toward[0] - (a[0] + b[0] + c[0]) / 3, toward[1] - (a[1] + b[1] + c[1]) / 3, toward[2] - (a[2] + b[2] + c[2]) / 3 }
                    : toward;
                if (n[0] * dir[0] + n[1] * dir[1] + n[2] * dir[2] < 0) { double[] t = b; b = c; c = t; }
            }
            pts.AddRange(a);
            pts.AddRange(b);
            pts.AddRange(c);
        }
    }
}
