using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AutoEDM.Com;
using AutoEDM.Diagnostics;
using AutoEDM.Sealing;

namespace AutoEDM.AddIn.UI
{
    /// <summary>
    /// Janela do botão "Alojamento de O'ring" (ambiente de PEÇA).
    ///
    /// É a única janela MODELESS do AutoEDM, e de propósito: o fluxo é "clique numa face,
    /// depois numa aresta", e uma janela modal CONGELA o Solid Edge — não daria para clicar
    /// em nada no modelo com ela aberta. Modeless roda na mesma thread STA da SE, que
    /// continua bombeando mensagens; por isso as chamadas COM daqui saem no contexto certo,
    /// sem thread extra (mesma condição das janelas modais do resto do add-in).
    ///
    /// A seleção é POR ETAPAS, como nos recursos nativos: quem assume o mouse da SE é o
    /// <see cref="SePicker"/>, e é ele que permite clicar numa ARESTA — a ferramenta
    /// Selecionar da SE não localiza aresta, então o fluxo antigo ("selecione e depois
    /// confirme no botão") nunca conseguia pegar a aresta.
    ///
    /// A janela só ORQUESTRA: quem mede é <see cref="ORingTargetReader"/>, quem dimensiona é
    /// <see cref="ORingGrooveCalculator"/> e quem corta é <see cref="ORingGrooveModeler"/>.
    /// Nenhuma regra de vedação mora aqui.
    /// </summary>
    public sealed class ORingGrooveForm : Form
    {
        /// <summary>Etapa da seleção — a mesma ideia das etapas de um recurso da SE.</summary>
        private enum Step { None, Face, Edge }

        /// <summary>Num canal de FACE, a PAREDE mínima (mm, no raio) entre a aresta marcada e
        /// a borda interna do canal — 1 mm é o piso de projeto do Carlos, e é o valor com que o
        /// campo entra. Medir a PAREDE (e não a distância até o centro do canal) é o que garante
        /// que o alojamento nunca invada o furo: o canal inteiro fica para fora dela.</summary>
        private const double MinFaceOffsetMm = 1.0;

        /// <summary>Afastamento AXIAL com que o campo entra num canal de eixo/furo.</summary>
        private const double DefaultRadialOffsetMm = 5.0;

        /// <summary>Primeiro item da lista de seções: cada aresta usa a seção que o diâmetro
        /// DELA pede, em vez de uma seção só para todas. É o padrão porque, marcando furos de
        /// tamanhos diferentes de uma vez, uma seção única serviria mal a metade deles.</summary>
        private const string AutoSectionItem = "automática (pelo diâmetro de cada aresta)";

        /// <summary>
        /// Um alojamento a criar: a aresta que você marcou, o que ela mede e o anel escolhido
        /// para ELA. Com várias arestas na mesma face, cada uma tem o seu plano — é o que
        /// permite furos de diâmetros diferentes numa tacada só, cada um com o seu anel.
        /// </summary>
        private sealed class GroovePlan
        {
            public object ComEdge;
            public ORingTarget Target;
            public ORingGrooveSpec Spec;
            public double SealingDiameterMm;
            public string Problem;          // por que esta aresta não vai virar alojamento

            public bool Ready { get { return Problem == null && Spec != null && Target != null && Target.Ok; } }
            public string EdgeLabel { get { return Target != null && Target.Ok ? $"Ø{Target.EdgeDiameterMm:0.000}" : "aresta"; } }
        }

        private readonly dynamic _app;            // Application do add-in — NUNCA desconecta
        private readonly ORingCatalog _catalog;

        private object _comFace;
        private readonly List<object> _comEdges = new List<object>();
        private List<GroovePlan> _plans = new List<GroovePlan>();
        private IReadOnlyList<ORingCandidate> _candidates = new List<ORingCandidate>();
        private bool _loading;

        private SePicker _picker;                 // null = a SE não deixou; cai no plano B (SelectSet)
        private Step _step = Step.None;
        private bool _offsetIsFaceMode;             // o campo de afastamento está no modo canal-de-face?
        private bool _offsetUiReady;
        private object _hlFace, _hlEdge;          // HighlightSets — realce do que já foi pego

        private Label _lblFace, _lblEdge;
        private Button _btnFace, _btnEdge, _btnCreate, _btnClose, _btnCatalog;
        private ComboBox _cboKind, _cboMotion, _cboElastomer, _cboPressure, _cboSection, _cboRing;
        private NumericUpDown _numOffset;
        private Label _lblOffset;
        private TextBox _txtReport;
        private readonly ToolTip _tips = new ToolTip();

        public ORingGrooveForm(object app, ORingCatalog catalog)
        {
            _app = app;
            _catalog = catalog ?? ORingCatalog.LoadOrCreateDefault();
            BuildUi();
        }

        /// <summary>Documento FRESCO a cada operação — o Application é o único proxy que
        /// sobrevive a tudo (ver a regra de RPC_E_DISCONNECTED do projeto).</summary>
        private dynamic Doc()
        {
            try { return _app?.ActiveDocument; } catch { return null; }
        }

        // ------------------------------------------------------------------ interface

