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
        private readonly string _pipeName;
        private readonly object _gate = new object();

        private Thread _thread;
        private NamedPipeServerStream _current;
        /// <summary>O pipe já criado pelo <see cref="Start"/>, que a thread usa na 1ª volta em vez
        /// de criar um novo — é o que fecha a janela de corrida na reserva do nome.</summary>
        private NamedPipeServerStream _pending;
        private volatile bool _stopping;

        /// <summary>true entre o <see cref="Start"/> bem-sucedido e o <see cref="Stop"/>.</summary>
        public bool Running { get; private set; }

        /// <summary>Quantos pedidos esta ponte já atendeu — aparece no "Status" da ribbon, e é
        /// como o Carlos confirma num piscar que o agente chegou de verdade até a SE.</summary>
        public int Served { get; private set; }

        /// <summary>Nome da última ferramenta pedida, para o mesmo Status.</summary>
        public string LastTool { get; private set; }

        /// <param name="pipeName">
        /// Nome do pipe. O padrão é o de produção (<see cref="BridgeProtocol.PipeName"/>); existe
        /// como parâmetro para os TESTES poderem subir uma ponte própria com nome único. Sem isso
        /// a suíte falha sempre que a Solid Edge estiver aberta com a ponte ligada — ela já é a
        /// dona desse nome —, e um teste que só passa com o CAD fechado não serve.
        /// </param>
        public BridgeServer(Func<BridgeRequest, BridgeResponse> marshal, string pipeName = null)
        {
            _marshal = marshal ?? throw new ArgumentNullException(nameof(marshal));
            _pipeName = string.IsNullOrEmpty(pipeName) ? BridgeProtocol.PipeName : pipeName;
        }

        /// <summary>O nome que esta ponte está (ou estaria) escutando.</summary>
        public string PipeName { get { return _pipeName; } }

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

                // Cria o pipe REAL aqui, de forma síncrona, e entrega-o à thread. Antes isto era
                // um pipe de teste criado-e-descartado, com a thread criando o de verdade depois:
                // entre o descarte e a criação havia uma JANELA em que um segundo hospedeiro
                // passava a reservar o mesmo nome e os dois se davam por no ar. Criar o definitivo
                // agora fecha a janela e mantém a vantagem de o erro "já existe" chegar ao usuário
                // no próprio clique, em vez de escondido num log de thread de fundo.
                NamedPipeServerStream first;
                try
                {
                    first = CreatePipe();
                }
                catch (IOException)
                {
                    error = $"O pipe '{_pipeName}' já está em uso — provavelmente outra instância da " +
                            "Solid Edge (ou outra sessão) já está hospedando a ponte. Só uma pode hospedar.";
                    return false;
                }
                catch (Exception ex)
                {
                    error = "Não foi possível criar o pipe: " + ex.GetBaseException().Message;
                    return false;
                }

                _stopping = false;
                _current = first;
                _pending = first;
                _thread = new Thread(AcceptLoop) { IsBackground = true, Name = "AutoEDM.Bridge" };
                _thread.Start();
                Running = true;
                Log.Info($"MCP: ponte no ar em '{_pipeName}' (contrato v{BridgeProtocol.Version}).");
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
                // Se o Start reservou um pipe e a thread ainda não o consumiu, ele vaza sem isto.
                try { if (_pending != null) _pending.Dispose(); } catch { }
                _pending = null;
            }

            // Cinto e suspensório: se o Dispose acima não desbloqueou (varia entre versões do
            // Windows), uma conexão-fantasma tira a thread do WaitForConnection.
            try
            {
                using (var poke = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut))
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

            return new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.None, 0, 0, security);
#else
            // Este alvo (net8.0-windows) existe só para os TESTES do Core rodarem fora do CAD —
            // a ponte de verdade sempre sobe no add-in, que é net472.
            return new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1,
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
                    lock (_gate)
                    {
                        // 1ª volta: usa o pipe que o Start já reservou. Dali em diante, cria.
                        pipe = _pending;
                        _pending = null;
                    }
                    if (pipe == null) pipe = CreatePipe();
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
