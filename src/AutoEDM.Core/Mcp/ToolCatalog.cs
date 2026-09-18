using System;
using System.Collections.Generic;

namespace AutoEDM.Mcp
{
    /// <summary>Uma ferramenta MCP, como o agente a vê.</summary>
    public sealed class ToolSpec
    {
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>JSON Schema do objeto de argumentos (o que o MCP chama de inputSchema).</summary>
        public string InputSchemaJson { get; set; }

        /// <summary>
        /// true = a ferramenta ALTERA a peça/montagem ou grava arquivo. Essas só rodam com a
        /// ponte em modo ESCRITA, ligado à mão pelo Carlos na ribbon.
        /// </summary>
        public bool Writes { get; set; }
    }

    /// <summary>Modo da ponte. Nasce em <see cref="ReadOnly"/> a cada sessão da SE.</summary>
    public enum BridgeMode
    {
        /// <summary>Só ferramentas de leitura. É o padrão, e volta a ser a cada vez que a SE abre.</summary>
        ReadOnly = 0,

        /// <summary>Leitura + as ferramentas que escrevem. Ligado explicitamente pelo usuário.</summary>
        Write = 1
    }

    /// <summary>
    /// As ferramentas que o agente pode chamar. Vive no <c>Core</c> (os dois alvos) porque o
    /// servidor MCP precisa responder <c>tools/list</c> com a Solid Edge FECHADA — o Claude
    /// Code pergunta as ferramentas ao subir, muito antes de existir um documento aberto.
    ///
    /// DESENHO: cada ferramenta pousa AO LADO da ribbon, sobre o mesmo <c>AutoEDM.Core</c> — e
    /// nunca em cima do handler da ribbon. Dois motivos concretos:
    ///
    /// • todo handler termina em <c>MessageBox.Show</c>, e um diálogo modal disparado por
    ///   agente TRAVA a thread da SE esperando um humano que não sabe que foi perguntado;
    /// • o Core é o que já está validado no SE (ver "Estado por ferramenta" no PROJECT_STATE) —
    ///   descer direto nele não cria um segundo caminho de automação para manter em pé.
    ///
    /// A guarda de ambiente (documento certo + síncrono/ordenado) continua valendo: é a mesma
    /// regra dos botões, aplicada em <see cref="SeToolRunner"/>.
    /// </summary>
    public static class ToolCatalog
    {
        private const string NoArgs = "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}";

