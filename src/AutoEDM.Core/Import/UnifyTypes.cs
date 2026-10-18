using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AutoEDM.Assembly;

namespace AutoEDM.Import
{
    /// <summary>Uma ocorrência da montagem, com o que se precisa saber dela para unificar.</summary>
    public sealed class ImportedOccurrence
    {
        /// <summary>O objeto COM. Guardado porque a substituição mexe NELE — índice não serve:
        /// apagar uma ocorrência reindexa a coleção inteira.</summary>
        public object Com { get; set; }

        public string Name { get; set; }

        /// <summary>Caminho do arquivo que esta ocorrência referencia.</summary>
        public string FilePath { get; set; }

        /// <summary>A pose completa (rotação 3D + origem), em metros.</summary>
        public OccurrenceTransform Pose { get; set; }

        /// <summary>Por que esta ocorrência ficou de fora da unificação (null = está dentro).</summary>
        public string Skipped { get; set; }

        public override string ToString() => Name ?? "<sem nome>";
    }

    /// <summary>Um arquivo distinto referenciado pela montagem, lido UMA vez.</summary>
    public sealed class ImportedPart
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public GeometrySignature Signature { get; set; }

        /// <summary>As ocorrências que apontam para este arquivo.</summary>
        public readonly List<ImportedOccurrence> Occurrences = new List<ImportedOccurrence>();

        /// <summary>A nuvem de vértices em coordenadas LOCAIS (mm). Lida sob demanda — só para
        /// os arquivos que empataram na peneira.</summary>
        public List<double[]> VerticesMm { get; set; }

        public bool VerticesPartial { get; set; }
        public string VerticesWhy { get; set; }

        /// <summary>
        /// O perfil de revolução, quando a peça é TORNEADA (sem vértice nenhum, só arestas
        /// circulares num eixo comum). É o caminho para bucha, pino e coluna — as peças mais
        /// repetidas de um molde, e justamente as que não têm vértice para casar.
        /// Null quando nem se tentou; <c>Ok == false</c> quando se tentou e a peça não é de
        /// revolução.
        /// </summary>
        public AxisProfileFit.Profile Revolution { get; set; }

        /// <summary>Como a igualdade desta peça foi provada — entra no relatório, porque o
        /// usuário merece saber se a prova foi vértice a vértice ou por perfil de revolução.</summary>
        public bool UsedRevolutionPath { get; set; }

        /// <summary>Os ANÉIS (arestas circulares) do corpo, em coordenadas locais (mm). Lidos
        /// uma vez e usados por dois caminhos: a prova do corpo inteiro
        /// (<see cref="RingEvidence"/>) e o perfil de revolução.</summary>
        public List<Ring> Rings { get; set; }

