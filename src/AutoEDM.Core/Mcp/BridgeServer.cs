using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using AutoEDM.Diagnostics;

namespace AutoEDM.Mcp
{
    /// <summary>
    /// Hospeda o named pipe DENTRO do processo da Solid Edge (add-in). Aceita um cliente por
    /// vez — o Claude Code sobe um servidor MCP só — e reconecta quando ele cai, porque o
    /// agente é reiniciado muito mais vezes que o CAD.
    ///
    /// THREAD, que é o ponto todo desta classe: o laço de aceitação e a leitura do pipe rodam
    /// numa thread de FUNDO, mas COM não pode ser tocado de lá. Então cada pedido é entregue ao
    /// delegado <c>marshal</c>, que o hospedeiro implementa empurrando o trabalho para a thread
    /// STA da própria Solid Edge (um <c>Control.Invoke</c> num controle oculto criado no
    /// OnConnection). A thread do pipe fica BLOQUEADA até a SE responder, o que é exatamente o
    /// desejado: serializa os pedidos e nenhum agente consegue pedir duas operações de
    /// modelagem ao mesmo tempo.
    /// </summary>
    public sealed class BridgeServer : IDisposable
    {
        private readonly Func<BridgeRequest, BridgeResponse> _marshal;
        private readonly object _gate = new object();

        private Thread _thread;
        private NamedPipeServerStream _current;
        private volatile bool _stopping;

        /// <summary>true entre o <see cref="Start"/> bem-sucedido e o <see cref="Stop"/>.</summary>
        public bool Running { get; private set; }

        /// <summary>Quantos pedidos esta ponte já atendeu — aparece no "Status" da ribbon, e é
        /// como o Carlos confirma num piscar que o agente chegou de verdade até a SE.</summary>
        public int Served { get; private set; }

        /// <summary>Nome da última ferramenta pedida, para o mesmo Status.</summary>
        public string LastTool { get; private set; }

        public BridgeServer(Func<BridgeRequest, BridgeResponse> marshal)
        {
            _marshal = marshal ?? throw new ArgumentNullException(nameof(marshal));
        }

        /// <summary>
        /// Sobe o laço de aceitação. Devolve false (com o motivo em <paramref name="error"/>)
        /// quando o pipe já está tomado — caso real: uma SEGUNDA instância da Solid Edge com o
        /// add-in carregado. A primeira hospeda; a segunda diz isso em vez de as duas brigarem
        /// pelo mesmo nome em silêncio.
        /// </summary>
        public bool Start(out string error)
        {
            error = null;
            lock (_gate)
            {
                if (Running) { error = "A ponte já está no ar."; return false; }

                // Testa o nome ANTES de subir a thread: assim o erro "já existe" chega ao
                // usuário no clique, e não escondido num log de thread de fundo.
                try
                {
                    using (NamedPipeServerStream probe = CreatePipe()) { }
                }
                catch (IOException)
                {
                    error = $"O pipe '{BridgeProtocol.PipeName}' já está em uso — provavelmente outra instância da " +
                            "Solid Edge (ou outra sessão) já está hospedando a ponte. Só uma pode hospedar.";
                    return false;
                }
                catch (Exception ex)
                {
                    error = "Não foi possível criar o pipe: " + ex.GetBaseException().Message;
                    return false;
                }

                _stopping = false;
                _thread = new Thread(AcceptLoop) { IsBackground = true, Name = "AutoEDM.Bridge" };
                _thread.Start();
                Running = true;
                Log.Info($"MCP: ponte no ar em '{BridgeProtocol.PipeName}' (contrato v{BridgeProtocol.Version}).");
                return true;
            }
        }

        public void Stop()
        {
            Thread t;
            lock (_gate)
            {
                if (!Running) return;
                _stopping = true;
                Running = false;
                t = _thread;
                _thread = null;
                // Desbloqueia um WaitForConnection pendente.
                try { if (_current != null) _current.Dispose(); } catch { }
                _current = null;
            }

            // Cinto e suspensório: se o Dispose acima não desbloqueou (varia entre versões do
            // Windows), uma conexão-fantasma tira a thread do WaitForConnection.
            try
            {
                using (var poke = new NamedPipeClientStream(".", BridgeProtocol.PipeName, PipeDirection.InOut))
                    poke.Connect(200);
            }
            catch { }

            try { if (t != null && !t.Join(2000)) Log.Warn("MCP: a thread da ponte não encerrou em 2 s (segue como background)."); }
            catch { }
            Log.Info("MCP: ponte encerrada.");
        }

