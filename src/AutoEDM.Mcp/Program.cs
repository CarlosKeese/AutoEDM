using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoEDM.Mcp;

namespace AutoEDM.Mcp.Server;

/// <summary>
/// Servidor MCP do AutoEDM: JSON-RPC 2.0 em stdio, uma mensagem por linha.
///
/// O protocolo é implementado à mão de propósito, em vez de por um SDK. A regra de ouro do
/// projeto é que nenhuma assinatura é inventada — e usar um pacote cuja API eu teria de
/// adivinhar é pior que implementar um protocolo publicado e estável de ~4 métodos
/// (initialize, tools/list, tools/call, ping). Zero dependência nova, e o comportamento é
/// todo auditável aqui.
///
/// O trabalho de verdade é do add-in dentro da Solid Edge; este processo é um tradutor.
/// </summary>
internal static class Program
{
    private const string ServerName = "autoedm";

    /// <summary>Versão do protocolo usada quando o cliente não declara a dele.</summary>
    private const string DefaultProtocolVersion = "2025-06-18";

    /// <summary>stdout DE VERDADE: só o protocolo escreve aqui.</summary>
    private static TextWriter _protocol = TextWriter.Null;

    private static readonly BridgeClient Bridge = new();

    private static int Main()
    {
        // PRIMEIRA COISA, antes de qualquer outra linha de código: stdout é o transporte do
        // MCP, e o AutoEDM.Core loga com Console.WriteLine. Sem este desvio, a primeira
        // mensagem de log do Core entraria no meio do JSON-RPC e derrubaria a sessão com um
        // erro de parsing que não aponta para lugar nenhum.
        // UTF-8 EXPLÍCITO nas duas pontas, sem BOM. Hoje isto é cinto de segurança: o
        // System.Text.Json escapa todo caractere não-ASCII (as descrições em português saem como
        // é etc.), então o que trafega é ASCII puro e a codepage do console não interfere.
        // Mas essa é uma propriedade do encoder padrão, não do protocolo — trocá-lo por
        // UnsafeRelaxedJsonEscaping faria acento cru atravessar o Console.Out do Windows e virar
        // '?' em silêncio. Amarrando a codificação aqui, essa troca deixa de ser uma armadilha.
        var utf8 = new UTF8Encoding(false);
        _protocol = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = false };
        Console.SetOut(Console.Error);

        var stdin = new StreamReader(Console.OpenStandardInput(), utf8);

        try
        {
            while (true)
            {
                string? line = stdin.ReadLine();
                if (line is null) return 0;          // o cliente fechou: fim normal
                if (line.Length == 0) continue;

                JsonNode? response = Handle(line);
                if (response is null) continue;      // era notificação: não se responde

                Write(response);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("AutoEDM.Mcp: encerrando por erro — " + ex);
            return 1;
        }
        finally
        {
            Bridge.Dispose();
        }
    }

    private static void Write(JsonNode response)
    {
        _protocol.WriteLine(response.ToJsonString());
        _protocol.Flush();
    }

