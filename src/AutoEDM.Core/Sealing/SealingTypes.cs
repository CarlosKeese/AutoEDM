namespace AutoEDM.Sealing
{
    /// <summary>
    /// Elastômero do anel. Não muda a COTA do canal (a geometria do alojamento é a mesma);
    /// muda o quanto o anel aguenta ser esticado e o quanto de folga o canal precisa ter para
    /// a dilatação térmica e o inchamento no fluido — por isso ele entra no cálculo como
    /// LIMITE, não como dimensão. Ver <see cref="ORingGrooveRules"/>.
    /// </summary>
    public enum Elastomer
    {
        /// <summary>NBR (Buna-N, nitrílica) — o anel comum: óleo mineral, −30 a +100 °C.</summary>
        Nbr,

        /// <summary>FKM (Viton®) — resiste a temperatura e produto químico, mas alonga menos
        /// que a NBR e dilata mais com o calor: limite de estiramento menor e canal mais folgado.</summary>
        Fkm
    }

    /// <summary>
    /// Tipo de vedação, na acepção da ISO 3601: é ele que decide o ESMAGAMENTO do anel. Quanto
    /// mais movimento, menos esmagamento (senão o anel arrasta, esquenta e rasga).
    /// </summary>
    public enum SealMotion
    {
        /// <summary>Estática — nada se move. Maior esmagamento; é a vedação mais segura.</summary>
        Static,

        /// <summary>Recíproca — haste/êmbolo entrando e saindo. Esmagamento médio.</summary>
        Reciprocating,

        /// <summary>
        /// Rotativa — eixo girando. Esmagamento MÍNIMO e o anel NÃO PODE ser esticado: borracha
        /// esticada se CONTRAI quando esquenta (efeito Gow-Joule), então um anel esticado num
        /// eixo que gira aperta cada vez mais até queimar. Aqui o anel tem de ser igual ou um
        /// pouco MAIOR que o fundo do canal.
        /// </summary>
        Rotary
    }

    /// <summary>Onde o canal é cortado — sai da geometria selecionada, não de uma escolha solta.</summary>
    public enum GrooveKind
    {
        /// <summary>Canal anular numa face PLANA (vedação de face/flange). O anel deita no canal:
        /// não estica nem comprime, e o canal é dimensionado pelo diâmetro médio do anel.</summary>
        AxialFace,

        /// <summary>Canal num EIXO (face cilíndrica com material por DENTRO). O anel é ESTICADO
        /// por cima do eixo até cair no canal.</summary>
        RadialExternal,

        /// <summary>Canal num FURO (face cilíndrica com material por FORA). O anel é COMPRIMIDO
        /// para entrar no furo e abre dentro do canal.</summary>
        RadialInternal
    }
}
