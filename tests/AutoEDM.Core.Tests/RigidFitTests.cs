using System;
using System.Collections.Generic;
using AutoEDM.Import;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// O encaixe rígido é o que decide se duas peças importadas são a mesma, e qual matriz a
    /// ocorrência nova recebe. Errar aqui não estoura exceção: produz uma montagem plausível com
    /// uma peça no lugar errado — ou, no caso do espelho, um postiço de mão trocada. Então os
    /// testes cobrem, nesta ordem: identidade, rotações de eixo, rotação composta, translação
    /// pura, a RECUSA de peças diferentes, e a recusa do ESPELHO.
    /// </summary>
    public class RigidFitTests
    {
        private const double Tol = 0.01;

        /// <summary>Um bloco 60x40x20 com os 8 cantos — a peça de teste mais simples que ainda
        /// fixa uma rotação sem ambiguidade.</summary>
        private static List<double[]> Block(double dx = 60, double dy = 40, double dz = 20)
        {
            var pts = new List<double[]>();
            for (int i = 0; i < 8; i++)
                pts.Add(new[]
                {
                    (i & 1) == 0 ? 0 : dx,
                    (i & 2) == 0 ? 0 : dy,
                    (i & 4) == 0 ? 0 : dz
                });
            return pts;
        }

        private static List<double[]> Transform(IEnumerable<double[]> pts, double[] r, double[] t)
        {
            var outp = new List<double[]>();
            foreach (double[] p in pts)
            {
                double[] q = RigidFit.Mul(r, p);
                outp.Add(new[] { q[0] + t[0], q[1] + t[1], q[2] + t[2] });
            }
            return outp;
        }

        private static double[] RotZ(double deg)
        {
            double a = deg * Math.PI / 180.0, c = Math.Cos(a), s = Math.Sin(a);
            return new[] { c, -s, 0, s, c, 0, 0, 0, 1.0 };
        }

        private static double[] RotY(double deg)
        {
            double a = deg * Math.PI / 180.0, c = Math.Cos(a), s = Math.Sin(a);
            return new[] { c, 0, s, 0, 1.0, 0, -s, 0, c };
        }

        private static double[] Identity => new[] { 1.0, 0, 0, 0, 1, 0, 0, 0, 1 };

        [Fact]
        public void Identical_Clouds_FitWithIdentity()
        {
            var a = Block();
            RigidFit.Result f = RigidFit.Fit(a, Block(), Tol, allowMirror: false);

            Assert.True(f.Ok, f.Why);
            Assert.False(f.IsMirror);
            Assert.True(f.MaxDeviationMm < 1e-9);
            Assert.True(RigidFit.IsRotation(f.R));
            Assert.True(RigidFit.AngleDeg(f.R) < 1e-6);
        }

        /// <summary>Translação pura: o caso do tradutor que assa a posição dentro da geometria.
        /// É o caso em que a translação do encaixe é TODA a informação de posição.</summary>
        [Fact]
        public void PureTranslation_IsRecovered()
        {
            var a = Block();
            var t = new[] { 125.0, -40.0, 17.5 };
            var b = Transform(a, Identity, t);

            RigidFit.Result f = RigidFit.Fit(a, b, Tol, false);

            Assert.True(f.Ok, f.Why);
            for (int i = 0; i < 3; i++) Assert.Equal(t[i], f.TMm[i], 6);
            Assert.True(RigidFit.AngleDeg(f.R) < 1e-6);
        }

        [Theory]
        [InlineData(90.0)]
        [InlineData(180.0)]
        [InlineData(270.0)]
        [InlineData(37.5)]
        public void RotationAboutZ_IsRecovered(double deg)
        {
            var a = Block();
            double[] r = RotZ(deg);
            var t = new[] { 10.0, 20.0, 30.0 };
            var b = Transform(a, r, t);

            RigidFit.Result f = RigidFit.Fit(a, b, Tol, false);

            Assert.True(f.Ok, f.Why);
            Assert.False(f.IsMirror);
            // A rotação recuperada tem de levar os pontos de A sobre B — conferir a MATRIZ
            // elemento a elemento falharia à toa numa peça simétrica, onde várias rotações
            // diferentes são todas corretas. O critério é o desvio.
            AssertMapsOnto(a, b, f);
        }

        /// <summary>Rotação COMPOSTA em dois eixos — o caso que a convenção de ângulos de Euler
        /// não resolve (ver OccurrenceTransform) e por isso o projeto trabalha com a matriz.</summary>
        [Fact]
        public void CompoundRotation_IsRecovered()
        {
            var a = Block();
            double[] r = RigidFit.MulMatrix(RotY(-90), RotZ(45));
            var t = new[] { -200.0, 15.0, 8.0 };
            var b = Transform(a, r, t);

            RigidFit.Result f = RigidFit.Fit(a, b, Tol, false);

            Assert.True(f.Ok, f.Why);
            Assert.True(RigidFit.IsRotation(f.R));
            AssertMapsOnto(a, b, f);
        }

        /// <summary>
        /// Uma peça QUIRAL: nenhuma rotação própria a leva sobre o espelho dela. São as três
        /// pernas de comprimentos diferentes que garantem isso — com elas, a correspondência
        /// entre vértices é forçada, e a única matriz que fecha é a de determinante negativo.
        ///
        /// CUIDADO AO EDITAR ESTES TESTES: um bloco retangular NÃO serve de caso de espelho. Ele
        /// é aquiral — o espelho dele coincide com ele mesmo por uma rotação de 180°, então o
        /// encaixe próprio existe e deve ser preferido (é o que
        /// <see cref="SymmetricPart_PrefersProperRotation_OverMirror"/> cobre). Usar um bloco
        /// aqui faria o teste cobrar do código uma recusa que seria matematicamente errada.
        /// </summary>
        private static List<double[]> ChiralPart() => new List<double[]>
        {
            new[] {  0.0,  0.0,  0.0 },
            new[] { 10.0,  0.0,  0.0 },
            new[] {  0.0, 20.0,  0.0 },
            new[] {  0.0,  0.0, 30.0 }
        };

        /// <summary>A recusa que protege o molde: um postiço de mão direita e o de mão esquerda
        /// têm volume, área e topologia idênticos, então a peneira os junta. Só o encaixe os
        /// separa — e trocar um pelo outro numa cavidade é refugo.</summary>
        [Fact]
        public void MirroredPart_IsRefused_ByDefault()
        {
            var a = ChiralPart();
            // Espelho em X mais uma rotação, para não ser um espelho "óbvio" de eixo.
            var mirror = new[] { -1.0, 0, 0, 0, 1.0, 0, 0, 0, 1.0 };
            var b = Transform(a, RigidFit.MulMatrix(RotZ(30), mirror), new[] { 5.0, 5.0, 5.0 });

            RigidFit.Result refused = RigidFit.Fit(a, b, Tol, allowMirror: false);
            Assert.False(refused.Ok);
            Assert.Contains("ESPELHAD", refused.Why.ToUpperInvariant());

            // E, quando explicitamente liberado, o encaixe existe e vem MARCADO como espelho —
            // nunca passa como se fosse a mesma peça.
            RigidFit.Result allowed = RigidFit.Fit(a, b, Tol, allowMirror: true);
            Assert.True(allowed.Ok, allowed.Why);
            Assert.True(allowed.IsMirror);
            Assert.True(RigidFit.Determinant(allowed.R) < 0);
        }

        /// <summary>Um bloco simétrico em X e Y é espelhável por ROTAÇÃO (gira 180° e dá no
        /// mesmo). Nesse caso o encaixe próprio existe e tem de ser preferido ao espelhado —
        /// senão a ferramenta recusaria peças perfeitamente unificáveis.</summary>
        [Fact]
        public void SymmetricPart_PrefersProperRotation_OverMirror()
        {
            var a = Block(40, 40, 20);
            var mirror = new[] { -1.0, 0, 0, 0, 1.0, 0, 0, 0, 1.0 };
            var b = Transform(a, mirror, new[] { 0.0, 0.0, 0.0 });

            RigidFit.Result f = RigidFit.Fit(a, b, Tol, allowMirror: false);

            Assert.True(f.Ok, f.Why);
            Assert.False(f.IsMirror);
            Assert.True(RigidFit.Determinant(f.R) > 0);
            AssertMapsOnto(a, b, f);
        }

        [Fact]
        public void DifferentSize_IsRefused()
        {
            RigidFit.Result f = RigidFit.Fit(Block(60, 40, 20), Block(60, 40, 20.5), Tol, false);

            Assert.False(f.Ok);
            Assert.NotNull(f.Why);
        }

        [Fact]
        public void DifferentVertexCount_IsRefused()
        {
            var a = Block();
            var b = Block();
            b.Add(new[] { 1.0, 2.0, 3.0 });

            RigidFit.Result f = RigidFit.Fit(a, b, Tol, false);

            Assert.False(f.Ok);
            Assert.Contains("contagem", f.Why);
        }

        /// <summary>Menos de 3 pontos não fixa rotação. A ferramenta tem de RECUSAR, não chutar:
        /// é a diferença entre "não sei" e "peça no lugar errado".</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void TooFewPoints_IsRefused(int n)
        {
            var a = new List<double[]>();
            var b = new List<double[]>();
            for (int i = 0; i < n; i++) { a.Add(new[] { (double)i, 0, 0 }); b.Add(new[] { (double)i, 0, 0 }); }

            RigidFit.Result f = RigidFit.Fit(a, b, Tol, false);

            Assert.False(f.Ok);
            Assert.NotNull(f.Why);
        }

        /// <summary>Geometria vinda de tradutor não é bit-a-bit igual. Ruído DENTRO da tolerância
        /// tem de passar; fora dela, não.</summary>
        [Fact]
        public void NoiseWithinTolerance_StillFits()
        {
            var a = Block();
            var b = Transform(a, RotZ(90), new[] { 3.0, 4.0, 5.0 });
            var rnd = new Random(1234);
            foreach (double[] p in b)
                for (int k = 0; k < 3; k++) p[k] += (rnd.NextDouble() - 0.5) * 0.004;   // +-0,002 mm

            RigidFit.Result f = RigidFit.Fit(a, b, Tol, false);

            Assert.True(f.Ok, f.Why);
            Assert.True(f.MaxDeviationMm <= Tol);
        }

        [Fact]
        public void NoiseBeyondTolerance_IsRefused()
        {
            var a = Block();
            var b = Transform(a, Identity, new[] { 0.0, 0.0, 0.0 });
            b[3][1] += 0.5;                       // um canto 0,5 mm fora: outra peça

            RigidFit.Result f = RigidFit.Fit(a, b, Tol, false);

            Assert.False(f.Ok);
        }

        /// <summary>Uma peça com muitos vértices (flange com furos) ainda encaixa, e encaixa
        /// rápido — a grade espacial é o que impede a busca de virar O(n²).</summary>
        [Fact]
        public void ManyVertices_StillFits()
        {
            var a = new List<double[]>();
            for (int i = 0; i < 400; i++)
            {
                double ang = i * 2 * Math.PI / 40.0;
                a.Add(new[] { 50 * Math.Cos(ang), 50 * Math.Sin(ang), (i / 40) * 3.0 });
            }
            var b = Transform(a, RigidFit.MulMatrix(RotY(90), RotZ(13)), new[] { 7.0, -9.0, 11.0 });

            RigidFit.Result f = RigidFit.Fit(a, b, Tol, false);

            Assert.True(f.Ok, f.Why);
            AssertMapsOnto(a, b, f);
        }

        /// <summary>
        /// A nuvem de vértices de um sextavado interno de parafuso: dois anéis hexagonais
        /// iguais, em profundidades diferentes. Virar a peça 180° em torno de um eixo
        /// transversal troca um anel pelo outro e a NUVEM coincide — mas o parafuso não, porque
        /// a haste existe só para um lado.
        /// </summary>
        private static List<double[]> HexSocketVertices()
        {
            var pts = new List<double[]>();
            foreach (double z in new[] { 0.0, -6.0 })
                for (int i = 0; i < 6; i++)
                {
                    double ang = i * Math.PI / 3.0;
                    pts.Add(new[] { 5 * Math.Cos(ang), 5 * Math.Sin(ang), z });
                }
            return pts;
        }

        /// <summary>
        /// O BUG RELATADO (2026-10-18, "alguns parafusos ficaram invertidos"), reproduzido na
        /// sua forma mais crua: encaixar a peça CONTRA ELA MESMA tem de dar a IDENTIDADE.
        ///
        /// O código antigo devolvia o primeiro candidato que passasse na conferência de desvio,
        /// e numa nuvem simétrica isso costuma ser um giro de 180° (ou 60°, ou 120°) em vez de
        /// não girar nada. Medido num projeto importado real: dezenas de peças com autoencaixe
        /// de 180°/120°/60°. Agora vence o candidato de MENOR GIRO, e nenhum gira menos que a
        /// identidade.
        /// </summary>
        [Fact]
        public void SelfFit_YieldsIdentity_NotASymmetry()
        {
            var a = HexSocketVertices();

            RigidFit.Result f = RigidFit.Fit(a, a, Tol, allowMirror: false);

            Assert.True(f.Ok, f.Why);
            Assert.True(RigidFit.AngleDeg(f.R) < 0.5,
                $"autoencaixe girou {RigidFit.AngleDeg(f.R):0.0}° — deveria ser a identidade");
            for (int i = 0; i < 3; i++) Assert.Equal(0.0, f.TMm[i], 6);
        }

        /// <summary>O mesmo, com a peça apenas transladada: o encaixe tem de recuperar a
        /// translação SEM inventar giro.</summary>
        [Fact]
        public void TranslatedSymmetricPart_YieldsNoRotation()
        {
            var a = HexSocketVertices();
            var t = new[] { 120.0, -45.0, 8.0 };
            var b = Transform(a, Identity, t);

            RigidFit.Result f = RigidFit.Fit(a, b, Tol, false);

            Assert.True(f.Ok, f.Why);
            Assert.True(RigidFit.AngleDeg(f.R) < 0.5, $"girou {RigidFit.AngleDeg(f.R):0.0}° sem precisar");
            for (int i = 0; i < 3; i++) Assert.Equal(t[i], f.TMm[i], 6);
        }

        /// <summary>
        /// A prova do CORPO INTEIRO em ação. A nuvem de vértices aceita o movimento invertido;
        /// o corpo não. Com a prova ligada, o movimento invertido é RECUSADO — é o que impede o
        /// parafuso de ser assentado de ponta-cabeça quando a identidade não está disponível.
        /// </summary>
        [Fact]
        public void WholeBodyCheck_RejectsTransformThatOnlyFitsTheVertices()
        {
            var a = HexSocketVertices();

            // Prova que recusa TUDO: nenhum movimento encaixa o corpo, e o encaixe tem de
            // RECUSAR em vez de devolver um candidato aprovado só nos vértices.
            RigidFit.Result none = RigidFit.Fit(a, a, Tol, false, (r, t) => false);
            Assert.False(none.Ok);
            Assert.Contains("corpo inteiro", none.Why);

            // Prova que recusa só as SIMETRIAS (aceita apenas o movimento que não gira): é o
            // comportamento real dos anéis num parafuso, e o encaixe tem de convergir para a
            // identidade em vez de desistir.
            RigidFit.Result only = RigidFit.Fit(a, a, Tol, false, (r, t) => RigidFit.AngleDeg(r) < 0.5);
            Assert.True(only.Ok, only.Why);
            Assert.True(RigidFit.AngleDeg(only.R) < 0.5);

            // Prova que aceita tudo: o encaixe existe (a prova é quem recusa, não um defeito
            // do encaixe) — e, pela preferência por menor giro, ainda assim sai a identidade.
            RigidFit.Result all = RigidFit.Fit(a, a, Tol, false, (r, t) => true);
            Assert.True(all.Ok, all.Why);
            Assert.True(RigidFit.AngleDeg(all.R) < 0.5);
        }

        /// <summary>A prova que importa: o encaixe leva TODO ponto de A sobre algum ponto de B.
        /// Comparar matrizes não serve — numa peça simétrica há várias rotações corretas.</summary>
        private static void AssertMapsOnto(List<double[]> a, List<double[]> b, RigidFit.Result f)
        {
            foreach (double[] p in a)
            {
                double[] q = RigidFit.Mul(f.R, p);
                q[0] += f.TMm[0]; q[1] += f.TMm[1]; q[2] += f.TMm[2];

                double best = double.MaxValue;
                foreach (double[] t in b)
                {
                    double dx = q[0] - t[0], dy = q[1] - t[1], dz = q[2] - t[2];
                    best = Math.Min(best, Math.Sqrt(dx * dx + dy * dy + dz * dz));
                }
                Assert.True(best <= Tol, $"ponto sem par: desvio {best:0.0000} mm");
            }
        }
    }
}
