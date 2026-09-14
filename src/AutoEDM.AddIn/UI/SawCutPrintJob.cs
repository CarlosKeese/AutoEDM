using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Linq;
using AutoEDM.Electrode;
using AutoEDM.Reporting;

namespace AutoEDM.AddIn.UI
{
    /// <summary>
    /// "Imprimir" da Lista de corte (Carlos, 2026-09-14): desenha na impressora a MESMA tabela do
    /// "Copiar para impressão" (<see cref="SawCutReportFormatter"/>), sem passar por Word/Excel, com a
    /// miniatura isométrica de cada eletrodo (<see cref="SawCutListItem.Thumbnail"/>) na 1ª coluna
    /// quando houver. Colunas de texto medidas pelo conteúdo; se não couberem na largura, a fonte
    /// diminui até caber (e, no limite, o nome do arquivo é abreviado com "…"). O cabeçalho da tabela
    /// se repete a cada página. Unidades: as do <see cref="PrintPageEventArgs"/> (1/100 pol.).
    /// </summary>
    public sealed class SawCutPrintJob : IDisposable
    {
        private const float BaseFontPt = 10f;
        private const float MinFontPt = 7f;
        private const float CellPadX = 6f;
        private const float CellPadY = 4f;
        /// <summary>Lado da miniatura impressa (1/100 pol.) — 1 polegada.</summary>
        private const float ThumbSize = 100f;
        private const string ThumbHeader = "Eletrodo";

        private readonly string[] _info;
        private readonly string[] _footer;
        private readonly string[] _headers;
        private readonly List<string[]> _rows;
        private readonly List<Image> _thumbs;
        private readonly bool _withThumbs;
        private readonly Brush _headerFill = new SolidBrush(Color.FromArgb(217, 217, 217));

        private Font _font, _bold, _title;
        private float[] _widths;
        private int _nextRow, _page;
        private bool _done;

        public PrintDocument Document { get; }

        /// <summary>Páginas do último trabalho (visualização ou impressão).</summary>
        public int PageCount { get; private set; }

        public SawCutPrintJob(IEnumerable<SawCutListItem> items, string assemblyName, DateTime when)
        {
            var list = (items ?? Enumerable.Empty<SawCutListItem>()).ToList();
            _info = SawCutReportFormatter.InfoLines(assemblyName, when);
            _footer = SawCutReportFormatter.FooterLines(list);
            _headers = SawCutReportFormatter.ColumnHeaders.ToArray();
            _rows = list.Select(SawCutReportFormatter.ToCells).ToList();
            _thumbs = list.Select(i => i.Thumbnail).ToList();
            _withThumbs = _thumbs.Any(t => t != null);

            Document = new PrintDocument
            {
                DocumentName = "Lista de corte" + (string.IsNullOrWhiteSpace(assemblyName) ? "" : " - " + assemblyName),
            };
            Document.DefaultPageSettings.Margins = new Margins(50, 50, 50, 50);
            // Zera a cada trabalho: a mesma instância vai para a visualização e depois para a impressora.
            Document.BeginPrint += (s, e) => { _nextRow = 0; _page = 0; _done = false; _widths = null; };
            Document.EndPrint += (s, e) => PageCount = _page;
            Document.PrintPage += OnPrintPage;
        }

        private float ThumbColumnWidth => _withThumbs ? ThumbSize + 2 * CellPadY : 0f;

        private void OnPrintPage(object sender, PrintPageEventArgs e)
        {
            Graphics g = e.Graphics;
            RectangleF area = e.MarginBounds;
            if (_widths == null) FitColumns(g, area.Width - ThumbColumnWidth);
            _page++;

            float lineH = _font.GetHeight(g) + 1;
            float headerH = _bold.GetHeight(g) + 2 * CellPadY;
            float rowH = Math.Max(headerH, _withThumbs ? ThumbSize + 2 * CellPadY : 0f);
            float bottom = area.Bottom - lineH - 4; // reserva do número da página
            float y = area.Top;

            if (_page == 1)
            {
                g.DrawString(SawCutReportFormatter.Title, _title, Brushes.Black, area.Left, y);
                y += _title.GetHeight(g) + 4;
                foreach (string line in _info)
                {
                    DrawText(g, line, _font, new RectangleF(area.Left, y, area.Width, lineH), false);
                    y += lineH;
                }
                y += 10;
            }

            bool drewSomething = false;
            if (_nextRow < _rows.Count)
            {
                y = DrawRow(g, area.Left, y, headerH, _headers, _bold, null, true);
                // Pelo menos uma linha por página, senão uma página baixa demais nunca termina.
                while (_nextRow < _rows.Count && (y + rowH <= bottom || !drewSomething))
                {
                    y = DrawRow(g, area.Left, y, rowH, _rows[_nextRow], _font, _thumbs[_nextRow], false);
                    _nextRow++;
                    drewSomething = true;
                }
            }

            if (_nextRow >= _rows.Count)
            {
                float need = 8 + lineH * _footer.Length;
                if (y + need <= bottom || !drewSomething)
                {
                    y += 8;
                    for (int i = 0; i < _footer.Length; i++)
                    {
                        DrawText(g, _footer[i], i == 0 ? _font : _bold, new RectangleF(area.Left, y, area.Width, lineH), false);
                        y += lineH;
                    }
                    _done = true;
                }
            }

            DrawText(g, $"Página {_page}", _font, new RectangleF(area.Left, area.Bottom - lineH, area.Width, lineH), true);
            e.HasMorePages = !_done;
        }

