using System;
using System.Runtime.InteropServices;
using AutoEDM.Diagnostics;
using SolidEdgeConstants;

namespace AutoEDM.AddIn.UI
{
    /// <summary>
    /// Seleção POR ETAPAS igual à dos recursos nativos do Solid Edge: em vez de pedir que o
    /// usuário selecione ANTES e depois confirme num botão, o AutoEDM assume o mouse da SE,
    /// diz o que quer ("clique na aresta circular") e recebe o objeto clicado.
    ///
    /// POR QUE ISSO EXISTE: a ferramenta Selecionar da SE, no ambiente de PEÇA, só localiza
    /// FACE/feature — ARESTA nunca entra no <c>SelectSet</c>. Quem manda no que é localizável
    /// é o COMANDO ativo; logo, para pegar uma aresta é preciso ter um comando NOSSO ativo,
    /// com o filtro de localização certo. É exatamente o que a SE faz nos próprios recursos.
    ///
    /// Como funciona (tudo conferido por reflexão do Interop.SolidEdge 219 / SE 2023):
    ///   Application.CreateCommand(seNoDeactivate) → Command.Start() → Command.Mouse,
    ///   Mouse.AddToLocateFilter(seLocateFace | seLocateEdge | …) e o evento
    ///   MouseClick(..., pGraphicDispatch), cujo último argumento É o objeto COM clicado
    ///   (a Face ou a Edge) — pronto para ir direto ao núcleo, sem passar por SelectSet.
    ///
    /// Vive na thread STA do Solid Edge (a mesma do add-in e das janelas): os eventos chegam
    /// no contexto certo, sem thread extra e sem marshaling.
    /// </summary>
    public sealed class SePicker : IDisposable
    {
        /// <summary>Objeto COM localizado (Face, Edge, …). Quem recebe fica dono do RCW.</summary>
        public event Action<object> Picked;

        /// <summary>O comando morreu sem escolha: Esc, ou o usuário disparou outro comando da SE.</summary>
        public event Action Cancelled;

        /// <summary>Clique esquerdo que não localizou geometria nenhuma. A etapa continua ativa.</summary>
        public event Action Missed;

        /// <summary>
        /// Clique SEM geometria também vira <see cref="Picked"/> (com <c>null</c>), em vez de
        /// <see cref="Missed"/>. Para quem decide a face sozinho pelo ponto do cursor
        /// (<c>ManualElectrodeForm</c>): no <c>seSmartLocate</c> a SE não entrega geometria ao
        /// comando, mas também não acende a face errada sob o mouse — o QuickPick acendia a peça
        /// de TRÁS antes do clique, e o Carlos lia isso como "vai pegar a de baixo" (2026-09-24).
        /// </summary>
        public bool AcceptEmptyClicks { get; set; }

        /// <summary>Ponto do último clique (metros, espaço do documento) — lido DENTRO do
        /// <see cref="Picked"/>, antes de sair do callback. É com ele e com a câmera de
        /// <see cref="LastWindow"/> que se monta o raio de visão.</summary>
        public double[] LastPointM { get; private set; }

        /// <summary>Janela do último clique (tem a <c>View</c> com a câmera).</summary>
        public object LastWindow { get; private set; }

        /// <summary>Cursor na TELA no instante do clique (pixels). Com a vista, é ele que dá o
        /// ponto sob o cursor — o (x, y, z) do evento não serve para o raio (ver
        /// <c>VisibleFacePicker.TryScreenToModel</c>).</summary>
        public System.Drawing.Point LastScreenPoint { get; private set; }

        private readonly SolidEdgeFramework.Application _app;
        private SolidEdgeFramework.Command _cmd;
        private SolidEdgeFramework.Mouse _mouse;

