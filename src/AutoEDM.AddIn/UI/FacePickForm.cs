using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using AutoEDM.Diagnostics;
using AutoEDM.Electrode;
using AutoEDM.Selection;

namespace AutoEDM.AddIn.UI
{
    /// <summary>O que o botão devolve ao criar: a janela só precisa saber se deu certo e o que dizer.</summary>
    public sealed class FacePickResult
    {
        public bool Created { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// O que muda de um botão para outro numa janela de seleção de faces: textos, o painel de
    /// opções (opcional) e a ação de criar. O resto — assumir o mouse, a face à vista pelo
    /// raio, a lista, o realce — é o mesmo para todos.
    /// </summary>
    public sealed class FacePickSpec
    {
        public string Title;               // barra da janela
        public string Instructions;        // texto do topo (até 3 linhas)
        public string CreateCaption;       // botão de criar
        public string LogTag;              // prefixo das linhas de log
        public string NextHint;            // anexado à mensagem depois de criar
        public string MultiOwnerNote;      // aviso quando as faces vêm de mais de uma peça
        /// <summary>Painel de opções, entre a lista e o relatório. Largura 456; altura livre.</summary>
        public Control Options;
        /// <summary>Chamado a cada mudança na lista (para o painel mostrar prévia).</summary>
        public Action<IList<PickedFace>> SelectionChanged;
        public Func<object, IList<PickedFace>, FacePickResult> Create;
    }

    /// <summary>
    /// Janela de seleção de faces na MONTAGEM, clique a clique, compartilhada por "Criar
    /// eletrodo (manual)" e "Nova peça" (Carlos, 2026-09-24). Nasceu como a janela do eletrodo.
    ///
    /// Antes o botão exigia as faces PRÉ-SELECIONADAS — e, na montagem, pegar a face em vez
    /// da ocorrência inteira pede o clique duplo ou o Alt, que ninguém lembra. Agora o botão
    /// abre esta janela e ela assume o mouse da SE (<see cref="SePicker"/>, filtro de FACE):
    /// cada clique no modelo soma uma face, clicar de novo na mesma face a tira, e "Criar
    /// eletrodo" cria e posiciona a peça. A janela continua aberta e já recomeça a coleta —
    /// um eletrodo atrás do outro, sem voltar à ribbon.
    ///
    /// MODELESS pelo mesmo motivo do O'ring: uma janela modal congela a SE justamente na hora
    /// de clicar no modelo. A janela só ORQUESTRA: quem desembrulha a face é
    /// <see cref="ElectrodeBuilder.TryUnwrapFace"/> e quem cria é o delegate que a ribbon passa
    /// (<see cref="ElectrodeBuilder.CreateElectrodeFromFaces"/> com a configuração da sessão).
    /// </summary>
    public sealed class FacePickForm : Form
    {
        /// <summary>Uma face escolhida: o objeto clicado (para o realce, que na montagem precisa
        /// do embrulho com a ocorrência) e a face já desembrulhada (para o núcleo).</summary>
        private sealed class Pick
        {
            public object Graphic;
            public PickedFace Face;
            public string Key;              // ocorrência + ID da face — detecta o 2º clique na mesma face
            public double[] Min, Max;       // caixa em mm, coordenadas da PEÇA
        }

        private readonly dynamic _app;      // Application do add-in — NUNCA desconecta
        private readonly FacePickSpec _spec;
        private readonly List<Pick> _picks = new List<Pick>();

        private SePicker _picker;           // null = a SE não deixou; cai no plano B (SelectSet)
        private bool _picking;
        private object _highlight;          // HighlightSet do que já foi escolhido

        /// <summary>Peças visíveis + pose + caixa, lidas no 1º clique e reaproveitadas nos
        /// seguintes (<see cref="VisibleScene"/>). Null = ler de novo no próximo clique: depois de
        /// criar eletrodo, em "Atualizar peças" e quando um proxy guardado morre.</summary>
        private VisibleScene _scene;

        private Button _btnPick, _btnUndo, _btnClear, _btnFromSelection, _btnCreate, _btnClose, _btnRefresh;

        private ListBox _list;
        private Label _lblSummary;
        private TextBox _txtReport;
        private readonly ToolTip _tips = new ToolTip();

        public FacePickForm(object app, FacePickSpec spec)
        {
            _app = app;
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            if (spec.Create == null) throw new ArgumentException("FacePickSpec.Create é obrigatório.", nameof(spec));
            BuildUi();
        }

        private string Tag => _spec.LogTag ?? "Seleção de faces";

        /// <summary>Documento FRESCO a cada operação — o Application é o único proxy que
        /// sobrevive a tudo (ver a regra de RPC_E_DISCONNECTED do projeto).</summary>
        private dynamic Doc()
        {
            try { return _app?.ActiveDocument; } catch { return null; }
        }

        // ------------------------------------------------------------------ interface

        private void BuildUi()
        {
            Text = _spec.Title ?? "AutoEDM — Seleção de faces";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;              // fica visível por cima da SE enquanto se seleciona
            StartPosition = FormStartPosition.Manual;   // encostada à direita — ver OnLoad
            Font = new Font("Segoe UI", 9F);

            int y = 12;
            Add(new Label
            {
                Left = 12, Top = y, Width = 456, Height = 48,
                Text = _spec.Instructions ?? "Clique direto no modelo, nas FACES — uma por clique. " +
                       "Clicar de novo numa face já escolhida a retira."
            });
            y += 54;

            _btnPick = Button("Selecionar faces", 12, y, 140, (s, e) => StartPicking());
            _tips.SetToolTip(_btnPick, "Assume o mouse do Solid Edge para escolher faces no modelo. " +
                                       "Esc encerra a coleta; clicar aqui retoma.");
            _btnUndo = Button("Remover última", 160, y, 110, (s, e) => RemoveLast());
            _btnClear = Button("Limpar", 278, y, 80, (s, e) => ClearPicks());
            _btnFromSelection = Button("Usar seleção", 366, y, 102, (s, e) => LoadFromSelectSet(explain: true));
            _tips.SetToolTip(_btnFromSelection, "Soma as faces que já estão selecionadas no Solid Edge " +
                                                "(o jeito antigo: clique duplo ou Alt+clique na face).");
            y += 34;


            _list = Add(new ListBox
            {
                Left = 12, Top = y, Width = 456, Height = 130,
                Font = new Font("Consolas", 9F), IntegralHeight = false
            });
            y += 136;

            _lblSummary = Add(new Label { Left = 12, Top = y, Width = 456, Height = 36, ForeColor = Color.Firebrick });
            y += 40;

            if (_spec.Options != null)
            {
                _spec.Options.Left = 12; _spec.Options.Top = y; _spec.Options.Width = 456;
                Add(_spec.Options);
                y += _spec.Options.Height + 6;
            }

            _txtReport = Add(new TextBox
            {
                Left = 12, Top = y, Width = 456, Height = 96,
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                BackColor = Color.White
            });
            y += 104;

            _btnRefresh = Button("Atualizar peças", 12, y, 120, (s, e) =>
            {
                _scene = null;
                Report("As peças serão relidas no próximo clique (posição e visibilidade).");
            });
            _tips.SetToolTip(_btnRefresh, "A janela guarda quais peças estão visíveis e onde estão, para o clique ser rápido. " +
                                          "Moveu, escondeu ou mostrou alguma peça com a janela aberta? Clique aqui.");
            _btnCreate = Button(_spec.CreateCaption ?? "Criar", 250, y, 120, (s, e) => Create());
            _btnClose = Button("Fechar", 378, y, 90, (s, e) => Close());
            AcceptButton = _btnCreate;

            ClientSize = new Size(480, y + 26 + 12);
            RefreshList();
        }

        /// <summary>Encosta a janela no lado DIREITO da tela, como a do O'ring — longe do
        /// PathFinder e deixando o modelo livre para os cliques.</summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            const int margin = 8;
            Location = new Point(
                area.Right - Width - margin,
                Math.Max(area.Top + margin, area.Top + (area.Height - Height) / 2));
        }

        /// <summary>
        /// Abre já coletando, como um recurso da SE. O que estava selecionado ANTES de clicar no
        /// botão entra na lista — quem já tinha o hábito de pré-selecionar não perde o clique.
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LoadFromSelectSet(explain: false);
            StartPicking();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            StopPicking();
            ClearHighlight();
            if (_picker != null) { _picker.Dispose(); _picker = null; }
            base.OnFormClosed(e);
        }

