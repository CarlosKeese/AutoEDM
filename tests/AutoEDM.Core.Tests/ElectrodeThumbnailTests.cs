using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AutoEDM.Reporting;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>Miniatura isométrica da Lista de corte: vista de baixo, +Z para cima, arestas vivas.</summary>
    public class ElectrodeThumbnailTests
    {
        /// <summary>Quadrado horizontal (2 triângulos) em Z = z, lado s.</summary>
        private static double[] Square(double z, double s = 10) => new double[]
        {
            0, 0, z,  s, 0, z,  s, s, z,
            0, 0, z,  s, s, z,  0, s, z,
        };

        /// <summary>Cubo de lado s como UMA malha (12 triângulos), vértices repetidos por coordenada.</summary>
        private static double[] Cube(double s = 10)
        {
            var v = new[]
            {
                new double[] { 0, 0, 0 }, new double[] { s, 0, 0 }, new double[] { s, s, 0 }, new double[] { 0, s, 0 },
                new double[] { 0, 0, s }, new double[] { s, 0, s }, new double[] { s, s, s }, new double[] { 0, s, s },
            };
            int[][] quads = { new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 } };
            var pts = new List<double>();
            foreach (int[] q in quads)
                foreach (int i in new[] { q[0], q[1], q[2], q[0], q[2], q[3] })
                    pts.AddRange(v[i]);
            return pts.ToArray();
        }

        private static float CentroidY(ElectrodeThumbnail.Facet f) => (f.A.Y + f.B.Y + f.C.Y) / 3f;

        [Fact]
        public void ViewFromBelow_BottomIsNearerAndDrawnLowerOnScreen()
        {
            var facets = ElectrodeThumbnail.Project(new[] { Square(10), Square(0) });
            // Ordem de desenho: do mais longe ao mais perto — a face de BAIXO (Z=0) é a mais perto.
            Assert.True(facets.Last().Depth > facets.First().Depth);
            // +Z para cima na tela (y do bitmap cresce para baixo): Z=0 fica abaixo de Z=10.
            Assert.True(CentroidY(facets.Last()) > CentroidY(facets.First()));
        }

        [Fact]
        public void ViewFromAbove_FlipsWhichFaceIsNearer_ButKeepsZUpOnScreen()
        {
            // Peça de molde na Lista de modificações é olhada de CIMA (Carlos, 2026-09-18);
            // o eletrodo continua de baixo por causa da figura de queima.
            var facets = ElectrodeThumbnail.Project(new[] { ElectrodeThumbnailTests.Square(10), ElectrodeThumbnailTests.Square(0) },
                view: ElectrodeThumbnail.View.FromAbove);

            // Agora a face de CIMA (Z=10) é a mais perto da câmera — o contrário da vista do eletrodo.
            var nearest = facets.Last();
            Assert.True(nearest.Depth > facets.First().Depth);
            // E +Z continua para cima na tela: a face mais alta fica com o y MENOR.
            Assert.True(CentroidY(nearest) < CentroidY(facets.First()));
        }

        [Fact]
        public void CalloutTip_LandsOnTheBiggestVisiblePieceOfTheFeature()
        {
            // Uma operação com dois pedaços em pontas OPOSTAS. A média dos pixels cai no meio da
            // peça, onde não há nada dela — era ali que a linha do balão terminava (Carlos, 2026-09-18).
            const int s = 60;
            var gbuf = new int[s * s];
            for (int i = 0; i < gbuf.Length; i++) gbuf[i] = -1;
            Fill(gbuf, s, 2, 2, 14, 14, 0);     // mancha grande, canto superior esquerdo
            Fill(gbuf, s, 52, 52, 6, 6, 0);     // mancha pequena, canto oposto

            PointF tip = ElectrodeThumbnail.LargestBlobCenters(gbuf, s, 1, minPixels: 8)[0];

            Assert.InRange(tip.X, 2, 15);   // dentro da mancha GRANDE
            Assert.InRange(tip.Y, 2, 15);
            Assert.Equal(0, gbuf[(int)tip.Y * s + (int)tip.X]);   // e num pixel REAL dela
        }

        [Fact]
        public void FeatureWithOnlySpecks_GetsNoCallout()
        {
            const int s = 40;
            var gbuf = new int[s * s];
            for (int i = 0; i < gbuf.Length; i++) gbuf[i] = -1;
            Fill(gbuf, s, 5, 5, 2, 2, 0);   // 4 pixels: respingo, feature escondida

            PointF tip = ElectrodeThumbnail.LargestBlobCenters(gbuf, s, 1, minPixels: 8)[0];
            Assert.True(float.IsNaN(tip.X));
        }

        [Fact]
        public void ConcaveFeature_PutsTheTipOnTheMaterial_NotInTheHollow()
        {
            // Mancha em U: o centro geométrico cai no vão, fora da feature.
            const int s = 40;
            var gbuf = new int[s * s];
            for (int i = 0; i < gbuf.Length; i++) gbuf[i] = -1;
            Fill(gbuf, s, 10, 10, 4, 20, 0);    // perna esquerda
            Fill(gbuf, s, 26, 10, 4, 20, 0);    // perna direita
            Fill(gbuf, s, 10, 26, 20, 4, 0);    // base

            PointF tip = ElectrodeThumbnail.LargestBlobCenters(gbuf, s, 1, minPixels: 8)[0];
            Assert.Equal(0, gbuf[(int)tip.Y * s + (int)tip.X]);
        }

        /// <summary>Pinta um retângulo de pixels com o índice do grupo.</summary>
        private static void Fill(int[] gbuf, int s, int x0, int y0, int w, int h, int group)
        {
            for (int y = y0; y < y0 + h && y < s; y++)
                for (int x = x0; x < x0 + w && x < s; x++)
                    gbuf[y * s + x] = group;
        }

        [Fact]
        public void CloseCallouts_AreSeparated_KeepingTheirOrderAroundThePart()
        {
            // Três features vizinhas + uma longe: sem separar, três balões caem um sobre o outro.
            // A 1ª versão (empurra-empurra iterativo) oscilava e devolvia dois ângulos IDÊNTICOS.
            var angles = new List<double> { -1.62, -1.60, -1.58, 0.30 };
            var original = new List<double>(angles);
            ElectrodeThumbnail.SeparateAngles(angles, 0.293);

            var sorted = angles.OrderBy(a => a).ToList();
            for (int i = 1; i < sorted.Count; i++)
                Assert.True(sorted[i] - sorted[i - 1] >= 0.293 - 1e-9,
                    $"vão {i} ficou em {sorted[i] - sorted[i - 1]:0.000}");

            // A ordem em volta da peça é preservada: quem apontava mais à esquerda continua à esquerda.
            List<int> before = Enumerable.Range(0, 4).OrderBy(i => original[i]).ToList();
            List<int> after = Enumerable.Range(0, 4).OrderBy(i => angles[i]).ToList();
            Assert.Equal(before, after);
        }

        [Fact]
        public void TooManyCallouts_AreSpreadEvenly_InsteadOfOverlapping()
        {
            var angles = Enumerable.Repeat(0.5, 8).ToList();
            ElectrodeThumbnail.SeparateAngles(angles, 1.5);   // 8 × 1,5 rad não cabe em 2π

            var sorted = angles.OrderBy(a => a).ToList();
            double step = 2 * System.Math.PI / 8;
            for (int i = 1; i < sorted.Count; i++)
                Assert.Equal(step, sorted[i] - sorted[i - 1], 6);
        }

        [Fact]
        public void Cube_OutlinesTheTwelveEdgesButNotFaceDiagonals()
        {
            var facets = ElectrodeThumbnail.Project(new[] { Cube() });
            Assert.Equal(12, facets.Count);
            int outlined = facets.Sum(f => (f.EdgeAB ? 1 : 0) + (f.EdgeBC ? 1 : 0) + (f.EdgeCA ? 1 : 0));
            Assert.Equal(24, outlined); // 12 arestas do cubo × 2 facetas; as 6 diagonais (×2) não
        }

        [Fact]
        public void Cube_ThreeAxisFacesGetDistinctShades()
        {
            var shades = ElectrodeThumbnail.Project(new[] { Cube() }).Select(f => System.Math.Round(f.Shade, 3)).Distinct().ToList();
            Assert.Equal(3, shades.Count); // X, Y, Z distintos (sombreamento de dois lados: opostas iguais)
        }

        [Fact]
        public void Render_EmptyMesh_ReturnsNull()
        {
            Assert.Null(ElectrodeThumbnail.Render(new double[0][]));
        }

        [Fact]
        public void Render_CentersThePartOnWhite()
        {
            using (Bitmap bmp = ElectrodeThumbnail.Render(new[] { Cube() }, 120))
            {
                Assert.Equal(120, bmp.Width);
                Assert.Equal(120, bmp.Height);
                Assert.Equal(Color.White.ToArgb(), bmp.GetPixel(1, 1).ToArgb());
                Assert.NotEqual(Color.White.ToArgb(), bmp.GetPixel(60, 60).ToArgb());
            }
        }
    }
}
