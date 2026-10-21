using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AutoEDM.Sealing
{
    /// <summary>Gravidade de um apontamento do cálculo do alojamento.</summary>
    public enum GrooveIssueLevel
    {
        /// <summary>Informação — vale registrar, não impede nada.</summary>
        Note,
        /// <summary>Fora da faixa recomendada, mas usinável — decisão do ferramenteiro.</summary>
        Warning,
        /// <summary>Fora do que a norma admite: não deve virar canal sem revisão.</summary>
        Error
    }

    public sealed class GrooveIssue
    {
        public GrooveIssueLevel Level { get; set; }
        public string Message { get; set; }
        public override string ToString() =>
            (Level == GrooveIssueLevel.Error ? "ERRO: " : Level == GrooveIssueLevel.Warning ? "ATENÇÃO: " : "") + Message;
    }

    /// <summary>
    /// O alojamento calculado, com TODAS as contas que levaram até ele. É o que a janela mostra
    /// e o que vai para o log: um canal de O'ring que ninguém consegue conferir é um canal em
    /// que ninguém confia.
    ///
    /// Todas as cotas em MILÍMETROS.
    /// </summary>
    public sealed class ORingGrooveSpec
    {
        public ORingSize Ring { get; set; }
        public GrooveKind Kind { get; set; }
        public SealMotion Motion { get; set; }
        public Elastomer Elastomer { get; set; }

        /// <summary>Lado da pressão — só pesa no canal de FACE (decide a parede de apoio).</summary>
        public FacePressure Pressure { get; set; }

        /// <summary>Diâmetro MEDIDO na peça que serviu de referência: Ø do eixo, Ø do furo, ou
        /// o Ø médio pedido para o canal na face plana.</summary>
        public double SealingDiameter { get; set; }

        /// <summary>Profundidade CORTADA na peça — RADIAL no canal de eixo/furo, AXIAL no de face.</summary>
        public double Depth { get; set; }

        /// <summary>
        /// Altura do ALOJAMENTO (L): do fundo do canal até a superfície do conjugado. É ela que
        /// esmaga o anel — não a profundidade cortada. As duas só coincidem quando o conjugado
        /// está no mesmo Ø nominal (canal radial) ou encostado (canal de face); com folga, o
        /// canal fica mais raso para o esmagamento continuar o mesmo.
        /// </summary>
        public double GlandDepth { get; set; }

        /// <summary>Tabela ou desenhado pelo anel (ver <see cref="GrooveSizing"/>).</summary>
        public GrooveSizing Sizing { get; set; }

        /// <summary>Ø nominal do conjugado usado na conta (furo do canal de eixo, eixo do canal
        /// de furo); 0 no canal de face.</summary>
        public double MatingDiameter { get; set; }

        /// <summary>De onde veio o Ø conjugado — null quando foi suposto igual ao da peça.</summary>
        public string MatingSource { get; set; }

        /// <summary>Canal de face: vão entre as faces montadas (mm).</summary>
        public double FaceGap { get; set; }

        /// <summary>Jogo DIAMETRAL nominal entre a peça e o conjugado (mm) — só canal radial.</summary>
        public double NominalClearance { get; set; }

        public double PressureBar { get; set; }

        /// <summary>Extremos com as tolerâncias; null quando o ajuste não foi informado/entendido.</summary>
        public GrooveWorstCase WorstCase { get; set; }

        /// <summary>Largura do canal — AXIAL no canal de eixo/furo, RADIAL no de face.</summary>
        public double Width { get; set; }

        /// <summary>Diâmetro do FUNDO do canal (só canal radial: eixo ou furo).</summary>
        public double GrooveBottomDiameter { get; set; }

        /// <summary>Diâmetros interno/externo do canal anular (só canal de FACE).</summary>
        public double GrooveInnerDiameter { get; set; }
        public double GrooveOuterDiameter { get; set; }

        public double BottomRadius { get; set; }
        public double EdgeBreak { get; set; }

        /// <summary>De onde saíram largura e profundidade: a tabela de alojamento do catálogo,
        /// ou o cálculo por esmagamento/preenchimento. Vai no relatório porque muda o peso da
        /// cota — tabela é norma, cálculo é critério de projeto.</summary>
        public string Source { get; set; } = "cálculo (esmagamento/preenchimento)";

        /// <summary>Faixa de profundidade publicada (mm); 0 quando a cota veio de cálculo.</summary>
        public double DepthMin { get; set; }
        public double DepthMax { get; set; }

        /// <summary>Faixa de largura publicada (mm); 0 quando a cota veio de cálculo.</summary>
        public double WidthMin { get; set; }
        public double WidthMax { get; set; }

        /// <summary>Jogo diametral admissível entre o alojamento e a superfície de contato
        /// (mm) — não é cota do canal, é a folga de MONTAGEM que evita extrusão do anel.</summary>
        public double ClearanceMin { get; set; }
        public double ClearanceMax { get; set; }

        /// <summary>Excentricidade admissível entre o alojamento e a superfície oposta (mm).</summary>
        public double Eccentricity { get; set; }

        /// <summary>Esmagamento efetivo (fração), já descontando o afinamento do cordão pelo
        /// estiramento — é o número que realmente veda.</summary>
        public double Squeeze { get; set; }

        /// <summary>Estiramento do d1 (fração). Positivo estica, negativo sobra. Em canal de
        /// FURO — e em canal de FACE com pressão INTERNA — este campo guarda a COMPRESSÃO do
        /// diâmetro externo (positivo = comprimido).</summary>
        public double Stretch { get; set; }

        /// <summary>O <see cref="Stretch"/> é compressão do Ø externo (e não estiramento do d1)?</summary>
        public bool StretchIsOuterCompression =>
            Kind == GrooveKind.RadialInternal || (Kind == GrooveKind.AxialFace && Pressure == FacePressure.Internal);

        /// <summary>Fração da área do canal ocupada pela borracha.</summary>
        public double Fill { get; set; }

        public List<GrooveIssue> Issues { get; set; } = new List<GrooveIssue>();

        /// <summary>Nenhum apontamento de nível ERRO — o canal pode ser cortado.</summary>
        public bool IsWithinStandard => !Issues.Any(i => i.Level == GrooveIssueLevel.Error);

        public bool HasWarnings => Issues.Any(i => i.Level != GrooveIssueLevel.Note);

        private static string Pct(double v) => (v * 100.0).ToString("0.0", CultureInfo.CurrentCulture) + "%";
        private static string Mm(double v) => v.ToString("0.000", CultureInfo.CurrentCulture);

        /// <summary>Relatório em texto — a mesma coisa vai para a janela e para o log.</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            string motion = Motion == SealMotion.Static ? "estática"
                          : Motion == SealMotion.Reciprocating ? "recíproca" : "rotativa";
            string kind = Kind == GrooveKind.AxialFace ? "face plana (anular)"
                        : Kind == GrooveKind.RadialExternal ? "eixo (canal externo)" : "furo (canal interno)";

            sb.AppendLine($"Anel: {Ring.Designation}  (d1 {Mm(Ring.InnerDiameter)} × d2 {Mm(Ring.CrossSection)})");
            if (!Ring.Verified) sb.AppendLine("  ⚠ medida do anel AINDA NÃO CONFERIDA no catálogo do fornecedor.");
            sb.AppendLine($"Vedação: {motion} em {kind}, elastômero {(Elastomer == Elastomer.Nbr ? "NBR" : "FKM (Viton)")}");
            sb.AppendLine($"Referência medida na peça: Ø {Mm(SealingDiameter)}");
            if (Kind != GrooveKind.AxialFace)
                sb.AppendLine($"Conjugado ({(Kind == GrooveKind.RadialExternal ? "furo" : "eixo")}): Ø {Mm(MatingDiameter)}" +
                              (string.IsNullOrEmpty(MatingSource) ? " — suposto no mesmo Ø nominal" : " — " + MatingSource) +
                              $"   jogo diametral nominal {Mm(NominalClearance)}");
            else if (FaceGap > 0)
                sb.AppendLine($"Vão entre as faces montadas: {Mm(FaceGap)}");
            sb.AppendLine($"Pressão de trabalho: {PressureBar.ToString("0.#", CultureInfo.CurrentCulture)} bar");
            sb.AppendLine();
            sb.AppendLine($"CANAL:  largura {Mm(Width)}   profundidade {Mm(Depth)}" +
                          (System.Math.Abs(GlandDepth - Depth) > 0.0005 ? $"   (altura do alojamento até o conjugado {Mm(GlandDepth)})" : ""));
            if (DepthMax > 0)
                sb.AppendLine($"        (faixa publicada: largura {Mm(WidthMin)}–{Mm(WidthMax)}, " +
                              $"profundidade {Mm(DepthMin)}–{Mm(DepthMax)})");
            if (Kind == GrooveKind.AxialFace)
            {
                sb.AppendLine($"        Ø interno {Mm(GrooveInnerDiameter)}   Ø externo {Mm(GrooveOuterDiameter)}");
                sb.AppendLine(Pressure == FacePressure.Internal
                    ? "        pressão INTERNA: o anel apoia pelo Ø EXTERNO na parede externa do canal"
                    : "        pressão EXTERNA: o anel apoia pelo Ø INTERNO na parede interna do canal");
            }
            else
                sb.AppendLine($"        Ø do fundo {Mm(GrooveBottomDiameter)}");
            sb.AppendLine($"        raio do fundo {Mm(BottomRadius)}   quebra de canto {Mm(EdgeBreak)}");
            if (ClearanceMax > 0)
                sb.AppendLine($"        jogo diametral de montagem {Mm(ClearanceMin)}–{Mm(ClearanceMax)}   " +
                              $"excentricidade máx. {Mm(Eccentricity)}");
            sb.AppendLine($"        cota de: {Source}");
            sb.AppendLine();
            sb.AppendLine($"Esmagamento {Pct(Squeeze)}   " +
                          (StretchIsOuterCompression ? "compressão do Ø externo " : "estiramento ") + Pct(Stretch) +
                          $"   preenchimento {Pct(Fill)}");
            if (WorstCase != null)
            {
                var w = WorstCase;
                sb.AppendLine($"Pior caso ({w.Fit}, canal ±{Mm(w.GrooveTolerance)}, anel d2 ±{Mm(w.RingSectionTolerance)} " +
                              $"d1 ±{Mm(w.RingInnerTolerance)}):");
                sb.AppendLine($"        esmagamento {Pct(w.SqueezeMin)}–{Pct(w.SqueezeMax)}   " +
                              (StretchIsOuterCompression ? "compressão " : "estiramento ") +
                              $"{Pct(w.StretchMin)}–{Pct(w.StretchMax)}   preenchimento até {Pct(w.FillMax)}");
                if (Kind != GrooveKind.AxialFace)
                    sb.AppendLine($"        jogo diametral {Mm(w.ClearanceMin)}–{Mm(w.ClearanceMax)}");
            }
            if (Issues.Count > 0)
            {
                sb.AppendLine();
                foreach (var i in Issues) sb.AppendLine("  • " + i);
            }
            return sb.ToString();
        }
    }
}