        private T Add<T>(T c) where T : Control { Controls.Add(c); return c; }

        private Button Button(string text, int left, int top, int width, EventHandler onClick)
        {
            var b = Add(new Button { Text = text, Left = left, Top = top, Width = width, Height = 26 });
            b.Click += onClick;
            return b;
        }

        // ------------------------------------------------------------------ seleção

        /// <summary>
        /// TODOS os tipos de face. Só com <c>seLocateFace</c> a SE localizava apenas face PLANA
        /// na montagem (relato do Carlos, 2026-09-24) — a queima real é quase sempre curva
        /// (B-spline, revolvida, projetada/cilíndrica, concordância = toro/cone). O enum traz um
        /// filtro por tipo de superfície, e a lista abaixo é ele inteiro.
        /// </summary>
        private static readonly SolidEdgeConstants.seLocateFilterConstants[] AnyFace =
        {
            SolidEdgeConstants.seLocateFilterConstants.seLocateFace,
            SolidEdgeConstants.seLocateFilterConstants.seLocatePlane,
            SolidEdgeConstants.seLocateFilterConstants.seLocateCone,
            SolidEdgeConstants.seLocateFilterConstants.seLocateSphere,
            SolidEdgeConstants.seLocateFilterConstants.seLocateTorus,
            SolidEdgeConstants.seLocateFilterConstants.seLocateProjectedFace,
            SolidEdgeConstants.seLocateFilterConstants.seLocateRevolvedFace,
            SolidEdgeConstants.seLocateFilterConstants.seLocateBspSurfaceFace,
            SolidEdgeConstants.seLocateFilterConstants.seLocateRuledFace,
        };

