using System;
using System.Globalization;
using System.Text;

namespace AutoEDM.Assembly
{
    /// <summary>
    /// Como os 16 doubles de <c>Occurrence.GetMatrix</c> estão arrumados. O Solid Edge não
    /// documenta isso no dump da typelib (a assinatura só diz <c>SAFEARRAY(double)</c>), e
    /// chutar errado troca a rotação pela transposta — que em cavidade INCLINADA dá um eletrodo
    /// deitado para o lado errado, sem erro nenhum aparecer. Por isso o layout é DETECTADO ao
    /// vivo (ver <see cref="OccurrenceTransform.FromMatrix"/>), não assumido.
    /// </summary>
    public enum MatrixLayout
    {
        /// <summary>Translação nos índices 12,13,14 (vetor-linha: <c>p' = p · M</c>).</summary>
        TranslationLast = 0,

        /// <summary>Translação nos índices 3,7,11 (vetor-coluna: <c>p' = M · p</c>).</summary>
        TranslationColumn = 1
    }

    /// <summary>
    /// A pose completa de uma ocorrência na montagem — rotação 3D de verdade, não só o ângulo
    /// em Z.
    ///
    /// POR QUE ISTO EXISTE. "Criar eletrodos" posicionava o eletrodo somando <c>occZmm +
    /// baseZmm</c> e girando só em Z. Com a cavidade INCLINADA (job real MD-14972, rotação
    /// Y = −90°) isso é matematicamente errado: o eixo Z local da cavidade não é paralelo ao Z
    /// da montagem, então somar as coordenadas de Z soma grandezas de eixos diferentes. Pior
    /// que errar feio, ele às vezes ACERTA por coincidência de magnitude — foi o que aconteceu
    /// em 3 dos 5 eletrodos daquele job, escondendo o problema.
    ///
    /// POR QUE MATRIZ E NÃO OS TRÊS ÂNGULOS. <c>Occurrence.GetTransform</c> devolve origem + 3
    /// ângulos, mas NÃO diz em que ordem eles se compõem (Rz·Ry·Rx? Rx·Ry·Rz? intrínseco ou
    /// extrínseco?). Errar a ordem só aparece quando dois eixos giram ao mesmo tempo — ou seja,
    /// exatamente no caso que viemos consertar. <c>GetMatrix</c> entrega a rotação já composta:
    /// não há convenção para adivinhar. Passar o resultado de volta por <c>PutMatrix</c> fecha
    /// o ciclo sem nunca converter para ângulo.
    ///
    /// Convenção interna, única e explícita: <c>p_montagem = R · p_local + t</c>, com R guardado
    /// linha-a-linha e tudo em METROS (a unidade da API COM do Solid Edge).
    /// </summary>
    public sealed class OccurrenceTransform
    {
        private readonly double[] _r;   // 9 valores, _r[linha*3 + coluna]
        private readonly double[] _t;   // 3 valores, metros

        /// <summary>Layout em que a matriz foi lida — e em que ela tem de ser escrita de volta.</summary>
        public MatrixLayout Layout { get; }

        /// <summary>Como o layout foi decidido: batendo com a origem conhecida, ou assumido.</summary>
        public string LayoutEvidence { get; }

        private OccurrenceTransform(double[] r, double[] t, MatrixLayout layout, string evidence)
        {
            _r = r; _t = t; Layout = layout; LayoutEvidence = evidence;
        }

        /// <summary>Translação (metros).</summary>
        public double TxM => _t[0];
        public double TyM => _t[1];
        public double TzM => _t[2];

        /// <summary>
        /// Um elemento da rotação. Só para teste e log — o resto do código não precisa saber
        /// como a rotação está guardada.
        /// </summary>
        public double R(int row, int col) => _r[row * 3 + col];

        /// <summary>
        /// A ocorrência está INCLINADA? Isto é: o eixo Z local ainda é paralelo ao Z da
        /// montagem? Se for, a conta antiga (somar Z, girar em Z) valia; se não, não valia.
        /// A terceira COLUNA de R é a imagem do eixo Z local no espaço da montagem.
        /// </summary>
        public bool IsTilted(double toleranceDeg = 0.5)
        {
            double zx = R(0, 2), zy = R(1, 2), zz = R(2, 2);
            double norm = Math.Sqrt(zx * zx + zy * zy + zz * zz);
            if (norm <= 1e-9) return true;                       // matriz degenerada: trate como suspeita

            // Cosseno COM SINAL, de propósito: o eixo de queima tem direção, não é só uma reta.
            // Uma ocorrência virada de cabeça para baixo tem o Z local antiparalelo ao da
            // montagem — o eletrodo desceria ao contrário — e isso é tão "inclinado" quanto
            // estar deitada. Usar |cos| aqui trataria 180° como se fosse 0°.
            double cos = zz / norm;
            if (cos > 1.0) cos = 1.0; else if (cos < -1.0) cos = -1.0;
            return Math.Acos(cos) > toleranceDeg * Math.PI / 180.0;
        }

