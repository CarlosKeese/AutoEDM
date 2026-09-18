using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AutoEDM.Reporting.Xlsx;
using AutoEDM.Revisions;

namespace AutoEDM.Reporting
{
    /// <summary>
    /// A "Lista de modificações" como PLANILHA (Carlos, 2026-09-18): monta o .xlsx no layout da
    /// folha MD que ele preenche à mão hoje, para subir no Drive e abrir com o Google Sheets.
    ///
    /// O .xlsx virou a saída principal depois do teste de importação: a imagem SOBREVIVE à
    /// conversão (fica flutuando por cima da célula, que é como ele monta mesmo). O
    /// <see cref="ChangeReportFormatter"/> (HTML) continua existindo para colar num e-mail.
    ///
    /// Desenho da página: em cima a CAPA — cabeçalho do projeto à esquerda, "LISTA DE PEÇAS
    /// ALTERADAS" à direita, como na planilha dele; embaixo um BLOCO por peça, empilhados.
    /// Lógica pura, sem COM.
    /// </summary>
    public static class ChangeReportXlsx
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // Colunas (1 = A). A é só uma margem estreita, como na folha original.
        private const int ColLabel = 2;      // B — rótulos
        private const int ColValue = 3;      // C — valores (mesclado até ColValueEnd)
        private const int ColValueEnd = 6;   // F
        private const int ColSummaryFile = 9;    // I — arquivo, na lista de peças alteradas
        private const int ColSummaryDesc = 10;   // J — descrição

        /// <summary>
        /// Onde as miniaturas flutuam: ao LADO da lista de caixas de seleção (colunas C e D), que é
        /// a parte alta e estreita do bloco — as imagens ocupam o vazio à direita dela, nenhuma
        /// linha precisa crescer e nada do texto fica coberto. Layout desenhado pelo Carlos na
        /// planilha de exemplo (2026-09-18); a 1ª versão ancorava na coluna do texto e obrigava a
        /// esticar a linha, o que desmontava o bloco.
        /// </summary>
        private const int ColImageLeft = 3;   // C — vista Z+
        private const int ColImageRight = 4;  // D — vista Z−

        private static XlsxStyle Title => new XlsxStyle { Bold = true, FontSize = 13, FontColor = "C00000" };
        private static XlsxStyle Label => new XlsxStyle { Bold = true, Fill = "D9D9D9", Border = true, VerticalAlign = "center" };
        private static XlsxStyle Head => new XlsxStyle { Bold = true, Fill = "D9D9D9", Border = true, Align = "center" };
        private static XlsxStyle Value => new XlsxStyle { Border = true, VerticalAlign = "center" };
        private static XlsxStyle Wrapped => new XlsxStyle { Border = true, Wrap = true };
        /// <summary>Título de bloco de caixas ("AÇÃO NECESSÁRIA") — cabeçalho de tabela, com fundo.</summary>
        private static XlsxStyle Section => new XlsxStyle { Bold = true, FontColor = "C00000", Fill = "D9D9D9", Border = true };
        /// <summary>Legenda da miniatura, centrada sobre a imagem.</summary>
        private static XlsxStyle Caption => new XlsxStyle { Bold = true, Fill = "D9D9D9", Border = true, Align = "center" };
        /// <summary>Uma caixa de seleção: contorno igual ao do resto da folha.</summary>
        private static XlsxStyle Task => new XlsxStyle { Border = true, VerticalAlign = "center" };
        private static XlsxStyle Plain => new XlsxStyle();

        /// <summary>Grava o relatório como .xlsx no caminho dado.</summary>
        public static void Save(string path, ChangeReport report) =>
            XlsxWriter.Save(path, Build(report));