        /// <summary>TODA aresta do corpo é circular? É o que autoriza a conclusão "sólido de
        /// revolução".</summary>
        public bool AllEdgesCircular { get; set; }
    }

    /// <summary>
    /// Um conjunto de arquivos DIFERENTES cuja geometria é a mesma peça — e, para cada um, o
    /// movimento rígido que leva a geometria da mestra sobre a dele.
    /// </summary>
    public sealed class DuplicateGroup
    {
        /// <summary>O arquivo que SOBREVIVE. Todas as ocorrências dos outros passam a apontar
        /// para ele.</summary>
        public ImportedPart Master { get; set; }

        /// <summary>Os arquivos que vão sair de cena, com o encaixe conferido de cada um.</summary>
        public readonly List<GroupMember> Members = new List<GroupMember>();

        /// <summary>Arquivos que a peneira juntou mas o encaixe REPROVOU — com o motivo. Ficam
        /// no relatório de propósito: é aqui que aparece a peça espelhada, que é o caso em que
        /// unificar seria refugo.</summary>
        public readonly List<RejectedMember> Rejected = new List<RejectedMember>();

        /// <summary>Quantas ocorrências a montagem perde de arquivo distinto se este grupo for
        /// aplicado.</summary>
        public int FilesRemoved => Members.Count;

        /// <summary>Quantas ocorrências serão reposicionadas.</summary>
        public int OccurrencesReplaced
        {
            get
            {
                int n = 0;
                foreach (GroupMember m in Members) n += m.Part.Occurrences.Count;
                return n;
            }
        }

        public string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "{0}  ({1})", Master.FileName, Master.Signature.Describe());
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "\n    mantém 1 arquivo; dispensa {0}; reposiciona {1} ocorrência(s)",
                FilesRemoved, OccurrencesReplaced);
            return sb.ToString();
        }
    }

    /// <summary>Um arquivo que será substituído pela mestra, com a prova do encaixe.</summary>
    public sealed class GroupMember
    {
        public ImportedPart Part { get; set; }

        /// <summary>Rotação do encaixe mestra -> este (linha-a-linha).</summary>
        public double[] FitR { get; set; }

        /// <summary>Translação do encaixe mestra -> este, em MILÍMETROS.</summary>
        public double[] FitTMm { get; set; }

        /// <summary>Pior desvio medido no encaixe (mm) — a evidência de que a troca é exata.</summary>
        public double MaxDeviationMm { get; set; }

        public double RotationDeg { get; set; }

        /// <summary>A igualdade foi provada pelo PERFIL DE REVOLUÇÃO (peça torneada, sem
        /// vértices) em vez de vértice a vértice. Aparece no relatório porque muda o que a prova
        /// significa: aqui o giro em torno do eixo é livre, e isso é correto por simetria.</summary>
        public bool ByRevolution { get; set; }

        /// <summary>
        /// O encaixe é uma REFLEXÃO (det(R) = -1), não uma rotação. Membro assim NUNCA é
        /// aplicado: ver a nota em <see cref="AssemblyUnifier.Apply"/> — uma matriz de
        /// determinante negativo não é um posicionamento rígido válido, e mandá-la para
        /// <c>PutMatrix</c> seria pedir à Solid Edge algo que ela não faz por esse caminho.
        /// </summary>
        public bool IsMirror { get; set; }
    }

    /// <summary>Um candidato que a peneira juntou e o encaixe recusou.</summary>
    public sealed class RejectedMember
    {
        public ImportedPart Part { get; set; }
        public string Why { get; set; }
        public bool IsMirror { get; set; }
    }

    /// <summary>
    /// O que a varredura encontrou — tudo o que o usuário precisa CONFERIR antes de qualquer
    /// escrita. A varredura é só leitura, de propósito: é o que permite mostrar o plano e pedir
    /// confirmação, em vez de mexer na montagem e informar depois.
    /// </summary>
    public sealed class UnifyScan
    {
        public bool Ok { get; set; }
        public string Error { get; set; }

        public int OccurrenceCount { get; set; }
        public int DistinctFilesBefore { get; set; }

        public readonly List<DuplicateGroup> Groups = new List<DuplicateGroup>();
        public readonly List<ImportedPart> Unique = new List<ImportedPart>();
        public readonly List<ImportedOccurrence> Skipped = new List<ImportedOccurrence>();
        public readonly List<string> Notes = new List<string>();

        /// <summary>
        /// Como a montagem carrega a posição — a pergunta que decide se este botão tem trabalho
        /// a fazer e que NÃO pode ser presumida: tradutores diferentes fazem diferente.
        /// </summary>
        public string PlacementStyle { get; set; }

        public int DistinctFilesAfter
        {
            get
            {
                int removed = 0;
                foreach (DuplicateGroup g in Groups) removed += g.FilesRemoved;
                return DistinctFilesBefore - removed;
            }
        }

        public int OccurrencesToReplace
        {
            get
            {
                int n = 0;
                foreach (DuplicateGroup g in Groups) n += g.OccurrencesReplaced;
                return n;
            }
        }

        public int MirrorPairs
        {
            get
            {
                int n = 0;
                foreach (DuplicateGroup g in Groups)
                    foreach (RejectedMember r in g.Rejected) if (r.IsMirror) n++;
                return n;
            }
        }

        /// <summary>O resumo que vai para a janela de confirmação.</summary>
        public string Summary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{OccurrenceCount} ocorrência(s) em {DistinctFilesBefore} arquivo(s) distinto(s).");
            if (PlacementStyle != null) sb.AppendLine("Posição: " + PlacementStyle);

            // O aviso de espelho vem ANTES da lista de grupos, não depois. Na primeira rodada
            // real ele saía no fim de 31 grupos e o Carlos concluiu que a ferramenta tinha
            // falhado em reconhecer peças iguais — quando ela tinha ACERTADO e dito por quê,
            // num lugar que ninguém lê.
            if (MirrorPairs > 0)
            {
                sb.AppendLine();
                sb.AppendLine($">>> {MirrorPairs} par(es) de peças são ESPELHADAS entre si (mão esquerda e mão");
                sb.AppendLine("    direita) e por isso NÃO foram unificadas. Elas têm volume, área e topologia");
                sb.AppendLine("    idênticos e parecem iguais na tela, mas não são intercambiáveis: nenhum");
                sb.AppendLine("    movimento rígido leva uma na outra, só uma reflexão. A lista está abaixo,");
                sb.AppendLine("    marcada [ESPELHADA].");
            }
            sb.AppendLine();

            if (Groups.Count == 0)
            {
                sb.AppendLine("Nenhuma peça repetida encontrada — nada a unificar.");
            }
            else
            {
                sb.AppendLine($"{Groups.Count} peça(s) repetida(s): {DistinctFilesBefore} -> {DistinctFilesAfter} arquivo(s), " +
                              $"{OccurrencesToReplace} ocorrência(s) reposicionada(s).");
                sb.AppendLine();
                foreach (DuplicateGroup g in Groups) sb.AppendLine("  " + g.Describe());
            }

            if (Skipped.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"{Skipped.Count} ocorrência(s) fora da análise:");
                int shown = 0;
                foreach (ImportedOccurrence o in Skipped)
                {
                    if (shown++ >= 8) { sb.AppendLine("  ..."); break; }
                    sb.AppendLine($"  {o.Name}: {o.Skipped}");
                }
            }

            foreach (string n in Notes) sb.AppendLine(n);
            return sb.ToString();
        }
    }

    /// <summary>O que a execução de fato fez. Uma linha por ocorrência tocada.</summary>
    public sealed class UnifyResult
    {
        public int Replaced { get; set; }
        public int Failed { get; set; }
        public readonly List<string> Messages = new List<string>();

        /// <summary>Arquivos que deixaram de ser referenciados. NÃO são apagados do disco — quem
        /// decide apagar é o usuário, depois de abrir a montagem e conferir.</summary>
        public readonly List<string> OrphanedFiles = new List<string>();

        public double WorstPositionErrorMm { get; set; }

        public string Summary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Replaced} ocorrência(s) substituída(s) pela peça-mestra.");
            if (Failed > 0) sb.AppendLine($"{Failed} falhou/falharam — a ocorrência ORIGINAL foi mantida nessas.");
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "Pior erro de posição conferido: {0:0.0000} mm.{1}", WorstPositionErrorMm, Environment.NewLine);
            if (OrphanedFiles.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"{OrphanedFiles.Count} arquivo(s) não são mais usados pela montagem. " +
                              "NADA foi apagado do disco — confira a montagem e apague à mão se quiser.");
            }
            foreach (string m in Messages) sb.AppendLine(m);
            return sb.ToString();
        }
    }
}
