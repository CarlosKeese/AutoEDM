using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AutoEDM.Electrode;

namespace AutoEDM.Reporting
{
    /// <summary>
    /// Conteúdo da Lista de corte fora da tela: a MESMA tabela em texto monoespaçado (bloco de
    /// notas, e-mail em texto puro), em HTML (Word, Excel e Outlook colam como tabela) e as peças
    /// soltas (título, linhas de cabeçalho, células, rodapé) que a impressão direta desenha — assim
    /// copiar e imprimir nunca divergem.
    /// </summary>
    public static class SawCutReportFormatter
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public const string Title = "LISTA DE CORTE — ELETRODOS";

        private static readonly string[] HeaderCells = { "Arquivo", "Posições", "Perfil", "Uso", "Código", "Corte (mm)" };
        private static readonly bool[] RightAligned = { false, true, false, false, false, true };

        /// <summary>Cabeçalho das colunas, na ordem de <see cref="ToCells"/>.</summary>
        public static IReadOnlyList<string> ColumnHeaders => HeaderCells;

        /// <summary>Coluna numérica (alinhada à direita)?</summary>
        public static bool IsRightAligned(int column) => RightAligned[column];

        /// <summary>As células de uma linha, já formatadas, na ordem de <see cref="ColumnHeaders"/>.</summary>
        public static string[] ToCells(SawCutListItem it)
        {
            SawCut cut = it.Cut ?? new SawCut();
            return new[]
            {
                it.FileName ?? "—",
                it.Positions.ToString(Inv),
                cut.Blank == null ? "NÃO IDENTIFICADO" : cut.Blank.Name + (cut.Blank.Material != null ? " " + cut.Blank.Material : ""),
                cut.Orientation ?? "—",
                cut.Blank?.Code ?? "—",
                cut.CutMm.HasValue ? cut.CutMm.Value.ToString("0", Inv) : "—",
            };
        }

        /// <summary>Linhas abaixo do título: montagem, data e a regra do corte.</summary>
        public static string[] InfoLines(string assemblyName, DateTime when, double allowanceMm = SawCutPlanner.DefaultAllowanceMm) =>
            new[]
            {
                "Montagem: " + (string.IsNullOrWhiteSpace(assemblyName) ? "—" : assemblyName),
                "Data: " + when.ToString("dd/MM/yyyy HH:mm", Inv),
                $"Corte: comprimento do eletrodo no eixo da barra, arredondado para cima, + {allowanceMm.ToString("0.#", Inv)} mm de sobremetal",
            };

        /// <summary>Totais e, se houver, o aviso de linhas sem medida (sempre a partir da 2ª linha).</summary>
        public static string[] FooterLines(IEnumerable<SawCutListItem> items)
        {
            var list = (items ?? Enumerable.Empty<SawCutListItem>()).ToList();
            var lines = new List<string> { $"Total: {list.Count} arquivo(s), {list.Sum(i => i.Positions)} posição(ões)" };
            int pending = PendingCount(list);
            // "ATENÇÃO:" e não "⚠": o símbolo não existe em toda fonte de impressora.
            if (pending > 0)
                lines.Add($"ATENÇÃO: {pending} eletrodo(s) sem medida de corte — escolha o perfil na janela antes de imprimir");
            return lines.ToArray();
        }

        /// <summary>Linhas sem medida de corte (perfil não identificado ou que não comporta o eletrodo).</summary>
        public static int PendingCount(IEnumerable<SawCutListItem> items) =>
            (items ?? Enumerable.Empty<SawCutListItem>()).Count(i => i.Cut == null || !i.Cut.CutMm.HasValue);

        /// <summary>Tabela em texto monoespaçado, com cabeçalho e totais.</summary>
        public static string ToText(IEnumerable<SawCutListItem> items, string assemblyName, DateTime when,
            double allowanceMm = SawCutPlanner.DefaultAllowanceMm)
        {
            var list = (items ?? Enumerable.Empty<SawCutListItem>()).ToList();
            var rows = list.Select(ToCells).ToList();

            int[] width = HeaderCells.Select(h => h.Length).ToArray();
            foreach (var r in rows)
                for (int c = 0; c < width.Length; c++) width[c] = Math.Max(width[c], r[c].Length);

            var sb = new StringBuilder();
            sb.AppendLine(Title);
            foreach (string line in InfoLines(assemblyName, when, allowanceMm)) sb.AppendLine(line);
            sb.AppendLine();
            sb.AppendLine(Line(HeaderCells, width));
            sb.AppendLine(string.Join("  ", width.Select(w => new string('-', w))));
            foreach (var r in rows) sb.AppendLine(Line(r, width));
            sb.AppendLine();
            foreach (string line in FooterLines(list)) sb.AppendLine(line);
            return sb.ToString();
        }