        /// <remarks>
        /// Modo de localização FIXO em SmartLocate, com o clique aceito SEM geometria (logs de
        /// 2026-09-24, montagem MD-15335): quem decide a face é o raio de visão pelo pixel do
        /// cursor, então a face que a SE localiza não é necessária. Os outros modos atrapalham —
        /// no Simples nenhum <c>MouseClick</c> chega (o clique cai na seleção nativa, que pega a
        /// ocorrência inteira); no QuickPick o clique chega, mas o realce de passagem acende a
        /// peça de TRÁS antes do clique e engana. No SmartLocate o clique chega e nada acende.
        /// </remarks>
        private void StartPicking()
        {
            StopPicking();
            if (Picker() != null && _picker.Start(
                    "AutoEDM — clique nas FACES do fundo do bolsão (de novo na mesma face para tirar; Esc encerra).",
                    SolidEdgeConstants.seLocateModes.seSmartLocate, AnyFace))
            {
                _picking = true;
                UpdatePickButton();
                return;
            }

            // Plano B: a SE não deixou assumir o mouse. Sobra o jeito antigo — selecionar no SE
            // (clique duplo ou Alt+clique) e somar pelo botão "Usar seleção".
            Report("Não deu para assumir o mouse do Solid Edge (o log diz por quê).\r\n" +
                   "Modo reserva: selecione as faces no Solid Edge (clique duplo ou Alt+clique) e use \"Usar seleção\".");
        }