        private void BuildUi()
        {
            Text = "AutoEDM — Alojamento de anel O'ring";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;              // fica visível por cima da SE enquanto se seleciona
            StartPosition = FormStartPosition.Manual;   // encostada à direita — ver OnLoad
            ClientSize = new Size(560, 560);
            Font = new Font("Segoe UI", 9F);

            int y = 12;

            Add(new Label
            {
                Left = 12, Top = y, Width = 536, Height = 32,
                Text = "Clique direto no modelo, uma etapa de cada vez. A FACE diz onde é a vedação " +
                       "(cilíndrica = eixo/furo, plana = vedação de face); cada ARESTA circular vira um alojamento."
            });
            y += 38;

            _btnFace = Button("1. Selecionar FACE", 12, y, 150, (s, e) => StartStep(Step.Face));
            _tips.SetToolTip(_btnFace, "Clique aqui e depois na face de vedação, no modelo. " +
                                       "Esc cancela a etapa; clicar de novo aqui reinicia.");
            _lblFace = Add(new Label { Left = 172, Top = y + 4, Width = 376, Text = "— nenhuma face selecionada —", ForeColor = Color.Firebrick });
            y += 32;

            _btnEdge = Button("2. Selecionar ARESTA(S)", 12, y, 150, (s, e) => RestartEdges());
            _tips.SetToolTip(_btnEdge, "Clique aqui e depois nas arestas CIRCULARES, uma a uma: cada uma vira um " +
                                       "alojamento, com o melhor anel para o diâmetro dela. Esc encerra a coleta; " +
                                       "clicar aqui de novo recomeça. Se a aresta estiver encostada na face, a " +
                                       "listinha do QuickPick da SE desempata.");
            _lblEdge = Add(new Label { Left = 172, Top = y + 4, Width = 376, Text = "— nenhuma aresta selecionada —", ForeColor = Color.Firebrick });
            y += 40;

            _cboKind = Combo("Tipo de canal", 12, ref y, new[] { "Eixo (canal externo)", "Furo (canal interno)", "Face plana (anular)" });
            _tips.SetToolTip(_cboKind, "Vem preenchido pela geometria que você capturou. Troque se o AutoEDM " +
                                       "tiver adivinhado errado se o cilindro é eixo ou furo.");

            _cboMotion = Combo("Tipo de vedação", 12, ref y, new[] { "Estática", "Recíproca (haste/êmbolo)", "Rotativa (eixo girando)" });
            _tips.SetToolTip(_cboMotion, "Decide o ESMAGAMENTO do anel: quanto mais movimento, menos esmagamento. " +
                                         "Em rotativa o anel não pode ser esticado (efeito Gow-Joule).");

            _cboElastomer = Combo("Elastômero", 12, ref y, new[] { "NBR (nitrílica)", "FKM (Viton)" });
            _tips.SetToolTip(_cboElastomer, "Não muda a cota do canal: muda o quanto o anel pode ser esticado e " +
                                            "quanta folga o canal precisa para a dilatação. O FKM é mais exigente nos dois.");

            _cboPressure = Combo("Pressão (canal de face)", 12, ref y, new[]
            {
                "Interna — anel apoia no Ø EXTERNO do canal",
                "Externa / vácuo — anel apoia no Ø INTERNO do canal"
            });
            _cboPressure.Width = 328;
            _tips.SetToolTip(_cboPressure, "Só no canal de FACE: de que lado vem a pressão. Ela empurra o anel contra a " +
                                           "parede oposta, então o canal é posicionado com o anel JÁ ENCOSTADO nela — " +
                                           "pressão por dentro apoia o Ø externo do anel; por fora (ou vácuo), o Ø interno.\r\n" +
                                           "No canal de eixo/furo o anel já se apoia no próprio diâmetro, e isto não se aplica.");

            _cboSection = Combo("Seção do cordão (d2)", 12, ref y, new string[0]);
            _tips.SetToolTip(_cboSection, "Grossura do cordão. Em AUTOMÁTICA, cada aresta usa a seção que o " +
                                          "diâmetro dela pede — é o que serve quando você marca furos de tamanhos " +
                                          "diferentes de uma vez. Fixe uma seção para usar a mesma em todas.");

            _cboRing = Combo("Anel", 12, ref y, new string[0]);
            _cboRing.Width = 400;
            _tips.SetToolTip(_cboRing, "Anéis do catálogo, do que melhor serve para o pior. " +
                                       "O ⚠ marca medida ainda não conferida no catálogo do fornecedor.");

            _lblOffset = Add(new Label { Left = 12, Top = y + 4, Width = 200, Text = OffsetCaption(GrooveKind.RadialInternal) });
            _numOffset = Add(new NumericUpDown
            {
                Left = 220, Top = y, Width = 90, DecimalPlaces = 2, Increment = 0.5M,
                Minimum = 0M, Maximum = 500M, Value = (decimal)DefaultRadialOffsetMm
            });
            _numOffset.ValueChanged += (s, e) => Recompute();
            _tips.SetToolTip(_numOffset, "Canal de EIXO/FURO: a que distância da aresta, ao longo do eixo e para dentro " +
                                         "do material, fica o CENTRO do canal.\r\n" +
                                         "Canal de FACE: quanta PAREDE fica entre a aresta marcada e a borda " +
                                         "interna do canal, no raio (mínimo " + MinFaceOffsetMm.ToString("0.0") +
                                         " mm). O canal inteiro fica para fora dessa parede.");
            y += 34;

            _txtReport = Add(new TextBox
            {
                Left = 12, Top = y, Width = 536, Height = 232,
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9F), BackColor = Color.White
            });
            y += 240;

