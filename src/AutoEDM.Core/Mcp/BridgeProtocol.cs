using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoEDM.Mcp
{
    /// <summary>
    /// Contrato de fio entre o servidor MCP (processo separado, net8.0) e a ponte
    /// hospedada DENTRO do Solid Edge (add-in, net472).
    ///
    /// Por que dois processos: o add-in é obrigatoriamente net472 (a SE hospeda
    /// .NET Framework in-process) e um servidor MCP precisa de net8+. Esta classe vive
    /// no <c>AutoEDM.Core</c>, que já compila para os DOIS alvos — então o contrato é
    /// escrito uma vez e os dois lados usam o MESMO tipo, em vez de duas cópias que
    /// divergem no primeiro campo novo.
    ///
    /// Formato: uma linha de JSON por mensagem (JSON compacto nunca contém '\n' cru;
    /// texto com quebra de linha viaja escapado como \n dentro da string). Ler por
    /// linha dispensa enquadramento por tamanho e é depurável com um olho só.
    /// </summary>
    public static class BridgeProtocol
    {
        /// <summary>
        /// Nome do named pipe. Fixo de propósito: o servidor MCP é iniciado pelo Claude Code
        /// e não tem como descobrir um nome sorteado pelo add-in. Com duas instâncias da SE
        /// abertas, a PRIMEIRA hospeda a ponte e a segunda registra no log que já existe uma
        /// — em vez de as duas brigarem pelo mesmo nome em silêncio.
        /// </summary>
        public const string PipeName = "AutoEDM.Bridge.v1";

        /// <summary>Versão do contrato. O servidor MCP recusa uma ponte de versão diferente
        /// em vez de falhar num campo ausente mais tarde — o caso real é o usuário atualizar
        /// o pacote e deixar a SE aberta com o add-in ANTIGO ainda em memória.</summary>
        public const int Version = 1;

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            // Sem indentação: a mensagem TEM de caber numa linha.
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

        public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
    }

    /// <summary>Pedido do servidor MCP para a ponte: execute esta ferramenta com estes argumentos.</summary>
    public sealed class BridgeRequest
    {
        [JsonPropertyName("id")] public int Id { get; set; }

        /// <summary>Nome da ferramenta (ver <see cref="ToolCatalog"/>).</summary>
        [JsonPropertyName("tool")] public string Tool { get; set; }

        /// <summary>Argumentos como JSON cru — a ponte é que sabe interpretar cada ferramenta.</summary>
        [JsonPropertyName("args")] public string ArgsJson { get; set; }

        [JsonPropertyName("v")] public int Version { get; set; } = BridgeProtocol.Version;
    }

    /// <summary>Resposta da ponte. <see cref="Text"/> é o que o agente lê.</summary>
    public sealed class BridgeResponse
    {
        [JsonPropertyName("id")] public int Id { get; set; }

        /// <summary>false = a ferramenta recusou ou falhou; <see cref="Text"/> explica o motivo.</summary>
        [JsonPropertyName("ok")] public bool Ok { get; set; }

        [JsonPropertyName("text")] public string Text { get; set; }

        [JsonPropertyName("v")] public int Version { get; set; } = BridgeProtocol.Version;

        public static BridgeResponse Good(int id, string text) =>
            new BridgeResponse { Id = id, Ok = true, Text = text };

        public static BridgeResponse Bad(int id, string text) =>
            new BridgeResponse { Id = id, Ok = false, Text = text };
    }
}
