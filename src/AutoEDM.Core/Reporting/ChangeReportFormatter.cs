using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AutoEDM.Revisions;

namespace AutoEDM.Reporting
{
    /// <summary>
    /// A "Lista de modificações" em HTML (Carlos, 2026-09-17): o mesmo conteúdo da planilha
    /// "REGISTRO DE REVISÕES E AÇÕES EM PROJETOS DE MOLDES" que hoje é montada à mão no Google
    /// Sheets. Sai como fragmento HTML para colar direto na planilha (ou no Word / e-mail) pela
    /// área de transferência — ver <see cref="ClipboardHtml"/>.
    ///
    /// Duas partes, como a folha: a CAPA (cabeçalho do projeto + lista de peças alteradas) e uma
    /// FOLHA por peça (arquivo, localização, ações indicadas numeradas e as caixas de seleção).
    ///
    /// IMAGEM: a miniatura NÃO vai embutida. Colar HTML com &lt;img&gt; no Google Sheets perde a
    /// imagem (e no Word um data: URI também costuma cair), então o relatório cita o ARQUIVO PNG que
    /// o add-in exporta ao lado — a imagem se insere uma vez, na planilha. Lógica pura, sem COM.
    /// </summary>
    public static class ChangeReportFormatter
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Caixa marcada / em branco. Glifos de fonte — bons no Sheets e no Word, NÃO em impressora P&amp;B.</summary>
        public const string Checked = "☑";
        public const string Unchecked = "☐";

        private const string Cell = "border:1px solid #000;padding:2pt 6pt;vertical-align:top;";
        private const string LabelCell = Cell + "background:#d9d9d9;font-weight:bold;";

        /// <summary>Cabeçalho do projeto, na ordem da planilha. Campo vazio vira "—" (é para preencher à mão).</summary>
        public static IEnumerable<KeyValuePair<string, string>> HeaderFields(ChangeReport r)
        {
            yield return Field("PRODUTO ACABADO (PA)", r.ProductCode);
            yield return Field("PART NUMBERS (PN's)", r.PartNumbers);
            yield return Field("PORTA MOLDE (PM)", r.MoldBaseCode);
            yield return Field("MOLDE (MD)", r.MoldCode);
            yield return Field("RVPA", r.Rvpa);
            yield return Field("DIRETÓRIO DO PROJETO", r.ProjectDirectory);
        }

        /// <summary>Uma linha da "LISTA DE PEÇAS ALTERADAS": arquivo + descrição.</summary>
        public static string[] ToSummaryCells(PartChange p) => new[]
        {
            p.FileName ?? "—",
            string.IsNullOrWhiteSpace(p.Description) ? "(sem descrição — preencher)" : p.Description,
        };

        /// <summary>Quantas peças ainda estão sem descrição ou sem nenhuma ação indicada.</summary>
        public static int PendingCount(ChangeReport r) => r == null ? 0 : r.IncludedParts.Count(IsPending);

        /// <summary>
        /// Peça que ainda não dá para mandar para a oficina. Sempre precisa de DESCRIÇÃO; ação
        /// indicada só é exigida de peça ALTERADA — peça nova muitas vezes é só "fabricar N", e
        /// cobrar uma ação ali deixaria a folha laranja à toa (Carlos, 2026-09-18).
        /// </summary>
        public static bool IsPending(PartChange p) =>
            p == null || string.IsNullOrWhiteSpace(p.Description) ||
            (!p.IsNew && p.Actions.All(string.IsNullOrWhiteSpace));

        /// <summary>"PEÇA NOVA — fabricar 2" ou "peça alterada". Linha de situação da folha.</summary>
        public static string SituationText(PartChange p) =>
            p == null ? "—"
            : p.IsNew ? $"PEÇA NOVA — fabricar {p.Positions}" +
                        (string.IsNullOrWhiteSpace(p.RevisionSource) ? "" : "  (" + p.RevisionSource + ")")
            : "peça alterada";

        /// <summary>O relatório inteiro em HTML (só o fragmento — ver <see cref="ClipboardHtml.Wrap"/>).</summary>
        public static string ToHtmlFragment(ChangeReport r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            var sb = new StringBuilder();
            sb.Append("<div style=\"font-family:Arial,sans-serif;font-size:10pt\">");

            sb.Append("<p style=\"margin:0 0 6pt 0\"><b style=\"font-size:13pt;color:#c00000\">")
              .Append(H(ChangeReport.Title)).Append("</b><br>")
              .Append(H($"Rev. {r.Revision}"))
              .Append(H("  |  Montagem: " + Or(r.AssemblyName)))
              .Append(H("  |  Data: " + r.When.ToString("dd/MM/yyyy HH:mm", Inv)))
              .Append("</p>");

            AppendHeaderTable(sb, r);
            AppendSummaryTable(sb, r);
            foreach (PartChange p in r.IncludedParts) AppendPartSheet(sb, p, r);

            int pending = PendingCount(r);
            sb.Append("<p style=\"margin:8pt 0 0 0\">")
              .Append(H($"Total: {r.IncludedParts.Count()} peça(s) alterada(s) na Rev. {r.Revision}"));
            if (pending > 0)
                sb.Append("<br><b>").Append(H($"ATENÇÃO: {pending} peça(s) sem descrição ou sem ação indicada")).Append("</b>");
            if (r.Responsibles.Count > 0)
                sb.Append("<br>").Append(H("Responsáveis: " + string.Join(" / ", r.Responsibles)));
            sb.Append("</p></div>");
            return sb.ToString();
        }