        /// <summary>Leva um ponto do espaço LOCAL da ocorrência para o da montagem (metros).</summary>
        public void TransformPointM(double xM, double yM, double zM,
            out double outXM, out double outYM, out double outZM)
        {
            outXM = _r[0] * xM + _r[1] * yM + _r[2] * zM + _t[0];
            outYM = _r[3] * xM + _r[4] * yM + _r[5] * zM + _t[1];
            outZM = _r[6] * xM + _r[7] * yM + _r[8] * zM + _t[2];
        }

        /// <summary>
        /// A MESMA orientação, numa posição nova (metros). É esta a pose que o eletrodo recebe:
        /// herda a inclinação da cavidade — para que o eixo de queima dele seja o eixo de queima
        /// dela — mas fica na superfície de queima, não na origem da cavidade.
        /// </summary>
        public OccurrenceTransform WithTranslationM(double xM, double yM, double zM) =>
            new OccurrenceTransform((double[])_r.Clone(), new[] { xM, yM, zM }, Layout, LayoutEvidence);

        /// <summary>
        /// Interpreta os 16 doubles do <c>GetMatrix</c>.
        ///
        /// <paramref name="knownOriginM"/> é a origem lida por <c>GetTransform</c> — que já é
        /// caminho validado no projeto. Ela serve de GABARITO: das duas arrumações possíveis,
        /// vale a que põe a translação onde a origem realmente está. Sem gabarito (null), assume
        /// <see cref="MatrixLayout.TranslationLast"/> e DIZ que assumiu, para o log não fingir
        /// certeza que não tem.
        /// </summary>
        public static OccurrenceTransform FromMatrix(double[] m, double[] knownOriginM = null)
        {
            if (m == null || m.Length < 16)
                throw new ArgumentException("A matriz da ocorrência precisa de 16 valores.", nameof(m));

            var last = new[] { m[12], m[13], m[14] };
            var column = new[] { m[3], m[7], m[11] };

            MatrixLayout layout;
            string evidence;
            if (knownOriginM != null && knownOriginM.Length >= 3)
            {
                double dLast = Distance(last, knownOriginM);
                double dColumn = Distance(column, knownOriginM);
                if (dLast <= dColumn)
                {
                    layout = MatrixLayout.TranslationLast;
                    evidence = $"translação em [12,13,14] bate com a origem do GetTransform (erro {dLast:0.000000} m contra {dColumn:0.000000} m da outra arrumação)";
                }
                else
                {
                    layout = MatrixLayout.TranslationColumn;
                    evidence = $"translação em [3,7,11] bate com a origem do GetTransform (erro {dColumn:0.000000} m contra {dLast:0.000000} m da outra arrumação)";
                }
            }
            else
            {
                layout = MatrixLayout.TranslationLast;
                evidence = "SEM gabarito de origem — arrumação [12,13,14] ASSUMIDA, não confirmada";
            }

            double[] r;
            double[] t;
            if (layout == MatrixLayout.TranslationLast)
            {
                // Vetor-linha (p' = p · M): a linha i de M é a imagem do eixo i local, então a
                // rotação na nossa convenção (p' = R · p) é a TRANSPOSTA do bloco 3x3.
                r = new[] { m[0], m[4], m[8],
                            m[1], m[5], m[9],
                            m[2], m[6], m[10] };
                t = last;
            }
            else
            {
                // Vetor-coluna (p' = M · p): o bloco 3x3 já está na nossa convenção.
                r = new[] { m[0], m[1], m[2],
                            m[4], m[5], m[6],
                            m[8], m[9], m[10] };
                t = column;
            }
            return new OccurrenceTransform(r, t, layout, evidence);
        }

        /// <summary>
        /// De volta aos 16 doubles, NO MESMO layout em que foi lida — escrever na arrumação
        /// errada seria transpor a rotação silenciosamente.
        /// </summary>
        public double[] ToMatrix()
        {
            var m = new double[16];
            if (Layout == MatrixLayout.TranslationLast)
            {
                m[0] = _r[0]; m[4] = _r[1]; m[8] = _r[2];
                m[1] = _r[3]; m[5] = _r[4]; m[9] = _r[5];
                m[2] = _r[6]; m[6] = _r[7]; m[10] = _r[8];
                m[12] = _t[0]; m[13] = _t[1]; m[14] = _t[2];
                m[15] = 1.0;
            }
            else
            {
                m[0] = _r[0]; m[1] = _r[1]; m[2] = _r[2];
                m[4] = _r[3]; m[5] = _r[4]; m[6] = _r[5];
                m[8] = _r[6]; m[9] = _r[7]; m[10] = _r[8];
                m[3] = _t[0]; m[7] = _t[1]; m[11] = _t[2];
                m[15] = 1.0;
            }
            return m;
        }

        /// <summary>Uma linha de log legível — o eixo Z local visto da montagem é o que importa
        /// para EDM (é a direção em que o eletrodo desce).</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "origem ({0:0.0}, {1:0.0}, {2:0.0}) mm; eixo Z local -> ({3:0.000}, {4:0.000}, {5:0.000})",
                _t[0] * 1000.0, _t[1] * 1000.0, _t[2] * 1000.0, R(0, 2), R(1, 2), R(2, 2));
            return sb.ToString();
        }

        private static double Distance(double[] a, double[] b)
        {
            double dx = a[0] - b[0], dy = a[1] - b[1], dz = a[2] - b[2];
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
