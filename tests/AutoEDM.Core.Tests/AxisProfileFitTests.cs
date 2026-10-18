using System;
using System.Collections.Generic;
using AutoEDM.Import;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// O caminho das peças TORNEADAS, e ele existe por uma medição, não por uma previsão: no
    /// primeiro run real (base de molde Polimold, 45 ocorrências, 2026-10-18) a bucha extratora,
    /// a bucha guia, o pino de retorno e a coluna guia apareceram com **zero vértices** — numa
    /// peça de revolução toda aresta é círculo, e círculo não tem canto. Eram justamente as
    /// peças mais repetidas do molde, e o encaixe por nuvem de pontos não as alcançava.
    ///
    /// O teste que mais importa aqui é <see cref="DifferentBore_IsRefused"/>: ele reproduz com
    /// os números medidos as duas buchas guia daquela base — mesma caixa 47×47×116, mesma
    /// topologia, nomes quase iguais, e furo Ø30 contra Ø32. São a coluna de centragem que
    /// impede montar o molde ao contrário. Unificá-las destruiria a prova de erro do molde.
    /// </summary>
    public class AxisProfileFitTests
    {
        private const double Tol = 0.01;

        /// <summary>Uma bucha: anéis num eixo só. <paramref name="bore"/> é o Ø do furo.</summary>
        private static List<Ring> Bushing(double bore = 30.0, double flange = 47.0, double len = 116.0)
        {
            var z = new[] { -len / 2, -len / 2 + 20, 0.0, len / 2 - 20, len / 2 };
            var rings = new List<Ring>();
            // Furo em toda a extensão + flange nas pontas: perfil típico de bucha guia.
            foreach (double zz in z) rings.Add(R(bore / 2, zz));
            rings.Add(R(42.0 / 2, -len / 2));
            rings.Add(R(flange / 2, -len / 2));
            rings.Add(R(42.0 / 2, len / 2));
            return rings;
        }

        private static Ring R(double radius, double z, int axisSign = 1) => new Ring
        {
            CenterMm = new[] { 0.0, 0.0, z },
            // Sinal alternado de propósito: é o que a Solid Edge devolve de verdade (a
            // orientação da aresta inverte o eixo sem significar nada), e canonizar isso é
            // responsabilidade do código sob teste.
            AxisDir = new[] { 0.0, 0.0, (double)axisSign },
            RadiusMm = radius
        };

        private static List<Ring> Transform(IEnumerable<Ring> rings, double[] r, double[] t)
        {
            var outp = new List<Ring>();
            foreach (Ring g in rings)
            {
                double[] c = Mul(r, g.CenterMm);
                outp.Add(new Ring
                {
                    CenterMm = new[] { c[0] + t[0], c[1] + t[1], c[2] + t[2] },
                    AxisDir = Mul(r, g.AxisDir),
                    RadiusMm = g.RadiusMm
                });
            }
            return outp;
        }

        private static double[] Mul(double[] r, double[] p) => new[]
        {
            r[0] * p[0] + r[1] * p[1] + r[2] * p[2],
            r[3] * p[0] + r[4] * p[1] + r[5] * p[2],
            r[6] * p[0] + r[7] * p[1] + r[8] * p[2]
        };

        private static double[] RotX(double deg)
        {
            double a = deg * Math.PI / 180.0, c = Math.Cos(a), s = Math.Sin(a);
            return new[] { 1.0, 0, 0, 0, c, -s, 0, s, c };
        }

        private static double[] Identity => new[] { 1.0, 0, 0, 0, 1, 0, 0, 0, 1 };

        private static AxisProfileFit.Profile P(List<Ring> rings) => AxisProfileFit.BuildProfile(rings, Tol);

        [Fact]
        public void TurnedPart_IsRecognisedAsRevolution()
        {
            AxisProfileFit.Profile p = P(Bushing());

            Assert.True(p.Ok, p.Why);
            Assert.Equal(8, p.Count);
            // O eixo sai canonizado: Z positivo, apesar de metade dos anéis vir com (0,0,−1).
            Assert.Equal(1.0, Math.Abs(p.AxisDir[2]), 9);
        }

        /// <summary>O sinal do eixo vem da orientação da aresta e não significa nada. Se o
        /// código não canonizar, a mesma peça parece ter dois eixos e é recusada.</summary>
        [Fact]
        public void MixedAxisSigns_StillOneAxis()
        {
            var rings = new List<Ring> { R(15, -50, +1), R(15, 50, -1), R(21, -50, -1), R(23.5, 50, +1) };
            AxisProfileFit.Profile p = P(rings);
            Assert.True(p.Ok, p.Why);
        }

        [Fact]
        public void IdenticalBushings_Fit()
        {
            AxisProfileFit.Result f = AxisProfileFit.Fit(P(Bushing()), P(Bushing()), Tol);

            Assert.True(f.Ok, f.Why);
            Assert.True(f.MaxDeviationMm < 1e-9);
            Assert.False(f.Flipped);
        }

        /// <summary>
        /// O CASO REAL medido na base de molde: duas buchas guia de mesma caixa envolvente,
        /// mesma contagem de faces/arestas, nome quase igual — e furo Ø30 contra Ø32. Uma delas
        /// é a centragem que impede montar o molde ao contrário. Tem de ser RECUSADA.
        /// </summary>
        [Fact]
        public void DifferentBore_IsRefused()
        {
            AxisProfileFit.Result f = AxisProfileFit.Fit(P(Bushing(bore: 30.0)), P(Bushing(bore: 32.0)), Tol);

            Assert.False(f.Ok);
            Assert.Contains("não coincidem", f.Why);
        }

        /// <summary>Diferença de 2 centésimos no furo (H7 contra H6) já separa — a tolerância é
        /// linear, não percentual.</summary>
        [Fact]
        public void BoreDifferingByHundredths_IsRefused()
        {
            Assert.False(AxisProfileFit.Fit(P(Bushing(bore: 30.00)), P(Bushing(bore: 30.05)), Tol).Ok);
        }

        [Fact]
        public void TranslatedBushing_RecoversTranslation()
        {
            var t = new[] { 137.0, -158.0, -96.0 };
            AxisProfileFit.Result f = AxisProfileFit.Fit(P(Bushing()), P(Transform(Bushing(), Identity, t)), Tol);

            Assert.True(f.Ok, f.Why);
            AssertMapsRings(Bushing(), Transform(Bushing(), Identity, t), f);
        }

        /// <summary>Bucha deitada: o eixo sai de Z para Y. O encaixe tem de achar a rotação que
        /// alinha os eixos.</summary>
        [Fact]
        public void RotatedBushing_RecoversRotation()
        {
            var a = Bushing();
            var b = Transform(a, RotX(90), new[] { 10.0, 20.0, 30.0 });

            AxisProfileFit.Result f = AxisProfileFit.Fit(P(a), P(b), Tol);

            Assert.True(f.Ok, f.Why);
            AssertMapsRings(a, b, f);
        }

        /// <summary>Bucha montada de ponta-cabeça: o perfil só fecha invertendo o eixo. Tem de
        /// encaixar E dizer que inverteu.</summary>
        [Fact]
        public void FlippedBushing_FitsAndReportsFlip()
        {
            // Perfil ASSIMÉTRICO de propósito: num perfil simétrico a inversão seria
            // indistinguível e o teste não provaria nada.
            var a = new List<Ring> { R(15, -50), R(15, 50), R(21, -50), R(23.5, 30) };
            var b = Transform(a, RotX(180), new double[] { 0, 0, 0 });

            AxisProfileFit.Result f = AxisProfileFit.Fit(P(a), P(b), Tol);

            Assert.True(f.Ok, f.Why);
            AssertMapsRings(a, b, f);
        }

        /// <summary>Um corpo com círculos em eixos CRUZADOS (um mancal com furo lateral) tem só
        /// arestas circulares e NÃO é de revolução. Aceitá-lo daria um encaixe com a peça
        /// girada, porque o giro em torno do eixo deixaria de ser livre.</summary>
        [Fact]
        public void CirclesOnCrossedAxes_IsNotRevolution()
        {
            var rings = new List<Ring>
            {
                R(15, -50), R(15, 50),
                new Ring { CenterMm = new[] { 0.0, 0.0, 0.0 }, AxisDir = new[] { 1.0, 0.0, 0.0 }, RadiusMm = 5 }
            };

            AxisProfileFit.Profile p = P(rings);

            Assert.False(p.Ok);
            Assert.Contains("eixo", p.Why);
        }

        /// <summary>Círculo FORA do eixo comum (furo paralelo deslocado, como num flange
        /// parafusado) também quebra a revolução.</summary>
        [Fact]
        public void CircleOffAxis_IsNotRevolution()
        {
            var rings = new List<Ring>
            {
                R(15, -50), R(15, 50),
                new Ring { CenterMm = new[] { 30.0, 0.0, 0.0 }, AxisDir = new[] { 0.0, 0.0, 1.0 }, RadiusMm = 5 }
            };

            AxisProfileFit.Profile p = P(rings);

            Assert.False(p.Ok);
            Assert.Contains("fora do eixo", p.Why);
        }

        [Fact]
        public void DifferentRingCount_IsRefused()
        {
            var a = Bushing();
            var b = Bushing();
            b.Add(R(10, 5));

            AxisProfileFit.Result f = AxisProfileFit.Fit(P(a), P(b), Tol);

            Assert.False(f.Ok);
            Assert.Contains("contagem", f.Why);
        }

        [Fact]
        public void TooFewRings_IsNotRevolution()
        {
            Assert.False(P(new List<Ring> { R(15, 0) }).Ok);
            Assert.False(P(new List<Ring>()).Ok);
        }

        /// <summary>A rotação entre dois eixos ANTIPARALELOS é o caso degenerado do Rodrigues
        /// (produto vetorial nulo). Tem de sair uma meia-volta de verdade, não NaN.</summary>
        [Fact]
        public void RotationBetween_HandlesAntiparallel()
        {
            double[] r = AxisProfileFit.RotationBetween(new[] { 0.0, 0, 1 }, new[] { 0.0, 0, -1 });

            Assert.NotNull(r);
            Assert.True(RigidFit.IsRotation(r, 1e-9));
            double[] got = Mul(r, new[] { 0.0, 0, 1 });
            Assert.Equal(0.0, got[0], 9);
            Assert.Equal(0.0, got[1], 9);
            Assert.Equal(-1.0, got[2], 9);
        }

        [Fact]
        public void RotationBetween_HandlesIdentical()
        {
            double[] r = AxisProfileFit.RotationBetween(new[] { 0.0, 0, 1 }, new[] { 0.0, 0, 1 });
            Assert.NotNull(r);
            Assert.True(RigidFit.IsRotation(r, 1e-9));
        }

        /// <summary>
        /// A prova que importa: o movimento devolvido leva CADA anel de A sobre um anel de B com
        /// o mesmo raio. Comparar a matriz não serviria — numa peça de revolução infinitas
        /// rotações são igualmente corretas, e é por isso que o giro em torno do eixo é livre.
        /// </summary>
        private static void AssertMapsRings(List<Ring> a, List<Ring> b, AxisProfileFit.Result f)
        {
            Assert.True(RigidFit.IsRotation(f.R, 1e-9), "a rotação devolvida não é ortonormal");

            foreach (Ring g in a)
            {
                double[] c = Mul(f.R, g.CenterMm);
                c[0] += f.TMm[0]; c[1] += f.TMm[1]; c[2] += f.TMm[2];

                double best = double.MaxValue;
                foreach (Ring h in b)
                {
                    if (Math.Abs(h.RadiusMm - g.RadiusMm) > Tol) continue;
                    double dx = c[0] - h.CenterMm[0], dy = c[1] - h.CenterMm[1], dz = c[2] - h.CenterMm[2];
                    best = Math.Min(best, Math.Sqrt(dx * dx + dy * dy + dz * dz));
                }
                Assert.True(best <= Tol, $"anel Ø{2 * g.RadiusMm:0.##} sem par: desvio {best:0.0000} mm");
            }
        }
    }
}