        private void StopPicking()
        {
            if (_picker != null) _picker.Stop();
            _picking = false;
            UpdatePickButton();
        }

        private void UpdatePickButton()
        {
            if (_btnPick == null) return;
            _btnPick.Text = _picking ? "Clique nas faces…" : "Selecionar faces";
            _btnPick.BackColor = _picking ? Color.FromArgb(255, 244, 205) : SystemColors.Control;
            _btnPick.UseVisualStyleBackColor = !_picking;
            _btnPick.FlatStyle = _picking ? FlatStyle.Flat : FlatStyle.Standard;
        }

        /// <summary>Cria o picker na primeira vez; devolve null se a SE não colaborar.</summary>
        private SePicker Picker()
        {
            if (_picker != null) return _picker;
            try
            {
                _picker = new SePicker((SolidEdgeFramework.Application)_app) { AcceptEmptyClicks = true };
                _picker.Picked += OnPicked;
                _picker.Cancelled += OnPickCancelled;
                _picker.Missed += OnPickMissed;
            }
            catch (Exception e)
            {
                Log.Warn($"{Tag}: seleção por etapas indisponível — " + e.GetBaseException().Message);
                _picker = null;
            }
            return _picker;
        }

        /// <summary>
        /// Chegou um clique. Sai do evento COM antes de mexer em qualquer coisa: encerrar ou
        /// reiniciar o comando de dentro do próprio callback dele é pedir para a SE reclamar.
        /// </summary>
        private void OnPicked(object graphic)
        {
            // Ponto e janela lidos AQUI, ainda dentro do callback: é o que monta o raio de visão.
            double[] point = _picker?.LastPointM;
            object window = _picker?.LastWindow;
            System.Drawing.Point screen = _picker?.LastScreenPoint ?? System.Drawing.Point.Empty;
            try { BeginInvoke(new Action(() => Accept(graphic, point, window, screen))); }
            catch (Exception e) { Log.Error($"{Tag}: seleção não pôde ser processada.", e); }
        }

        private void OnPickMissed()
        {
            try
            {
                BeginInvoke(new Action(() => Report(
                    "O clique não localizou nenhuma face" +
                    "." +
                    " Clique sobre uma face do modelo.")));
            }
            catch { /* janela já fechando */ }
        }

        private void OnPickCancelled()
        {
            try
            {
                BeginInvoke(new Action(() =>
                {
                    _picking = false;
                    UpdatePickButton();
                    Report($"Coleta encerrada. \"Selecionar faces\" retoma; \"{_spec.CreateCaption}\" cria com as faces da lista.");
                }));
            }
            catch { /* janela já fechando */ }
        }

        /// <summary>Soma (ou tira, se já estava) a face clicada e continua coletando.</summary>
        private void Accept(object graphic, double[] pointM, object window, System.Drawing.Point screen)
        {
            StopPicking();
            try
            {
                if (_picks.Count == 0 && graphic != null) Log.Info($"{Tag}: 1º clique — tipo do objeto: " + ComTypeName(graphic));

                // A face que a SE localizou não respeita a profundidade (peça de trás ganhava da
                // da frente). Quem decide é o raio de visão; a da SE só vale se o raio falhar.
                VisiblePick seen = PickByView(pointM, window, screen);
                if (seen?.Face != null) Toggle(seen.HighlightItem, "clique (vista)", resolved: seen.Face);
                else
                {
                    if (graphic == null)
                        Report("Nenhuma face à vista sob o cursor (o log diz por quê). Clique sobre uma face do modelo.");
                    else
                    {
                        Log.Warn($"{Tag}: raio de visão sem resultado — usando a face que a SE localizou.");
                        Toggle(graphic, "clique");
                    }
                }
            }
            finally { StartPicking(); }
        }

