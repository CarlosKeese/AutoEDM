using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AutoEDM.Import
{
    /// <summary>
    /// A "impressão digital" de um corpo sólido: só grandezas que NÃO mudam quando a peça é
    /// girada ou transladada. Serve para peneirar candidatos a peça repetida sem pagar o preço
    /// de ler vértice por vértice de cada corpo da montagem.
    ///
    /// REGRA DE PROJETO: tudo aqui é invariante de rotação. A caixa envolvente, por exemplo,
    /// ficou DE FORA da chave de agrupamento de propósito — a caixa alinhada aos eixos muda
    /// quando a peça gira num ângulo qualquer, e usá-la como chave faria o tradutor perder
    /// exatamente as repetições giradas, que são o caso comum de projeto importado. Ela é
    /// guardada só para o relatório ficar legível (<see cref="BoxMm"/>).
    ///
    /// A impressão digital NÃO É PROVA. Ela junta candidatos; quem decide é
    /// <see cref="RigidFit"/>, medindo o encaixe. Um postiço e o espelho dele têm volume, área
    /// e topologia idênticos, e portanto a MESMA impressão digital.
    ///
    /// Grandezas em mm / mm² / mm³.
    /// </summary>
    public sealed class GeometrySignature
    {
        /// <summary>Volume do corpo (mm³). O discriminante mais forte e mais barato que existe:
        /// uma leitura de propriedade por corpo.</summary>
        public double VolumeMm3 { get; set; }

        /// <summary>Soma das áreas das faces (mm²).</summary>
        public double AreaMm2 { get; set; }

        public int FaceCount { get; set; }
        public int EdgeCount { get; set; }
        public int VertexCount { get; set; }

        /// <summary>Áreas das faces, ORDENADAS (mm²). Invariante de rotação e muito discriminante:
        /// duas peças de volume e topologia iguais mas geometria diferente separam aqui.</summary>
        public double[] SortedFaceAreasMm2 { get; set; }

        /// <summary>Raios das faces cilíndricas, ORDENADOS (mm). Num molde é o que distingue
        /// buchas e pinos de medidas vizinhas.</summary>
        public double[] SortedCylinderRadiiMm { get; set; }

        /// <summary>Caixa envolvente (mm), lados ORDENADOS — só para o relatório, nunca para a
        /// chave de agrupamento (ver a nota da classe).</summary>
        public double[] BoxMm { get; set; }

        /// <summary>A leitura do corpo falhou em parte? Um corpo lido pela metade não pode entrar
        /// em grupo nenhum — senão a peneira acerta por acidente.</summary>
        public bool Partial { get; set; }

        public string ReadError { get; set; }

        /// <summary>
        /// Chave de PENEIRA grossa: SÓ a topologia — contagem de faces, arestas e vértices.
        /// Corpos com a mesma chave são candidatos; corpos com chaves diferentes não precisam
        /// nem ser comparados.
        ///
        /// POR QUE O VOLUME SAIU DA CHAVE (2026-10-18, achado em projeto importado real). A
        /// versão anterior punha 6 dígitos significativos de volume aqui, e isso ERRAVA de um
        /// jeito silencioso: `Corpo do Desenho_279` e `Corpo do Desenho_356`, a mesma peça com
        /// mesma caixa, mesma topologia e mesmos diâmetros, tinham volumes de 99 622,7357 e
        /// 99 623,2167 mm³ — diferença de 0,48 mm³, ou 4,8 PPM, puro arredondamento do tradutor
        /// de STEP. Os 6 dígitos caíam em lados opostos do último passo, as duas foram para
        /// baldes DIFERENTES e nunca chegaram a ser comparadas. O crivo fino, que tolera 191 mm³
        /// nessa peça, nunca era alcançado.
        ///
        /// A lição: uma chave de agrupamento não pode ser feita de grandeza CONTÍNUA. Por mais
        /// fina que seja a tolerância, sempre existem dois valores quase iguais em lados opostos
        /// de uma fronteira, e para a chave eles são tão diferentes quanto quaisquer outros —
        /// o erro não aparece como aproximação ruim, aparece como par que nunca foi testado.
        /// Contagem de topologia é INTEIRA: não tem fronteira para cair em cima.
        ///
        /// O volume continua sendo o discriminante mais forte — só que aplicado em
        /// <see cref="Matches"/>, COM TOLERÂNCIA, dentro do balde.
        /// </summary>
        public string CoarseKey() =>
            string.Format(CultureInfo.InvariantCulture, "f{0}|e{1}|x{2}", FaceCount, EdgeCount, VertexCount);

        /// <summary>
        /// Esta impressão digital bate com a outra, dentro da tolerância? É o segundo crivo:
        /// roda só dentro de um grupo da <see cref="CoarseKey"/> e compara as listas ordenadas,
        /// que são caras de ler e fortes de discriminar.
        /// </summary>
        public bool Matches(GeometrySignature other, double tolMm)
        {
            if (other == null || Partial || other.Partial) return false;
            if (FaceCount != other.FaceCount || EdgeCount != other.EdgeCount || VertexCount != other.VertexCount)
                return false;

            // Tolerâncias derivadas da linear: uma área tolera ~tol x a dimensão típica da peça,
            // um volume ~tol x a área típica. Derivar em vez de fixar evita tanto reprovar placa
            // grande por arredondamento quanto aprovar pino pequeno por folga.
            double typical = Math.Sqrt(Math.Max(AreaMm2, 1e-9));
            if (!Close(VolumeMm3, other.VolumeMm3, tolMm * Math.Max(AreaMm2, 1e-9))) return false;
            if (!Close(AreaMm2, other.AreaMm2, tolMm * Math.Max(typical, 1e-9) * 4.0)) return false;
            if (!CloseList(SortedFaceAreasMm2, other.SortedFaceAreasMm2, tolMm * Math.Max(typical, 1e-9))) return false;
            if (!CloseList(SortedCylinderRadiiMm, other.SortedCylinderRadiiMm, tolMm)) return false;
            return true;
        }

        private static bool Close(double a, double b, double tol) => Math.Abs(a - b) <= Math.Max(tol, 1e-9);

        private static bool CloseList(double[] a, double[] b, double tol)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (!Close(a[i], b[i], tol)) return false;
            return true;
        }

        /// <summary>Uma linha legível para o relatório e o log.</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "{0:0.###} mm3, {1} face(s), {2} aresta(s), {3} vértice(s)",
                VolumeMm3, FaceCount, EdgeCount, VertexCount);
            if (BoxMm != null && BoxMm.Length == 3)
                sb.AppendFormat(CultureInfo.InvariantCulture, ", caixa {0:0.##} x {1:0.##} x {2:0.##} mm",
                    BoxMm[0], BoxMm[1], BoxMm[2]);
            if (Partial) sb.Append(" [LEITURA INCOMPLETA]");
            return sb.ToString();
        }

        /// <summary>Lado maior da caixa (mm) — serve para escolher a peça-mestra e ordenar o
        /// relatório da maior para a menor.</summary>
        public double LargestSideMm()
        {
            if (BoxMm == null || BoxMm.Length != 3) return 0;
            return Math.Max(BoxMm[0], Math.Max(BoxMm[1], BoxMm[2]));
        }

        /// <summary>Ordena uma lista de valores, tolerando nulo. Usado por quem monta a
        /// impressão digital a partir do COM.</summary>
        public static double[] Sorted(List<double> values)
        {
            if (values == null) return new double[0];
            double[] a = values.ToArray();
            Array.Sort(a);
            return a;
        }
    }
}
