using System;
using System.Collections.Generic;
using System.Globalization;

namespace AutoEDM.Reporting.Xlsx
{
    /// <summary>
    /// Formatação de uma célula. É comparável por VALOR porque o <see cref="XlsxWriter"/> junta as
    /// iguais numa entrada só do styles.xml — a folha de revisões usa meia dúzia de estilos em
    /// centenas de células.
    /// </summary>
    public sealed class XlsxStyle : IEquatable<XlsxStyle>
    {
        public bool Bold { get; set; }
        public double FontSize { get; set; } = 10;

        /// <summary>Cor da fonte em RRGGBB (ex.: "C00000"). Null = automática.</summary>
        public string FontColor { get; set; }

        /// <summary>Fundo sólido em RRGGBB (ex.: "D9D9D9"). Null = sem preenchimento.</summary>
        public string Fill { get; set; }

        /// <summary>Borda fina nos quatro lados.</summary>
        public bool Border { get; set; }

        /// <summary>Quebra o texto dentro da célula (as "AÇÕES INDICADAS" são várias linhas).</summary>
        public bool Wrap { get; set; }

        /// <summary>"left" (padrão), "center" ou "right".</summary>
        public string Align { get; set; }

        /// <summary>"top" (padrão), "center" ou "bottom".</summary>
        public string VerticalAlign { get; set; }

        public bool Equals(XlsxStyle other) =>
            other != null && Bold == other.Bold && FontSize.Equals(other.FontSize) &&
            FontColor == other.FontColor && Fill == other.Fill && Border == other.Border &&
            Wrap == other.Wrap && Align == other.Align && VerticalAlign == other.VerticalAlign;

        public override bool Equals(object obj) => Equals(obj as XlsxStyle);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Bold ? 17 : 19;
                h = h * 31 + FontSize.GetHashCode();
                h = h * 31 + (FontColor ?? "").GetHashCode();
                h = h * 31 + (Fill ?? "").GetHashCode();
                h = h * 31 + (Border ? 1 : 0);
                h = h * 31 + (Wrap ? 1 : 0);
                h = h * 31 + (Align ?? "").GetHashCode();
                h = h * 31 + (VerticalAlign ?? "").GetHashCode();
                return h;
            }
        }
    }

    /// <summary>Uma célula: texto OU número, com estilo opcional.</summary>
    public sealed class XlsxCell
    {
        public int Row { get; set; }        // 1-based, como a planilha mostra
        public int Column { get; set; }     // 1-based: 1 = A
        public string Text { get; set; }
        public double? Number { get; set; }
        public XlsxStyle Style { get; set; }
    }

    /// <summary>Imagem ancorada numa célula — flutua POR CIMA, que é como a folha MD é montada.</summary>
    public sealed class XlsxImage
    {
        public int Row { get; set; }        // 1-based: canto superior esquerdo
        public int Column { get; set; }
        public byte[] Png { get; set; }
        public int WidthPx { get; set; }
        public int HeightPx { get; set; }
    }

    /// <summary>Intervalo mesclado (A1:D1).</summary>
    public sealed class XlsxMerge
    {
        public int FromRow, FromColumn, ToRow, ToColumn;
    }

    /// <summary>
    /// Uma planilha em memória. Só o que a folha de revisões precisa: texto, número, estilo,
    /// largura de coluna, altura de linha, mesclagem e imagem ancorada.
    /// </summary>
    public sealed class XlsxSheet
    {
        public XlsxSheet(string name = "Planilha1") { Name = name; }

        /// <summary>Nome da aba. O Excel proíbe : \ / ? * [ ] e mais de 31 caracteres — ver <see cref="SafeName"/>.</summary>
        public string Name { get; set; }

        public List<XlsxCell> Cells { get; } = new List<XlsxCell>();
        public List<XlsxImage> Images { get; } = new List<XlsxImage>();
        public List<XlsxMerge> Merges { get; } = new List<XlsxMerge>();

        /// <summary>Largura por coluna (1-based), na unidade do Excel (~caracteres).</summary>
        public Dictionary<int, double> ColumnWidths { get; } = new Dictionary<int, double>();

        /// <summary>Altura por linha (1-based), em pontos.</summary>
        public Dictionary<int, double> RowHeights { get; } = new Dictionary<int, double>();

        public XlsxSheet Set(int row, int column, string text, XlsxStyle style = null)
        {
            Cells.Add(new XlsxCell { Row = row, Column = column, Text = text, Style = style });
            return this;
        }

        public XlsxSheet SetNumber(int row, int column, double value, XlsxStyle style = null)
        {
            Cells.Add(new XlsxCell { Row = row, Column = column, Number = value, Style = style });
            return this;
        }

        /// <summary>
        /// Mescla um intervalo. Com <paramref name="style"/>, as células COBERTAS recebem o mesmo
        /// estilo — sem isso o Excel desenha a borda só em volta da primeira e o contorno do
        /// intervalo fica pela metade (Carlos, 2026-09-18: "os contornos estão faltando").
        /// </summary>
        public XlsxSheet Merge(int fromRow, int fromColumn, int toRow, int toColumn, XlsxStyle style = null)
        {
            Merges.Add(new XlsxMerge { FromRow = fromRow, FromColumn = fromColumn, ToRow = toRow, ToColumn = toColumn });
            if (style == null) return this;

            for (int row = fromRow; row <= toRow; row++)
                for (int column = fromColumn; column <= toColumn; column++)
                {
                    if (row == fromRow && column == fromColumn) continue;   // a âncora já tem o valor
                    Cells.Add(new XlsxCell { Row = row, Column = column, Style = style });
                }
            return this;
        }

        public XlsxSheet Width(int column, double width) { ColumnWidths[column] = width; return this; }

        public XlsxSheet Height(int row, double points) { RowHeights[row] = points; return this; }

        /// <summary>Referência de célula no estilo A1 (coluna 1-based → A, 27 → AA).</summary>
        public static string CellRef(int row, int column) =>
            ColumnName(column) + row.ToString(CultureInfo.InvariantCulture);

        /// <summary>Letra da coluna (1 = A).</summary>
        public static string ColumnName(int column)
        {
            if (column < 1) throw new ArgumentOutOfRangeException(nameof(column), "coluna é 1-based");
            string name = "";
            while (column > 0)
            {
                int rest = (column - 1) % 26;
                name = (char)('A' + rest) + name;
                column = (column - 1) / 26;
            }
            return name;
        }

        /// <summary>Nome de aba aceito pelo Excel: sem : \ / ? * [ ], no máximo 31 caracteres.</summary>
        public static string SafeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Planilha1";
            var clean = new System.Text.StringBuilder();
            foreach (char c in name.Trim())
                clean.Append(":\\/?*[]".IndexOf(c) >= 0 ? '-' : c);
            string s = clean.ToString();
            return s.Length <= 31 ? s : s.Substring(0, 31);
        }
    }
}
