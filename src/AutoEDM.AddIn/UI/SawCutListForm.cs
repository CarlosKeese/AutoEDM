using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AutoEDM.Diagnostics;
using AutoEDM.Electrode;
using AutoEDM.Reporting;

namespace AutoEDM.AddIn.UI
{
    /// <summary>
    /// Janela "Lista de corte" (Carlos, 2026-09-14): uma linha por ARQUIVO de eletrodo selecionado
    /// na montagem — posições, perfil de cobre do estoque, medida na serra com sobremetal. O perfil
    /// vem identificado pelas medidas da peça; quando não bate (ou bate com mais de um), o usuário
    /// troca na própria grade e a medida de corte é recalculada. "Copiar para impressão" põe a
    /// tabela na área de transferência em texto e em HTML, na ordem em que a grade está.
    /// </summary>
    public sealed class SawCutListForm : Form
    {
        private readonly IReadOnlyList<BlankSpec> _catalog;
        private readonly string _assemblyName;
        private readonly DataGridView _grid;
        private readonly Label _status;
        private bool _filling;
        private readonly IReadOnlyList<SawCutListItem> _items;
        private readonly Func<SawCutListItem, Image> _thumbnailProvider;

        /// <summary>Item da lista de perfis (precisa ser público com propriedades p/ o data binding do combo).</summary>
        public sealed class ProfileOption
        {
            public string Key { get; set; }
            public string Label { get; set; }
        }

        /// <param name="thumbnailProvider">Gera a miniatura de um item (lê a peça no SE). Null = imprime sem imagens.</param>
        public SawCutListForm(IReadOnlyList<SawCutListItem> items, IReadOnlyList<BlankSpec> catalog, string assemblyName,
            Func<SawCutListItem, Image> thumbnailProvider = null)
        {
            items = items ?? new SawCutListItem[0];
            _items = items;
            _thumbnailProvider = thumbnailProvider;
            _catalog = catalog ?? new BlankSpec[0];
            _assemblyName = assemblyName;

            Text = $"AutoEDM — Lista de corte ({items.Count} eletrodo(s))";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1000, 420);
            MinimumSize = new Size(680, 260);