        // Os delegates PRECISAM ficar em campo: são eles que a gente descadastra no Stop().
        // Sem isso o sink continua pendurado no ponto de conexão da SE depois do comando morrer.
        private SolidEdgeFramework.DISEMouseEvents_MouseClickEventHandler _onClick;
        private SolidEdgeFramework.DISECommandEvents_TerminateEventHandler _onTerminate;

        private bool _stopping;

        public SePicker(SolidEdgeFramework.Application app)
        {
            if (app == null) throw new ArgumentNullException("app");
            _app = app;
        }

        /// <summary>Uma etapa está em andamento (comando nosso ativo, esperando o clique).</summary>
        public bool Running { get; private set; }

        /// <summary>
        /// Começa uma etapa de seleção. O <paramref name="prompt"/> vai para a barra de status da
        /// SE, como nos comandos nativos. Devolve <c>false</c> (e loga) se a SE não deixar criar
        /// o comando — o chamador então cai no plano B (ler o SelectSet).
        /// </summary>
        public bool Start(string prompt, params seLocateFilterConstants[] filters)
            => Start(prompt, seLocateModes.seLocateQuickPick, filters);

        /// <summary>
        /// Igual, escolhendo o modo de localização. <c>seLocateQuickPick</c> serve quando é
        /// preciso desempatar elementos encostados (aresta × face, no O'ring), mas sem a escolha
        /// na listinha a SE devolve o 1º candidato da lista interna, que NÃO é o mais próximo da
        /// vista — o Carlos viu faces de trás/síncronas ganharem da face visível (2026-09-24).
        /// <c>seSmartLocate</c> é o modo da ferramenta Selecionar nativa: prioridade visual.
        /// </summary>
        public bool Start(string prompt, seLocateModes locateMode, params seLocateFilterConstants[] filters)
        {
            Stop();
            try
            {
                // seNoDeactivate (=2): o comando sobrevive à troca de foco — a nossa janela é
                // MODELESS e o usuário clica nela entre uma etapa e outra.
                _cmd = _app.CreateCommand((int)seCmdFlag.seNoDeactivate);
                _cmd.Start();

                _onTerminate = OnTerminate;
                ((SolidEdgeFramework.DISECommandEvents_Event)_cmd).Terminate += _onTerminate;

                _mouse = _cmd.Mouse;

                // Cada propriedade num try próprio: se uma versão da SE recusar alguma, a etapa
                // continua funcionando em vez de morrer inteira.
                // WindowTypes=1 → janelas de MODELO (esse enum não veio no interop; 1 é o valor
                // documentado, e o log abaixo mostra o que a SE de fato aceitou).
                TrySet("WindowTypes", () => _mouse.WindowTypes = 1);
                TrySet("LocateMode", () => _mouse.LocateMode = (int)locateMode);
                TrySet("ClearLocateFilter", () => _mouse.ClearLocateFilter());
                foreach (var f in filters)
                {
                    var filter = f;
                    TrySet("AddToLocateFilter(" + filter + ")", () => _mouse.AddToLocateFilter((int)filter));
                }

                _onClick = OnMouseClick;
                ((SolidEdgeFramework.DISEMouseEvents_Event)_mouse).MouseClick += _onClick;

                SetStatus(prompt);
                Running = true;
                Log.Info(string.Format("[picker] etapa iniciada — filtros: {0}; WindowTypes={1}, LocateMode={2}.",
                    string.Join(", ", Array.ConvertAll(filters, f => f.ToString())),
                    Read(() => _mouse.WindowTypes), Read(() => _mouse.LocateMode)));
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("[picker] não deu para assumir o mouse da SE: " + e.GetBaseException().Message);
                Stop();
                return false;
            }
        }