            _btnCatalog = Button("Abrir catálogo…", 12, y, 130, OpenCatalog);
            _tips.SetToolTip(_btnCatalog, "Abre o arquivo de medidas de anel no bloco de notas. " +
                                          "Cole ali a lista do fornecedor e reabra esta janela.");
            _btnCreate = Button("Criar alojamento", 300, y, 140, Create);
            _btnCreate.Enabled = false;
            _btnClose = Button("Fechar", 448, y, 100, (s, e) => Close());

            _cboKind.SelectedIndexChanged += (s, e) => { UpdateOffsetUi(); Recompute(); };
            _cboMotion.SelectedIndexChanged += (s, e) => Recompute();
            _cboElastomer.SelectedIndexChanged += (s, e) => Recompute();
            _cboPressure.SelectedIndexChanged += (s, e) => RefillRings();
            _cboSection.SelectedIndexChanged += (s, e) => RefillRings();
            _cboRing.SelectedIndexChanged += (s, e) => ShowSelectedSpec();

            _loading = true;
            _cboKind.SelectedIndex = 1;
            _cboMotion.SelectedIndex = 0;
            _cboElastomer.SelectedIndex = 0;
            _cboPressure.SelectedIndex = 0;
            _cboSection.Items.Add(AutoSectionItem);   // índice 0 = cada aresta escolhe a sua
            foreach (double d2 in _catalog.CrossSections) _cboSection.Items.Add(d2.ToString("0.00"));
            _cboSection.SelectedIndex = 0;
            _loading = false;

            UpdateOffsetUi();
            Report("Clique na face de vedação, no modelo.");

            // A altura sai do CONTEÚDO: com ClientSize fixo, qualquer linha a mais some por
            // baixo da borda — foi o que cortou os botões pela metade (relato de 2026-09-04).
            ClientSize = new Size(560, y + 26 + 12);
        }