        /// <summary>A mesma tabela em HTML (só o fragmento — ver <see cref="ToClipboardHtml"/>).</summary>
        public static string ToHtmlFragment(IEnumerable<SawCutListItem> items, string assemblyName, DateTime when,
            double allowanceMm = SawCutPlanner.DefaultAllowanceMm)
        {
            var list = (items ?? Enumerable.Empty<SawCutListItem>()).ToList();
            const string cell = "border:1px solid #000;padding:2pt 6pt;";

            var sb = new StringBuilder();
            sb.Append("<div style=\"font-family:Arial,sans-serif;font-size:10pt\">");
            sb.Append("<p style=\"margin:0 0 6pt 0\"><b style=\"font-size:12pt\">").Append(Html(Title)).Append("</b>");
            foreach (string line in InfoLines(assemblyName, when, allowanceMm)) sb.Append("<br>").Append(Html(line));
            sb.Append("</p>");

            sb.Append("<table style=\"border-collapse:collapse;font-family:Arial,sans-serif;font-size:10pt\"><tr>");
            for (int c = 0; c < HeaderCells.Length; c++)
                sb.Append("<th style=\"").Append(cell).Append("background:#d9d9d9;text-align:")
                  .Append(RightAligned[c] ? "right" : "left").Append("\">").Append(Html(HeaderCells[c])).Append("</th>");
            sb.Append("</tr>");
            foreach (var r in list.Select(ToCells))
            {
                sb.Append("<tr>");
                for (int c = 0; c < r.Length; c++)
                    sb.Append("<td style=\"").Append(cell).Append("text-align:")
                      .Append(RightAligned[c] ? "right" : "left").Append("\">").Append(Html(r[c])).Append("</td>");
                sb.Append("</tr>");
            }
            sb.Append("</table>");

            string[] footer = FooterLines(list);
            sb.Append("<p style=\"margin:6pt 0 0 0\">").Append(Html(footer[0]));
            for (int i = 1; i < footer.Length; i++) sb.Append("<br><b>").Append(Html(footer[i])).Append("</b>");
            sb.Append("</p></div>");
            return sb.ToString();
        }

        /// <summary>
        /// Envelopa o fragmento no formato "HTML Format" da área de transferência do Windows: um
        /// cabeçalho com os OFFSETS EM BYTES UTF-8 do documento e do fragmento. Sem esse cabeçalho o
        /// Word/Excel colam o HTML como texto cru. Quem põe na área de transferência tem de gravar
        /// os bytes de <see cref="ToClipboardHtmlBytes"/>, não a string — ver lá.
        /// </summary>
        public static string ToClipboardHtml(string htmlFragment)
        {
            const string header =
                "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
            const string pre = "<html><head><meta charset=\"utf-8\"></head><body><!--StartFragment-->";
            const string post = "<!--EndFragment--></body></html>";
            string fragment = htmlFragment ?? "";

            var utf8 = Encoding.UTF8;
            int startHtml = utf8.GetByteCount(string.Format(Inv, header, 0, 0, 0, 0)); // D10: tamanho fixo
            int startFragment = startHtml + utf8.GetByteCount(pre);
            int endFragment = startFragment + utf8.GetByteCount(fragment);
            int endHtml = endFragment + utf8.GetByteCount(post);
            return string.Format(Inv, header, startHtml, endHtml, startFragment, endFragment) + pre + fragment + post;
        }

        /// <summary>
        /// <see cref="ToClipboardHtml"/> já em bytes UTF-8 (sem BOM), para gravar como STREAM na área
        /// de transferência. Dentro do Solid Edge, <c>DataObject.SetData(DataFormats.Html, string)</c>
        /// NÃO grava UTF-8: o WinForms só faz isso quando o processo foi compilado para .NET ≥ 4.5, e
        /// o Edge.exe é nativo — cai no modo antigo, que converte para ANSI. Os offsets do cabeçalho
        /// (em bytes UTF-8) deixam de bater e o Excel mostra "Posi綷s", "NÏ IDENTIFICADO"
        /// (Carlos, 2026-09-14).
        /// </summary>
        public static byte[] ToClipboardHtmlBytes(string htmlFragment) =>
            new UTF8Encoding(false).GetBytes(ToClipboardHtml(htmlFragment));

        private static string Line(string[] cells, int[] width) =>
            string.Join("  ", cells.Select((s, c) => RightAligned[c] ? s.PadLeft(width[c]) : s.PadRight(width[c]))).TrimEnd();

        // Não WebUtility.HtmlEncode: ele troca "ç"/"é" por &#231; — válido, mas ilegível no HTML colado.
        private static string Html(string s) =>
            (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
