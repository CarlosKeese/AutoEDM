using System;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace AutoEDM.Mcp
{
    /// <summary>
    /// Lado cliente da ponte, usado pelo servidor MCP (processo separado). Conecta sob demanda
    /// e reconecta sozinho: a Solid Edge abre e fecha muitas vezes durante a vida de UMA sessão
    /// do Claude Code, e o agente não deve ter de ser reiniciado por isso.
    /// </summary>
    public sealed class BridgeClient : IDisposable
    {
        private readonly int _connectTimeoutMs;
        private readonly string _pipeName;
        private NamedPipeClientStream _pipe;
        private StreamReader _reader;
        private StreamWriter _writer;
        private int _nextId = 1;

        /// <param name="pipeName">
        /// Nome do pipe; o padrão é o de produção. Parametrizado pelo mesmo motivo do
        /// <see cref="BridgeServer"/>: deixar a suíte rodar mesmo com a ponte de verdade ligada.
        /// </param>
        public BridgeClient(int connectTimeoutMs = 3000, string pipeName = null)
        {
            _connectTimeoutMs = connectTimeoutMs;
            _pipeName = string.IsNullOrEmpty(pipeName) ? BridgeProtocol.PipeName : pipeName;
        }

        public bool Connected
        {
            get { return _pipe != null && _pipe.IsConnected; }
        }

        /// <summary>
        /// Chama uma ferramenta. NUNCA levanta exceção: devolve uma resposta com
        /// <c>Ok = false</c> e um texto que explica o que fazer, porque esse texto vai direto
        /// para o agente — e "pipe não encontrado" sem instrução só gera uma segunda tentativa
        /// idêntica.
        /// </summary>
        public BridgeResponse Call(string tool, string argsJson)
        {
            int id = _nextId++;
            try
            {
                EnsureConnected();

                var req = new BridgeRequest { Id = id, Tool = tool, ArgsJson = argsJson };
                _writer.WriteLine(BridgeProtocol.Serialize(req));

                string line = _reader.ReadLine();
                if (line == null)
                {
                    Close();
                    return BridgeResponse.Bad(id,
                        "A ponte fechou a conexão no meio do pedido. A Solid Edge foi fechada, ou a ponte foi " +
                        "desligada no grupo MCP da ribbon. Chame de novo depois de reabrir.");
                }

                BridgeResponse res = BridgeProtocol.Deserialize<BridgeResponse>(line);
                return res ?? BridgeResponse.Bad(id, "A ponte respondeu algo ilegível.");
            }
            catch (TimeoutException)
            {
                Close();
                return BridgeResponse.Bad(id, NotListening());
            }
            catch (IOException ex)
            {
                Close();
                return BridgeResponse.Bad(id, "A conexão com a Solid Edge caiu: " + ex.GetBaseException().Message +
                                              "\n\n" + NotListening());
            }
            catch (Exception ex)
            {
                Close();
                return BridgeResponse.Bad(id, "Falha ao falar com a Solid Edge: " + ex.GetBaseException().Message);
            }
        }

        private static string NotListening() =>
            "Ninguém está hospedando a ponte do AutoEDM. Para ligar:\n" +
            "  1. abra a Solid Edge (o add-in AutoEDM precisa estar instalado e ativo);\n" +
            "  2. na aba AutoEDM da ribbon, grupo MCP, clique em \"Ligar ponte\".\n\n" +
            "A ponte NÃO sobe sozinha de propósito — quem decide se um agente alcança o CAD é o usuário.";

        private void EnsureConnected()
        {
            if (Connected) return;
            Close();

            var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
            pipe.Connect(_connectTimeoutMs);   // TimeoutException quando ninguém hospeda

            var utf8 = new UTF8Encoding(false);
            _pipe = pipe;
            _reader = new StreamReader(pipe, utf8, false, 4096, true);
            _writer = new StreamWriter(pipe, utf8, 4096, true) { AutoFlush = true };
        }

        private void Close()
        {
            try { if (_writer != null) _writer.Dispose(); } catch { }
            try { if (_reader != null) _reader.Dispose(); } catch { }
            try { if (_pipe != null) _pipe.Dispose(); } catch { }
            _writer = null; _reader = null; _pipe = null;
        }

        public void Dispose() => Close();
    }
}
