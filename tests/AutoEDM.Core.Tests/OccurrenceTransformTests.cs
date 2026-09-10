using System;
using AutoEDM.Assembly;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// A pose 3D da ocorrência. Toda a geometria vive fora do COM justamente para caber aqui:
    /// o Solid Edge não participa de decidir onde o eletrodo vai, só de executar.
    ///
    /// O caso que motivou tudo isto está em <see cref="CavidadeDeitada_MD14972"/> — vale ler
    /// esse primeiro, é o job real onde a conta antiga (somar Z, girar só em Z) dava um número
    /// plausível e errado.
    /// </summary>
    public class OccurrenceTransformTests
    {
        /// <summary>Matriz de vetor-LINHA (translação em 12,13,14): as LINHAS são as imagens dos eixos locais.</summary>
        private static double[] RowMajor(double[] xAxis, double[] yAxis, double[] zAxis, double[] t) => new[]
        {
            xAxis[0], xAxis[1], xAxis[2], 0,
            yAxis[0], yAxis[1], yAxis[2], 0,
            zAxis[0], zAxis[1], zAxis[2], 0,
            t[0],     t[1],     t[2],     1
        };

        /// <summary>A MESMA pose arrumada como vetor-COLUNA (translação em 3,7,11).</summary>
        private static double[] ColumnMajor(double[] xAxis, double[] yAxis, double[] zAxis, double[] t) => new[]
        {
            xAxis[0], yAxis[0], zAxis[0], t[0],
            xAxis[1], yAxis[1], zAxis[1], t[1],
            xAxis[2], yAxis[2], zAxis[2], t[2],
            0,        0,        0,        1
        };

        private static readonly double[] X = { 1, 0, 0 };
        private static readonly double[] Y = { 0, 1, 0 };
        private static readonly double[] Z = { 0, 0, 1 };

        // ---------------------------------------------------------------- layout

        [Fact]
        public void Layout_eDetectadoPelaOrigemDoGetTransform_naoAssumido()
        {
            var t = new double[] { 0.010, 0.020, -0.017 };

            var linha = OccurrenceTransform.FromMatrix(RowMajor(X, Y, Z, t), t);
            var coluna = OccurrenceTransform.FromMatrix(ColumnMajor(X, Y, Z, t), t);

            Assert.Equal(MatrixLayout.TranslationLast, linha.Layout);
            Assert.Equal(MatrixLayout.TranslationColumn, coluna.Layout);
            Assert.Contains("bate com a origem", linha.LayoutEvidence);
            Assert.Contains("bate com a origem", coluna.LayoutEvidence);
        }

        /// <summary>
        /// Sem gabarito o código tem de DIZER que assumiu. Um layout assumido em silêncio é o
        /// jeito de trocar a rotação pela transposta sem ninguém notar.
        /// </summary>
        [Fact]
        public void SemGabaritoDeOrigem_assumeMasAvisa()
        {
            var m = OccurrenceTransform.FromMatrix(RowMajor(X, Y, Z, new double[] { 1, 2, 3 }));

            Assert.Equal(MatrixLayout.TranslationLast, m.Layout);
            Assert.Contains("ASSUMIDA", m.LayoutEvidence);
        }

        /// <summary>
        /// A mesma pose, escrita nas duas arrumações, tem de PRODUZIR O MESMO resultado. É esta
        /// a garantia de que a transposição do bloco 3x3 está do lado certo.
        /// </summary>
        [Fact]
        public void AsDuasArrumacoes_descrevemAMesmaPose()
        {
            // Cavidade girada −90° em Y: o eixo Z local aponta para −X da montagem.
            double[] xAxis = { 0, 0, 1 }, yAxis = { 0, 1, 0 }, zAxis = { -1, 0, 0 };
            var t = new double[] { 0.005, 0, -0.017 };

            var a = OccurrenceTransform.FromMatrix(RowMajor(xAxis, yAxis, zAxis, t), t);
            var b = OccurrenceTransform.FromMatrix(ColumnMajor(xAxis, yAxis, zAxis, t), t);

            a.TransformPointM(0.001, 0.002, 0.003, out double ax, out double ay, out double az);
            b.TransformPointM(0.001, 0.002, 0.003, out double bx, out double by, out double bz);

            Assert.Equal(ax, bx, 9);
            Assert.Equal(ay, by, 9);
            Assert.Equal(az, bz, 9);
        }

        [Fact]
        public void IdaEVolta_pelaMatriz_naoPerdeNada()
        {
            double[] xAxis = { 0, 0, 1 }, yAxis = { 0, 1, 0 }, zAxis = { -1, 0, 0 };
            var t = new double[] { 0.005, 0.006, -0.017 };
            var original = RowMajor(xAxis, yAxis, zAxis, t);

            var voltou = OccurrenceTransform.FromMatrix(original, t).ToMatrix();

            for (int i = 0; i < 16; i++) Assert.Equal(original[i], voltou[i], 9);
        }

        // ---------------------------------------------------------------- inclinação

        [Fact]
        public void SemInclinacao_naoEConsideradaInclinada_mesmoGirandoEmZ()
        {
            // 30° em Z: o eixo Z local continua paralelo ao Z da montagem — era o caso que a
            // conta antiga tratava certo, e tem de continuar passando pelo caminho simples.
            double c = Math.Cos(0.5236), s = Math.Sin(0.5236);
            var m = OccurrenceTransform.FromMatrix(
                RowMajor(new[] { c, s, 0.0 }, new[] { -s, c, 0.0 }, Z, new double[] { 0, 0, 0 }),
                new double[] { 0, 0, 0 });

            Assert.False(m.IsTilted());
        }

        [Theory]
        [InlineData(0, 0, 1, false)]     // Z local = Z da montagem
        [InlineData(0, 0, -1, true)]     // de cabeça para baixo: 180°, inclinada
        [InlineData(-1, 0, 0, true)]     // deitada (o caso MD-14972)
        [InlineData(0, 1, 0, true)]
        public void Inclinacao_olhaOEixoZLocal(double zx, double zy, double zz, bool esperado)
        {
            // Só o eixo Z importa aqui; os outros dois completam um triedro qualquer.
            var m = OccurrenceTransform.FromMatrix(
                RowMajor(X, Y, new[] { zx, zy, zz }, new double[] { 0, 0, 0 }),
                new double[] { 0, 0, 0 });

            Assert.Equal(esperado, m.IsTilted());
        }

        // ---------------------------------------------------------------- o caso real

        /// <summary>
        /// Job MD-14972 (14972.103.par), lido ao vivo em 2026-07-20: cavidade com origem
        /// (0, 0, −17) mm e rotação Y = −90°, ou seja, DEITADA — o eixo Z local dela aponta para
        /// −X da montagem.
        ///
        /// A conta antiga fazia <c>asmZ = occZ + baseZ</c>: para uma queima a 11,1 mm de
        /// profundidade local, dava −17 + 11,1 = −5,9 mm. O número parece razoável, e foi por
        /// isso que passou batido em 3 dos 5 eletrodos. Mas está somando profundidade LOCAL
        /// (que aqui corre no eixo X da montagem) com altura da MONTAGEM: eixos diferentes.
        ///
        /// Com a matriz, a mesma queima cai em X = −11,1 mm e Z = −17 mm — que é onde ela
        /// realmente está.
        /// </summary>
        [Fact]
        public void CavidadeDeitada_MD14972()
        {
            // Rotação de −90° em torno de Y: X local -> −Z da montagem, Z local -> −X? Não:
            // para Ry(−90°), o eixo X local vai para (0,0,1) e o Z local vai para (−1,0,0).
            double[] xAxis = { 0, 0, 1 }, yAxis = { 0, 1, 0 }, zAxis = { -1, 0, 0 };
            var origem = new double[] { 0, 0, -0.017 };            // metros

            var pose = OccurrenceTransform.FromMatrix(RowMajor(xAxis, yAxis, zAxis, origem), origem);

            Assert.True(pose.IsTilted());

            // Queima 11,1 mm "para baixo" no espaço LOCAL da cavidade.
            pose.TransformPointM(0, 0, 0.0111, out double xM, out double yM, out double zM);

            Assert.Equal(-11.1, xM * 1000.0, 3);   // a profundidade correu no X da montagem...
            Assert.Equal(0.0, yM * 1000.0, 3);
            Assert.Equal(-17.0, zM * 1000.0, 3);   // ...e o Z ficou onde a cavidade está

            // E o que a conta antiga teria dito, para o contraste ficar registrado:
            double zAntigo = -17.0 + 11.1;
            Assert.Equal(-5.9, zAntigo, 3);
            Assert.NotEqual(Math.Round(zAntigo, 3), Math.Round(zM * 1000.0, 3));
        }

        /// <summary>
        /// O eletrodo herda a ORIENTAÇÃO da cavidade e ganha a POSIÇÃO da superfície de queima:
        /// é assim que o eixo de queima dele fica paralelo ao da cavidade, em vez de descer no Z
        /// da montagem enquanto a cavidade aponta para o lado.
        /// </summary>
        [Fact]
        public void PoseDoEletrodo_herdaAOrientacaoEtrocaSoAPosicao()
        {
            double[] xAxis = { 0, 0, 1 }, yAxis = { 0, 1, 0 }, zAxis = { -1, 0, 0 };
            var origem = new double[] { 0, 0, -0.017 };
            var cavidade = OccurrenceTransform.FromMatrix(RowMajor(xAxis, yAxis, zAxis, origem), origem);

            var eletrodo = cavidade.WithTranslationM(-0.0111, 0, -0.017);

            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    Assert.Equal(cavidade.R(i, j), eletrodo.R(i, j), 9);

            Assert.Equal(-11.1, eletrodo.TxM * 1000.0, 3);
            Assert.Equal(-17.0, eletrodo.TzM * 1000.0, 3);
            Assert.Equal(cavidade.Layout, eletrodo.Layout);   // escrever no layout que foi lido
        }

        [Fact]
        public void MatrizCurta_eRecusada_emVezDeLerLixo()
        {
            Assert.Throws<ArgumentException>(() => OccurrenceTransform.FromMatrix(new double[9]));
            Assert.Throws<ArgumentException>(() => OccurrenceTransform.FromMatrix(null));
        }
    }
}
