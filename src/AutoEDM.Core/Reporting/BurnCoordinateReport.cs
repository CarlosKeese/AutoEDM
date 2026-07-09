using System.Collections.Generic;
using System.Drawing;

namespace AutoEDM.Reporting
{
    /// <summary>
    /// Coordenada de queima de UM detalhe/eletrodo, relativa ao ZERO-MÁQUINA
    /// (= origem da montagem). No fluxo do Carlos a cavidade é posicionada com a
    /// origem alinhada ao zero-máquina, então a coordenada de queima é o ponto onde
    /// a origem do eletrodo tocaria a região: XY no centro da pegada do detalhe e Z
    /// no FUNDO (ponto mais baixo). Todos os valores em milímetros.
    /// </summary>
    public sealed class BurnCoordinate
    {
        /// <summary>Índice do detalhe (1..N), na ordem em que foi segmentado.</summary>
        public int DetailIndex { get; set; }

        /// <summary>Ra da cor do detalhe (µm) — para casar com o eletrodo gerado.</summary>
        public double Ra { get; set; }

        /// <summary>Cor de queima detectada na face (identifica o Ra).</summary>
        public Color Color { get; set; }

        /// <summary>Quantidade de faces de queima no detalhe.</summary>
        public int FaceCount { get; set; }

        /// <summary>Coordenada de queima X (mm, zero-máquina) — centro da pegada.</summary>
        public double X { get; set; }

        /// <summary>Coordenada de queima Y (mm, zero-máquina) — centro da pegada.</summary>
        public double Y { get; set; }

        /// <summary>Coordenada de queima Z (mm, zero-máquina) — fundo da região.</summary>
        public double Z { get; set; }

        /// <summary>Dimensão X da pegada do detalhe (mm).</summary>
        public double SizeX { get; set; }

        /// <summary>Dimensão Y da pegada do detalhe (mm).</summary>
        public double SizeY { get; set; }

        /// <summary>Profundidade Z do detalhe (mm).</summary>
        public double SizeZ { get; set; }

        /// <summary>True se a bounding box (e logo a coordenada) foi lida com sucesso.</summary>
        public bool CoordinateKnown { get; set; }

        /// <summary>Observações por linha (ex.: rotação não aplicada, bbox não lida).</summary>
        public List<string> Notes { get; } = new List<string>();
    }

    /// <summary>
    /// Relatório de coordenadas de queima da montagem ativa: uma linha por detalhe
    /// (= por eletrodo). É uma operação SOMENTE-LEITURA — não cria geometria — e
    /// entrega ao desenhista a tabela de coordenadas que antes era feita à mão no
    /// "plano de erosão".
    /// </summary>
    public sealed class BurnCoordinateReport
    {
        /// <summary>Nome da montagem analisada.</summary>
        public string AssemblyName { get; set; }

        /// <summary>Ocorrência (cavidade) onde as faces de queima foram encontradas.</summary>
        public string TargetOccurrenceName { get; set; }

        /// <summary>Origem da ocorrência-alvo na montagem (mm), se lida.</summary>
        public double OriginX { get; set; }
        public double OriginY { get; set; }
        public double OriginZ { get; set; }

        /// <summary>True se a posição da ocorrência-alvo foi lida (senão coord. local).</summary>
        public bool OriginKnown { get; set; }

        /// <summary>Coordenadas de queima, uma por detalhe.</summary>
        public List<BurnCoordinate> Coordinates { get; } = new List<BurnCoordinate>();

        /// <summary>Avisos gerais do relatório.</summary>
        public List<string> Warnings { get; } = new List<string>();
    }
}
