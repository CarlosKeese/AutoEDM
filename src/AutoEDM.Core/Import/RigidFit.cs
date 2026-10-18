using System;
using System.Collections.Generic;

namespace AutoEDM.Import
{
    /// <summary>
    /// O encaixe RÍGIDO entre duas nuvens de pontos: a rotação + translação que leva a nuvem
    /// A exatamente sobre a nuvem B, ou a recusa de que não existe tal movimento.
    ///
    /// POR QUE ISTO EXISTE. Quando um projeto vem de outro CAD (STEP/Parasolid), cada posição
    /// da mesma peça chega como um ARQUIVO diferente. Para substituir todas por uma cópia só é
    /// preciso responder duas coisas, nesta ordem: (a) estes dois corpos são a MESMA peça? e
    /// (b) se são, que movimento rígido leva um ao outro? A segunda pergunta é a que preserva a
    /// posição — é exatamente a matriz que a ocorrência nova tem de receber.
    ///
    /// POR QUE NÃO BASTA COMPARAR MEDIDAS. Volume, área e contagem de faces são invariantes de
    /// rotação, então servem para PENEIRAR candidatos barato. Mas eles não provam igualdade: um
    /// postiço e o seu ESPELHO têm volume, área e topologia idênticos, e trocar um pelo outro
    /// num molde é refugo. A prova tem de ser o encaixe medido ponto a ponto, e o espelho
    /// aparece nele como det(R) = -1 — ver <see cref="Determinant"/>.
    ///
    /// POR QUE SEM SVD. O caminho clássico (Kabsch) pede a decomposição em valores singulares
    /// de uma 3x3, que é código numérico comprido e difícil de conferir. Aqui os pontos são
    /// VÉRTICES CORRESPONDENTES exatos, não medidas com ruído: dois vetores independentes
    /// bastam para montar um triedro ortonormal por Gram-Schmidt, e a rotação sai de
    /// <c>R = F_B · F_A^T</c>. É exato, curto e conferível à mão.
    ///
    /// Convenção, a MESMA de <see cref="AutoEDM.Assembly.OccurrenceTransform"/>:
    /// <c>p' = R · p + t</c>, R guardado linha-a-linha (<c>r[linha*3 + coluna]</c>).
    /// Tudo em MILÍMETROS aqui — a conversão para metros é de quem fala com o COM.
    /// </summary>
    public static class RigidFit
    {
        /// <summary>Quantos pares de pontos de referência tentar antes de desistir. Na prática o
        /// primeiro par resolve; o teto existe para peça muito simétrica (um pino torneado tem
        /// infinitas soluções válidas e qualquer uma serve), onde a busca poderia explodir.</summary>
        private const int MaxAttempts = 4096;

        /// <summary>O resultado do encaixe. <see cref="Ok"/> falso sempre vem com <see cref="Why"/>
        /// preenchido — esta classe nunca recusa em silêncio.</summary>
        public sealed class Result
        {
            public bool Ok;

            /// <summary>Rotação 3x3, linha-a-linha. Só válida com <see cref="Ok"/>.</summary>
            public double[] R;

            /// <summary>Translação em mm. Só válida com <see cref="Ok"/>.</summary>
            public double[] TMm;

            /// <summary>O pior desvio medido (mm) depois de aplicar o encaixe — a evidência de
            /// que ele vale. Zero não acontece na prática; abaixo da tolerância, sim.</summary>
            public double MaxDeviationMm;

            /// <summary>O encaixe só fecha ESPELHANDO (det(R) = -1). Peça espelhada NÃO é a mesma
            /// peça: num molde, trocar um postiço pelo espelho dele é refugo.</summary>
            public bool IsMirror;

            public string Why;
        }