        /// <summary>A face à vista sob o clique, pelo raio de visão. Null se câmera/ponto faltarem.</summary>
        private VisiblePick PickByView(double[] pointM, object window, System.Drawing.Point screen)
        {
            try
            {
                // O ponto do raio sai do PIXEL do cursor, convertido pela própria vista. O (x, y, z)
                // do evento fica só de reserva: sondado ao vivo, ele não está sob o cursor — o raio
                // por ele acertava a placa de trás (2026-09-24).
                if (VisibleFacePicker.TryScreenToModel(window, screen.X, screen.Y, out double[] underCursor))
                {
                    if (pointM != null)
                        Log.Info(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "[vista] ponto do evento MouseClick ({0:0.###}, {1:0.###}, {2:0.###}) mm — só para comparação.",
                            pointM[0] * 1000, pointM[1] * 1000, pointM[2] * 1000));
                    pointM = underCursor;
                }
                else Log.Warn("[vista] sem o ponto sob o cursor — usando o ponto do evento MouseClick (pode acertar peça de trás).");
                if (pointM == null) return null;
                if (!VisibleFacePicker.TryReadCamera(window, out double[] eye, out double[] target, out bool perspective)) return null;
                if (!RayMath.TryViewRay(eye, target, perspective, pointM, out double[] origin, out double[] dir)) return null;
                dynamic doc = Doc();
                if (doc == null) return null;
                Cursor = Cursors.WaitCursor;
                if (_scene == null) _scene = VisibleScene.Build((object)doc);
                try { return VisibleFacePicker.PickNearest(doc, origin, dir, _scene); }
                catch (Exception e)
                {
                    // Proxy guardado morreu (peça recarregada, documento mexido): relê e tenta 1 vez.
                    Log.Warn($"{Tag}: cena guardada inválida, relendo — " + e.GetBaseException().Message);
                    _scene = VisibleScene.Build((object)doc);
                    return VisibleFacePicker.PickNearest(doc, origin, dir, _scene);
                }
            }
            catch (Exception e)
            {
                Log.Warn($"{Tag}: raio de visão falhou — " + e.GetBaseException().Message);
                return null;
            }
            finally { Cursor = Cursors.Default; }
        }

        /// <summary>O jeito antigo, mantido: o que está no SelectSet entra na lista.</summary>
        private void LoadFromSelectSet(bool explain)
        {
            dynamic doc = Doc();
            if (doc == null) return;
            int before = _picks.Count, n = 0;
            try
            {
                dynamic ss = doc.SelectSet;
                try { n = (int)ss.Count; } catch { }
                for (int i = 1; i <= n; i++)
                {
                    object item; try { item = ss.Item(i); } catch { continue; }
                    Toggle(item, $"SelectSet[{i}]", addOnly: true);
                }
            }
            catch (Exception e) { Log.Warn($"{Tag}: SelectSet ilegível — " + e.GetBaseException().Message); }

            int added = _picks.Count - before;
            if (explain)
                Report(added > 0
                    ? $"{added} face(s) somada(s) da seleção do Solid Edge."
                    : n == 0 ? "Nada selecionado no Solid Edge." : "A seleção do Solid Edge não tem FACE (veja o log).");
            else if (added > 0)
                Report($"{added} face(s) que já estavam selecionadas entraram na lista. Continue clicando no modelo.");
        }