        /// <summary>Monta a planilha em memória (é o que os testes conferem).</summary>
        public static XlsxSheet Build(ChangeReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var sheet = new XlsxSheet(XlsxSheet.SafeName("Rev." + report.Revision.ToString(Inv)));

            // Larguras do exemplo do Carlos: B comporta a caixa de seleção mais longa sem cortar,
            // C e D são as colunas das duas miniaturas (e, mescladas com E/F, comportam o caminho
            // de rede), e a lista da capa não espreme a descrição.
            sheet.Width(1, 2.4).Width(ColLabel, 43.5).Width(ColImageLeft, 46.5)
                 .Width(ColImageRight, 37.5).Width(5, 9).Width(ColValueEnd, 9).Width(7, 2.4)
                 .Width(8, 2.4).Width(ColSummaryFile, 29.5).Width(ColSummaryDesc, 55.5);

            int row = Cover(sheet, report);
            foreach (PartChange p in report.IncludedParts)
                row = PartBlock(sheet, report, p, row + 1);

            if (report.Responsibles.Count > 0)
                sheet.Set(row + 1, ColLabel, "Responsáveis: " + string.Join(" / ", report.Responsibles), Plain);
            return sheet;
        }

        /// <summary>Capa: título, cabeçalho do projeto e a lista de peças alteradas. Devolve a última linha usada.</summary>
        private static int Cover(XlsxSheet sheet, ChangeReport r)
        {
            sheet.Set(2, ColLabel, ChangeReport.Title, Title);
            sheet.Merge(2, ColLabel, 2, ColImageRight, Title);
            // "Rev." e o número nas duas últimas colunas do bloco — a coluna 7 é só respiro.
            sheet.Set(2, ColValueEnd - 1, "Rev.", Label);
            sheet.SetNumber(2, ColValueEnd, r.Revision, new XlsxStyle { Border = true, Align = "center", Bold = true });

            int row = 4;
            foreach (var f in ChangeReportFormatter.HeaderFields(r))
            {
                sheet.Set(row, ColLabel, f.Key + ":", Label);
                sheet.Set(row, ColValue, f.Value, Value);
                sheet.Merge(row, ColValue, row, ColValueEnd, Value);
                row++;
            }
            sheet.Set(row, ColLabel, "MONTAGEM:", Label);
            sheet.Set(row, ColValue, string.IsNullOrWhiteSpace(r.AssemblyName) ? "—" : r.AssemblyName, Value);
            sheet.Merge(row, ColValue, row, ColValueEnd, Value);
            row++;
            sheet.Set(row, ColLabel, "DATA:", Label);
            sheet.Set(row, ColValue, r.When.ToString("dd/MM/yyyy HH:mm", Inv), Value);
            sheet.Merge(row, ColValue, row, ColValueEnd, Value);

            // Lista de peças alteradas, à direita — a mesma posição da planilha do Carlos.
            sheet.Set(2, ColSummaryFile, "LISTA DE PEÇAS ALTERADAS", Head);
            sheet.Set(2, ColSummaryDesc, "DESCRIÇÃO", Head);
            int listRow = 3;
            foreach (PartChange p in r.IncludedParts)
            {
                string[] cells = ChangeReportFormatter.ToSummaryCells(p);
                sheet.Set(listRow, ColSummaryFile, cells[0], Value);
                sheet.Set(listRow, ColSummaryDesc, cells[1], Value);
                listRow++;
            }
            return Math.Max(row, listRow) + 1;
        }

