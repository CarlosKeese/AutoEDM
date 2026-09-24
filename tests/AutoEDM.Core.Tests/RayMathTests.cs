using System;
using AutoEDM.Assembly;
using AutoEDM.Selection;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// O raio de visão do "Criar eletrodo (manual)". O caso que motivou está em
    /// <see cref="FacePequenaNaFrente_GanhaDaFaceGrandeAtras"/>: com uma peça atrás da peça-alvo,
    /// a localização da SE devolvia a face MAIOR de trás; aqui ganha o primeiro impacto.
    /// </summary>
    public class RayMathTests
    {
        /// <summary>Quadrado no plano Z=z, lado 2·h, centrado em (cx, cy), como 2 facetas.</summary>
        private static double[] Square(double cx, double cy, double z, double h) => new[]
        {
            cx - h, cy - h, z,  cx + h, cy - h, z,  cx + h, cy + h, z,
            cx - h, cy - h, z,  cx + h, cy + h, z,  cx - h, cy + h, z,
        };

        private static readonly double[] Down = { 0, 0, -1 };

        [Fact]
        public void Triangulo_AcertoNoMeio_DaDistanciaAteOPlano()
        {
            double t = RayMath.IntersectTriangle(new double[] { 0.2, 0.2, 5 }, Down, 0, 0, 1, 1, 0, 1, 0, 1, 1);
            Assert.Equal(4.0, t, 9);
        }

        [Fact]
        public void Triangulo_ForaDaArea_NaoAcerta()
        {
            Assert.True(double.IsNaN(RayMath.IntersectTriangle(new double[] { 0.9, 0.9, 5 }, Down, 0, 0, 1, 1, 0, 1, 0, 1, 1)));
        }

        [Fact]
        public void Triangulo_AtrasDaOrigem_NaoConta()
        {
            Assert.True(double.IsNaN(RayMath.IntersectTriangle(new double[] { 0.2, 0.2, 0 }, Down, 0, 0, 1, 1, 0, 1, 0, 1, 1)));
        }

        [Fact]
        public void Triangulo_OrientacaoInvertida_AcertaIgual()
        {
            // A orientação da malha da SE não é garantida: os dois lados contam.
            double a = RayMath.IntersectTriangle(new double[] { 0.2, 0.2, 5 }, Down, 0, 0, 1, 1, 0, 1, 0, 1, 1);
            double b = RayMath.IntersectTriangle(new double[] { 0.2, 0.2, 5 }, Down, 0, 0, 1, 0, 1, 1, 1, 0, 1);
            Assert.Equal(a, b, 9);
        }

        [Fact]
        public void FacePequenaNaFrente_GanhaDaFaceGrandeAtras()
        {
            double[] origin = { 0, 0, 100 };
            double big = RayMath.NearestHit(origin, Down, Square(0, 0, 10, 50));   // placa grande, atrás
            double small = RayMath.NearestHit(origin, Down, Square(0, 0, 30, 1));  // face pequena, na frente
            Assert.True(small < big);
            Assert.Equal(70.0, small, 9);
        }

        [Fact]
        public void Malha_PegaOImpactoMaisProximo_DentroDaMesmaFace()
        {
            double[] two = new double[18];
            Array.Copy(Square(0, 0, 10, 5), 0, two, 0, 9);
            Array.Copy(Square(0, 0, 20, 5), 0, two, 9, 9);
            Assert.Equal(80.0, RayMath.NearestHit(new double[] { 0, 0, 100 }, Down, two), 9);
        }

        [Fact]
        public void Caixa_CruzadaPeloRaio()
        {
            Assert.True(RayMath.HitsBox(new double[] { 0, 0, 100 }, Down, new double[] { -1, -1, 0 }, new double[] { 1, 1, 1 }));
            Assert.False(RayMath.HitsBox(new double[] { 5, 0, 100 }, Down, new double[] { -1, -1, 0 }, new double[] { 1, 1, 1 }));
            Assert.False(RayMath.HitsBox(new double[] { 0, 0, -100 }, Down, new double[] { -1, -1, 0 }, new double[] { 1, 1, 1 }));
        }

        [Fact]
        public void RaioOrtografico_RecuaAtrasDoPontoPelaDirecaoDaVista()
        {
            Assert.True(RayMath.TryViewRay(new double[] { 0, 0, 10 }, new double[] { 0, 0, 0 }, false,
                new double[] { 3, 4, -2 }, out double[] o, out double[] d, backOffM: 100));
            Assert.Equal(new double[] { 0, 0, -1 }, d);
            Assert.Equal(new double[] { 3, 4, 98 }, o);
        }

        [Fact]
        public void RaioPerspectiva_SaiDoOlhoPeloPonto()
        {
            Assert.True(RayMath.TryViewRay(new double[] { 0, 0, 10 }, new double[] { 0, 0, 0 }, true,
                new double[] { 0, 3, 6 }, out double[] o, out double[] d));
            Assert.Equal(new double[] { 0, 0, 10 }, o);
            Assert.Equal(0.6, d[1], 9);
            Assert.Equal(-0.8, d[2], 9);
        }

        /// <summary>
        /// Caso real (MD-15335, 2026-09-24): faces de dois postiços lado a lado, 15335.201 em
        /// X=10 e 15335.202 em X=33,3. Juntando as caixas LOCAIS o centro saía em 12,1 (→ 45,4 na
        /// montagem); levando a caixa do 201 para o espaço do 202, o centro fica em torno de 0
        /// (→ ~33 na montagem), que é onde as faces estão.
        /// </summary>
        [Fact]
        public void CaixaDeOutroPostico_VaiParaOEspacoDaReferencia()
        {
            static double[] At(double x, double y, double z) => new[]
            {
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                x, y, z, 1
            };
            OccurrenceTransform p201 = OccurrenceTransform.FromMatrix(At(0.010, -0.065, -0.0361), new[] { 0.010, -0.065, -0.0361 });
            OccurrenceTransform p202 = OccurrenceTransform.FromMatrix(At(0.0333, -0.065, -0.0361), new[] { 0.0333, -0.065, -0.0361 });

            OccurrenceTransform.MapBoxMm(p201, p202, new[] { 14.8, -8.5, 41.0 }, new[] { 31.7, 8.5, 51.2 },
                out double[] mn, out double[] mx);
            Assert.Equal(14.8 - 23.3, mn[0], 6);
            Assert.Equal(31.7 - 23.3, mx[0], 6);
            Assert.Equal(-8.5, mn[1], 6);
            Assert.Equal(41.0, mn[2], 6);

            // Junta com a face do próprio 202 (-7,5…7,5): o centro fica perto de 0, não em 12,1.
            double center = (Math.Min(mn[0], -7.5) + Math.Max(mx[0], 7.5)) / 2;
            Assert.InRange(center, -1.0, 1.0);
        }

        [Fact]
        public void PoseInversa_DesfazATransformacao()
        {
            // 90° em Z + translação: rotação e volta têm de fechar.
            double[] m =
            {
                0, 1, 0, 0,
                -1, 0, 0, 0,
                0, 0, 1, 0,
                0.1, 0.2, 0.3, 1
            };
            OccurrenceTransform pose = OccurrenceTransform.FromMatrix(m, new[] { 0.1, 0.2, 0.3 });
            pose.TransformPointM(0.01, 0.02, 0.03, out double x, out double y, out double z);
            pose.InverseTransformPointM(x, y, z, out double lx, out double ly, out double lz);
            Assert.Equal(0.01, lx, 12);
            Assert.Equal(0.02, ly, 12);
            Assert.Equal(0.03, lz, 12);

            pose.InverseRotate(0, 0, -1, out double dx, out double dy, out double dz);
            Assert.Equal(-1.0, dz, 12);
            Assert.Equal(0.0, dx, 12);
            Assert.Equal(0.0, dy, 12);
        }
    }
}