    /// <summary>Trata uma mensagem. Devolve null quando não há o que responder (notificação).</summary>
    private static JsonNode? Handle(string line)
    {
        JsonNode? msg;
        try { msg = JsonNode.Parse(line); }
        catch (Exception ex) { return Error(null, -32700, "JSON inválido: " + ex.Message); }
        if (msg is null) return Error(null, -32600, "Mensagem vazia.");

        string method = msg["method"]?.GetValue<string>() ?? "";
        JsonNode? id = msg["id"];

        // Sem "id" é NOTIFICAÇÃO: o protocolo proíbe responder.
        if (id is null)
        {
            if (method == "notifications/cancelled")
                Console.Error.WriteLine("AutoEDM.Mcp: o cliente cancelou um pedido.");
            return null;
        }

        try
        {
            switch (method)
            {
                case "initialize": return Ok(id, Initialize(msg["params"]));
                case "ping": return Ok(id, new JsonObject());
                case "tools/list": return Ok(id, ToolsList());
                case "tools/call": return Ok(id, ToolsCall(msg["params"]));
                default:
                    return Error(id, -32601, $"Método não suportado: '{method}'. Este servidor expõe apenas ferramentas.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"AutoEDM.Mcp: '{method}' falhou — {ex}");
            return Error(id, -32603, "Erro interno: " + ex.Message);
        }
    }

    private static JsonObject Initialize(JsonNode? prms)
    {
        // Ecoa a versão que o cliente pediu. Um servidor só de ferramentas é compatível com
        // todas as revisões em uso, e ecoar evita travar numa data fixa que envelhece.
        string version = prms?["protocolVersion"]?.GetValue<string>() ?? DefaultProtocolVersion;

        return new JsonObject
        {
            ["protocolVersion"] = version,
            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
            ["serverInfo"] = new JsonObject
            {
                ["name"] = ServerName,
                ["version"] = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0"
            },
            ["instructions"] =
                "Dirige o Solid Edge através do add-in AutoEDM. Chame 'se_status' PRIMEIRO: ele diz se a ponte está " +
                "ligada, qual documento está ativo, o ambiente de modelagem (síncrono/ordenado) e se a escrita está " +
                "liberada. Quase toda ferramenta exige um tipo de documento e um ambiente específicos, e recusa com " +
                "explicação quando o pré-requisito não bate — o AutoEDM nunca troca o ambiente de modelagem sozinho, " +
                "porque a troca reconstrói o corpo e invalida as faces já lidas. As ferramentas marcadas como de " +
                "escrita só funcionam depois que o usuário clicar em 'Liberar escrita' no grupo MCP da ribbon; nenhum " +
                "agente pode ligar essa chave. Para descobrir a API COM real do Solid Edge, use " +
                "'se_inspecionar_selecao' em vez de supor uma assinatura."
        };
    }

    private static JsonObject ToolsList()
    {
        var tools = new JsonArray();
        foreach (ToolSpec spec in ToolCatalog.All)
        {
            // O schema vive no catálogo como texto; aqui ele tem de virar OBJETO JSON, senão o
            // cliente recebe uma string onde espera um schema e a ferramenta fica inutilizável.
            JsonNode schema;
            try { schema = JsonNode.Parse(spec.InputSchemaJson) ?? new JsonObject(); }
            catch { schema = new JsonObject { ["type"] = "object" }; }

            string description = spec.Description;
            if (spec.Writes)
                description += "\n\nESCREVE: só funciona com a ponte em modo escrita (o usuário libera na ribbon do AutoEDM).";

            tools.Add(new JsonObject
            {
                ["name"] = spec.Name,
                ["description"] = description,
                ["inputSchema"] = schema
            });
        }
        return new JsonObject { ["tools"] = tools };
    }

    private static JsonObject ToolsCall(JsonNode? prms)
    {
        string name = prms?["name"]?.GetValue<string>() ?? "";
        if (name.Length == 0) return TextResult("Faltou o nome da ferramenta ('name').", isError: true);

        if (ToolCatalog.Find(name) is null)
        {
            var known = new List<string>();
            foreach (ToolSpec t in ToolCatalog.All) known.Add(t.Name);
            return TextResult($"Ferramenta desconhecida: '{name}'. Disponíveis: {string.Join(", ", known)}.", isError: true);
        }

        // Os argumentos seguem como JSON cru: quem sabe interpretá-los é a ponte, do lado da SE.
        JsonNode? args = prms?["arguments"];
        string argsJson = args?.ToJsonString() ?? "{}";

        BridgeResponse res = Bridge.Call(name, argsJson);
        return TextResult(res.Text ?? "(a ponte respondeu sem texto)", isError: !res.Ok);
    }

    private static JsonObject TextResult(string text, bool isError) => new()
    {
        ["content"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = text } },
        ["isError"] = isError
    };

    private static JsonObject Ok(JsonNode id, JsonNode result) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id.DeepClone(),
        ["result"] = result
    };

    private static JsonObject Error(JsonNode? id, int code, string message) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message }
    };
}