        /// <summary>
        /// Acha o movimento rígido que leva <paramref name="aMm"/> sobre <paramref name="bMm"/>.
        /// Recusa (sem exceção) quando as nuvens não são a mesma forma, quando não há pontos
        /// suficientes para decidir, ou quando só fecham espelhando e o espelho não foi liberado.
        /// </summary>
        /// <param name="tolMm">O quanto um ponto pode cair fora do seu par. Geometria vinda de
        /// tradução entre CADs não é bit-a-bit idêntica: 0,01 mm é folgado para o arredondamento
        /// do tradutor e apertado demais para deixar passar duas peças de medidas diferentes.</param>
        /// <param name="allowMirror">Aceitar o encaixe espelhado como "mesma peça".</param>
        /// <summary>
        /// Prova ADICIONAL, do corpo inteiro, para um candidato (R, t). Devolve falso quando o
        /// movimento encaixa os vértices mas não encaixa a peça.
        ///
        /// POR QUE É NECESSÁRIA (achado em projeto importado real, 2026-10-18). Os vértices são
        /// um SUBCONJUNTO do corpo, e um movimento que leva o subconjunto em si mesmo não leva
        /// necessariamente o corpo. Medido: num projeto de 138 peças, dezenas encaixaram CONTRA
        /// SI MESMAS com giro de 180°, 120° ou 60° — num parafuso de sextavado interno, o anel de
        /// vértices do topo e o do fundo são iguais e paralelos, então virar a peça troca um anel
        /// pelo outro e a nuvem coincide, mesmo que o parafuso (que tem haste só para um lado)
        /// não coincida. O sintoma relatado pelo Carlos foi exatamente este: "alguns parafusos
        /// ficaram invertidos".
        /// </summary>
        public delegate bool WholeBodyCheck(double[] r, double[] tMm);

        public static Result Fit(IList<double[]> aMm, IList<double[]> bMm, double tolMm, bool allowMirror) =>
            Fit(aMm, bMm, tolMm, allowMirror, null);