        private NamedPipeServerStream CreatePipe()
        {
#if NET472
            // Pipe restrito ao USUÁRIO corrente. A ponte pode acabar dirigindo modelagem numa
            // montagem de molde viva; o DACL padrão de named pipe é mais aberto que isso
            // precisa ser, e apertar aqui custa três linhas.
            var security = new PipeSecurity();
            System.Security.Principal.SecurityIdentifier me =
                System.Security.Principal.WindowsIdentity.GetCurrent().User;
            security.AddAccessRule(new PipeAccessRule(me, PipeAccessRights.FullControl,
                System.Security.AccessControl.AccessControlType.Allow));

            return new NamedPipeServerStream(BridgeProtocol.PipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.None, 0, 0, security);
#else
            // Este alvo (net8.0-windows) existe só para os TESTES do Core rodarem fora do CAD —
            // a ponte de verdade sempre sobe no add-in, que é net472.
            return new NamedPipeServerStream(BridgeProtocol.PipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.None);
#endif
        }

        private void AcceptLoop()
        {
            while (!_stopping)
            {
                NamedPipeServerStream pipe = null;
                try
                {
                    pipe = CreatePipe();
                    lock (_gate)
                    {
                        if (_stopping) { pipe.Dispose(); return; }
                        _current = pipe;
                    }

                    pipe.WaitForConnection();
                    if (_stopping) return;
                    Log.Info("MCP: servidor conectado à ponte.");
                    Serve(pipe);
                    Log.Info("MCP: servidor desconectado da ponte.");
                }
                catch (Exception ex)
                {
                    // Parada normal aparece aqui como ObjectDisposed/IO — não é erro.
                    if (!_stopping) Log.Warn("MCP: laço da ponte tropeçou — " + ex.GetBaseException().Message);
                }
                finally
                {
                    try { if (pipe != null) pipe.Dispose(); } catch { }
                    lock (_gate) { if (ReferenceEquals(_current, pipe)) _current = null; }
                }
            }
        }

        /// <summary>Atende um cliente conectado: uma linha de JSON por pedido, uma por resposta.</summary>
        private void Serve(NamedPipeServerStream pipe)
        {
            var utf8 = new UTF8Encoding(false);
            using (var reader = new StreamReader(pipe, utf8, false, 4096, true))
            using (var writer = new StreamWriter(pipe, utf8, 4096, true) { AutoFlush = true })
            {
                while (!_stopping && pipe.IsConnected)
                {
                    string line = reader.ReadLine();
                    if (line == null) return;              // cliente fechou
                    if (line.Length == 0) continue;

                    BridgeResponse response = Handle(line);
                    writer.WriteLine(BridgeProtocol.Serialize(response));
                }
            }
        }

        private BridgeResponse Handle(string line)
        {
            BridgeRequest req;
            try { req = BridgeProtocol.Deserialize<BridgeRequest>(line); }
            catch (Exception ex) { return BridgeResponse.Bad(0, "Pedido ilegível: " + ex.GetBaseException().Message); }
            if (req == null) return BridgeResponse.Bad(0, "Pedido vazio.");

            if (req.Version != BridgeProtocol.Version)
                return BridgeResponse.Bad(req.Id,
                    $"Versão de contrato incompatível: o servidor MCP fala v{req.Version} e este add-in fala " +
                    $"v{BridgeProtocol.Version}.\n\n" +
                    "Quase sempre isto é pacote novo instalado com a Solid Edge AINDA ABERTA: ela mantém o add-in " +
                    "ANTIGO em memória. Feche e reabra a Solid Edge.");

            lock (_gate) { Served++; LastTool = req.Tool; }

            try { return _marshal(req); }
            catch (Exception ex)
            {
                Log.Error("MCP: marshaling para a thread da Solid Edge falhou.", ex);
                return BridgeResponse.Bad(req.Id,
                    "Não foi possível entregar o pedido à thread da Solid Edge: " + ex.GetBaseException().Message +
                    "\n\nSe a SE estiver com uma caixa de diálogo aberta, feche-a e tente de novo.");
            }
        }

        public void Dispose() => Stop();
    }
}
