using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AutoEDM.Reporting;
using AutoEDM.Revisions;

namespace AutoEDM.AddIn.UI
{
    /// <summary>
    /// Janela "Lista de modificações" (Carlos, 2026-09-17/18). A varredura traz do modelo o que ele
    /// sabe — quais peças têm grupo "Rev.N" e que operações estão nele; aqui o projetista escreve o
    /// que o modelo NÃO sabe: a descrição da alteração, as ações para a oficina e as caixas de
    /// serviço. Sai como .xlsx no layout da folha MD, para subir no Drive.
    ///
    /// Layout: cabeçalho do projeto em cima (preenchido pelo nome da pasta), a grade de peças no
    /// meio, e embaixo o detalhe da peça selecionada — ações numeradas e as três listas de caixas.
    /// Tudo que é digitado vai para o .json ao lado da montagem (<see cref="ChangeReportStore"/>),
    /// senão fechar a janela jogaria fora o trabalho.
    /// </summary>
    public sealed class ChangeReportForm : Form
    {
        private readonly ChangeReport _report;
        private readonly string _storePath;
        private readonly Func<PartChange, List<byte[]>> _thumbnailProvider;
        private readonly DataGridView _grid;
        private readonly TextBox _actions;
        private readonly Dictionary<string, CheckedListBox> _taskLists = new Dictionary<string, CheckedListBox>();
        private readonly Dictionary<string, TextBox> _header = new Dictionary<string, TextBox>();
        private readonly Label _status;
        private PartChange _current;
        private bool _filling;

        /// <param name="thumbnailProvider">Rende as miniaturas da peça em PNG (lê a peça no SE). Null = planilha sem imagem.</param>
        public ChangeReportForm(ChangeReport report, string storePath, Func<PartChange, List<byte[]>> thumbnailProvider = null)
        {
            _report = report ?? throw new ArgumentNullException(nameof(report));
            _storePath = storePath;
            _thumbnailProvider = thumbnailProvider;

            Text = $"AutoEDM — Lista de modificações (Rev. {_report.Revision}, {_report.Parts.Count} peça(s))";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1120, 680);
            MinimumSize = new Size(900, 560);

            Panel header = BuildHeader();
            Controls.Add(header);

            _grid = BuildGrid();
            _grid.Top = header.Bottom + 6;
            _grid.Height = 210;
            Controls.Add(_grid);

            var split = new SplitContainer
            {
                Left = 8,
                Top = _grid.Bottom + 6,
                Width = ClientSize.Width - 16,
                Height = ClientSize.Height - _grid.Bottom - 56,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                SplitterDistance = 420,
                FixedPanel = FixedPanel.Panel1,
            };
            Controls.Add(split);

            split.Panel1.Controls.Add(new Label
            {
                Text = "AÇÕES INDICADAS (uma por linha — saem numeradas 1, 2, 3 na folha)",
                Dock = DockStyle.Top, Height = 32, ForeColor = Color.FromArgb(160, 60, 0),
            });
            _actions = new TextBox
            {
                Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill,
                AcceptsReturn = true, WordWrap = true,
            };
            _actions.TextChanged += (s, e) => { if (!_filling) CaptureActions(); };
            split.Panel1.Controls.Add(_actions);
            split.Panel1.Controls.SetChildIndex(_actions, 0);

            BuildTaskLists(split.Panel2);

            var btnClose = new Button { Text = "Fechar", Width = 100, Height = 28, DialogResult = DialogResult.OK };
            var btnXlsx = new Button { Text = "Gerar planilha (.xlsx)...", Width = 190, Height = 28 };
            var btnHtml = new Button { Text = "Copiar (HTML)", Width = 140, Height = 28 };
            foreach (Button b in new[] { btnClose, btnXlsx, btnHtml })
            {
                b.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                b.Top = ClientSize.Height - b.Height - 8;
                Controls.Add(b);
            }
            btnClose.Left = ClientSize.Width - btnClose.Width - 8;
            btnXlsx.Left = btnClose.Left - btnXlsx.Width - 8;
            btnHtml.Left = btnXlsx.Left - btnHtml.Width - 8;
            btnXlsx.Click += (s, e) => GerarXlsx();
            btnHtml.Click += (s, e) => CopiarHtml();