        /// <summary>
        /// Acha o movimento rígido, agora com duas defesas contra a ambiguidade do subconjunto
        /// de vértices:
        ///
        /// 1. <paramref name="wholeBody"/> — quando dado, todo candidato aprovado nos vértices
        ///    ainda tem de passar por uma prova do corpo inteiro (na prática: os anéis, que
        ///    cobrem a peça e distinguem as duas pontas de um parafuso).
        /// 2. Entre os candidatos que passam, vence o de MENOR GIRO, não o primeiro encontrado.
        ///    Numa importação em que a posição vem na ocorrência — o caso mais comum — o
        ///    movimento certo entre dois arquivos iguais é a IDENTIDADE, e a identidade é
        ///    sempre o candidato de menor giro. A versão anterior devolvia o primeiro que
        ///    passasse, e por isso assentava a peça girada mesmo tendo a resposta certa em mãos.
        /// </summary>
        public static Result Fit(IList<double[]> aMm, IList<double[]> bMm, double tolMm, bool allowMirror,
            WholeBodyCheck wholeBody)
        {
            if (aMm == null || bMm == null) return Fail("nuvem de pontos nula.");
            if (aMm.Count != bMm.Count)
                return Fail($"contagem de vértices diferente ({aMm.Count} contra {bMm.Count}) — não são a mesma peça.");

            int n = aMm.Count;

            // Três pontos é o mínimo para FIXAR uma rotação. Com menos, qualquer resposta que
            // déssemos seria chute: é melhor recusar e deixar o grupo marcado como não
            // confirmado do que substituir uma peça por outra com base em nada.
            if (n < 3)
                return Fail(n == 0
                    ? "o corpo não expôs vértice nenhum (esfera, cilindro inteiro ou leitura falhada) — " +
                      "sem pontos não há como provar que as peças coincidem."
                    : $"só {n} vértice(s): são precisos 3 para fixar uma rotação.");

            double[] ca = Centroid(aMm), cb = Centroid(bMm);

            // Raio até o centroide: a única grandeza por ponto que não muda com rotação nem
            // translação. Se os dois conjuntos ORDENADOS de raios não batem, as nuvens não são
            // a mesma forma e nem vale montar triedro.
            double[] ra = Radii(aMm, ca), rb = Radii(bMm, cb);
            var sa = (double[])ra.Clone(); Array.Sort(sa);
            var sb = (double[])rb.Clone(); Array.Sort(sb);
            for (int i = 0; i < n; i++)
                if (Math.Abs(sa[i] - sb[i]) > tolMm)
                    return Fail($"a distribuição de vértices difere (raio {sa[i]:0.###} contra {sb[i]:0.###} mm " +
                                $"no {i + 1}o vértice mais próximo do centro) — formas diferentes.");

            // O ponto mais LONGE do centroide é o melhor primeiro referencial: é o que tem menos
            // companhia no mesmo raio, então gera menos candidatos ambíguos.
            int i1 = IndexOfMax(ra);
            if (ra[i1] <= tolMm)
                return Fail("todos os vértices estão sobre o centroide — nuvem degenerada.");

            var hashB = new PointHash(bMm, Math.Max(tolMm * 4.0, 1e-6));
            List<int> c1 = CandidatesByRadius(rb, ra[i1], tolMm);

            // O segundo referencial não pode ser (anti)paralelo ao primeiro, senão o triedro
            // degenera. Varre do mais longe para o mais perto e pega o primeiro bem aberto.
            int[] byRadiusDesc = IndicesSortedByRadiusDesc(ra);
            int attempts = 0;
            string lastWhy = "nenhum par de referência produziu encaixe dentro da tolerância.";

            // O melhor candidato até agora, por MENOR GIRO. Ver a nota de Fit: entre movimentos
            // todos válidos nos vértices, o de menor giro é o que não gira a peça à toa.
            Result best = null;
            double bestAngle = double.MaxValue;
            bool rejectedByBody = false;

            foreach (int i2 in byRadiusDesc)
            {
                if (i2 == i1) continue;
                double[] v1 = Sub(aMm[i1], ca), v2 = Sub(aMm[i2], ca);

                // sen do ângulo entre os dois: perto de zero é colinear (ou antiparalelo), e aí
                // o Gram-Schmidt não tem o que ortogonalizar.
                if (Norm(Cross(v1, v2)) < 0.30 * Norm(v1) * Norm(v2)) continue;

                double d12 = Distance(aMm[i1], aMm[i2]);
                foreach (int j1 in c1)
                {
                    foreach (int j2 in CandidatesByRadius(rb, ra[i2], tolMm))
                    {
                        if (j2 == j1) continue;

                        // Poda forte e barata: a distância entre os dois referenciais é rígida.
                        // Corta a esmagadora maioria dos pares antes de montar triedro nenhum.
                        if (Math.Abs(Distance(bMm[j1], bMm[j2]) - d12) > tolMm) continue;
                        // Teto de busca. Se já houver candidato aprovado, ele VALE — esgotar o
                        // orçamento procurando um giro ainda menor não invalida o que já passou
                        // na conferência de desvio e na prova do corpo. (Recusar aqui foi uma
                        // regressão introduzida junto com a escolha pelo menor giro: peça muito
                        // simétrica tem centenas de candidatos válidos e estourava o teto antes
                        // de devolver qualquer um deles.)
                        if (++attempts > MaxAttempts)
                            return best ?? Fail("busca de correspondência esgotada (peça muito simétrica ou " +
                                                "tolerância folgada demais) — nada foi substituído.");

                        // Os DOIS encaixes possíveis para este par de referências, nesta ordem:
                        // o próprio (rotação) e o espelhado (reflexão). A ordem importa — numa
                        // peça AQUIRAL (um bloco retangular, por exemplo) os dois fecham, e o
                        // certo é preferir a rotação: dizer "espelhada" de uma peça que é
                        // simplesmente simétrica mandaria o usuário procurar um problema que não
                        // existe.
                        foreach (bool asMirror in new[] { false, true })
                        {
                            double[] r = RotationFromTwoVectors(v1, v2,
                                Sub(bMm[j1], cb), Sub(bMm[j2], cb), asMirror);
                            if (r == null) continue;

                            double dev = MaxDeviation(aMm, ca, r, cb, hashB, tolMm);
                            if (dev > tolMm) continue;

                            // Encaixa espelhando, e o espelho não foi liberado: guarda o MOTIVO
                            // certo e segue procurando. É informação que o usuário precisa — um
                            // par esquerdo/direito de postiços não é um defeito da varredura.
                            if (asMirror && !allowMirror)
                            {
                                lastWhy = "as peças só coincidem ESPELHADAS — são um par de mão " +
                                          "esquerda/direita, não a mesma peça.";
                                continue;
                            }

                            // t sai do encaixe dos centroides: p_b = R·(p_a - ca) + cb.
                            double[] tCand = Sub(cb, Mul(r, ca));

                            // A PROVA DO CORPO INTEIRO. Sem ela, um movimento que só encaixa o
                            // subconjunto de vértices passaria — e é assim que um parafuso é
                            // assentado de ponta-cabeça.
                            if (wholeBody != null && !wholeBody(r, tCand))
                            {
                                rejectedByBody = true;
                                continue;
                            }

                            double angle = AngleDeg(r);
                            if (angle < bestAngle)
                            {
                                bestAngle = angle;
                                best = new Result
                                {
                                    Ok = true,
                                    R = r,
                                    TMm = tCand,
                                    MaxDeviationMm = dev,
                                    IsMirror = asMirror,
                                    Why = $"encaixe conferido em {n} vértices, pior desvio {dev:0.0000} mm" +
                                          (wholeBody != null ? ", confirmado no corpo inteiro" : "") +
                                          (angle > 0.5 ? $", giro {angle:0.0}°" : "") +
                                          (asMirror ? " (ESPELHADO)" : "")
                                };

                                // Não existe candidato melhor que "não gira nada": pode parar.
                                if (angle <= 0.5) return best;
                            }
                        }
                    }
                }
            }

            if (best != null) return best;
            if (rejectedByBody)
                return Fail("o movimento encaixa os vértices mas NÃO o corpo inteiro — " +
                            "é o caso do parafuso que coincidiria de ponta-cabeça. Não unificada.");
            return Fail(lastWhy);
        }

