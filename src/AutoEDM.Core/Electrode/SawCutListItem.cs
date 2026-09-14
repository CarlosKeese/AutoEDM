using System.Collections.Generic;

namespace AutoEDM.Electrode
{
    /// <summary>
    /// Uma linha da janela "Lista de corte" (Carlos, 2026-09-14): UM arquivo de eletrodo
    /// selecionado na montagem, quantas posições ele ocupa, o perfil do estoque e a medida na
    /// serra. Várias ocorrências selecionadas do mesmo .par viram uma linha só. SOMENTE-LEITURA
    /// na montagem; o perfil pode ser trocado na janela (<see cref="SawCutPlanner.ForBlank"/>).
    /// </summary>
    public sealed class SawCutListItem
    {
        /// <summary>Nome do arquivo da peça (ex.: "15142.200_EDM_EE01.par").</summary>
        public string FileName { get; set; }

        /// <summary>Caminho completo da peça. Null = não lido.</summary>
        public string FullPath { get; set; }

        /// <summary>Ocorrências desse MESMO arquivo na montagem inteira (não só as selecionadas).</summary>
        public int Positions { get; set; }

        /// <summary>True se a caixa envolvente do corpo foi lida.</summary>
        public bool SizeKnown { get; set; }

        /// <summary>Caixa envolvente do corpo, no sistema da PEÇA (mm).</summary>
        public double SizeXmm { get; set; }
        public double SizeYmm { get; set; }
        public double SizeZmm { get; set; }

        /// <summary>Material gravado na peça (ex.: "Cobre", "CuW80"). Null = não lido.</summary>
        public string Material { get; set; }

        /// <summary>
        /// Documento COM da peça — vivo só enquanto a montagem está aberta (a janela é modal, então
        /// está). A miniatura é lida dele na hora de imprimir, não na listagem.
        /// </summary>
        public object PartDocument { get; set; }

        /// <summary>Miniatura isométrica p/ a impressão. Null = não gerada ainda ou falhou (<see cref="ThumbnailTried"/>).</summary>
        public System.Drawing.Image Thumbnail { get; set; }

        /// <summary>Já tentou gerar a miniatura (não tenta de novo a cada impressão se falhou).</summary>
        public bool ThumbnailTried { get; set; }

        /// <summary>Perfil + medida de corte. Nunca null depois de montado.</summary>
        public SawCut Cut { get; set; } = new SawCut();

        /// <summary>Avisos da leitura (medidas/caminho não lidos). Os do corte ficam em <see cref="SawCut.Note"/>.</summary>
        public List<string> Notes { get; } = new List<string>();
    }
}