        /// <summary>Um bloco por peça. Devolve a última linha usada.</summary>
        private static int PartBlock(XlsxSheet sheet, ChangeReport r, PartChange p, int row)
        {
            sheet.Set(row, ColLabel, "NOME DO ARQUIVO:", Label);
            sheet.Set(row, ColValue, p.FileName ?? "—", Value);
            sheet.Merge(row, ColValue, row, ColImageRight, Value);
            sheet.Set(row, ColValueEnd - 1, ChangeReportFormatter.Unchecked + " FEITO", Value);
            sheet.Merge(row, ColValueEnd - 1, row, ColValueEnd, Value);
            row++;

            sheet.Set(row, ColLabel, "LOCALIZAÇÃO DO ARQUIVO:", Label);
            sheet.Set(row, ColValue, p.FullPath ?? "—", Value);
            sheet.Merge(row, ColValue, row, ColValueEnd, Value);
            row++;

            sheet.Set(row, ColLabel, "DESCRIÇÃO DA ALTERAÇÃO:", Label);
            sheet.Set(row, ColValue, ChangeReportFormatter.ToSummaryCells(p)[1], Value);
            sheet.Merge(row, ColValue, row, ColValueEnd, Value);
            row++;

            sheet.Set(row, ColLabel, "SITUAÇÃO:", Label);
            sheet.Set(row, ColValue, ChangeReportFormatter.SituationText(p), Value);
            sheet.Merge(row, ColValue, row, ColValueEnd, Value);
            row++;

            // O que o MODELO diz que mudou. Vai na folha junto com o texto do projetista: quem
            // recebe a peça na oficina consegue conferir a descrição contra as operações reais
            // (Carlos, 2026-09-18 — na 1ª planilha de verdade isto tinha ficado só na janela).
            if (p.Features.Count > 0)
            {
                sheet.Set(row, ColLabel, $"OPERAÇÕES DO GRUPO (Rev. {r.Revision}):", Label);
                sheet.Set(row, ColValue, string.Join(", ", p.Features), Wrapped);
                sheet.Merge(row, ColValue, row, ColValueEnd, Wrapped);
                row++;
            }

            var acoes = p.Actions.Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
            sheet.Set(row, ColLabel, "AÇÕES INDICADAS:", Label);
            sheet.Set(row, ColValue, acoes.Count == 0
                ? "(nenhuma — preencher)"
                : string.Join("\n", acoes.Select((a, i) => (i + 1).ToString(Inv) + " - " + a)), Wrapped);
            sheet.Merge(row, ColValue, row, ColValueEnd);
            sheet.Height(row, Math.Max(15, 13.5 * Math.Max(acoes.Count, 1)));
            row++;

            bool first = true;
            foreach (string group in ChangeTaskCatalog.Groups)
            {
                List<ChangeTask> tasks = (p.Tasks ?? new List<ChangeTask>()).Where(t => t.Group == group).ToList();
                if (tasks.Count == 0) continue;

                sheet.Set(row, ColLabel, group, Section);
                // As miniaturas começam na MESMA linha do primeiro bloco de caixas: a lista tem
                // altura de sobra à direita, e a legenda fica alinhada com o título do bloco.
                if (first) { Illustrations(sheet, p, row); first = false; }
                row++;
                foreach (ChangeTask t in tasks)
                {
                    sheet.Set(row, ColLabel, TaskLine(t), Task);
                    row++;
                }
            }
            return row;
        }

        /// <summary>
        /// As miniaturas do bloco, ancoradas à direita das colunas de texto e a partir da PRIMEIRA
        /// linha dele: flutuam sobre células vazias, então nenhuma linha precisa crescer e nada do
        /// bloco fica coberto. Sem miniatura, uma nota no lugar — a folha não pode dar a entender
        /// que a peça não tem figura porque a figura falhou.
        /// </summary>
        private static void Illustrations(XlsxSheet sheet, PartChange p, int row)
        {
            var images = (p.Thumbnails ?? new List<byte[]>()).Where(b => b != null && b.Length > 0).ToList();
            if (images.Count == 0)
            {
                sheet.Set(row, ColImageLeft, "(sem miniatura)", Plain);
                return;
            }

            int[] columns = { ColImageLeft, ColImageRight };
            string[] captions = { "VISTA Z+ (de cima)", "VISTA Z− (de baixo)" };
            for (int i = 0; i < images.Count && i < columns.Length; i++)
            {
                sheet.Set(row, columns[i], captions[i], Caption);
                int w, h;
                if (!XlsxWriter.TryReadPngSize(images[i], out w, out h)) { w = 300; h = 300; }
                // +1 na linha: a legenda fica em cima, a imagem começa na linha seguinte.
                sheet.Images.Add(new XlsxImage { Row = row + 1, Column = columns[i], Png = images[i], WidthPx = w, HeightPx = h });
            }
        }

        /// <summary>"☑ SOLDA TIG / LASER", com o complemento digitado quando tem.</summary>
        private static string TaskLine(ChangeTask t) =>
            (t.Checked ? ChangeReportFormatter.Checked : ChangeReportFormatter.Unchecked) + " " + t.Label +
            (string.IsNullOrWhiteSpace(t.Detail) ? "" : " " + t.Detail);
    }
}