        /// <summary>Encerra a etapa e devolve o mouse à SE. Idempotente e nunca lança.</summary>
        public void Stop()
        {
            if (_stopping) return;
            _stopping = true;
            try
            {
                Running = false;

                if (_mouse != null && _onClick != null)
                    try { ((SolidEdgeFramework.DISEMouseEvents_Event)_mouse).MouseClick -= _onClick; } catch { }
                if (_cmd != null && _onTerminate != null)
                    try { ((SolidEdgeFramework.DISECommandEvents_Event)_cmd).Terminate -= _onTerminate; } catch { }
                _onClick = null;
                _onTerminate = null;

                // Limpar o filtro antes de encerrar: se por algum motivo o comando sobreviver
                // ao Done, ele pelo menos não fica com a localização restrita a aresta/face.
                if (_mouse != null) { try { _mouse.ClearLocateFilter(); } catch { } }
                // Done=true é como se diz à SE que o comando acabou; ela volta sozinha para a
                // ferramenta Selecionar.
                if (_cmd != null) { try { _cmd.Done = true; } catch (Exception e) { Log.Warn("[picker] Done recusado: " + e.GetBaseException().Message); } }

                Release(ref _mouse);
                Release(ref _cmd);
                SetStatus("");
            }
            finally { _stopping = false; }
        }

        public void Dispose() { Stop(); }

        // ---------------------------------------------------------------- eventos

        private void OnMouseClick(short button, short shift, double x, double y, double z,
            object window, int keyPointType, object graphic)
        {
            // Botão direito é do menu de contexto/QuickPick da SE — não é escolha nossa.
            if (button != (short)seButton.seLEFT) return;
            LastPointM = new[] { x, y, z };
            LastWindow = window;
            LastScreenPoint = System.Windows.Forms.Cursor.Position;
            Log.Info(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "[picker] clique em ({0:0.###}, {1:0.###}, {2:0.###}) mm, modo {3}{4}.", x * 1000, y * 1000, z * 1000,
                Read(() => _mouse.LocateMode), graphic == null ? " — SEM geometria localizada" : ""));
            if (graphic == null && !AcceptEmptyClicks)
            {
                // Clique no vazio, ou modo de localização que não entrega a geometria ao nosso
                // comando (seSmartLocate, visto em 2026-09-24: cursor em cruz e nada selecionado).
                // Quem está na tela precisa saber — o aviso só no log deixava o clique "morto".
                var missed = Missed;
                if (missed != null && Running) { try { missed(); } catch (Exception e) { Log.Error("[picker] erro no aviso de clique vazio.", e); } }
                return;
            }

            var handler = Picked;
            if (handler == null || !Running) return;
            // A etapa acaba AQUI, antes de avisar quem espera: o tratamento é adiado para fora
            // deste callback, e sem isso um segundo clique no meio do caminho entraria de novo.
            Running = false;
            try { handler(graphic); }
            catch (Exception e) { Log.Error("[picker] erro tratando a seleção.", e); }
        }

        private void OnTerminate()
        {
            // A SE matou o comando (Esc, ou outro comando assumiu). Não se chama Stop() de
            // dentro do evento: marca-se como parado e avisa-se quem está na tela.
            if (!Running) return;
            Running = false;
            Log.Info("[picker] etapa cancelada pelo Solid Edge (Esc ou outro comando).");
            var handler = Cancelled;
            if (handler != null)
            {
                try { handler(); }
                catch (Exception e) { Log.Error("[picker] erro no cancelamento.", e); }
            }
        }

        // ---------------------------------------------------------------- utilidades

        private void SetStatus(string text)
        {
            try { _app.StatusBar = text ?? ""; } catch { }
        }

        private static void TrySet(string what, Action set)
        {
            try { set(); }
            catch (Exception e) { Log.Warn("[picker] " + what + " recusado pela SE: " + e.GetBaseException().Message); }
        }

        private static string Read(Func<int> get)
        {
            try { return get().ToString(); } catch { return "?"; }
        }

        private static void Release<T>(ref T o) where T : class
        {
            var v = o;
            o = null;
            if (v == null) return;
            try { if (Marshal.IsComObject(v)) Marshal.ReleaseComObject(v); } catch { }
        }
    }
}