            _status = new Label
            {
                Left = 8, Top = btnClose.Top, Height = 28, Width = btnHtml.Left - 16,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Text = _storePath == null
                    ? "Sem caminho da montagem — o preenchimento NÃO será guardado."
                    : "O que você digitar fica guardado em " + System.IO.Path.GetFileName(_storePath) + ".",
            };
            Controls.Add(_status);

            AcceptButton = btnClose;
            CancelButton = btnClose;
            FormClosing += (s, e) => Salvar();

            FillGrid();
            if (_grid.Rows.Count > 0) _grid.Rows[0].Selected = true;
            ShowPart(_report.Parts.FirstOrDefault());
        }

        // ------------------------------------------------------------------ montagem da janela

        private Panel BuildHeader()
        {
            var panel = new Panel { Left = 0, Top = 0, Width = ClientSize.Width, Height = 76, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            int x = 8;
            AddHeaderField(panel, "PA", _report.ProductCode, 90, ref x);
            AddHeaderField(panel, "PN", _report.PartNumbers, 90, ref x);
            AddHeaderField(panel, "PM", _report.MoldBaseCode, 90, ref x);
            AddHeaderField(panel, "MD", _report.MoldCode, 90, ref x);
            AddHeaderField(panel, "RVPA", _report.Rvpa, 90, ref x);
            AddHeaderField(panel, "Responsáveis", string.Join(" / ", _report.Responsibles), 260, ref x);

            panel.Controls.Add(new Label
            {
                Left = 8, Top = 48, Width = panel.Width - 16, Height = 22,
                Text = "Diretório: " + (_report.ProjectDirectory ?? "—") +
                       "    |    Montagem: " + (_report.AssemblyName ?? "—"),
                ForeColor = SystemColors.GrayText,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            });
            return panel;
        }

        private void AddHeaderField(Panel panel, string label, string value, int width, ref int x)
        {
            panel.Controls.Add(new Label { Left = x, Top = 6, Width = width, Height = 16, Text = label, ForeColor = SystemColors.GrayText });
            var box = new TextBox { Left = x, Top = 22, Width = width, Text = value ?? "" };
            _header[label] = box;
            panel.Controls.Add(box);
            x += width + 8;
        }

        private DataGridView BuildGrid()
        {
            var grid = new DataGridView
            {
                Left = 8, Width = ClientSize.Width - 16,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
                BackgroundColor = SystemColors.Window,
            };
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Include", HeaderText = "Sai", FillWeight = 5 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "File", HeaderText = "Arquivo", FillWeight = 18, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rev", HeaderText = "Rev.", FillWeight = 5, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "Descrição da alteração (você escreve)", FillWeight = 34 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Operations", HeaderText = "Operações do grupo (lidas do modelo)", FillWeight = 26, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Marked", HeaderText = "Ações / caixas", FillWeight = 12, ReadOnly = true });
            grid.Columns["Rev"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewCheckBoxCell)
                    grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            grid.CellValueChanged += OnGridEdited;
            grid.SelectionChanged += (s, e) =>
            {
                if (_filling || grid.CurrentRow == null) return;
                ShowPart(grid.CurrentRow.Tag as PartChange);
            };
            grid.DataError += (s, e) => { e.ThrowException = false; };
            return grid;
        }

        private void BuildTaskLists(Control host)
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            foreach (string group in ChangeTaskCatalog.Groups)
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / ChangeTaskCatalog.Groups.Count));

            int column = 0;
            foreach (string group in ChangeTaskCatalog.Groups)
            {
                layout.Controls.Add(new Label { Text = group, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(160, 60, 0) }, column, 0);
                var list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
                // ItemCheck dispara ANTES de a caixa mudar de estado, então o valor novo vem do
                // EVENTO (e.NewValue) — e não de um GetItemChecked adiado por BeginInvoke, que era
                // como estava e explodia: preencher a lista com uma caixa JÁ MARCADA (vinda do
                // .json) dispara ItemCheck ainda dentro do construtor, antes de a janela ter
                // identificador — "Não é possível chamar Invoke ou BeginInvoke..." (Carlos, 2026-09-18).
                // Na 1ª abertura nada estava marcado, nenhum evento disparava e o defeito não aparecia.
                list.ItemCheck += (s, e) =>
                {
                    if (_filling || e.Index < 0 || e.Index >= list.Items.Count) return;
                    ((TaskItem)list.Items[e.Index]).Task.Checked = e.NewValue == CheckState.Checked;
                    RefreshCurrentRow();
                };
                _taskLists[group] = list;
                layout.Controls.Add(list, column, 1);
                column++;
            }
            host.Controls.Add(layout);
        }

        // ------------------------------------------------------------------ dados

        private void FillGrid()
        {
            _filling = true;
            _grid.Rows.Clear();
            foreach (PartChange p in _report.Parts)
            {
                int r = _grid.Rows.Add();
                _grid.Rows[r].Tag = p;
                UpdateRow(_grid.Rows[r]);
            }
            _filling = false;
        }

        private void UpdateRow(DataGridViewRow row)
        {
            var p = (PartChange)row.Tag;
            row.Cells["Include"].Value = p.Include;
            row.Cells["File"].Value = p.FileName ?? "—";
            row.Cells["Rev"].Value = p.Revision > 0 ? p.Revision.ToString() : "—";
            row.Cells["Description"].Value = p.Description ?? "";
            row.Cells["Operations"].Value = p.IsNew
                ? "PEÇA NOVA — " + (p.RevisionSource ?? "revisão na propriedade do arquivo")
                : string.Join(", ", p.Features);

            int actions = p.Actions.Count(a => !string.IsNullOrWhiteSpace(a));
            int marked = p.CheckedTasks.Count();
            row.Cells["Marked"].Value = $"{actions} ação(ões), {marked} caixa(s)";

            // Laranja = ainda não dá para mandar para a oficina (sem descrição ou sem ação).
            row.DefaultCellStyle.ForeColor = p.Include && ChangeReportFormatter.IsPending(p)
                ? Color.FromArgb(160, 90, 0)
                : SystemColors.ControlText;
        }

        private void OnGridEdited(object sender, DataGridViewCellEventArgs e)
        {
            if (_filling || e.RowIndex < 0) return;
            DataGridViewRow row = _grid.Rows[e.RowIndex];
            var p = (PartChange)row.Tag;
            string column = _grid.Columns[e.ColumnIndex].Name;

            if (column == "Include") p.Include = Convert.ToBoolean(row.Cells["Include"].Value ?? false);
            else if (column == "Description") p.Description = Convert.ToString(row.Cells["Description"].Value);

            _filling = true;
            UpdateRow(row);
            _filling = false;
        }

        private void ShowPart(PartChange part)
        {
            _current = part;
            _filling = true;

            _actions.Enabled = part != null;
            _actions.Text = part == null ? "" : string.Join(Environment.NewLine, part.Actions);
            foreach (var pair in _taskLists)
            {
                CheckedListBox list = pair.Value;
                list.Items.Clear();
                list.Enabled = part != null;
                if (part == null) continue;
                foreach (ChangeTask t in part.Tasks.Where(t => t.Group == pair.Key))
                    list.Items.Add(new TaskItem(t), t.Checked);
            }

            _filling = false;
        }

        private void CaptureActions()
        {
            if (_current == null) return;
            _current.Actions.Clear();
            _current.Actions.AddRange(_actions.Lines.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()));
            RefreshCurrentRow();
        }

        private void RefreshCurrentRow()
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (!ReferenceEquals(row.Tag, _current)) continue;
                _filling = true;
                UpdateRow(row);
                _filling = false;
                return;
            }
        }

        /// <summary>Despeja o cabeçalho editado de volta no relatório (é ele que vai para a planilha).</summary>
        private void CaptureHeader()
        {
            _report.ProductCode = Value("PA");
            _report.PartNumbers = Value("PN");
            _report.MoldBaseCode = Value("PM");
            _report.MoldCode = Value("MD");
            _report.Rvpa = Value("RVPA");

            _report.Responsibles.Clear();
            string people = Value("Responsáveis");
            if (!string.IsNullOrWhiteSpace(people))
                _report.Responsibles.AddRange(people.Split('/').Select(s => s.Trim()).Where(s => s.Length > 0));
        }

        private string Value(string label)
        {
            TextBox box;
            return _header.TryGetValue(label, out box) && !string.IsNullOrWhiteSpace(box.Text) ? box.Text.Trim() : null;
        }

        private void Salvar()
        {
            CaptureHeader();
            if (_storePath == null) return;
            ChangeArchive archive = ChangeReportStore.Load(_storePath);
            ChangeReportStore.Save(_storePath, ChangeReportStore.Capture(archive, _report));
        }

        // ------------------------------------------------------------------ saídas

        private void GerarXlsx()
        {
            CaptureHeader();
            int pending = ChangeReportFormatter.PendingCount(_report);
            if (pending > 0 &&
                MessageBox.Show(
                    $"{pending} peça(s) sem descrição ou sem ação indicada (linhas em laranja).\n\nGerar a planilha assim mesmo?",
                    "AutoEDM — Lista de modificações", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            using (var dialog = new SaveFileDialog
            {
                Title = "Salvar a lista de modificações",
                Filter = "Planilha do Excel (*.xlsx)|*.xlsx",
                FileName = $"MD-{_report.MoldCode ?? "molde"} Rev.{_report.Revision} - lista de modificações.xlsx",
                InitialDirectory = _report.ProjectDirectory ?? "",
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    RenderThumbnails();
                    ChangeReportXlsx.Save(dialog.FileName, _report);
                    Salvar();
                    _status.Text = "Planilha gerada: " + dialog.FileName;
                    if (MessageBox.Show("Planilha gerada. Abrir agora?", "AutoEDM — Lista de modificações",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        System.Diagnostics.Process.Start(dialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Não deu para gravar a planilha:\n\n" + ex.Message,
                        "AutoEDM — Lista de modificações", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// Gera as miniaturas na HORA DE EXPORTAR, não ao abrir a janela: cada uma lê a malha da
        /// peça no Solid Edge e numa montagem de molde isso levaria a abertura a rastejar. Peça que
        /// falhar fica sem imagem — a folha sai assim mesmo, com "(sem miniatura)".
        /// </summary>
        private void RenderThumbnails()
        {
            if (_thumbnailProvider == null) return;
            Cursor previous = Cursor;
            Cursor = Cursors.WaitCursor;
            try
            {
                foreach (PartChange p in _report.IncludedParts)
                {
                    if (p.Thumbnails.Count > 0) continue;   // já geradas nesta sessão
                    _status.Text = "Gerando as miniaturas de " + p.FileName + "...";
                    _status.Refresh();
                    try
                    {
                        List<byte[]> images = _thumbnailProvider(p);
                        if (images != null) p.Thumbnails.AddRange(images.Where(b => b != null && b.Length > 0));
                    }
                    catch (Exception ex) { AutoEDM.Diagnostics.Log.Warn($"Miniatura de '{p.FileName}' falhou — {ex.Message}"); }
                }
            }
            finally { Cursor = previous; }
        }

        private void CopiarHtml()
        {
            CaptureHeader();
            try
            {
                string fragment = ChangeReportFormatter.ToHtmlFragment(_report);
                var data = new DataObject();
                // Bytes UTF-8 como STREAM: dentro do Edge.exe a string vira ANSI (ver ClipboardHtml).
                data.SetData(DataFormats.Html, new System.IO.MemoryStream(ChangeReportFormatter.ToClipboardHtmlBytes(_report)));
                data.SetData(DataFormats.UnicodeText, fragment);
                Clipboard.SetDataObject(data, true);
                _status.Text = "Tabela copiada — cole no e-mail ou no Word.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não deu para copiar:\n\n" + ex.Message, "AutoEDM — Lista de modificações",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Item da lista de caixas: mostra o rótulo e carrega a tarefa por trás.</summary>
        private sealed class TaskItem
        {
            public readonly ChangeTask Task;
            public TaskItem(ChangeTask task) { Task = task; }
            public override string ToString() =>
                Task.Label + (string.IsNullOrWhiteSpace(Task.Detail) ? "" : " " + Task.Detail);
        }
    }
}