        private float DrawRow(Graphics g, float left, float y, float h, string[] cells, Font font, Image thumb, bool header)
        {
            float x = left;
            if (_withThumbs)
            {
                float w = ThumbColumnWidth;
                if (header) g.FillRectangle(_headerFill, x, y, w, h);
                g.DrawRectangle(Pens.Black, x, y, w, h);
                if (header)
                    DrawText(g, ThumbHeader, font, new RectangleF(x + CellPadX, y, w - 2 * CellPadX, h), false);
                else if (thumb != null)
                    DrawThumb(g, thumb, new RectangleF(x + CellPadY, y + (h - ThumbSize) / 2, ThumbSize, ThumbSize));
                else
                    DrawText(g, "sem imagem", font, new RectangleF(x + CellPadX, y, w - 2 * CellPadX, h), false);
                x += w;
            }

            for (int c = 0; c < _widths.Length; c++)
            {
                if (header) g.FillRectangle(_headerFill, x, y, _widths[c], h);
                g.DrawRectangle(Pens.Black, x, y, _widths[c], h);
                DrawText(g, cells[c], font, new RectangleF(x + CellPadX, y, _widths[c] - 2 * CellPadX, h),
                    SawCutReportFormatter.IsRightAligned(c));
                x += _widths[c];
            }
            return y + h;
        }

        private static void DrawThumb(Graphics g, Image img, RectangleF box)
        {
            float s = Math.Min(box.Width / img.Width, box.Height / img.Height);
            float w = img.Width * s, h = img.Height * s;
            InterpolationMode old = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(img, box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
            g.InterpolationMode = old;
        }

        private static void DrawText(Graphics g, string text, Font font, RectangleF box, bool right)
        {
            using (var sf = new StringFormat(StringFormatFlags.NoWrap)
            {
                Alignment = right ? StringAlignment.Far : StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
            })
            {
                g.DrawString(text ?? "", font, Brushes.Black, box, sf);
            }
        }

        /// <summary>Largura de cada coluna de texto = maior texto dela; diminui a fonte até a tabela caber.</summary>
        private void FitColumns(Graphics g, float width)
        {
            for (float pt = BaseFontPt; ; pt -= 0.5f)
            {
                DisposeFonts();
                _font = new Font("Arial", pt);
                _bold = new Font("Arial", pt, FontStyle.Bold);
                _title = new Font("Arial", pt + 4, FontStyle.Bold);

                _widths = new float[_headers.Length];
                for (int c = 0; c < _headers.Length; c++)
                {
                    float w = g.MeasureString(_headers[c], _bold).Width;
                    foreach (string[] r in _rows) w = Math.Max(w, g.MeasureString(r[c], _font).Width);
                    _widths[c] = w + 2 * CellPadX + 2;
                }

                float excess = _widths.Sum() - width;
                if (excess <= 0) return;
                if (pt - 0.5f < MinFontPt)
                {
                    // Nem na menor fonte: abrevia a coluna do arquivo (a mais larga e a única livre).
                    _widths[0] = Math.Max(_widths[0] - excess, width * 0.2f);
                    return;
                }
            }
        }

        private void DisposeFonts()
        {
            _font?.Dispose();
            _bold?.Dispose();
            _title?.Dispose();
            _font = _bold = _title = null;
        }

        /// <summary>Não descarta as miniaturas — elas são dos itens (a janela da lista reaproveita).</summary>
        public void Dispose()
        {
            DisposeFonts();
            _headerFill.Dispose();
            Document.Dispose();
        }
    }
}
