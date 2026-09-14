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