        // ------------------------------------------------------------------ o triedro

        /// <summary>
        /// A rotação que leva o par de vetores (<paramref name="a1"/>, <paramref name="a2"/>) sobre
        /// (<paramref name="b1"/>, <paramref name="b2"/>). Monta um triedro ortonormal de cada lado
        /// por Gram-Schmidt e compõe: <c>R = F_B · F_A^T</c>. Devolve null quando um dos pares é
        /// colinear (triedro degenerado).
        ///
        /// O terceiro eixo sai do produto vetorial dos dois primeiros, nos DOIS lados — então os
        /// dois triedros nascem destros e o resultado é SEMPRE uma rotação própria. Um par de
        /// vetores não distingue reflexão de rotação (os dois primeiros eixos são idênticos nos
        /// dois casos; só o terceiro troca de sinal), por isso o espelho não "aparece" sozinho:
        /// é <paramref name="asMirror"/> que o pede explicitamente, invertendo o terceiro eixo
        /// do lado B. Quem chama tenta os dois e julga pelo desvio medido.
        /// </summary>
        /// <param name="asMirror">Inverte o terceiro eixo do triedro de B, produzindo a REFLEXÃO
        /// (det = -1) em vez da rotação.</param>
        public static double[] RotationFromTwoVectors(double[] a1, double[] a2, double[] b1, double[] b2,
            bool asMirror = false)
        {
            double[] fa = Frame(a1, a2), fb = Frame(b1, b2);
            if (fa == null || fb == null) return null;
            if (asMirror) for (int i = 0; i < 3; i++) fb[i * 3 + 2] = -fb[i * 3 + 2];

            // R = FB · FA^T, com os triedros guardados em COLUNAS (f[linha*3+coluna]).
            var r = new double[9];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    double s = 0;
                    for (int k = 0; k < 3; k++) s += fb[i * 3 + k] * fa[j * 3 + k];
                    r[i * 3 + j] = s;
                }
            return r;
        }