        /// <summary>O fragmento já embrulhado e em bytes, pronto para a área de transferência.</summary>
        public static byte[] ToClipboardHtmlBytes(ChangeReport r) => ClipboardHtml.Bytes(ToHtmlFragment(r));

        /// <summary>Página HTML completa — para gravar num arquivo e abrir no navegador.</summary>
        public static string ToHtmlPage(ChangeReport r) =>
            "<!DOCTYPE html>\r\n<html lang=\"pt-BR\"><head><meta charset=\"utf-8\">" +
            "<title>" + H(ChangeReport.Title) + "</title></head>" +
            "<body style=\"margin:16px\">" + ToHtmlFragment(r) + "</body></html>";

        // ----------------------------------------------------------------- partes

        private static void AppendHeaderTable(StringBuilder sb, ChangeReport r)
        {
            sb.Append("<table style=\"border-collapse:collapse;font-size:10pt\">");
            foreach (var f in HeaderFields(r))
                sb.Append("<tr><td style=\"").Append(LabelCell).Append("\">").Append(H(f.Key))
                  .Append("</td><td style=\"").Append(Cell).Append("\">").Append(H(f.Value)).Append("</td></tr>");
            sb.Append("</table>");
        }

        private static void AppendSummaryTable(StringBuilder sb, ChangeReport r)
        {
            sb.Append("<p style=\"margin:8pt 0 2pt 0\"><b>LISTA DE PEÇAS ALTERADAS</b></p>");
            sb.Append("<table style=\"border-collapse:collapse;font-size:10pt\">");
            sb.Append("<tr><th style=\"").Append(LabelCell).Append("\">Arquivo</th><th style=\"")
              .Append(LabelCell).Append("\">Descrição</th></tr>");
            foreach (PartChange p in r.IncludedParts)
            {
                string[] cells = ToSummaryCells(p);
                sb.Append("<tr><td style=\"").Append(Cell).Append("\">").Append(H(cells[0]))
                  .Append("</td><td style=\"").Append(Cell).Append("\">").Append(H(cells[1])).Append("</td></tr>");
            }
            sb.Append("</table>");
        }

        private static void AppendPartSheet(StringBuilder sb, PartChange p, ChangeReport r)
        {
            sb.Append("<p style=\"margin:12pt 0 2pt 0\"><b>").Append(H(Or(p.FileName)))
              .Append("</b>&nbsp;&nbsp;").Append(H(Unchecked + " FEITO")).Append("</p>");

            sb.Append("<table style=\"border-collapse:collapse;font-size:10pt\">");
            Row(sb, "LOCALIZAÇÃO DO ARQUIVO", Or(p.FullPath));
            Row(sb, "DESCRIÇÃO DA ALTERAÇÃO", ToSummaryCells(p)[1]);
            if (!string.IsNullOrWhiteSpace(p.ThumbnailFile))
                Row(sb, "ILUSTRAÇÃO", p.ThumbnailFile + "  (inserir a imagem nesta célula)");

            Row(sb, "SITUAÇÃO", SituationText(p));
            if (p.Features.Count > 0)
                Row(sb, $"OPERAÇÕES DO GRUPO (Rev. {r.Revision})", string.Join(", ", p.Features));

            var acoes = p.Actions.Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
            Row(sb, "AÇÕES INDICADAS", acoes.Count == 0
                ? "(nenhuma — preencher)"
                : string.Join("\n", acoes.Select((a, i) => (i + 1).ToString(Inv) + " - " + a)));

            foreach (string group in ChangeTaskCatalog.Groups)
            {
                List<ChangeTask> tasks = (p.Tasks ?? new List<ChangeTask>()).Where(t => t.Group == group).ToList();
                if (tasks.Count == 0) continue;
                Row(sb, group, string.Join("\n", tasks.Select(TaskLine)));
            }
            sb.Append("</table>");
        }

        /// <summary>"☑ SOLDA TIG / LASER" — com o complemento digitado, quando tem.</summary>
        private static string TaskLine(ChangeTask t) =>
            (t.Checked ? Checked : Unchecked) + " " + t.Label +
            (string.IsNullOrWhiteSpace(t.Detail) ? "" : " " + t.Detail);

        private static void Row(StringBuilder sb, string label, string value)
        {
            sb.Append("<tr><td style=\"").Append(LabelCell).Append("\">").Append(H(label))
              .Append("</td><td style=\"").Append(Cell).Append("\">")
              .Append(H(value).Replace("\n", "<br>")).Append("</td></tr>");
        }

        private static KeyValuePair<string, string> Field(string label, string value) =>
            new KeyValuePair<string, string>(label, Or(value));

        private static string Or(string s) => string.IsNullOrWhiteSpace(s) ? "—" : s;

        private static string H(string s) => ClipboardHtml.Escape(s);
    }
}
