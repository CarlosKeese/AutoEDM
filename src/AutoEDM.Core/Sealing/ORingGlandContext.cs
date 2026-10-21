namespace AutoEDM.Sealing
{
    /// <summary>De onde saem largura e profundidade do canal.</summary>
    public enum GrooveSizing
    {
        /// <summary>Cota da tabela de alojamento (ou do cálculo, fora dela) — o anel é escolhido
        /// para o canal. É o que vale para o anel RECOMENDADO.</summary>
        Table,

        /// <summary>O canal é desenhado PARA O ANEL escolhido: o fundo segue o estiramento (ou a
        /// compressão do Ø externo) do anel, dentro dos limites, e a largura acompanha o
        /// preenchimento. É o que vale quando o operador troca o anel na lista (Carlos, 2026-10-21).</summary>
        FromRing
    }

    /// <summary>
    /// O que o alojamento precisa saber ALÉM da peça em que é cortado: a peça conjugada (o furo
    /// que recebe o eixo, o eixo que entra no furo, a tampa que fecha a face), a pressão e as
    /// tolerâncias. Sem ele o cálculo supõe o conjugado no MESMO Ø nominal, que é o que o
    /// AutoEDM fazia antes — e continua sendo o resultado quando nada é informado.
    /// </summary>
    public sealed class ORingGlandContext
    {
        /// <summary>Ø nominal do conjugado (mm): o FURO num canal de eixo, o EIXO num canal de
        /// furo. null = mesmo Ø nominal da peça (só o ajuste separa os dois).</summary>
        public double? MatingDiameterMm { get; set; }

        /// <summary>De onde veio o Ø conjugado ("lido em contexto: Peça.par", "digitado") — vai
        /// no relatório para ninguém confundir medida com suposição.</summary>
        public string MatingSource { get; set; }

        /// <summary>Canal de FACE: vão axial entre as duas faces montadas (mm). 0 = encostadas.
        /// O vão soma na altura do alojamento, então o canal fica mais raso.</summary>
        public double FaceGapMm { get; set; }

        public GrooveSizing Sizing { get; set; } = GrooveSizing.Table;

        /// <summary>Pressão de trabalho (bar). Padrão: água de refrigeração de molde.</summary>
        public double PressureBar { get; set; } = ORingGrooveRules.DefaultPressureBar;

        /// <summary>Ajuste ISO furo/eixo para o pior caso (ex.: "H7/g6"). Vazio = sem pior caso.</summary>
        public string Fit { get; set; } = IsoFits.DefaultFit;

        /// <summary>Tolerância ± das cotas do canal (fundo, largura, profundidade), mm.</summary>
        public double GrooveToleranceMm { get; set; } = ORingGrooveRules.DefaultGrooveTolerance;

        public static ORingGlandContext Default => new ORingGlandContext();

        public ORingGlandContext Clone() => (ORingGlandContext)MemberwiseClone();
    }

    /// <summary>
    /// Os extremos do alojamento com todas as tolerâncias empilhadas para o lado ruim — o
    /// número nominal diz se o canal está certo; este diz se ele CONTINUA certo na peça que sai
    /// da máquina, com o anel que vem no saquinho.
    /// </summary>
    public sealed class GrooveWorstCase
    {
        public string Fit { get; set; }
        public double SqueezeMin { get; set; }
        public double SqueezeMax { get; set; }
        public double FillMax { get; set; }
        public double StretchMin { get; set; }
        public double StretchMax { get; set; }

        /// <summary>Jogo DIAMETRAL entre a peça e o conjugado (mm) — só canal radial.</summary>
        public double ClearanceMin { get; set; }
        public double ClearanceMax { get; set; }

        /// <summary>Tolerâncias usadas, para o relatório.</summary>
        public double RingSectionTolerance { get; set; }
        public double RingInnerTolerance { get; set; }
        public double GrooveTolerance { get; set; }
    }
}