        public static readonly IReadOnlyList<ToolSpec> All = new List<ToolSpec>
        {
            // ---------------------------------------------------------------- leitura
            new ToolSpec
            {
                Name = "se_status",
                Writes = false,
                InputSchemaJson = NoArgs,
                Description =
                    "Estado da Solid Edge agora: versão, documentos abertos, qual é o ATIVO (nome, tipo peça/montagem/desenho), " +
                    "o ambiente de modelagem (síncrono ou ordenado), quantos objetos estão selecionados e em que modo a ponte " +
                    "está (somente-leitura ou escrita). Chame isto PRIMEIRO — quase toda outra ferramenta exige um tipo de " +
                    "documento e um ambiente específicos, e este é o jeito de saber se o pré-requisito está atendido antes de tentar."
            },
            new ToolSpec
            {
                Name = "se_inspecionar_selecao",
                Writes = false,
                InputSchemaJson =
                    "{\"type\":\"object\",\"properties\":{" +
                    "\"profundidade\":{\"type\":\"integer\",\"minimum\":0,\"maximum\":3," +
                    "\"description\":\"Quantos níveis de coleções filhas expandir (padrão 1). 0 = só o próprio objeto.\"}}," +
                    "\"additionalProperties\":false}",
                Description =
                    "INTROSPECÇÃO AO VIVO do que está selecionado na Solid Edge: tipo COM real, propriedades com valor, métodos " +
                    "e coleções expandidas. É a ferramenta para descobrir a API de verdade em vez de adivinhar uma assinatura — " +
                    "a regra do projeto é que nenhuma assinatura COM é inventada. Selecione no SE a feature, face, aresta, " +
                    "superfície ou ocorrência de interesse e chame. Não altera nada."
            },
            new ToolSpec
            {
                Name = "se_arvore",
                Writes = false,
                InputSchemaJson = NoArgs,
                Description =
                    "Estrutura do documento ATIVO. Em PEÇA: corpos (sólido ou de facetas/malha), features na ordem da árvore, " +
                    "superfícies de construção e curvas de construção. Em MONTAGEM: as ocorrências com nome e arquivo. " +
                    "Serve para saber o que existe no modelo antes de pedir qualquer operação. Não altera nada."
            },
            new ToolSpec
            {
                Name = "se_medir_selecao",
                Writes = false,
                InputSchemaJson = NoArgs,
                Description =
                    "Mede o que está selecionado: para cada face/aresta/corpo dá o tipo de geometria (plano, cilindro, cone, " +
                    "esfera, toro, B-spline, malha...) e a caixa envolvente em MILÍMETROS. Usa a leitura EXATA de extensão " +
                    "(GetExactRange), porque em aresta B-spline o GetRange devolve caixa inflada em ~0,005 mm por lado. " +
                    "Não altera nada."
            },
            new ToolSpec
            {
                Name = "se_analisar_z",
                Writes = false,
                InputSchemaJson = NoArgs,
                Description =
                    "MONTAGEM ativa: analisa (sem alterar nada) a cavidade e propõe os eletrodos por nível de profundidade Z, " +
                    "com a detecção de queima por cor e o laudo de USINABILIDADE — raio menor que a menor fresa, canto vivo, " +
                    "furo fundo demais, região fora do alcance. É o mesmo cálculo do botão 'Analisar (Z)'."
            },
            new ToolSpec
            {
                Name = "se_coordenadas",
                Writes = false,
                InputSchemaJson = NoArgs,
                Description =
                    "MONTAGEM ativa: para as ocorrências de eletrodo SELECIONADAS, devolve posição (X, Y, Z, rotação), o GAP e o " +
                    "Ra gravados na peça, a área de queima e a seção. Mesma leitura do botão 'Coordenadas'. Não altera nada."
            },
            new ToolSpec
            {
                Name = "se_log",
                Writes = false,
                InputSchemaJson =
                    "{\"type\":\"object\",\"properties\":{" +
                    "\"linhas\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":2000," +
                    "\"description\":\"Quantas linhas do FIM do log trazer (padrão 200).\"}},\"additionalProperties\":false}",
                Description =
                    "Fim do log do add-in nesta sessão da Solid Edge. Todas as sondas e ferramentas gravam o detalhe técnico " +
                    "ali — é onde está a resposta quando uma operação 'funcionou' mas o resultado não foi o esperado."
            },

            // ---------------------------------------------------- escrita (modo ESCRITA)
            new ToolSpec
            {
                Name = "se_curvas_superficies",
                Writes = true,
                InputSchemaJson = NoArgs,
                Description =
                    "ESCREVE NA PEÇA. Exige PEÇA (.par) em modelagem SÍNCRONA. Reconhece as superfícies (as selecionadas, ou " +
                    "todas as de construção se nada estiver selecionado) e cria na peça uma curva derivada sobre cada " +
                    "extremidade paralela ao plano XY — o contorno do fundo e o do topo, que é por onde o fio do WEDM corta. " +
                    "As curvas nascem na árvore como 'WEDM Z = XX.XX'; rodar de novo substitui as da rodada anterior."
            },
            new ToolSpec
            {
                Name = "se_exportar_perfis_wedm",
                Writes = true,
                InputSchemaJson = NoArgs,
                Description =
                    "GRAVA ARQUIVOS no disco (não altera o modelo). Exige PEÇA (.par) já salva, em modelagem SÍNCRONA. Lê as " +
                    "curvas de construção visíveis e grava na pasta da peça um .igs por altura Z, em milímetros e nas " +
                    "coordenadas da própria peça — retas e arcos exatos, B-splines com polos e nós originais, prontos para o " +
                    "Pitágoras. Rode 'se_curvas_superficies' antes se as curvas ainda não existirem."
            }
        };

        public static ToolSpec Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (ToolSpec t in All)
                if (string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }
    }
}