            _grid = new DataGridView
            {
                Left = 0, Top = 0, Width = ClientSize.Width, Height = ClientSize.Height - 44,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                EditMode = DataGridViewEditMode.EditOnEnter,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithAutoHeaderText,
                BackgroundColor = SystemColors.Window,
            };
            AddText("File", "Arquivo", 22);
            AddText("Positions", "Posições", 7, right: true);
            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "Profile",
                HeaderText = "Perfil (estoque)",
                FillWeight = 20,
                DataSource = ProfileOptions(),
                DisplayMember = nameof(ProfileOption.Label),
                ValueMember = nameof(ProfileOption.Key),
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                FlatStyle = FlatStyle.Flat,
                ToolTipText = "Identificado pelas medidas da peça. Clique para trocar — a medida de corte é recalculada.",
            });
            AddText("Use", "Uso", 7);
            AddText("Size", "Medidas X × Y × Z (mm)", 14, right: true);
            AddText("Cut", "Corte c/ sobremetal (mm)", 10, right: true);
            AddText("Notes", "Observações", 30);
            _grid.Columns["Cut"].ToolTipText =
                $"Comprimento do eletrodo no eixo da barra (em pé = altura Z; deitado = lado maior da pegada), " +
                $"arredondado para cima + {SawCutPlanner.DefaultAllowanceMm:0.#} mm de sobremetal.";
            _grid.Columns["Cut"].DefaultCellStyle.Font = new Font(_grid.Font, FontStyle.Bold);

            _filling = true;
            foreach (var it in items)
            {
                int r = _grid.Rows.Add();
                _grid.Rows[r].Tag = it;
                FillRow(_grid.Rows[r]);
            }
            _filling = false;

            // O combo só grava o valor ao sair da célula; commit imediato recalcula na hora.
            _grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewComboBoxCell)
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _grid.CellValueChanged += OnCellValueChanged;
            _grid.DataError += (s, e) => { e.ThrowException = false; };
            Controls.Add(_grid);

            var btnClose = new Button
            {
                Text = "Fechar", DialogResult = DialogResult.OK,
                Width = 100, Height = 28,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            };
            btnClose.Left = ClientSize.Width - btnClose.Width - 8;
            btnClose.Top = ClientSize.Height - btnClose.Height - 8;
            Controls.Add(btnClose);

            var btnCopy = new Button
            {
                Text = "Copiar para impressão",
                Width = 170, Height = 28,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            };
            btnCopy.Left = btnClose.Left - btnCopy.Width - 8;
            btnCopy.Top = btnClose.Top;
            btnCopy.Click += (s, e) => CopyForPrint();
            Controls.Add(btnCopy);

            var btnPrint = new Button
            {
                Text = "Imprimir...",
                Width = 110, Height = 28,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            };
            btnPrint.Left = btnCopy.Left - btnPrint.Width - 8;
            btnPrint.Top = btnClose.Top;
            btnPrint.Click += (s, e) => PrintDirect();
            Controls.Add(btnPrint);

            _status = new Label
            {
                AutoSize = false,
                Left = 8, Top = btnClose.Top, Height = 28, Width = btnPrint.Left - 16,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Text = "Linhas em laranja pedem conferência. Troque o perfil clicando na coluna \"Perfil\".",
            };
            Controls.Add(_status);

            AcceptButton = btnClose;
            CancelButton = btnClose;
        }

        private void AddText(string name, string header, float weight, bool right = false)
        {
            var col = new DataGridViewTextBoxColumn { Name = name, HeaderText = header, FillWeight = weight, ReadOnly = true };
            if (right) col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns.Add(col);
        }

        private List<ProfileOption> ProfileOptions()
        {
            var list = new List<ProfileOption> { new ProfileOption { Key = "", Label = "(não identificado)" } };
            list.AddRange(_catalog.Select(b => new ProfileOption { Key = b.Code, Label = b.Describe() }));
            return list;
        }

        private static void FillRow(DataGridViewRow row)
        {
            var it = (SawCutListItem)row.Tag;
            SawCut cut = it.Cut ?? new SawCut();

            row.Cells["File"].Value = it.FileName ?? "—";
            row.Cells["Positions"].Value = it.Positions;
            row.Cells["Profile"].Value = cut.Blank?.Code ?? "";
            row.Cells["Use"].Value = cut.Orientation ?? "—";
            row.Cells["Size"].Value = it.SizeKnown ? $"{it.SizeXmm:0.0} × {it.SizeYmm:0.0} × {it.SizeZmm:0.0}" : "—";
            row.Cells["Cut"].Value = cut.CutMm.HasValue ? cut.CutMm.Value.ToString("0") : "—";

            var notes = new List<string>(it.Notes);
            if (!string.IsNullOrEmpty(cut.Note)) notes.Add(cut.Note);
            if (cut.Blank != null && !cut.AutoIdentified) notes.Add("perfil escolhido à mão");
            row.Cells["Notes"].Value = string.Join(" | ", notes);

            bool attention = !cut.CutMm.HasValue || !string.IsNullOrEmpty(cut.Note) || it.Notes.Count > 0;
            row.DefaultCellStyle.ForeColor = attention ? Color.FromArgb(160, 90, 0) : SystemColors.ControlText;
        }

        private void OnCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_filling || e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "Profile") return;

            DataGridViewRow row = _grid.Rows[e.RowIndex];
            var it = (SawCutListItem)row.Tag;
            string code = row.Cells["Profile"].Value as string;
            BlankSpec blank = _catalog.FirstOrDefault(b => b.Code == code);

            if (blank == null)
                it.Cut = new SawCut { Note = "sem perfil — escolha um para ter a medida de corte" };
            else if (!it.SizeKnown)
                it.Cut = new SawCut { Blank = blank, Note = "medidas da peça não lidas — sem medida de corte" };
            else
                it.Cut = SawCutPlanner.ForBlank(blank, it.SizeXmm, it.SizeYmm, it.SizeZmm);

            _filling = true;
            try { FillRow(row); }
            finally { _filling = false; }
        }

        /// <summary>Linhas na ordem em que a grade está (o usuário pode ter ordenado por coluna).</summary>
        private List<SawCutListItem> GridItems()
        {
            _grid.EndEdit();
            return _grid.Rows.Cast<DataGridViewRow>().Select(r => (SawCutListItem)r.Tag).ToList();
        }

        private void PrintDirect()
        {
            List<SawCutListItem> items = GridItems();
            int pending = SawCutReportFormatter.PendingCount(items);
            if (pending > 0 && MessageBox.Show(this,
                    $"{pending} eletrodo(s) ainda sem medida de corte (perfil não identificado).\n\nImprimir mesmo assim?",
                    "AutoEDM — Lista de corte", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            GenerateThumbnails(items);
            try
            {
                using (var job = new SawCutPrintJob(items, _assemblyName, DateTime.Now))
                using (var preview = new SawCutPrintPreviewForm(job))
                {
                    if (preview.ShowDialog(this) != DialogResult.OK)
                    {
                        _status.Text = "Visualização fechada sem imprimir.";
                        return;
                    }
                    _status.Text = $"Enviado para a impressora \"{job.Document.PrinterSettings.PrinterName}\".";
                    Log.Info($"Lista de corte: impressa em '{job.Document.PrinterSettings.PrinterName}' ({items.Count} linha(s)).");
                }
            }
            catch (Exception ex)
            {
                _status.Text = "A impressão falhou — " + ex.GetBaseException().Message;
                Log.Warn("Lista de corte: impressão falhou — " + ex.GetBaseException().Message);
            }
        }

        /// <summary>
        /// Miniatura de cada item que ainda não tem — lida da peça no SE na hora de imprimir (uma vez
        /// só: a janela guarda para as próximas impressões). Falha vira "sem imagem" na folha, nunca
        /// impede a impressão.
        /// </summary>
        private void GenerateThumbnails(List<SawCutListItem> items)
        {
            if (_thumbnailProvider == null) return;
            var pending = items.Where(i => i.Thumbnail == null && !i.ThumbnailTried).ToList();
            if (pending.Count == 0) return;

            Cursor old = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                for (int i = 0; i < pending.Count; i++)
                {
                    SawCutListItem it = pending[i];
                    _status.Text = $"Gerando miniaturas {i + 1}/{pending.Count}: {it.FileName}...";
                    _status.Refresh();
                    try { it.Thumbnail = _thumbnailProvider(it); }
                    catch (Exception ex) { Log.Warn($"Lista de corte: miniatura de '{it.FileName}' falhou — {ex.GetBaseException().Message}"); }
                    it.ThumbnailTried = true;
                }
                int missing = pending.Count(i => i.Thumbnail == null);
                _status.Text = missing == 0 ? "Miniaturas prontas." : $"{missing} miniatura(s) não gerada(s) — veja o log.";
            }
            finally
            {
                Cursor.Current = old;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                foreach (SawCutListItem it in _items)
                {
                    it.Thumbnail?.Dispose();
                    it.Thumbnail = null;
                }
            base.Dispose(disposing);
        }

        private void CopyForPrint()
        {
            List<SawCutListItem> items = GridItems();
            DateTime now = DateTime.Now;
            string text = SawCutReportFormatter.ToText(items, _assemblyName, now);
            byte[] html = SawCutReportFormatter.ToClipboardHtmlBytes(SawCutReportFormatter.ToHtmlFragment(items, _assemblyName, now));

            var data = new DataObject();
            data.SetData(DataFormats.UnicodeText, text);
            // Bytes UTF-8 prontos, como STREAM: a string passaria pela conversão ANSI do modo antigo
            // do WinForms dentro do Edge.exe (ver ToClipboardHtmlBytes).
            data.SetData(DataFormats.Html, new System.IO.MemoryStream(html));
            try
            {
                Clipboard.SetDataObject(data, true, 5, 100);
                _status.Text = "Copiado — cole no Word, Excel ou e-mail para imprimir.";
                Log.Info("Lista de corte: tabela copiada para a área de transferência.\r\n" + text);
            }
            catch (Exception ex)
            {
                _status.Text = "Não consegui copiar (área de transferência ocupada) — tente de novo.";
                Log.Warn("Lista de corte: cópia falhou — " + ex.GetBaseException().Message);
            }
        }
    }
}