        /// <param name="graphic">O que vai para o realce (embrulho da seleção ou referência de montagem); pode ser null.</param>
        /// <param name="resolved">Face já decidida (pelo raio de visão); null = desembrulhar <paramref name="graphic"/>.</param>
        private void Toggle(object graphic, string where, bool addOnly = false, PickedFace resolved = null)
        {
            PickedFace face = resolved ?? ElectrodeBuilder.TryUnwrapFace(graphic, where);
            if (face == null)
            {
                Report("O que foi clicado não é uma face utilizável (o log mostra o que veio). Clique numa FACE.");
                return;
            }

            string key = KeyOf(face);
            int existing = key == null ? -1 : _picks.FindIndex(k => k.Key == key);
            if (existing >= 0)
            {
                if (addOnly) return;
                _picks.RemoveAt(existing);
                RebuildHighlight();
                RefreshList();
                return;
            }

            var pick = new Pick { Graphic = graphic, Face = face, Key = key };
            if (FaceGeometry.TryGetRangeMm(face.Face, out double[] mn, out double[] mx)) { pick.Min = mn; pick.Max = mx; }
            Log.Info($"{Tag}: +face {key ?? "(sem ID)"} de '{face.OccurrenceName ?? "?"}'" +
                     (pick.Min == null ? ", sem caixa." :
                      $", caixa na peça X {pick.Min[0]:0.0}…{pick.Max[0]:0.0}  Y {pick.Min[1]:0.0}…{pick.Max[1]:0.0}  Z {pick.Min[2]:0.0}…{pick.Max[2]:0.0} mm."));
            _picks.Add(pick);
            if (graphic != null) AddHighlight(graphic);
            RefreshList();
        }

        private void RemoveLast()
        {
            if (_picks.Count == 0) return;
            _picks.RemoveAt(_picks.Count - 1);
            RebuildHighlight();
            RefreshList();
        }

        private void ClearPicks()
        {
            _picks.Clear();
            ClearHighlight();
            RefreshList();
        }

        /// <summary>
        /// Identidade de uma face entre cliques: a ocorrência + o <c>Face.ID</c> (estável dentro do
        /// corpo). Dois proxies COM da mesma face NÃO são o mesmo objeto .NET, então comparar por
        /// referência nunca acharia o 2º clique. Sem ID legível, cada clique conta como face nova.
        /// </summary>
        private static string KeyOf(PickedFace face)
        {
            object id = GetQuiet(face.Face, "ID");
            return id == null ? null : (face.OccurrenceName ?? "?") + "#" + Convert.ToString(id);
        }

        // ------------------------------------------------------------------ lista e resumo

        private void RefreshList()
        {
            _list.BeginUpdate();
            try
            {
                _list.Items.Clear();
                for (int i = 0; i < _picks.Count; i++)
                {
                    Pick k = _picks[i];
                    string occ = k.Face.OccurrenceName ?? "(ocorrência ?)";
                    string box = k.Min == null ? "sem caixa"
                        : $"{k.Max[0] - k.Min[0]:0.0} × {k.Max[1] - k.Min[1]:0.0}  Z {k.Min[2]:0.0}…{k.Max[2]:0.0}";
                    _list.Items.Add($"{i + 1,2}. {occ}  —  {box}");
                }
            }
            finally { _list.EndUpdate(); }

            List<Pick> boxed = _picks.Where(k => k.Min != null).ToList();
            if (_picks.Count == 0)
            {
                _lblSummary.ForeColor = Color.Firebrick;
                _lblSummary.Text = "— nenhuma face escolhida —";
            }
            else
            {
                _lblSummary.ForeColor = Color.DarkGreen;
                string summary = $"{_picks.Count} face(s).";
                if (boxed.Count > 0)
                {
                    double x = boxed.Max(k => k.Max[0]) - boxed.Min(k => k.Min[0]);
                    double yy = boxed.Max(k => k.Max[1]) - boxed.Min(k => k.Min[1]);
                    double z = boxed.Max(k => k.Max[2]) - boxed.Min(k => k.Min[2]);
                    summary += $"  Pegada {x:0.0} × {yy:0.0} mm, altura {z:0.0} mm (na peça).";
                }
                int owners = _picks.Select(k => k.Face.OccurrenceName).Distinct().Count();
                if (owners > 1)
                {
                    _lblSummary.ForeColor = Color.DarkOrange;
                    summary += $"\r\nATENÇÃO: faces de {owners} peças — " + (_spec.MultiOwnerNote ?? "o centro sai de todas.");
                }
                _lblSummary.Text = summary;
            }
            _btnCreate.Enabled = _picks.Count > 0;
            _btnUndo.Enabled = _btnClear.Enabled = _picks.Count > 0;

            try { _spec.SelectionChanged?.Invoke(_picks.Select(k => k.Face).ToList()); }
            catch (Exception e) { Log.Warn($"{Tag}: prévia do painel falhou — " + e.GetBaseException().Message); }
        }