        /// <summary>Triedro ortonormal (colunas) a partir de dois vetores independentes.</summary>
        private static double[] Frame(double[] v1, double[] v2)
        {
            double[] e1 = Normalize(v1);
            if (e1 == null) return null;
            double proj = Dot(v2, e1);
            double[] e2 = Normalize(new[] { v2[0] - proj * e1[0], v2[1] - proj * e1[1], v2[2] - proj * e1[2] });
            if (e2 == null) return null;
            double[] e3 = Cross(e1, e2);
            return new[] { e1[0], e2[0], e3[0],
                           e1[1], e2[1], e3[1],
                           e1[2], e2[2], e3[2] };
        }

        // ------------------------------------------------------------------ a conferência

        /// <summary>
        /// O pior desvio depois de aplicar o encaixe: para cada ponto de A, a distância até o
        /// ponto de B mais próximo. Sai cedo no primeiro ponto que já estoura a tolerância — é o
        /// que torna barato varrer muitos candidatos.
        /// </summary>
        private static double MaxDeviation(IList<double[]> aMm, double[] ca, double[] r, double[] cb,
            PointHash hashB, double tolMm)
        {
            double worst = 0;
            for (int i = 0; i < aMm.Count; i++)
            {
                double[] p = Add(Mul(r, Sub(aMm[i], ca)), cb);
                double d = hashB.NearestDistance(p);
                if (d > tolMm) return double.MaxValue;
                if (d > worst) worst = d;
            }
            return worst;
        }

        /// <summary>
        /// Grade espacial para achar o vizinho mais próximo sem comparar todos contra todos. Sem
        /// ela o encaixe seria O(n^2) por candidato, e um candidato errado custaria o mesmo que o
        /// certo — com peça de mil vértices a busca não terminaria.
        /// </summary>
        private sealed class PointHash
        {
            private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
            private readonly IList<double[]> _pts;
            private readonly double _cell;

            public PointHash(IList<double[]> pts, double cell)
            {
                _pts = pts; _cell = cell;
                for (int i = 0; i < pts.Count; i++)
                {
                    long k = Key(pts[i]);
                    List<int> list;
                    if (!_cells.TryGetValue(k, out list)) _cells[k] = list = new List<int>();
                    list.Add(i);
                }
            }

            private long Key(double[] p) => Key(
                (int)Math.Floor(p[0] / _cell), (int)Math.Floor(p[1] / _cell), (int)Math.Floor(p[2] / _cell));

            private static long Key(int x, int y, int z) =>
                ((long)(x & 0x1FFFFF) << 42) | ((long)(y & 0x1FFFFF) << 21) | (uint)(z & 0x1FFFFF);

