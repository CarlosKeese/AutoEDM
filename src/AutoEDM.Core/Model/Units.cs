namespace AutoEDM.Model
{
    /// <summary>
    /// Ponto único de conversão metros&lt;-&gt;milímetros (revisão 2026-07-23,
    /// docs/REVISAO-AutoEDM.md P1.2): a API do Solid Edge trabalha em METROS e radianos
    /// (<c>Face.GetRange</c>, <c>Occurrence.PutOrigin</c>, <c>GetTransform</c>...); o domínio
    /// deste projeto (offset de eletrodo, gap, catálogo de blank, Ra) trabalha em MILÍMETROS.
    ///
    /// Um fator 1000 trocado não estoura exceção — produz um resultado PLAUSÍVEL e ERRADO
    /// (eletrodo usinado do tamanho errado), o tipo de bug mais caro que existe aqui e o mais
    /// difícil de pegar em revisão de código, porque <c>double</c> é <c>double</c>. Em vez de
    /// `x / 1000.0` / `x * 1000.0` soltos pelo código, use sempre <see cref="MmToM"/> /
    /// <see cref="MToMm"/> — concentra a conversão num único lugar e deixa a intenção explícita
    /// no ponto de chamada (ex.: <c>Units.MmToM(offsetMm)</c> em vez de <c>offsetMm / 1000.0</c>).
    /// </summary>
    public static class Units
    {
        /// <summary>Milímetros (domínio deste projeto) -> metros (domínio do Solid Edge/COM).</summary>
        public static double MmToM(double mm) => mm / 1000.0;

        /// <summary>Metros (domínio do Solid Edge/COM) -> milímetros (domínio deste projeto).</summary>
        public static double MToMm(double m) => m * 1000.0;
    }
}
