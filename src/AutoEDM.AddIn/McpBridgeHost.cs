using System;
using System.Threading;
using System.Windows.Forms;
using AutoEDM.Diagnostics;
using AutoEDM.Mcp;

namespace AutoEDM.AddIn
{
    /// <summary>
    /// Cola entre a ponte MCP e a Solid Edge. Guarda três coisas: o servidor de pipe, o modo
    /// (somente-leitura × escrita) e o controle oculto que serve de trampolim para a thread STA
    /// da SE.
    ///
    /// Por que o trampolim: o laço do pipe roda numa thread de fundo, e tocar COM de lá
    /// atravessa apartamento — é a receita do RPC_E_CALL_REJECTED intermitente. Um
    /// <see cref="Control"/> criado NA THREAD DA SE dá um <c>BeginInvoke</c> que executa o
    /// trabalho de volta na thread certa. Isso funciona porque a Solid Edge bomba mensagens
    /// (é a mesma razão pela qual o <c>System.Windows.Forms.Timer</c> do relógio de estado da
    /// ribbon funciona in-process).
    /// </summary>
    internal sealed class McpBridgeHost : IDisposable
    {
        /// <summary>
        /// Teto de espera por UMA operação na thread da SE. Existe para o agente receber
        /// "a SE está ocupada" em vez de o Claude Code ficar pendurado para sempre: se houver
        /// uma caixa de diálogo modal aberta no CAD, a thread da SE não processa o BeginInvoke
        /// até alguém fechá-la — e ninguém avisou o usuário que foi perguntado.
        /// </summary>
        private const int OperationTimeoutMs = 120_000;

        private readonly Control _trampoline;
        private readonly SeToolRunner _runner;
        private BridgeServer _server;
        private BridgeMode _mode = BridgeMode.ReadOnly;

        /// <summary>Constrói NA THREAD DA SOLID EDGE (é chamado do clique da ribbon ou do
        /// OnConnection). O <c>.Handle</c> força a criação da janela agora, na thread certa —
        /// sem handle, o <c>BeginInvoke</c> mais tarde levantaria InvalidOperationException.</summary>
        internal McpBridgeHost()
        {
            _trampoline = new Control();
            IntPtr forceHandleCreation = _trampoline.Handle;
            GC.KeepAlive(forceHandleCreation);

            _runner = new SeToolRunner(
                appProvider: () => ElectrodeAddIn.Current?.App,
                modeProvider: () => _mode,
                logPathProvider: () => ElectrodeAddIn.Current?.LogPath);
        }

        internal bool Running => _server != null && _server.Running;

        internal BridgeMode Mode => _mode;

        internal int Served => _server != null ? _server.Served : 0;

        internal string LastTool => _server != null ? _server.LastTool : null;

        internal bool Start(out string error)
        {
            if (Running) { error = "A ponte já está no ar."; return false; }
            _server = new BridgeServer(Marshal);
            bool ok = _server.Start(out error);
            if (!ok) { _server.Dispose(); _server = null; }
            return ok;
        }

        internal void Stop()
        {
            if (_server == null) return;
            _server.Stop();
            _server.Dispose();
            _server = null;
            // A chave de escrita NÃO sobrevive a um desligamento: religar a ponte religa em
            // somente-leitura, para que "liberei escrita uma vez" não fique valendo o dia todo.
            _mode = BridgeMode.ReadOnly;
        }

        /// <summary>Liga/desliga a escrita. Só o usuário chega aqui (vem de um clique na ribbon);
        /// nenhuma ferramenta MCP tem como alterar o modo.</summary>
        internal void SetMode(BridgeMode mode)
        {
            _mode = mode;
            Log.Info($"MCP: modo da ponte agora é {(mode == BridgeMode.Write ? "ESCRITA LIBERADA" : "SOMENTE-LEITURA")}.");
        }

        /// <summary>Chamado NA THREAD DO PIPE. Empurra para a thread da SE e espera.</summary>
        private BridgeResponse Marshal(BridgeRequest req)
        {
            Func<BridgeRequest, BridgeResponse> work = r => _runner.Execute(r);

            if (!_trampoline.InvokeRequired)
                return work(req);   // já estamos na thread da SE (não deve acontecer, mas é correto)

            IAsyncResult async;
            try { async = _trampoline.BeginInvoke(work, new object[] { req }); }
            catch (Exception ex)
            {
                return BridgeResponse.Bad(req.Id,
                    "Não foi possível agendar o trabalho na thread da Solid Edge: " + ex.GetBaseException().Message);
            }

            if (!async.AsyncWaitHandle.WaitOne(OperationTimeoutMs))
            {
                Log.Warn($"MCP: '{req.Tool}' passou de {OperationTimeoutMs / 1000} s sem a thread da SE atender.");
                return BridgeResponse.Bad(req.Id,
                    $"A Solid Edge não atendeu em {OperationTimeoutMs / 1000} s. Quase sempre é uma CAIXA DE DIÁLOGO " +
                    "aberta no CAD esperando resposta (ou um recálculo muito longo). Peça ao usuário para olhar a " +
                    "janela da Solid Edge e fechar o que estiver aberto.\n\n" +
                    "A operação pode ainda terminar sozinha — confira com 'se_log' antes de repetir, para não " +
                    "executá-la duas vezes.");
            }

            try { return (BridgeResponse)_trampoline.EndInvoke(async); }
            catch (Exception ex)
            {
                Log.Error($"MCP: '{req.Tool}' levantou exceção na thread da Solid Edge.", ex);
                return BridgeResponse.Bad(req.Id, "A operação falhou dentro da Solid Edge: " + ex.GetBaseException().Message);
            }
        }

        public void Dispose()
        {
            try { Stop(); } catch { }
            try { _trampoline.Dispose(); } catch { }
        }
    }
}
