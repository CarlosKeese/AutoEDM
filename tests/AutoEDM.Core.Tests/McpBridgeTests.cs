using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using AutoEDM.Mcp;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// Testes da ponte MCP que rodam SEM Solid Edge. Cobrem as três coisas que dariam errado em
    /// silêncio: o contrato de fio, o catálogo que o agente lê, e — a mais importante — a
    /// recusa das ferramentas que escrevem quando a ponte está em somente-leitura.
    /// </summary>
    public class McpBridgeTests
    {
        /// <summary>
        /// Nome de pipe único por teste. A suíte NÃO pode usar o nome de produção: se a Solid Edge
        /// estiver aberta com a ponte ligada, ela já é a dona dele e todo teste de pipe falharia —
        /// um teste que só passa com o CAD fechado não serve de rede.
        /// </summary>
        private static string FreshPipe() => "AutoEDM.Test." + System.Guid.NewGuid().ToString("N");

        // ------------------------------------------------------------- contrato de fio

        [Fact]
        public void Request_RoundTrips()
        {
            var sent = new BridgeRequest { Id = 7, Tool = "se_status", ArgsJson = "{\"linhas\":10}" };

            BridgeRequest back = BridgeProtocol.Deserialize<BridgeRequest>(BridgeProtocol.Serialize(sent));

            Assert.Equal(7, back.Id);
            Assert.Equal("se_status", back.Tool);
            Assert.Equal("{\"linhas\":10}", back.ArgsJson);
            Assert.Equal(BridgeProtocol.Version, back.Version);
        }

        [Fact]
        public void Response_RoundTrips_AndKeepsNewlinesEscaped()
        {
            // O enquadramento é POR LINHA: se um texto com \n viajasse cru, ele partiria a
            // mensagem em duas e o leitor travaria no meio de um JSON.
            var sent = BridgeResponse.Good(3, "linha 1\nlinha 2\nlinha 3");

            string wire = BridgeProtocol.Serialize(sent);
            Assert.DoesNotContain("\n", wire);
            Assert.DoesNotContain("\r", wire);

            BridgeResponse back = BridgeProtocol.Deserialize<BridgeResponse>(wire);
            Assert.True(back.Ok);
            Assert.Equal("linha 1\nlinha 2\nlinha 3", back.Text);
        }

        // ---------------------------------------------------------------- catálogo

        [Fact]
        public void Catalog_HasUniqueNames()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ToolSpec t in ToolCatalog.All)
                Assert.True(seen.Add(t.Name), $"nome de ferramenta repetido: {t.Name}");
        }

        [Fact]
        public void Catalog_EverySchemaIsValidJsonObject()
        {
            // O servidor MCP publica isto como inputSchema. Schema inválido = ferramenta
            // inutilizável no cliente, e o erro apareceria só em produção.
            foreach (ToolSpec t in ToolCatalog.All)
            {
                using JsonDocument doc = JsonDocument.Parse(t.InputSchemaJson);
                Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
                Assert.Equal("object", doc.RootElement.GetProperty("type").GetString());
            }
        }

        [Fact]
        public void Catalog_EveryToolHasDescription()
        {
            foreach (ToolSpec t in ToolCatalog.All)
            {
                Assert.False(string.IsNullOrWhiteSpace(t.Name));
                // A descrição é como o agente escolhe a ferramenta certa; uma linha solta não basta.
                Assert.True(t.Description != null && t.Description.Length > 60,
                    $"'{t.Name}' precisa de uma descrição que diga o pré-requisito e o efeito.");
            }
        }

        [Fact]
        public void Catalog_Find_IsCaseInsensitive_AndRejectsUnknown()
        {
            Assert.NotNull(ToolCatalog.Find("SE_STATUS"));
            Assert.NotNull(ToolCatalog.Find("se_status"));
            Assert.Null(ToolCatalog.Find("se_apagar_tudo"));
            Assert.Null(ToolCatalog.Find(null));
        }

        [Fact]
        public void Catalog_WritingToolsAreExactlyTheExpectedSet()
        {
            // Trava a fronteira: qualquer ferramenta NOVA que altere o modelo tem de aparecer
            // aqui de propósito. Sem este teste, um Writes esquecido em false passaria a
            // permitir escrita com a ponte em somente-leitura — e ninguém notaria.
            var writers = new List<string>();
            foreach (ToolSpec t in ToolCatalog.All) if (t.Writes) writers.Add(t.Name);
            writers.Sort(StringComparer.Ordinal);   // a ordem no catálogo é de apresentação, não contrato

            Assert.Equal(
                new[]
                {
                    "se_alojamento_oring", "se_aplicar_gap", "se_criar_base", "se_criar_eletrodo_manual",
                    "se_criar_eletrodos", "se_curvas_superficies", "se_duplicar_eletrodo", "se_exportar_perfis_wedm",
                    "se_ficha", "se_modelar", "se_nova_peca", "se_refrigeracao", "se_sonda_interpart", "se_sonda_rosca", "se_trocar_ambiente",
                    "se_unir_superficies"
                },
                writers.ToArray());
        }

        // ------------------------------------------------- a chave de escrita (segurança)

        [Fact]
        public void ReadOnlyMode_RefusesEveryWritingTool_WithoutTouchingSolidEdge()
        {
            // appProvider devolve null de propósito: se a recusa NÃO viesse antes de tocar no
            // Application, este teste veria "add-in não inicializado" em vez da recusa — e a
            // ordem é o que importa, porque é ela que garante que o modo é conferido primeiro.
            var runner = new SeToolRunner(
                appProvider: () => null,
                modeProvider: () => BridgeMode.ReadOnly,
                logPathProvider: () => null);

            foreach (ToolSpec t in ToolCatalog.All)
            {
                if (!t.Writes) continue;

                BridgeResponse res = runner.Execute(new BridgeRequest { Id = 1, Tool = t.Name, ArgsJson = "{}" });

                Assert.False(res.Ok, $"'{t.Name}' deveria ser RECUSADA em somente-leitura.");
                Assert.Contains("SOMENTE-LEITURA", res.Text);
                // A recusa tem de dizer o que fazer, senão o agente só repete a chamada.
                Assert.Contains("Liberar escrita", res.Text);
            }
        }

        [Fact]
        public void WriteMode_LetsWritingToolsThrough_ToTheSolidEdgeCheck()
        {
            // Com a escrita liberada, a recusa por modo sai do caminho e a ferramenta chega ao
            // passo seguinte — que aqui falha por não haver SE, e é exatamente o que se espera.
            var runner = new SeToolRunner(
                appProvider: () => null,
                modeProvider: () => BridgeMode.Write,
                logPathProvider: () => null);

            BridgeResponse res = runner.Execute(
                new BridgeRequest { Id = 1, Tool = "se_curvas_superficies", ArgsJson = "{}" });

            Assert.DoesNotContain("SOMENTE-LEITURA", res.Text);
            Assert.Contains("Add-in não inicializado", res.Text);
        }

        [Fact]
        public void UnknownTool_IsRejected()
        {
            var runner = new SeToolRunner(() => null, () => BridgeMode.Write, () => null);

            BridgeResponse res = runner.Execute(new BridgeRequest { Id = 1, Tool = "rm_-rf", ArgsJson = "{}" });

            Assert.False(res.Ok);
            Assert.Contains("desconhecida", res.Text);
        }

        [Fact]
        public void ReadOnlyTools_FailGracefully_WithNoSolidEdge()
        {
            // Nenhuma ferramenta pode levantar exceção: ela morreria na thread da SE e o agente
            // veria só o pipe fechar sem motivo.
            var runner = new SeToolRunner(() => null, () => BridgeMode.ReadOnly, () => null);

            foreach (ToolSpec t in ToolCatalog.All)
            {
                if (t.Writes) continue;
                BridgeResponse res = runner.Execute(new BridgeRequest { Id = 1, Tool = t.Name, ArgsJson = "{}" });
                Assert.False(string.IsNullOrWhiteSpace(res.Text), $"'{t.Name}' respondeu vazio.");
            }
        }

        // ------------------------------------------------------ ponte de verdade, num pipe real

        [Fact]
        public void Bridge_CarriesACallEndToEnd_OverARealPipe()
        {
            // Servidor + cliente de verdade, no named pipe de verdade, com um marshaler falso no
            // lugar da Solid Edge. É o que prova o enquadramento por linha, a ida e a volta.
            string pipe = FreshPipe();
            BridgeRequest seen = null;
            var server = new BridgeServer(req =>
            {
                seen = req;
                return BridgeResponse.Good(req.Id, $"ecoando {req.Tool} com {req.ArgsJson}");
            }, pipe);

            string error;
            Assert.True(server.Start(out error), "a ponte não subiu: " + error);
            try
            {
                using var client = new BridgeClient(connectTimeoutMs: 5000, pipeName: pipe);

                BridgeResponse first = client.Call("se_status", "{}");
                Assert.True(first.Ok, first.Text);
                Assert.Equal("ecoando se_status com {}", first.Text);
                Assert.Equal("se_status", seen.Tool);

                // Segunda chamada na MESMA conexão: o laço tem de seguir servindo, não só atender
                // o primeiro pedido e parar.
                BridgeResponse second = client.Call("se_arvore", "{\"x\":1}");
                Assert.True(second.Ok, second.Text);
                Assert.Equal("ecoando se_arvore com {\"x\":1}", second.Text);
                Assert.Equal(2, server.Served);
                Assert.Equal("se_arvore", server.LastTool);

                // Os ids são distintos e voltam pareados — é o que permite casar resposta com pedido.
                Assert.NotEqual(first.Id, second.Id);
            }
            finally { server.Dispose(); }
        }

        [Fact]
        public void Bridge_RejectsAMismatchedContractVersion()
        {
            // O caso real: pacote novo instalado com a Solid Edge AINDA ABERTA, que segue com o
            // add-in antigo em memória. Sem esta checagem o sintoma seria um campo faltando
            // muito mais tarde, longe da causa.
            string pipeName = FreshPipe();
            var server = new BridgeServer(req => BridgeResponse.Good(req.Id, "não deveria chegar aqui"), pipeName);

            string error;
            Assert.True(server.Start(out error), "a ponte não subiu: " + error);
            try
            {
                using var pipe = new System.IO.Pipes.NamedPipeClientStream(
                    ".", pipeName, System.IO.Pipes.PipeDirection.InOut);
                pipe.Connect(5000);

                var utf8 = new System.Text.UTF8Encoding(false);
                using var writer = new System.IO.StreamWriter(pipe, utf8, 4096, true) { AutoFlush = true };
                using var reader = new System.IO.StreamReader(pipe, utf8, false, 4096, true);

                writer.WriteLine(BridgeProtocol.Serialize(
                    new BridgeRequest { Id = 1, Tool = "se_status", ArgsJson = "{}", Version = 999 }));

                BridgeResponse res = BridgeProtocol.Deserialize<BridgeResponse>(reader.ReadLine());
                Assert.False(res.Ok);
                Assert.Contains("incompatível", res.Text);
                Assert.Contains("Feche e reabra", res.Text);   // a instrução que resolve
            }
            finally { server.Dispose(); }
        }

        [Fact]
        public void Bridge_RefusesASecondHostOnTheSamePipe()
        {
            // Duas instâncias da Solid Edge com o add-in carregado: a primeira hospeda, a segunda
            // tem de DIZER isso em vez de as duas brigarem pelo nome em silêncio.
            string pipe = FreshPipe();
            var first = new BridgeServer(req => BridgeResponse.Good(req.Id, "primeiro"), pipe);
            string error;
            Assert.True(first.Start(out error), "a primeira ponte não subiu: " + error);
            try
            {
                var second = new BridgeServer(req => BridgeResponse.Good(req.Id, "segundo"), pipe);
                Assert.False(second.Start(out error));
                Assert.Contains("já está em uso", error);
                second.Dispose();
            }
            finally { first.Dispose(); }
        }

        [Fact]
        public void Client_ExplainsHowToTurnTheBridgeOn_WhenNobodyIsHosting()
        {
            // Sem ponte no ar, a resposta vai para o agente — então ela tem de trazer o passo a
            // passo, senão o agente só tenta de novo.
            // Nome novo e nunca hospedado: garante "ninguém escutando" mesmo com a ponte real no ar.
            using var client = new BridgeClient(connectTimeoutMs: 300, pipeName: FreshPipe());

            BridgeResponse res = client.Call("se_status", "{}");

            Assert.False(res.Ok);
            Assert.Contains("Ligar ponte", res.Text);
            Assert.Contains("Solid Edge", res.Text);
        }

        [Fact]
        public void Bridge_SurvivesTheClientReconnecting()
        {
            // O Claude Code é reiniciado muito mais vezes que o CAD: a ponte tem de aceitar um
            // cliente novo depois do primeiro ir embora, sem precisar ser religada na ribbon.
            string pipe = FreshPipe();
            var server = new BridgeServer(req => BridgeResponse.Good(req.Id, "vivo"), pipe);

            string error;
            Assert.True(server.Start(out error), "a ponte não subiu: " + error);
            try
            {
                using (var a = new BridgeClient(5000, pipe))
                    Assert.True(a.Call("se_status", "{}").Ok);

                // O laço de aceitação precisa de um instante para recriar o pipe após a queda.
                BridgeResponse second = null;
                for (int attempt = 0; attempt < 20 && (second == null || !second.Ok); attempt++)
                {
                    using var b = new BridgeClient(1000, pipe);
                    second = b.Call("se_status", "{}");
                    if (!second.Ok) Thread.Sleep(100);
                }

                Assert.True(second != null && second.Ok, "a ponte não aceitou um segundo cliente: " + second?.Text);
            }
            finally { server.Dispose(); }
        }
    }
}
