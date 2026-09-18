using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace AutoEDM.Reporting.Xlsx
{
    /// <summary>
    /// Escreve um .xlsx à mão (Carlos, 2026-09-18): um .xlsx é um .zip com meia dúzia de XMLs, e
    /// escrevê-los aqui custa menos que carregar ClosedXML/EPPlus DENTRO do processo da Solid Edge
    /// — onde dependência extra vira conflito de assembly binding. Mesmo princípio do
    /// <c>IgesWriter</c>: formato publicado, escrito sem adivinhar assinatura de biblioteca.
    ///
    /// Cobre só o que a folha de revisões usa: texto e número, negrito/cor/fundo/borda/quebra de
    /// linha, largura de coluna, altura de linha, mesclagem e IMAGEM ancorada numa célula
    /// (flutuando por cima, como o Carlos monta hoje — confirmado no teste de importação para o
    /// Google Sheets em 2026-09-18).
    ///
    /// Texto vai como <c>inlineStr</c>: dispensa a tabela de strings compartilhadas, uma parte a
    /// menos para manter em sincronia. Lógica pura, sem COM.
    /// </summary>
    public static class XlsxWriter
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Pixels → EMU (English Metric Units): 914400 EMU por polegada, 96 px por polegada.</summary>
        private const int EmuPerPixel = 9525;

        /// <summary>Grava a planilha no caminho dado, sobrescrevendo.</summary>
        public static void Save(string path, XlsxSheet sheet)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            using (var file = new FileStream(path, FileMode.Create, FileAccess.Write))
                Save(file, sheet);
        }

        /// <summary>Grava a planilha no fluxo dado (o fluxo NÃO é fechado).</summary>
        public static void Save(Stream output, XlsxSheet sheet)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (sheet == null) throw new ArgumentNullException(nameof(sheet));

            List<XlsxImage> images = sheet.Images.Where(i => i != null && i.Png != null && i.Png.Length > 0).ToList();
            List<XlsxStyle> styles = CollectStyles(sheet);

            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                Write(zip, "[Content_Types].xml", ContentTypes(images.Count));
                Write(zip, "_rels/.rels", RootRels());
                Write(zip, "xl/workbook.xml", Workbook(sheet));
                Write(zip, "xl/_rels/workbook.xml.rels", WorkbookRels());
                Write(zip, "xl/styles.xml", Styles(styles));
                Write(zip, "xl/worksheets/sheet1.xml", Sheet(sheet, styles, images.Count > 0));

                if (images.Count > 0)
                {
                    Write(zip, "xl/worksheets/_rels/sheet1.xml.rels", SheetRels());
                    Write(zip, "xl/drawings/drawing1.xml", Drawing(images));
                    Write(zip, "xl/drawings/_rels/drawing1.xml.rels", DrawingRels(images.Count));
                    for (int i = 0; i < images.Count; i++)
                        WriteBinary(zip, $"xl/media/image{i + 1}.png", images[i].Png);
                }
            }
        }

        /// <summary>
        /// Largura e altura de um PNG, lidas do cabeçalho IHDR (bytes 16..23, big-endian). O
        /// desenho precisa do tamanho em EMU e o .NET Framework só daria isso carregando a imagem
        /// inteira — ler 24 bytes é mais barato e não depende de System.Drawing.
        /// Devolve false se não for um PNG.
        /// </summary>
        public static bool TryReadPngSize(byte[] png, out int widthPx, out int heightPx)
        {
            widthPx = heightPx = 0;
            if (png == null || png.Length < 24) return false;
            // assinatura: 89 50 4E 47 0D 0A 1A 0A
            if (png[0] != 0x89 || png[1] != 0x50 || png[2] != 0x4E || png[3] != 0x47) return false;
            widthPx = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
            heightPx = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
            return widthPx > 0 && heightPx > 0;
        }

        // ----------------------------------------------------------------- partes do pacote

        private static string ContentTypes(int imageCount)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
            sb.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            sb.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            if (imageCount > 0) sb.Append("<Default Extension=\"png\" ContentType=\"image/png\"/>");
            sb.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
            sb.Append("<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            sb.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            if (imageCount > 0)
                sb.Append("<Override PartName=\"/xl/drawings/drawing1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.drawing+xml\"/>");
            sb.Append("</Types>");
            return sb.ToString();
        }

        private static string RootRels() =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        private static string Workbook(XlsxSheet sheet) =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
            "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<sheets><sheet name=\"" + Xml(XlsxSheet.SafeName(sheet.Name)) + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
            "</workbook>";

        private static string WorkbookRels() =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
            "</Relationships>";

        private static string SheetRels() =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing\" Target=\"../drawings/drawing1.xml\"/>" +
            "</Relationships>";

        private static string DrawingRels(int imageCount)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (int i = 1; i <= imageCount; i++)
                sb.Append($"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"../media/image{i}.png\"/>");
            sb.Append("</Relationships>");
            return sb.ToString();
        }

        /// <summary>
        /// Os estilos usados, na ordem em que viram índice no cellXfs. O índice 0 é sempre o
        /// estilo padrão (sem nada) — célula sem estilo aponta para ele.
        /// </summary>
        private static List<XlsxStyle> CollectStyles(XlsxSheet sheet)
        {
            var list = new List<XlsxStyle> { new XlsxStyle() };
            foreach (XlsxCell c in sheet.Cells)
            {
                if (c.Style == null || list.Contains(c.Style)) continue;
                list.Add(c.Style);
            }
            return list;
        }

        private static string Styles(List<XlsxStyle> styles)
        {
            // Fontes, preenchimentos e bordas entram como TABELAS e o cellXfs aponta para elas.
            var fonts = new List<string>();
            var fills = new List<string>();
            var borders = new List<string>();
            var xfs = new StringBuilder();

            // Obrigatórios pelo formato: preenchimento 0 = nenhum, 1 = cinza125; borda 0 = nenhuma.
            fills.Add("<fill><patternFill patternType=\"none\"/></fill>");
            fills.Add("<fill><patternFill patternType=\"gray125\"/></fill>");
            borders.Add("<border><left/><right/><top/><bottom/><diagonal/></border>");

            foreach (XlsxStyle s in styles)
            {
                string font = "<font><sz val=\"" + s.FontSize.ToString(Inv) + "\"/>" +
                              (s.Bold ? "<b/>" : "") +
                              (s.FontColor != null ? "<color rgb=\"FF" + s.FontColor + "\"/>" : "") +
                              "<name val=\"Arial\"/></font>";
                int fontId = Index(fonts, font);

                int fillId = 0;
                if (s.Fill != null)
                    fillId = Index(fills, "<fill><patternFill patternType=\"solid\">" +
                                          "<fgColor rgb=\"FF" + s.Fill + "\"/><bgColor indexed=\"64\"/></patternFill></fill>");

                int borderId = 0;
                if (s.Border)
                    borderId = Index(borders,
                        "<border><left style=\"thin\"><color indexed=\"64\"/></left>" +
                        "<right style=\"thin\"><color indexed=\"64\"/></right>" +
                        "<top style=\"thin\"><color indexed=\"64\"/></top>" +
                        "<bottom style=\"thin\"><color indexed=\"64\"/></bottom><diagonal/></border>");

                bool aligned = s.Wrap || s.Align != null || s.VerticalAlign != null;
                xfs.Append($"<xf numFmtId=\"0\" fontId=\"{fontId}\" fillId=\"{fillId}\" borderId=\"{borderId}\" xfId=\"0\"")
                   .Append(fillId > 0 ? " applyFill=\"1\"" : "")
                   .Append(borderId > 0 ? " applyBorder=\"1\"" : "")
                   .Append(" applyFont=\"1\"")
                   .Append(aligned ? " applyAlignment=\"1\">" : "/>");
                if (aligned)
                {
                    xfs.Append("<alignment")
                       .Append(s.Align != null ? " horizontal=\"" + s.Align + "\"" : "")
                       .Append(" vertical=\"" + (s.VerticalAlign ?? "top") + "\"")
                       .Append(s.Wrap ? " wrapText=\"1\"" : "")
                       .Append("/></xf>");
                }
            }

            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sb.Append($"<fonts count=\"{fonts.Count}\">").Append(string.Concat(fonts)).Append("</fonts>");
            sb.Append($"<fills count=\"{fills.Count}\">").Append(string.Concat(fills)).Append("</fills>");
            sb.Append($"<borders count=\"{borders.Count}\">").Append(string.Concat(borders)).Append("</borders>");
            sb.Append("<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>");
            sb.Append($"<cellXfs count=\"{styles.Count}\">").Append(xfs).Append("</cellXfs>");
            sb.Append("<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>");
            sb.Append("</styleSheet>");
            return sb.ToString();
        }

        private static string Sheet(XlsxSheet sheet, List<XlsxStyle> styles, bool hasDrawing)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
                      "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");

            // <cols> antes de <sheetData>, <mergeCells> depois, <drawing> por último: a ordem é do esquema.
            if (sheet.ColumnWidths.Count > 0)
            {
                sb.Append("<cols>");
                foreach (var w in sheet.ColumnWidths.OrderBy(kv => kv.Key))
                    sb.Append($"<col min=\"{w.Key}\" max=\"{w.Key}\" width=\"{w.Value.ToString(Inv)}\" customWidth=\"1\"/>");
                sb.Append("</cols>");
            }

            sb.Append("<sheetData>");
            foreach (var row in sheet.Cells.GroupBy(c => c.Row).OrderBy(g => g.Key))
            {
                double height;
                sb.Append($"<row r=\"{row.Key}\"")
                  .Append(sheet.RowHeights.TryGetValue(row.Key, out height)
                      ? $" ht=\"{height.ToString(Inv)}\" customHeight=\"1\""
                      : "")
                  .Append(">");
                foreach (XlsxCell c in row.OrderBy(c => c.Column))
                {
                    int styleIndex = c.Style == null ? 0 : Math.Max(styles.IndexOf(c.Style), 0);
                    string reference = XlsxSheet.CellRef(c.Row, c.Column);
                    if (c.Number.HasValue)
                        sb.Append($"<c r=\"{reference}\" s=\"{styleIndex}\"><v>{c.Number.Value.ToString(Inv)}</v></c>");
                    else if (!string.IsNullOrEmpty(c.Text))
                        sb.Append($"<c r=\"{reference}\" s=\"{styleIndex}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">")
                          .Append(Xml(c.Text)).Append("</t></is></c>");
                    else
                        sb.Append($"<c r=\"{reference}\" s=\"{styleIndex}\"/>");
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData>");

            if (sheet.Merges.Count > 0)
            {
                sb.Append($"<mergeCells count=\"{sheet.Merges.Count}\">");
                foreach (XlsxMerge m in sheet.Merges)
                    sb.Append($"<mergeCell ref=\"{XlsxSheet.CellRef(m.FromRow, m.FromColumn)}:{XlsxSheet.CellRef(m.ToRow, m.ToColumn)}\"/>");
                sb.Append("</mergeCells>");
            }

            if (hasDrawing) sb.Append("<drawing r:id=\"rId1\"/>");
            sb.Append("</worksheet>");
            return sb.ToString();
        }

        private static string Drawing(List<XlsxImage> images)
        {
            const string xdr = "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
            const string a = "http://schemas.openxmlformats.org/drawingml/2006/main";
            const string r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append($"<xdr:wsDr xmlns:xdr=\"{xdr}\" xmlns:a=\"{a}\" xmlns:r=\"{r}\">");

            for (int i = 0; i < images.Count; i++)
            {
                XlsxImage img = images[i];
                int wPx = img.WidthPx, hPx = img.HeightPx;
                if (wPx <= 0 || hPx <= 0) TryReadPngSize(img.Png, out wPx, out hPx);
                if (wPx <= 0 || hPx <= 0) { wPx = 300; hPx = 300; }

                long cx = (long)wPx * EmuPerPixel, cy = (long)hPx * EmuPerPixel;
                int id = i + 2;   // 1 fica para o próprio desenho

                // oneCellAnchor: prende o canto superior esquerdo numa célula e mantém o tamanho.
                // A imagem FLUTUA sobre a grade — é assim que a folha MD é montada hoje.
                sb.Append("<xdr:oneCellAnchor>")
                  .Append("<xdr:from>")
                  .Append($"<xdr:col>{img.Column - 1}</xdr:col><xdr:colOff>0</xdr:colOff>")
                  .Append($"<xdr:row>{img.Row - 1}</xdr:row><xdr:rowOff>0</xdr:rowOff>")
                  .Append("</xdr:from>")
                  .Append($"<xdr:ext cx=\"{cx}\" cy=\"{cy}\"/>")
                  .Append("<xdr:pic>")
                  .Append("<xdr:nvPicPr>")
                  .Append($"<xdr:cNvPr id=\"{id}\" name=\"Imagem {i + 1}\"/><xdr:cNvPicPr><a:picLocks noChangeAspect=\"1\"/></xdr:cNvPicPr>")
                  .Append("</xdr:nvPicPr>")
                  .Append($"<xdr:blipFill><a:blip r:embed=\"rId{i + 1}\"/><a:stretch><a:fillRect/></a:stretch></xdr:blipFill>")
                  .Append("<xdr:spPr>")
                  .Append($"<a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{cx}\" cy=\"{cy}\"/></a:xfrm>")
                  .Append("<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom>")
                  .Append("</xdr:spPr>")
                  .Append("</xdr:pic>")
                  .Append("<xdr:clientData/>")
                  .Append("</xdr:oneCellAnchor>");
            }
            sb.Append("</xdr:wsDr>");
            return sb.ToString();
        }

        // ----------------------------------------------------------------- utilitários

        private static int Index(List<string> table, string item)
        {
            int i = table.IndexOf(item);
            if (i >= 0) return i;
            table.Add(item);
            return table.Count - 1;
        }

        private static void Write(ZipArchive zip, string entryName, string xml)
        {
            ZipArchiveEntry entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using (Stream s = entry.Open())
            using (var w = new StreamWriter(s, new UTF8Encoding(false)))
                w.Write(xml);
        }

        private static void WriteBinary(ZipArchive zip, string entryName, byte[] bytes)
        {
            ZipArchiveEntry entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using (Stream s = entry.Open())
                s.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Escapa para XML e derruba os caracteres de controle que o Excel recusa (um nome de
        /// arquivo vindo do CAD pode trazer qualquer coisa; \t, \n e \r são válidos e ficam).
        /// </summary>
        private static string Xml(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    case '\'': sb.Append("&apos;"); break;
                    default:
                        if (c >= 0x20 || c == '\t' || c == '\n' || c == '\r') sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