        /// <summary>
        /// Encosta a janela no lado DIREITO da tela, centrada na altura — longe do PathFinder (à
        /// esquerda) e abaixo da ribbon, deixando o modelo livre para os cliques. A tela é a do
        /// mouse: quem acabou de clicar no botão da ribbon está na tela da SE.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            const int margin = 8;
            Location = new Point(
                area.Right - Width - margin,
                Math.Max(area.Top + margin, area.Top + (area.Height - Height) / 2));
        }

        /// <summary>Abre já na etapa 1, como um recurso da SE que entra pedindo a primeira seleção.</summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            StartStep(Step.Face);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            StopPicking();
            ClearHighlight(ref _hlFace);
            ClearHighlight(ref _hlEdge);
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

        private ComboBox Combo(string caption, int left, ref int top, string[] items)
        {
            Add(new Label { Left = left, Top = top + 4, Width = 200, Text = caption });
            var c = Add(new ComboBox
            {
                Left = left + 208, Top = top, Width = 240, DropDownStyle = ComboBoxStyle.DropDownList
            });
            foreach (var i in items) c.Items.Add(i);
            top += 30;
            return c;
        }

        /// <summary>
        /// O campo de afastamento vale para os TRÊS tipos de canal, mas com sentidos
        /// diferentes: no canal de eixo/furo ele anda ao longo do EIXO (da aresta para dentro
        /// do material); no canal de face ele anda no RAIO — é o que separa o anel da parede do
        /// furo que você marcou. Trocar o tipo de canal troca também o valor padrão, senão os
        /// 5 mm axiais virariam 5 mm radiais sem ninguém pedir.
        /// </summary>
        private void UpdateOffsetUi()
        {
            bool face = Kind() == GrooveKind.AxialFace;
            if (face == _offsetIsFaceMode && _offsetUiReady) return;   // nada mudou

            bool wasLoading = _loading;
            _loading = true;                                          // não dispara Recompute aqui
            try
            {
                _lblOffset.Text = OffsetCaption(Kind());
                _numOffset.Minimum = face ? (decimal)MinFaceOffsetMm : 0M;
                _numOffset.Value = face ? (decimal)MinFaceOffsetMm : (decimal)DefaultRadialOffsetMm;
                _cboPressure.Enabled = face;
            }
            finally { _loading = wasLoading; }

            _offsetIsFaceMode = face;
            _offsetUiReady = true;
        }

        private static string OffsetCaption(GrooveKind kind) =>
            kind == GrooveKind.AxialFace
                ? "Parede entre a aresta e o canal (mm)"
                : "Aresta → centro do canal, no EIXO (mm)";

        // Ø de vedação: agora é POR ARESTA — ver SealingDiameterOf(GroovePlan).

        // ------------------------------------------------------------------ seleção por etapas

        /// <summary>
        /// Entra numa etapa: pede à SE o mouse com o filtro daquela etapa e espera o clique.
        /// Chamar de novo na mesma etapa reinicia (é o "refazer esta seleção").
        /// </summary>
        private void StartStep(Step step)
        {
            StopPicking();
            _step = step;
            UpdateStepUi();

            if (step == Step.None) return;

            bool isFace = step == Step.Face;
            string prompt = isFace
                ? "AutoEDM — clique na FACE de vedação (cilíndrica ou plana)."
                : "AutoEDM — clique na ARESTA circular que dá o diâmetro.";
            var filter = isFace
                ? SolidEdgeConstants.seLocateFilterConstants.seLocateFace
                : SolidEdgeConstants.seLocateFilterConstants.seLocateEdge;

            if (Picker() != null && _picker.Start(prompt, filter))
            {
                Report(isFace
                    ? "Etapa 1 — clique na FACE de vedação, no modelo.\r\n(Esc cancela.)"
                    : "Etapa 2 — clique na ARESTA circular de referência, no modelo.\r\n(Esc cancela.)");
                return;
            }

            // Plano B: a SE não deixou assumir o mouse. Volta ao fluxo antigo — selecionar
            // antes e confirmar no botão —, que funciona para FACE (a ferramenta Selecionar
            // da SE localiza face) e é justamente o que não funciona para aresta.
            _step = Step.None;
            UpdateStepUi();
            string err;
            object item = FirstSelected(out err);
            if (item == null)
            {
                Warn(isFace ? _lblFace : _lblEdge, "— " + err + " —");
                Report("Não deu para assumir o mouse do Solid Edge (o log diz por quê).\r\n\r\n" +
                       "Modo reserva: selecione o elemento no Solid Edge e clique neste botão de novo.\r\n" +
                       (isFace ? "" : "ATENÇÃO: neste modo a SE normalmente NÃO deixa selecionar aresta."));
                return;
            }
            Accept(step, item);
        }

        /// <summary>Botão da etapa 2: joga fora as arestas já marcadas e recomeça a coleta.</summary>
        private void RestartEdges()
        {
            _comEdges.Clear();
            _plans = new List<GroovePlan>();
            ClearHighlight(ref _hlEdge);
            UpdateEdgeLabel();
            _btnCreate.Enabled = false;
            StartStep(Step.Edge);
        }

        /// <summary>Encerra a etapa em andamento e devolve o mouse à SE.</summary>
        private void StopPicking()
        {
            if (_picker != null) _picker.Stop();
            _step = Step.None;
            UpdateStepUi();
        }

        /// <summary>Cria o picker na primeira vez; devolve null se a SE não colaborar.</summary>
        private SePicker Picker()
        {
            if (_picker != null) return _picker;
            try
            {
                var app = (SolidEdgeFramework.Application)_app;
                _picker = new SePicker(app);
                _picker.Picked += OnPicked;
                _picker.Cancelled += OnPickCancelled;
            }
            catch (Exception e)
            {
                Log.Warn("Alojamento de O'ring: seleção por etapas indisponível — " + e.GetBaseException().Message);
                _picker = null;
            }
            return _picker;
        }

        /// <summary>
        /// Chegou um clique do Solid Edge. Sai do evento COM antes de mexer em qualquer coisa
        /// (<see cref="Control.BeginInvoke(Delegate)"/>): encerrar o comando de dentro do próprio
        /// callback dele é pedir para a SE reclamar.
        /// </summary>
        private void OnPicked(object graphic)
        {
            Step step = _step;
            try { BeginInvoke(new Action(() => Accept(step, graphic))); }
            catch (Exception e) { Log.Error("Alojamento de O'ring: seleção não pôde ser processada.", e); }
        }

        private void OnPickCancelled()
        {
            try
            {
                BeginInvoke(new Action(() =>
                {
                    _step = Step.None;
                    UpdateStepUi();
                    Report("Seleção cancelada. Clique num dos botões de etapa para voltar.");
                }));
            }
            catch { /* janela já fechando */ }
        }

        /// <summary>Guarda o que foi clicado, realça no modelo e anda para a etapa seguinte.</summary>
        private void Accept(Step step, object graphic)
        {
            if (graphic == null || step == Step.None) return;
            StopPicking();

            if (step == Step.Face)
            {
                _comFace = graphic;
                Highlight(ref _hlFace, graphic, Rgb(255, 128, 0), append: false);
                Ok(_lblFace, "face selecionada — lendo…");
                Recompute();
                // Como num recurso nativo, a etapa 1 emenda na 2 sem pedir licença.
                if (_comEdges.Count == 0) { StartStep(Step.Edge); return; }
            }
            else
            {
                _comEdges.Add(graphic);
                Highlight(ref _hlEdge, graphic, Rgb(0, 176, 80), append: true);
                Recompute();
                if (_comFace == null) { StartStep(Step.Face); return; }
                // A etapa da ARESTA CONTINUA: clique em quantas quiser, uma atrás da outra, e
                // cada uma vira um alojamento com o anel dela. Esc (ou "Criar alojamento")
                // encerra a coleta.
                StartStep(Step.Edge);
                return;
            }
            UpdateStepUi();
        }

        /// <summary>Botões de etapa com cara de etapa: o da etapa ativa fica destacado.</summary>
        private void UpdateStepUi()
        {
            SetStepButton(_btnFace, _step == Step.Face, "1. Selecionar FACE", "1. Clique na FACE…");
            SetStepButton(_btnEdge, _step == Step.Edge, "2. Selecionar ARESTA(S)",
                          _comEdges.Count == 0 ? "2. Clique na ARESTA…" : $"2. +aresta ({_comEdges.Count})…");
        }

        private static void SetStepButton(Button b, bool active, string idle, string busy)
        {
            if (b == null) return;
            b.Text = active ? busy : idle;
            b.BackColor = active ? Color.FromArgb(255, 244, 205) : SystemColors.Control;
            b.UseVisualStyleBackColor = !active;
            b.FlatStyle = active ? FlatStyle.Flat : FlatStyle.Standard;
        }

        // ------------------------------------------------------------------ realce

        private static int Rgb(int r, int g, int b) { return r | (g << 8) | (b << 16); } // COLORREF

        /// <summary>
        /// Realça no modelo o que já foi escolhido (cosmético: falha em silêncio no log).
        /// <paramref name="append"/> ACUMULA no mesmo realce — é o que mantém todas as arestas
        /// já marcadas visíveis enquanto você clica na próxima.
        /// </summary>
        private void Highlight(ref object set, object item, int color, bool append)
        {
            try
            {
                dynamic doc = Doc();
                if (doc == null) return;
                if (set == null) set = doc.HighlightSets.Add();
                dynamic hs = set;
                if (!append) hs.RemoveAll();
                hs.Color = color;
                hs.AddItem(item);
                hs.Draw();
            }
            catch (Exception e) { Log.Warn("Alojamento de O'ring: realce não aplicado — " + e.GetBaseException().Message); }
        }

        private void ClearHighlight(ref object set)
        {
            object s = set;
            set = null;
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

        // ------------------------------------------------------------------ captura (plano B)

        /// <summary>
        /// Primeiro item da seleção do Solid Edge. Um item de SelectSet pode vir EMBRULHADO
        /// (em montagem, a face vem dentro de um wrapper com <c>.Object</c>) — por isso a
        /// tentativa de desembrulhar antes de desistir.
        /// </summary>
        private object FirstSelected(out string error)
        {
            error = null;
            dynamic doc = Doc();
            if (doc == null) { error = "nenhum documento ativo no Solid Edge."; return null; }
            try
            {
                dynamic ss = doc.SelectSet;
                int n = 0; try { n = (int)ss.Count; } catch { }
                if (n == 0) { error = "nada selecionado no Solid Edge."; return null; }
                object item = ss.Item(1);
                try
                {
                    object inner = ((dynamic)item).Object;
                    if (inner != null) return inner;
                }
                catch { /* item já é o objeto cru — caminho normal em documento de PEÇA */ }
                return item;
            }
            catch (Exception e) { error = e.GetBaseException().Message; return null; }
        }

        private void Warn(Label label, string text)
        {
            label.Text = text;
            label.ForeColor = Color.Firebrick;
        }

        private void Ok(Label label, string text)
        {
            label.Text = text;
            label.ForeColor = Color.DarkGreen;
        }

        // ------------------------------------------------------------------ cálculo

        private GrooveKind Kind()
        {
            switch (_cboKind.SelectedIndex)
            {
                case 0: return GrooveKind.RadialExternal;
                case 2: return GrooveKind.AxialFace;
                default: return GrooveKind.RadialInternal;
            }
        }

        private SealMotion Motion()
        {
            switch (_cboMotion.SelectedIndex)
            {
                case 1: return SealMotion.Reciprocating;
                case 2: return SealMotion.Rotary;
                default: return SealMotion.Static;
            }
        }

        private Elastomer Rubber() => _cboElastomer.SelectedIndex == 1 ? Elastomer.Fkm : Elastomer.Nbr;

        private FacePressure Pressure() => _cboPressure.SelectedIndex == 1 ? FacePressure.External : FacePressure.Internal;

        /// <summary>Seção fixada na janela, ou null em "automática" (cada aresta escolhe a sua).</summary>
        private double? Section()
        {
            int i = _cboSection.SelectedIndex - 1;   // o índice 0 é a opção automática
            if (i < 0 || i >= _catalog.CrossSections.Count) return null;
            return _catalog.CrossSections[i];
        }

        /// <summary>Relê a geometria de TODAS as arestas e refaz os planos. Chamado a cada mexida.</summary>
        private void Recompute()
        {
            if (_loading) return;
            _btnCreate.Enabled = false;

            if (_comFace == null || _comEdges.Count == 0)
            {
                Report(_comFace == null && _comEdges.Count == 0 ? "Clique na face de vedação, no modelo."
                     : _comFace == null ? "Falta selecionar a FACE." : "Falta selecionar a ARESTA.");
                _plans = new List<GroovePlan>();
                UpdateEdgeLabel();
                return;
            }

            // Um plano por aresta. Uma aresta ilegível não derruba as outras: ela fica com o
            // Problem preenchido e aparece no relatório — foi o que o Carlos pediu, "se der erro
            // numa eu simplesmente não seleciono aquela aresta".
            var plans = new List<GroovePlan>();
            foreach (object edge in _comEdges)
            {
                var plan = new GroovePlan { ComEdge = edge };
                plan.Target = ORingTargetReader.Read(_comFace, edge);
                if (plan.Target == null || !plan.Target.Ok) plan.Problem = plan.Target?.Error ?? "não deu para ler a aresta.";
                else if (plans.Any(other => SameCircle(other.Target, plan.Target)))
                    plan.Problem = "aresta repetida (esse círculo já está na lista).";
                plans.Add(plan);
            }
            _plans = plans;

            var first = plans.FirstOrDefault(pl => pl.Target != null && pl.Target.Ok);
            if (first == null)
            {
                Warn(_lblFace, "— revise a seleção —");
                UpdateEdgeLabel();
                Report("Não deu para ler nenhuma das arestas:\r\n\r\n" + plans[0].Problem);
                return;
            }

            Ok(_lblFace, first.Target.FaceIsCylindrical
                ? $"face cilíndrica Ø {first.Target.FaceDiameterMm:0.000} mm"
                : "face plana");
            UpdateEdgeLabel();

            // O TIPO de canal é da face, não da aresta: vem do primeiro alvo legível.
            _loading = true;
            _cboKind.SelectedIndex = first.Target.SuggestedKind == GrooveKind.RadialExternal ? 0
                                   : first.Target.SuggestedKind == GrooveKind.AxialFace ? 2 : 1;
            _loading = false;

            // O afastamento entra ANTES dos anéis: num canal de face é ele que decide em que
            // diâmetro o cordão corre, e é desse diâmetro que sai a seção de cada aresta.
            UpdateOffsetUi();

            RefillRings();
        }

        /// <summary>
        /// Duas arestas são o MESMO círculo? Clicar duas vezes no mesmo lugar é fácil quando se
        /// marcam vários furos em sequência, e sem isto o segundo clique viraria um corte
        /// repetido em cima do primeiro.
        /// </summary>
        private static bool SameCircle(ORingTarget a, ORingTarget b)
        {
            if (a == null || b == null || !a.Ok || !b.Ok) return false;
            if (a.AxisIndex != b.AxisIndex) return false;
            if (Math.Abs(a.EdgeDiameterMm - b.EdgeDiameterMm) > 0.005) return false;
            for (int i = 0; i < 3; i++)
                if (Math.Abs(a.CenterMm[i] - b.CenterMm[i]) > 0.01) return false;
            return true;
        }

        /// <summary>
        /// Escolhe o anel de cada plano. Com UMA aresta a lista de anéis fica ativa e você pode
        /// trocar à mão; com VÁRIAS, cada aresta leva automaticamente o melhor anel para o
        /// diâmetro dela — que é o ponto de selecionar várias de uma vez.
        /// </summary>
        private void RefillRings()
        {
            if (_loading || _plans.Count == 0) return;

            foreach (var plan in _plans) Solve(plan);

            bool single = _plans.Count == 1 && _plans[0].Target != null && _plans[0].Target.Ok;
            _loading = true;
            _cboRing.Items.Clear();
            if (single)
            {
                _candidates = Rank(_plans[0]);
                foreach (var c in _candidates.Take(20)) _cboRing.Items.Add(Describe(c));
            }
            else
            {
                _candidates = new List<ORingCandidate>();
                _cboRing.Items.Add($"automático — o melhor anel para cada uma das {_plans.Count} arestas");
            }
            _cboRing.Enabled = single;
            if (_cboRing.Items.Count > 0) _cboRing.SelectedIndex = 0;
            _loading = false;

            if (single && _candidates.Count == 0) { ReportNoRing(_plans[0]); return; }
            ShowSelectedSpec();
        }

        /// <summary>Seção do cordão para um plano: a escolhida na janela, ou a sugerida pelo
        /// diâmetro daquela aresta quando a janela está em "automática".</summary>
        private double SectionFor(GroovePlan plan)
        {
            double? chosen = Section();
            if (chosen.HasValue) return chosen.Value;
            double reference = plan.Target != null && plan.Target.Ok ? plan.Target.SealingDiameterMm : 20.0;
            return ORingGrooveCalculator.SuggestCrossSection(reference, _catalog.CrossSections);
        }

        /// <summary>Ø de vedação de um plano — com o afastamento já embutido no canal de face.</summary>
        private double SealingDiameterOf(GroovePlan plan)
        {
            var t = plan.Target;
            if (t == null || !t.Ok) return 0;
            if (Kind() != GrooveKind.AxialFace) return t.SealingDiameterMm;
            return ORingGrooveCalculator.FaceSealingDiameter(
                t.EdgeDiameterMm, (double)_numOffset.Value, SectionFor(plan), Motion(), Rubber(), t.FaceGrooveOutward);
        }

        private IReadOnlyList<ORingCandidate> Rank(GroovePlan plan)
        {
            plan.SealingDiameterMm = SealingDiameterOf(plan);
            return ORingGrooveCalculator.Rank(_catalog, Kind(), plan.SealingDiameterMm,
                Motion(), Rubber(), SectionFor(plan), Pressure());
        }

        /// <summary>Dá a um plano o melhor anel disponível para ELE.</summary>
        private void Solve(GroovePlan plan)
        {
            plan.Spec = null;
            if (plan.Target == null || !plan.Target.Ok) return;
            var ranked = Rank(plan);
            if (ranked.Count == 0)
            {
                plan.Problem = $"nenhum anel no catálogo para a seção {SectionFor(plan):0.00} mm.";
                return;
            }
            plan.Problem = null;
            plan.Spec = ranked[0].Spec;
        }

        private void UpdateEdgeLabel()
        {
            if (_comEdges.Count == 0) { Warn(_lblEdge, "— nenhuma aresta selecionada —"); return; }
            var legible = _plans.Where(pl => pl.Target != null && pl.Target.Ok).ToList();
            string list = string.Join("; ", legible.Take(4).Select(pl => pl.EdgeLabel));
            if (legible.Count > 4) list += $"; +{legible.Count - 4}";
            string bad = _plans.Count > legible.Count ? $"  ({_plans.Count - legible.Count} ilegível(is))" : "";
            Ok(_lblEdge, $"{_comEdges.Count} aresta(s): {list}{bad}");
        }

        private string Describe(ORingCandidate c)
        {
            string flag = !c.Spec.IsWithinStandard ? "✗ " : c.Spec.HasWarnings ? "! " : "✓ ";
            string metric = (c.Spec.StretchIsOuterCompression ? "compr. " : "estir. ") +
                            (c.Spec.Stretch * 100).ToString("+0.0;-0.0;0.0") + "%";
            if (c.Spec.Kind == GrooveKind.AxialFace)
                metric = $"canal Ø{c.Spec.GrooveInnerDiameter:0.00}–{c.Spec.GrooveOuterDiameter:0.00}   " + metric;
            return $"{flag}{c.Ring.Designation}   {metric}" + (c.Ring.Verified ? "" : "   ⚠");
        }

        private void ReportNoRing(GroovePlan plan)
        {
            double d2 = SectionFor(plan);
            double ideal = ORingGrooveCalculator.IdealInnerDiameter(Kind(), plan.SealingDiameterMm, d2, Motion(), Rubber(), Pressure());
            _btnCreate.Enabled = false;
            Report(
                plan.Target.Description + "\r\n\r\n" +
                "Nenhum anel no catálogo para esta seção.\r\n\r\n" +
                $"O anel IDEAL para este caso seria d1 ≈ {ideal:0.00} mm × d2 {d2:0.00} mm.\r\n\r\n" +
                "Acrescente as medidas que você compra no catálogo (botão \"Abrir catálogo…\") e reabra esta janela.");
        }

        /// <summary>
        /// O relatório: detalhado quando há uma aresta só (com o anel que você escolheu na
        /// lista), e uma linha por aresta quando há várias — aí o que interessa é conferir de
        /// relance o anel e a parede de cada furo antes de mandar cortar tudo.
        /// </summary>
        private void ShowSelectedSpec()
        {
            if (_loading) return;

            bool single = _plans.Count == 1 && _cboRing.Enabled;
            if (single)
            {
                int i = _cboRing.SelectedIndex;
                if (i >= 0 && i < _candidates.Count) _plans[0].Spec = _candidates[i].Spec;   // troca à mão
            }

            var ready = _plans.Where(pl => pl.Ready).ToList();
            _btnCreate.Enabled = ready.Count > 0;
            if (_plans.Count == 0) return;

            var sb = new System.Text.StringBuilder();
            if (single && _plans[0].Ready)
            {
                sb.AppendLine(_plans[0].Target.Description);
                sb.AppendLine();
                sb.Append(_plans[0].Spec.Describe());
                sb.AppendLine();
                sb.Append(PositionLine(_plans[0]));
            }
            else
            {
                sb.AppendLine($"{ready.Count} de {_plans.Count} aresta(s) prontas — melhor anel escolhido para cada uma:");
                sb.AppendLine();
                foreach (var plan in _plans) sb.AppendLine(SummaryLine(plan));
                sb.AppendLine();
                sb.AppendLine("Confira acima antes de criar. O detalhe completo de cada canal vai para o log.");
            }
            Report(sb.ToString());
        }

        /// <summary>Uma linha por aresta: anel, cotas do canal e a parede que sobrou.</summary>
        private string SummaryLine(GroovePlan plan)
        {
            if (!plan.Ready)
                return $"  ✗ {plan.EdgeLabel}: {plan.Problem ?? "sem anel"}";

            var spec = plan.Spec;
            string flag = !spec.IsWithinStandard ? "✗" : spec.HasWarnings ? "!" : "✓";
            string line = $"  {flag} {plan.EdgeLabel} → {spec.Ring.Designation}   " +
                          $"canal Ø{spec.GrooveInnerDiameter:0.00}–{spec.GrooveOuterDiameter:0.00} " +
                          $"× {spec.Depth:0.00} fundo";
            if (spec.Kind == GrooveKind.AxialFace)
            {
                double land = ORingGrooveCalculator.FaceGrooveWall(spec, plan.Target.EdgeDiameterMm, plan.Target.FaceGrooveOutward);
                line += $"   parede {land:0.00}";
                if (land < 0) line += " (INVADE a aresta!)";
            }
            return line;
        }

        private string PositionLine(GroovePlan plan)
        {
            var t = plan.Target;
            var spec = plan.Spec;
            if (spec.Kind != GrooveKind.AxialFace)
                return $"Posição: centro do canal a {_numOffset.Value:0.00} mm da aresta, " +
                       $"em {(t.MaterialDirection > 0 ? "+" : "−")}{t.AxisName}.\r\n";

            bool outward = t.FaceGrooveOutward;
            double land = ORingGrooveCalculator.FaceGrooveWall(spec, t.EdgeDiameterMm, outward);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Posição: canal afundando na face, para {(outward ? "FORA" : "DENTRO")} da aresta " +
                          $"Ø {t.EdgeDiameterMm:0.000} (centro do canal pedido no Ø {plan.SealingDiameterMm:0.000}).");
            sb.AppendLine($"Parede entre a aresta e o canal: {land:0.00} mm (pedida {_numOffset.Value:0.00} mm).");
            if (land < 0)
                sb.AppendLine($"ATENÇÃO: o canal passa POR CIMA da aresta em {-land:0.00} mm de raio — " +
                              "aumente a parede ou escolha outro anel na lista.");
            else if (land < (double)_numOffset.Value - 0.005)
                sb.AppendLine("ATENÇÃO: a parede saiu menor que a pedida — é o anel mais próximo que o " +
                              "catálogo tem para este diâmetro.");
            return sb.ToString();
        }

        private void Report(string text)
        {
            _txtReport.Text = (text ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n");
        }

        // ------------------------------------------------------------------ ações

        private void Create(object sender, EventArgs e)
        {
            var ready = _plans.Where(pl => pl.Ready).ToList();
            if (ready.Count == 0) return;

            // AMBIENTE, conferido AQUI e não só no botão da ribbon: esta janela é MODELESS e
            // fica aberta enquanto o usuário mexe no Solid Edge — nada impede que ele troque a
            // peça para síncrono entre abrir a janela e clicar em Criar. Em síncrono o esboço
            // do canal (ProfileSets é sempre ORDENADO) ficaria órfão no nó "Ordenado" do
            // PathFinder, sem dono e sem como apagar pela interface.
            ModelingEnv env = ModelingEnvironment.Read(Doc());
            if (env != ModelingEnv.Ordered)
            {
                MessageBox.Show(
                    ModelingEnvironment.WrongEnvironmentMessage("Alojamento de O'ring", ModelingEnv.Ordered, env),
                    "AutoEDM — ambiente de modelagem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Log.Warn($"Alojamento de O'ring: criação recusada — peça em modelagem {ModelingEnvironment.Name(env)}.");
                return;
            }

            // O Carlos pediu AVISAR em vez de recusar: mostra o que está fora e deixa decidir.
            var offSpec = ready.Where(pl => !pl.Spec.IsWithinStandard).ToList();
            if (offSpec.Count > 0)
            {
                var lines = offSpec.Select(pl => "• " + pl.EdgeLabel + ": " + string.Join(" / ", pl.Spec.Issues
                    .Where(i => i.Level == GrooveIssueLevel.Error).Select(i => i.Message)));
                var r = MessageBox.Show(
                    $"{offSpec.Count} de {ready.Count} alojamento(s) estão FORA do que a norma admite:\r\n\r\n\r\n\r\n" +
                    string.Join("\r\n\r\n", lines) + "\r\n\r\n\r\n\r\nCriar assim mesmo?",
                    "AutoEDM — alojamento fora da norma", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) { Log.Info("Alojamento de O'ring: criação cancelada (fora da norma)."); return; }
                Log.Warn("Alojamento de O'ring: criado FORA DA NORMA a pedido do usuário.");
            }

            // Devolve o mouse à SE ANTES de modelar: o corte cria features, e nenhum comando
            // nosso pode estar ativo enquanto isso.
            StopPicking();
            var done = new List<string>();
            var failed = new List<string>();
            try
            {
                Log.Info($"===== ALOJAMENTO DE O'RING ({ready.Count} aresta(s)) =====");
                Enabled = false;
                foreach (var plan in ready)
                {
                    // Cada aresta é INDEPENDENTE: uma que falha não leva as outras junto — foi
                    // exatamente o que o Carlos pediu ao aceitar seleção múltipla.
                    try
                    {
                        Log.Info($"--- aresta {plan.EdgeLabel} ---");
                        Log.Info(plan.Spec.Describe());
                        string featureName;
                        bool ok = ORingGrooveModeler.Cut((object)_app, plan.Target, plan.Spec, (double)_numOffset.Value, out featureName);
                        (ok ? done : failed).Add($"{plan.EdgeLabel} → {plan.Spec.Ring.Designation}" +
                                                 (ok && featureName != null ? $"   (feature \"{featureName}\")" : ""));
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Alojamento de O'ring: aresta {plan.EdgeLabel} falhou.", ex);
                        failed.Add($"{plan.EdgeLabel} ({ex.GetBaseException().Message})");
                    }
                }
            }
            finally
            {
                Enabled = true;
                Log.Info($"===== FIM (ALOJAMENTO DE O'RING) — {done.Count} criado(s), {failed.Count} falha(s) =====");
            }

            var sb = new System.Text.StringBuilder();
            if (done.Count > 0)
            {
                sb.AppendLine($"{done.Count} alojamento(s) criado(s):");
                foreach (var d in done) sb.AppendLine("  ✓ " + d);
            }
            if (failed.Count > 0)
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.AppendLine($"{failed.Count} NÃO foi/foram criado(s):");
                foreach (var f in failed) sb.AppendLine("  ✗ " + f);
                sb.AppendLine();
                sb.AppendLine("O log traz o passo exato que falhou (%LOCALAPPDATA%\\AutoEDM\\logs).");
            }
            if (done.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("O raio de fundo e a quebra de canto NÃO são cortados pelo AutoEDM — estão no " +
                              "relatório para você aplicar (arredondar/chanfrar) como preferir.");
            }
            MessageBox.Show(sb.ToString(), "AutoEDM — Alojamento de O'ring", MessageBoxButtons.OK,
                failed.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            if (done.Count > 0)
            {
                ClearSelection();
                Report($"{done.Count} alojamento(s) criado(s). Clique na face do próximo.");
                StartStep(Step.Face);   // já reabre na etapa 1, como um recurso da SE
            }
        }

        private void ClearSelection()
        {
            _comFace = null;
            _comEdges.Clear();
            _plans = new List<GroovePlan>();
            ClearHighlight(ref _hlFace);
            ClearHighlight(ref _hlEdge);
            Warn(_lblFace, "— nenhuma face selecionada —");
            Warn(_lblEdge, "— nenhuma aresta selecionada —");
            _btnCreate.Enabled = false;
        }

        private void OpenCatalog(object sender, EventArgs e)
        {
            try
            {
                string path = ORingCatalog.DefaultPath;
                if (!System.IO.File.Exists(path)) ORingCatalog.LoadOrCreateDefault(path); // cria com a tabela embutida
                System.Diagnostics.Process.Start("notepad.exe", "\"" + path + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não deu para abrir o catálogo:\r\n" + ex.GetBaseException().Message,
                    "AutoEDM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