            /// <summary>Distância até o ponto mais próximo, varrendo as 27 células vizinhas.
            /// Devolve <c>double.MaxValue</c> quando não há nenhum ponto por perto.</summary>
            public double NearestDistance(double[] p)
            {
                int cx = (int)Math.Floor(p[0] / _cell), cy = (int)Math.Floor(p[1] / _cell), cz = (int)Math.Floor(p[2] / _cell);
                double best = double.MaxValue;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            List<int> list;
                            if (!_cells.TryGetValue(Key(cx + dx, cy + dy, cz + dz), out list)) continue;
                            foreach (int i in list)
                            {
                                double d = Distance(p, _pts[i]);
                                if (d < best) best = d;
                            }
                        }
                return best;
            }
        }

        // ------------------------------------------------------------------ utilidades

        /// <summary>det(R). Negativo = o encaixe inverte a mão, isto é, as peças são ESPELHADAS.</summary>
        public static double Determinant(double[] r) =>
              r[0] * (r[4] * r[8] - r[5] * r[7])
            - r[1] * (r[3] * r[8] - r[5] * r[6])
            + r[2] * (r[3] * r[7] - r[4] * r[6]);

        /// <summary>A matriz é de rotação mesmo (ortonormal)? Conferência de sanidade do encaixe.</summary>
        public static bool IsRotation(double[] r, double tol = 1e-6)
        {
            if (r == null || r.Length != 9) return false;
            for (int i = 0; i < 3; i++)
                for (int j = i; j < 3; j++)
                {
                    double dot = 0;
                    for (int k = 0; k < 3; k++) dot += r[i * 3 + k] * r[j * 3 + k];
                    if (Math.Abs(dot - (i == j ? 1.0 : 0.0)) > tol) return false;
                }
            return Math.Abs(Math.Abs(Determinant(r)) - 1.0) <= tol;
        }

        /// <summary>Quanto esta rotação gira, em graus — só para o log ficar legível.</summary>
        public static double AngleDeg(double[] r)
        {
            double trace = r[0] + r[4] + r[8];
            if (Determinant(r) < 0) trace = -trace;           // espelho: o ângulo da parte própria
            double cos = (trace - 1.0) / 2.0;
            if (cos > 1.0) cos = 1.0; else if (cos < -1.0) cos = -1.0;
            return Math.Acos(cos) * 180.0 / Math.PI;
        }

        /// <summary>Aplica a rotação a um vetor.</summary>
        public static double[] Mul(double[] r, double[] p) => new[]
        {
            r[0] * p[0] + r[1] * p[1] + r[2] * p[2],
            r[3] * p[0] + r[4] * p[1] + r[5] * p[2],
            r[6] * p[0] + r[7] * p[1] + r[8] * p[2]
        };

        /// <summary>Compõe duas rotações: o resultado aplica <paramref name="first"/> e DEPOIS
        /// <paramref name="second"/>.</summary>
        public static double[] MulMatrix(double[] second, double[] first)
        {
            var r = new double[9];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    double s = 0;
                    for (int k = 0; k < 3; k++) s += second[i * 3 + k] * first[k * 3 + j];
                    r[i * 3 + j] = s;
                }
            return r;
        }

        private static double[] Centroid(IList<double[]> p)
        {
            var c = new double[3];
            foreach (double[] q in p) { c[0] += q[0]; c[1] += q[1]; c[2] += q[2]; }
            for (int i = 0; i < 3; i++) c[i] /= p.Count;
            return c;
        }

        private static double[] Radii(IList<double[]> p, double[] c)
        {
            var r = new double[p.Count];
            for (int i = 0; i < p.Count; i++) r[i] = Distance(p[i], c);
            return r;
        }

        private static List<int> CandidatesByRadius(double[] radii, double target, double tol)
        {
            var list = new List<int>();
            for (int i = 0; i < radii.Length; i++) if (Math.Abs(radii[i] - target) <= tol) list.Add(i);
            return list;
        }

        private static int[] IndicesSortedByRadiusDesc(double[] radii)
        {
            var idx = new int[radii.Length];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            Array.Sort(idx, (x, y) => radii[y].CompareTo(radii[x]));
            return idx;
        }

        private static int IndexOfMax(double[] v)
        {
            int best = 0;
            for (int i = 1; i < v.Length; i++) if (v[i] > v[best]) best = i;
            return best;
        }

        private static double[] Sub(double[] a, double[] b) => new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };
        private static double[] Add(double[] a, double[] b) => new[] { a[0] + b[0], a[1] + b[1], a[2] + b[2] };
        private static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

        private static double[] Cross(double[] a, double[] b) => new[]
        {
            a[1] * b[2] - a[2] * b[1],
            a[2] * b[0] - a[0] * b[2],
            a[0] * b[1] - a[1] * b[0]
        };

        private static double Norm(double[] a) => Math.Sqrt(Dot(a, a));

        private static double[] Normalize(double[] a)
        {
            double n = Norm(a);
            return n <= 1e-12 ? null : new[] { a[0] / n, a[1] / n, a[2] / n };
        }

        private static double Distance(double[] a, double[] b)
        {
            double dx = a[0] - b[0], dy = a[1] - b[1], dz = a[2] - b[2];
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static Result Fail(string why) => new Result { Ok = false, Why = why };
    }
}
