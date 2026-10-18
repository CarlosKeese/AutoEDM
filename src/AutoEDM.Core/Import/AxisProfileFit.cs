using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AutoEDM.Import
{
    /// <summary>Uma aresta circular: centro, direção do eixo e raio. Tudo em mm, no espaço
    /// LOCAL da peça.</summary>
    public sealed class Ring
    {
        public double[] CenterMm { get; set; }

        /// <summary>Direção do eixo, unitária. O SINAL não significa nada — ele vem da
        /// orientação da aresta, e a mesma peça traz Ø30 com eixo (0,0,−1) e Ø30 com (0,0,+1).
        /// Quem compara tem de canonizar o sinal.</summary>
        public double[] AxisDir { get; set; }

        public double RadiusMm { get; set; }
    }

    /// <summary>
    /// A prova do CORPO INTEIRO para um candidato de encaixe, usando os anéis (arestas
    /// circulares) da peça.
    ///
    /// POR QUE EXISTE. Os vértices são um subconjunto do corpo: num parafuso de sextavado
    /// interno o anel de vértices do topo e o do fundo são iguais, então virar a peça 180° faz a
    /// NUVEM coincidir mesmo que o parafuso não — ele tem haste só para um lado. Medido num
    /// projeto importado real (138 peças, 2026-10-18): dezenas encaixaram contra SI MESMAS com
    /// giro de 180°, 120° ou 60°, e o sintoma na tela foi "alguns parafusos ficaram invertidos".
    ///
    /// Os anéis consertam isso porque estão espalhados pela peça INTEIRA e carregam raio — a
    /// cabeça de um parafuso tem anéis de raio grande numa ponta só, então o movimento invertido
    /// manda a cabeça para onde está a ponta da haste e não acha anel que case. Não é prova
    /// completa (uma peça sem aresta circular nenhuma não tem anel), e por isso ela se SOMA à
    /// preferência pelo menor giro, em vez de substituí-la.
    /// </summary>
    public static class RingEvidence
    {
        /// <summary>
        /// O movimento (<paramref name="r"/>, <paramref name="tMm"/>) leva os anéis de
        /// <paramref name="from"/> sobre os de <paramref name="to"/>? Exige par para TODO anel:
        /// um anel sem correspondente já reprova o movimento.
        /// </summary>
        public static bool Agree(IList<Ring> from, IList<Ring> to, double[] r, double[] tMm, double tolMm)
        {
            if (from == null || to == null || from.Count == 0 || to.Count == 0) return true; // sem anel, sem prova
            if (from.Count != to.Count) return false;

            foreach (Ring g in from)
            {
                double[] c = Apply(r, tMm, g.CenterMm);
                double[] ax = Rotate(r, g.AxisDir);
                bool found = false;

                foreach (Ring h in to)
                {
                    if (Math.Abs(h.RadiusMm - g.RadiusMm) > tolMm) continue;
                    if (Distance(c, h.CenterMm) > tolMm) continue;

                    // Eixo paralelo, SEM olhar o sinal: o sinal vem da orientação da aresta e
                    // não significa nada geometricamente.
                    double dot = Math.Abs(Dot(Unit(ax), Unit(h.AxisDir)));
                    if (dot < 0.999) continue;

                    found = true;
                    break;
                }
                if (!found) return false;
            }
            return true;
        }

        private static double[] Apply(double[] r, double[] t, double[] p)
        {
            double[] q = Rotate(r, p);
            return new[] { q[0] + t[0], q[1] + t[1], q[2] + t[2] };
        }

        private static double[] Rotate(double[] r, double[] p) => new[]
        {
            r[0] * p[0] + r[1] * p[1] + r[2] * p[2],
            r[3] * p[0] + r[4] * p[1] + r[5] * p[2],
            r[6] * p[0] + r[7] * p[1] + r[8] * p[2]
        };

        private static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

        private static double[] Unit(double[] a)
        {
            double n = Math.Sqrt(Dot(a, a));
            return n <= 1e-12 ? new double[] { 0, 0, 0 } : new[] { a[0] / n, a[1] / n, a[2] / n };
        }

        private static double Distance(double[] a, double[] b)
        {
            double dx = a[0] - b[0], dy = a[1] - b[1], dz = a[2] - b[2];
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }

    /// <summary>
    /// O encaixe de peças TORNEADAS — e a razão de existir veio do primeiro run real, não de
    /// um projeto no papel.
    ///
    /// O QUE O RUN MOSTROU. Numa base de molde Polimold de 45 ocorrências, a bucha extratora, a
    /// bucha guia, o pino de retorno e a coluna têm **ZERO vértices**: numa peça torneada toda
    /// aresta é um círculo, e círculo não tem canto. O <see cref="RigidFit"/> exige 3 vértices
    /// para fixar uma rotação, então recusava exatamente as peças MAIS repetidas de um molde —
    /// buchas, pinos e colunas — e a ferramenta ficava sem resposta onde ela mais vale.
    ///
    /// POR QUE ESTE CAMINHO É MAIS SIMPLES, E NÃO MAIS COMPLICADO. A ausência de vértice não é
    /// um azar: é a assinatura de um SÓLIDO DE REVOLUÇÃO. Se todas as arestas são círculos em
    /// torno do mesmo eixo, toda face é limitada por esses círculos e o corpo é de revolução.
    /// Aí o problema encolhe: a peça não tem orientação angular, só um EIXO e um PERFIL. Duas
    /// delas são a mesma peça quando os perfis (posição axial, raio) coincidem; e o movimento
    /// que leva uma na outra é "alinhe os eixos" — o giro em torno do eixo é livre porque
    /// qualquer valor dele é igualmente correto.
    ///
    /// Por isso aqui NÃO se procura correspondência de pontos: ela não existe nem é necessária.
    ///
    /// ESPELHO não é questão neste caminho: um sólido de revolução é sempre aquiral (o espelho
    /// dele coincide consigo mesmo por uma rotação), então não há par esquerda/direita a
    /// confundir — ao contrário do caso geral, onde essa distinção é o risco principal.
    ///
    /// API confirmada AO VIVO (SE 2026 226.00.08.04, base de molde real, 2026-10-18):
    ///   Edge.Geometry -> Circle  (Type = igCircle = 167551105)
    ///   Circle.Radius                 -> double (m)            dispid 350
    ///   Circle.GetCenterPoint([in,out] SAFEARRAY(double))      dispid 347
    ///   Circle.GetAxisVector([in,out] SAFEARRAY(double))       dispid 346
    /// As três responderam em 21 de 21 arestas das duas buchas e das duas colunas.
    /// </summary>
    public static class AxisProfileFit
    {
        /// <summary>O perfil de um sólido de revolução: o eixo e a lista de anéis reduzida a
        /// (posição axial, raio).</summary>
        public sealed class Profile
        {
            public bool Ok;
            public string Why;

            /// <summary>Direção do eixo, canonizada (primeira componente não nula positiva).</summary>
            public double[] AxisDir;

            /// <summary>Um ponto do eixo: o centroide dos centros dos anéis (mm).</summary>
            public double[] OriginMm;

            /// <summary>Um par por anel: [0] = posição ao longo do eixo a partir de
            /// <see cref="OriginMm"/>, [1] = raio. Ordenado.</summary>
            public double[][] Rings;

            public int Count => Rings == null ? 0 : Rings.Length;

            public string Describe()
            {
                if (!Ok) return "não é de revolução: " + Why;
                var sb = new StringBuilder();
                sb.AppendFormat(CultureInfo.InvariantCulture, "eixo ({0:0.###}, {1:0.###}, {2:0.###}), {3} anéis",
                    AxisDir[0], AxisDir[1], AxisDir[2], Count);
                return sb.ToString();
            }
        }

        public sealed class Result
        {
            public bool Ok;
            public double[] R;
            public double[] TMm;
            public double MaxDeviationMm;

            /// <summary>O encaixe exigiu virar a peça de ponta-cabeça (eixo invertido). Normal
            /// numa bucha montada ao contrário — e é informação, não defeito.</summary>
            public bool Flipped;

            public string Why;
        }

        /// <summary>Tolerância de paralelismo entre eixos: |cos| acima disto é "mesmo eixo".</summary>
        private const double AxisParallelCos = 0.9999;   // ~0,8°

        /// <summary>
        /// Reduz os anéis a um perfil, ou explica por que a peça não é de revolução. Recusa (sem
        /// exceção) quando os eixos não são paralelos ou os centros não são colineares — e essa
        /// recusa é importante: um corpo com dois mancais de eixos cruzados tem só círculos e
        /// NÃO é de revolução, então aceitar tudo que só tem círculos seria errado.
        /// </summary>
        public static Profile BuildProfile(IList<Ring> rings, double tolMm)
        {
            if (rings == null || rings.Count < 2)
                return new Profile { Ok = false, Why = $"só {(rings == null ? 0 : rings.Count)} anel(éis) — são precisos 2." };

            // 1. Todos os eixos paralelos? O sinal é canonizado antes de comparar, porque a
            //    orientação da aresta o inverte sem significar nada geometricamente.
            double[] axis = Canonical(rings[0].AxisDir);
            if (axis == null) return new Profile { Ok = false, Why = "eixo do primeiro anel é degenerado." };

            foreach (Ring r in rings)
            {
                double[] a = Canonical(r.AxisDir);
                if (a == null) return new Profile { Ok = false, Why = "um anel tem eixo degenerado." };
                if (Math.Abs(Dot(a, axis)) < AxisParallelCos)
                    return new Profile { Ok = false, Why = "os anéis não compartilham um eixo (há círculos em eixos diferentes)." };
            }

            // 2. Centros colineares com esse eixo? A componente PERPENDICULAR ao eixo tem de ser
            //    nula; se não for, há círculos fora do eixo (um furo lateral, por exemplo) e a
            //    peça não é de revolução.
            double[] origin = Centroid(rings);
            foreach (Ring r in rings)
            {
                double[] d = Sub(r.CenterMm, origin);
                double along = Dot(d, axis);
                double[] perp = { d[0] - along * axis[0], d[1] - along * axis[1], d[2] - along * axis[2] };
                if (Norm(perp) > Math.Max(tolMm, 1e-6))
                    return new Profile { Ok = false, Why = $"um anel está {Norm(perp):0.###} mm fora do eixo comum." };
            }

            // 3. O perfil: (posição axial, raio), ordenado. É tudo o que distingue duas peças de
            //    revolução — e é invariante de rotação por construção.
            var list = new List<double[]>();
            foreach (Ring r in rings)
                list.Add(new[] { Dot(Sub(r.CenterMm, origin), axis), r.RadiusMm });
            list.Sort(CompareRing);

            return new Profile { Ok = true, AxisDir = axis, OriginMm = origin, Rings = list.ToArray() };
        }

        /// <summary>
        /// O movimento rígido que leva a peça de <paramref name="a"/> sobre a de
        /// <paramref name="b"/>, ou a recusa de que são peças diferentes.
        ///
        /// Tenta o perfil direto e o INVERTIDO (peça de ponta-cabeça). O giro em torno do eixo
        /// não é escolhido nem procurado: qualquer valor serve, então sai o mais curto.
        /// </summary>
        public static Result Fit(Profile a, Profile b, double tolMm)
        {
            if (a == null || b == null || !a.Ok || !b.Ok)
                return new Result { Ok = false, Why = "um dos perfis não é de revolução." };
            if (a.Count != b.Count)
                return new Result { Ok = false, Why = $"contagem de anéis diferente ({a.Count} contra {b.Count})." };

            foreach (bool flip in new[] { false, true })
            {
                double dev;
                if (!ProfilesMatch(a.Rings, b.Rings, flip, tolMm, out dev)) continue;

                // Alinha o eixo de A com o de B — invertido quando o encaixe pede virar a peça.
                double[] target = flip ? Negate(b.AxisDir) : b.AxisDir;
                double[] r = RotationBetween(a.AxisDir, target);
                if (r == null) continue;

                // t põe a origem do perfil de A sobre a de B: t = originB − R·originA.
                double[] t = Sub(b.OriginMm, Mul(r, a.OriginMm));

                return new Result
                {
                    Ok = true,
                    R = r,
                    TMm = t,
                    MaxDeviationMm = dev,
                    Flipped = flip,
                    Why = $"perfil de revolução conferido em {a.Count} anéis, pior desvio {dev:0.0000} mm" +
                          (flip ? " (peça invertida no eixo)" : "")
                };
            }

            return new Result
            {
                Ok = false,
                Why = "os perfis de revolução não coincidem (nem invertendo a peça) — são peças diferentes."
            };
        }

        /// <summary>Os dois perfis coincidem? Com <paramref name="flip"/>, compara o de B com a
        /// posição axial espelhada (e reordenada).</summary>
        private static bool ProfilesMatch(double[][] a, double[][] b, bool flip, double tolMm, out double maxDev)
        {
            maxDev = 0;
            double[][] bb = b;
            if (flip)
            {
                bb = new double[b.Length][];
                for (int i = 0; i < b.Length; i++) bb[i] = new[] { -b[i][0], b[i][1] };
                Array.Sort(bb, CompareRing);
            }

            for (int i = 0; i < a.Length; i++)
            {
                double dt = Math.Abs(a[i][0] - bb[i][0]);
                double dr = Math.Abs(a[i][1] - bb[i][1]);
                double d = Math.Max(dt, dr);
                if (d > tolMm) return false;
                if (d > maxDev) maxDev = d;
            }
            return true;
        }

        /// <summary>
        /// A rotação mais curta que leva o vetor unitário <paramref name="from"/> em
        /// <paramref name="to"/> (Rodrigues). Trata os dois casos degenerados: vetores já
        /// iguais (identidade) e antiparalelos (meia-volta em torno de qualquer perpendicular —
        /// e aí "qualquer" é literal, porque a peça é de revolução).
        /// </summary>
        public static double[] RotationBetween(double[] from, double[] to)
        {
            double[] f = Normalize(from), t = Normalize(to);
            if (f == null || t == null) return null;

            double c = Dot(f, t);
            if (c > 1.0) c = 1.0; else if (c < -1.0) c = -1.0;

            if (c > 0.999999) return Identity();

            if (c < -0.999999)
            {
                double[] perp = AnyPerpendicular(f);
                return AxisAngle(perp, Math.PI);
            }

            double[] v = Cross(f, t);
            double s = Norm(v);
            return AxisAngle(new[] { v[0] / s, v[1] / s, v[2] / s }, Math.Atan2(s, c));
        }

        private static double[] AxisAngle(double[] k, double angle)
        {
            double c = Math.Cos(angle), s = Math.Sin(angle), t = 1 - c;
            double x = k[0], y = k[1], z = k[2];
            return new[]
            {
                t * x * x + c,      t * x * y - s * z,  t * x * z + s * y,
                t * x * y + s * z,  t * y * y + c,      t * y * z - s * x,
                t * x * z - s * y,  t * y * z + s * x,  t * z * z + c
            };
        }

        private static double[] AnyPerpendicular(double[] v)
        {
            // Cruza com o eixo base MENOS alinhado a v — garante produto vetorial robusto.
            double ax = Math.Abs(v[0]), ay = Math.Abs(v[1]), az = Math.Abs(v[2]);
            double[] other = ax <= ay && ax <= az ? new[] { 1.0, 0, 0 }
                           : ay <= az ? new[] { 0, 1.0, 0 }
                           : new[] { 0, 0, 1.0 };
            return Normalize(Cross(v, other));
        }

        /// <summary>Canoniza o sinal: a primeira componente significativa fica positiva. Sem
        /// isto, o mesmo eixo lido de duas arestas de orientações opostas pareceria dois eixos.</summary>
        private static double[] Canonical(double[] v)
        {
            double[] u = Normalize(v);
            if (u == null) return null;
            for (int i = 0; i < 3; i++)
            {
                if (Math.Abs(u[i]) < 1e-9) continue;
                return u[i] < 0 ? Negate(u) : u;
            }
            return null;
        }

        private static int CompareRing(double[] x, double[] y)
        {
            int byT = x[0].CompareTo(y[0]);
            return byT != 0 ? byT : x[1].CompareTo(y[1]);
        }

        private static double[] Centroid(IList<Ring> rings)
        {
            var c = new double[3];
            foreach (Ring r in rings) { c[0] += r.CenterMm[0]; c[1] += r.CenterMm[1]; c[2] += r.CenterMm[2]; }
            for (int i = 0; i < 3; i++) c[i] /= rings.Count;
            return c;
        }

        private static double[] Identity() => new double[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };

        private static double[] Mul(double[] r, double[] p) => new[]
        {
            r[0] * p[0] + r[1] * p[1] + r[2] * p[2],
            r[3] * p[0] + r[4] * p[1] + r[5] * p[2],
            r[6] * p[0] + r[7] * p[1] + r[8] * p[2]
        };

        private static double[] Sub(double[] a, double[] b) => new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };
        private static double[] Negate(double[] a) => new[] { -a[0], -a[1], -a[2] };
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
            if (a == null || a.Length < 3) return null;
            double n = Norm(a);
            return n <= 1e-12 ? null : new[] { a[0] / n, a[1] / n, a[2] / n };
        }
    }
}