        private void Report(string text) { _txtReport.Text = text; }

        // ------------------------------------------------------------------ criar

        private void Create()
        {
            if (_picks.Count == 0) return;
            StopPicking();

            dynamic doc = Doc();
            int type = -1; try { type = (int)doc.Type; } catch { }
            if (doc == null || type != 3)   // igAssemblyDocument
            {
                Report($"O documento ativo não é mais a MONTAGEM. Volte para a janela da montagem e clique em \"{_spec.CreateCaption}\" de novo.");
                return;
            }

            FacePickResult res;
            Cursor = Cursors.WaitCursor;
            try
            {
                Log.Info($"{Tag} (janela): {_picks.Count} face(s).");
                res = _spec.Create((object)doc, _picks.Select(k => k.Face).ToList());
            }
            catch (Exception e)
            {
                Log.Error($"{Tag}: falha ao criar.", e);
                string logPath = ElectrodeAddIn.Current?.LogPath;
                Report("Não foi possível criar. " +
                       (string.IsNullOrEmpty(logPath) ? "Detalhes no log." : "Detalhes no log:\r\n" + logPath));
                StartPicking();
                return;
            }
            finally { Cursor = Cursors.Default; }

            if (res.Created)
            {
                // O que foi usado sai da lista: a próxima coleta é a próxima peça. A montagem
                // ganhou uma ocorrência: a cena é relida no próximo clique.
                _scene = null;
                ClearPicks();
                Report(res.Message + "\r\n\r\n" + (_spec.NextHint ?? "Pronto para a próxima: clique nas faces dela.") + " A montagem NÃO foi salva.");
            }
            else Report(res.Message);
            StartPicking();
        }

        // ------------------------------------------------------------------ realce

        private static int Rgb(int r, int g, int b) { return r | (g << 8) | (b << 16); } // COLORREF

        /// <summary>Realça no modelo o que já foi escolhido (cosmético: falha só no log).</summary>
        private void AddHighlight(object item)
        {
            try
            {
                dynamic doc = Doc();
                if (doc == null) return;
                if (_highlight == null) _highlight = doc.HighlightSets.Add();
                dynamic hs = _highlight;
                hs.Color = Rgb(255, 128, 0);
                hs.AddItem(item);
                hs.Draw();
            }
            catch (Exception e) { Log.Warn($"{Tag}: realce não aplicado — " + e.GetBaseException().Message); }
        }

        private void RebuildHighlight()
        {
            ClearHighlight();
            foreach (Pick k in _picks) if (k.Graphic != null) AddHighlight(k.Graphic);
        }

        private void ClearHighlight()
        {
            object s = _highlight;
            _highlight = null;
            if (s == null) return;
            try
            {
                dynamic hs = s;
                hs.RemoveAll();
                hs.Draw();
                hs.Delete();
            }
            catch { /* documento já fechado ou realce já sumiu */ }
        }

        // ------------------------------------------------------------------ COM

        private static object GetQuiet(object o, string name)
        {
            if (o == null) return null;
            try { return o.GetType().InvokeMember(name, BindingFlags.GetProperty, null, o, null); }
            catch { return null; }
        }

        private static string ComTypeName(object o)
        {
            try { return AutoEDM.Com.ComDiagnostics.TypeNameOf(o); }
            catch { return o?.GetType().Name ?? "null"; }
        }
    }
}
