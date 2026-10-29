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
        private const string CoolingArgs =
            "{\"type\":\"object\",\"properties\":{" +
                    "\"diametroMm\":{\"type\":\"number\",\"description\":\"Ø do canal (6, 8, 10 ou 12). Padrão 8.\"}," +
                    "\"sobrefuroMm\":{\"type\":\"number\",\"description\":\"Quanto o furo passa do cruzamento. Padrão Ø/2.\"}," +
                    "\"roscaEngate\":{\"type\":\"string\",\"description\":\"Tamanho da rosca do engate como na base de furos (ex.: G1/4, R 1/4-19, 1/8-27 NPT). Padrão G1/4.\"}," +
                    "\"roscaTampao\":{\"type\":\"string\",\"description\":\"Tamanho da rosca do tampão. Padrão G1/8.\"}," +
                    "\"pontas\":{\"type\":\"object\",\"description\":\"Troca a terminação AUTOMÁTICA de uma boca (engate nas pontas do caminho, tampão nos prolongamentos); chave = a do plano (ex.: {\\\"L1:I\\\":\\\"engate\\\",\\\"L3:F\\\":\\\"cega\\\"}).\",\"additionalProperties\":{\"type\":\"string\"}}," +
                    "\"todasAsLinhas\":{\"type\":\"boolean\",\"description\":\"true = todas as linhas de todos os esboços 3D, ignorando a seleção.\"}}," +
                    "\"additionalProperties\":false}";

        private const string MoldBaseArgs =
            "{\"type\":\"object\",\"properties\":{" +
                    "\"familia\":{\"type\":\"string\",\"enum\":[\"polimold\",\"mud\",\"babyplast\",\"zamak\"],\"description\":\"Tipo de porta-molde: polimold (2 placas, catálogo), mud (o molde tipo gaveta: unidade 246 x 170 do quadro de troca rápida 7160) babyplast (metades 76 x 76) ou zamak (matriz MINI 160 x 160), cada um no padrão da biblioteca da casa (Padrões/Moldes). Padrão: o do porta-molde existente / o último do projeto.\"}," +
                    "\"guias\":{\"type\":\"boolean\",\"description\":\"Polimold: gerar colunas e buchas como peças. MUD/Babyplast: pôr as peças de BIBLIOTECA do padrão (quadro 7160, guias e puxadores / engate de extração). Padrão true.\"}," +
                    "\"pinosRetorno\":{\"type\":\"boolean\",\"description\":\"Gerar os pinos de retorno como peças (Polimold e Zamak; os padrões da gaveta e do Babyplast não têm). Padrão true.\"}," +
                    "\"automatico\":{\"type\":\"boolean\",\"description\":\"true = série, P1/P2, curso e partição escolhidos de novo pelas peças, mesmo com porta-molde já existente (o padrão mantém a configuração atual dele).\"}," +
                    "\"serie\":{\"type\":\"string\",\"description\":\"Força a série Polimold (ex.: 30.35 ou 3035). Série diferente da atual solta P1/P2/curso forçados (voltam ao automático, salvo se vierem junto).\"}," +
                    "\"p1Mm\":{\"type\":\"number\",\"description\":\"Força a espessura de P1 (lado fixo).\"}," +
                    "\"p2Mm\":{\"type\":\"number\",\"description\":\"Força a espessura de P2 (lado móvel).\"}," +
                    "\"curso\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":3,\"description\":\"Força o curso padronizado 1, 2 ou 3 da série.\"}," +
                    "\"tipo\":{\"type\":\"integer\",\"enum\":[2,4],\"description\":\"2 = rasgo de fixação; 4 = aba (placas base com largura B). Padrão 4.\"}," +
                    "\"aco\":{\"type\":\"integer\",\"enum\":[1,3],\"description\":\"1 = SAE 1045; 3 = P20 em P1/P2. Padrão 1.\"}," +
                    "\"montagem\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":6,\"description\":\"Montagem Polimold 1–6 (guias da extratora e pinos de retorno). Padrão 5.\"}," +
                    "\"colunamento\":{\"type\":\"string\",\"enum\":[\"A\",\"B\",\"C\"],\"description\":\"A = colunas nas cavidades; B = + placa base superior e suporte; C = todas. Padrão B.\"}," +
                    "\"centro\":{\"type\":\"string\",\"enum\":[\"origem\",\"pecas\"],\"description\":\"Centro do porta-molde na origem da montagem ou no centro das peças.\"}," +
                    "\"particaoMm\":{\"type\":[\"number\",\"string\"],\"description\":\"Linha de partição: coordenada da montagem no eixo de altura do projeto. Omitida = a gravada para o projeto, ou automática se nunca escolhida; auto (ou automatico=true) volta à automática (0 quando as peças a atravessam).\"}," +
                    "\"ladoFixoNegativo\":{\"type\":\"boolean\",\"description\":\"true = lado fixo (P1, PBS) para o sentido negativo do eixo de altura.\"}," +
                    "\"girar\":{\"type\":\"boolean\",\"description\":\"Permite girar 90° na planta quando cabe numa série menor. Padrão true.\"}," +
                    "\"margemMm\":{\"type\":\"number\",\"description\":\"Margem por lado entre as peças e a área útil X×Y (espaço da refrigeração). Padrão 20.\"}," +
                    "\"fundoMm\":{\"type\":\"number\",\"description\":\"Fundo mínimo de aço atrás das peças em P1/P2. Padrão 20.\"}," +
                    "\"folgaCursoMm\":{\"type\":\"number\",\"description\":\"Folga somada à altura a extrair. Padrão 10.\"}," +
                    "\"alturaExtrairMm\":{\"type\":\"number\",\"description\":\"Altura a extrair digitada; omitida = a dos produtos (ou das peças acima da partição).\"}," +
                    "\"canalMm\":{\"type\":\"number\",\"description\":\"Ø do canal de refrigeração para a conferência. Padrão 8.\"}," +
                    "\"excluir\":{\"type\":\"array\",\"items\":{\"type\":\"string\"},\"description\":\"Ocorrências (nome ou arquivo) que NÃO entram na caixa.\"}," +
                    "\"produtos\":{\"type\":\"array\",\"items\":{\"type\":\"string\"},\"description\":\"Ocorrências que são o PRODUTO (substitui a detecção: peça sem código NNNNN.NNN = produto).\"}}," +
                    "\"additionalProperties\":false}";

        private const string NoArgs = "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}";

        private const string FaceZeroArg =
            "{\"type\":\"object\",\"properties\":{" +
            "\"face_zero\":{\"type\":\"integer\",\"description\":\"Face.ID da face plana de onde partem as cotas de comprimento " +
            "(fica virada para a direita). Os números estão no fim de 'se_desenho_2d_plano'. Omitido = a ponta oposta à cabeça.\"}}," +
            "\"additionalProperties\":false}";

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
                Name = "se_planos",
                Writes = false,
                InputSchemaJson = NoArgs,
                Description =
                    "PEÇA ativa: lista os planos de referência (RefPlanes) por ÍNDICE, com nome e — onde a API deixa ler — o " +
                    "vetor NORMAL de cada um. Serve para descobrir qual índice é o plano XY, XZ ou YZ nesta peça ANTES de " +
                    "modelar, em vez de supor: é o índice que decide o EIXO da extrusão em 'se_modelar'. Chame isto antes de " +
                    "posicionar qualquer coisa cujo eixo não seja o vertical. Não altera nada."
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

            new ToolSpec
            {
                Name = "se_reconhecer_malha",
                InputSchemaJson =
                    "{\"type\":\"object\",\"properties\":{" +
                    "\"corpo\":{\"type\":\"integer\",\"minimum\":1,\"description\":\"Índice do corpo na peça (padrão 1).\"}," +
                    "\"toleranciaMm\":{\"type\":\"number\",\"description\":\"Tolerância de tesselação. Só muda algo em corpo B-rep; em corpo de facetas os triângulos já existem. Padrão 0,01.\"}," +
                    "\"anguloPlanoGraus\":{\"type\":\"number\",\"description\":\"Divergência máxima de normal dentro de um mesmo plano. Padrão 2.\"}," +
                    "\"distanciaPlanoMm\":{\"type\":\"number\",\"description\":\"Distância máxima de um vértice ao plano da região. Padrão 0,05.\"}," +
                    "\"anguloQuinaGraus\":{\"type\":\"number\",\"description\":\"Ângulo diedral que conta como QUINA e separa duas superfícies curvas. Padrão 35.\"}}," +
                    "\"additionalProperties\":false}",
                Description =
                    "PEÇA ativa, SÓ LEITURA: lê os triângulos do corpo (Body.GetFacetData) e RECONHECE superfícies sobre a " +
                    "malha — planos (com normal, ponto e extensão), cilindros (com eixo, Ø, comprimento e quanto da volta " +
                    "cobrem, então furo sai como furo) e cones (chanfro, escareado, saída: semi-ângulo, Ø das duas bocas, " +
                    "ápice). Abre por uma IDENTIFICAÇÃO agrupada pela medida: furos/pinos, raios, chanfros/cones e faces " +
                    "inclinadas (ângulo contra a vertical e a horizontal). Cada superfície vem com o RMS do próprio ajuste, em mm: é o número " +
                    "que separa medida de palpite. O que não cabe em primitiva é reportado como região LIVRE, e a ÁREA dessa " +
                    "sobra é o que decide se a peça se reconstrói por primitivas ou se exige superfície free-form. Funciona " +
                    "em corpo de facetas (malha importada) e em sólido B-rep (que é tesselado na hora). Não cria feature, não " +
                    "altera a peça, não salva."
            },

            new ToolSpec
            {
                Name = "se_malha_para_solido",
                InputSchemaJson =
                    "{\"type\":\"object\",\"properties\":{" +
                    "\"corpo\":{\"type\":\"integer\",\"minimum\":1,\"description\":\"Índice do corpo de malha na peça (padrão 1).\"}," +
                    "\"corpos\":{\"type\":\"array\",\"items\":{\"type\":\"integer\",\"minimum\":1},\"description\":\"LOTE: índices dos corpos de malha a converter, cada um numa peça nova.\"}," +
                    "\"todos\":{\"type\":\"boolean\",\"description\":\"LOTE: todos os corpos de malha da peça.\"}," +
                    "\"montagem\":{\"type\":\"boolean\",\"description\":\"Com 'construir': salva cada peça e reúne todas numa MONTAGEM nova, cada uma na posição original. Nenhum arquivo é sobrescrito.\"}," +
                    "\"pasta\":{\"type\":\"string\",\"description\":\"Pasta das peças e da montagem. Padrão: '<peça> - sólidos' ao lado da peça da malha (que então precisa estar salva).\"}," +
                    "\"construir\":{\"type\":\"boolean\",\"description\":\"true = cria o sólido numa PEÇA NOVA (exige escrita liberada). Padrão false = só o plano.\"}," +
                    "\"direcao\":{\"type\":\"string\",\"enum\":[\"auto\",\"X\",\"Y\",\"Z\"],\"description\":\"Direção de extrusão das faixas. Padrão auto: Z, a não ser que a previsão em Z acuse erro de forma — aí vence X ou Y pelo menor erro previsto.\"}," +
                    "\"arredondarMm\":{\"type\":\"number\",\"description\":\"Passo de arredondamento das medidas reconhecidas. Padrão 0,01; 0 desliga.\"}," +
                    "\"toleranciaCilindroMm\":{\"type\":\"number\",\"description\":\"Desvio radial aceito num cilindro. Padrão 0,005 (STL de CAD); suba para malha de scanner.\"}}," +
                    "\"additionalProperties\":false}",
                Description =
                    "PEÇA ativa com corpo de MALHA: converte a malha em SÓLIDO pela rota prismática (2,5D). Reconhece as " +
                    "superfícies, ESCOLHE A DIREÇÃO de extrusão (Z por padrão; X ou Y quando a PREVISÃO do sólido contra a " +
                    "malha, feita antes de construir, acusa erro de forma em Z — 'direcao' impõe), tira os níveis dos planos " +
                    "perpendiculares a ela, secciona cada faixa e troca cada trecho do " +
                    "contorno pela geometria EXATA (parede plana = reta, cilindro vertical = arco/círculo, no centésimo), " +
                    "com os vértices pela interseção exata das superfícies vizinhas. Devolve níveis, contornos, três " +
                    "volumes (malha, plano, EXATO) e avisos — faixa NÃO prismática (raio em aresta horizontal, chanfro, " +
                    "ângulo de saída) e trecho aproximado são denunciados, nunca escondidos. Abre pela IDENTIFICAÇÃO (furos, " +
                    "raios, chanfros, faces inclinadas) e planeja as CORREÇÕES DE FRONTEIRA: cone de volta inteira vira " +
                    "revolução, raio de eixo X/Y e face inclinada viram extrusão ao longo do eixo, cada uma protrusão ou " +
                    "recorte conforme a malha diz que há material. Sem 'construir' é só leitura. Com 'construir': true " +
                    "(escrita liberada) cria uma PEÇA NOVA ordenada (faixas + correções), mede o DESVIO geométrico nos dois " +
                    "sentidos (malha → sólido e sólido → malha) além do volume, e não salva. LOTE: 'corpos' (lista) ou 'todos' " +
                    "converte vários corpos de uma vez; com 'montagem' (e 'construir') salva cada peça na 'pasta' e reúne todas " +
                    "numa montagem nova, na posição original de cada malha — a resposta traz um resumo por corpo e o detalhe " +
                    "vai para o log. A peça da malha não é tocada."
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
                Name = "se_modelar",
                Writes = true,
                InputSchemaJson =
                    "{\"type\":\"object\",\"properties\":{" +
                    "\"exemplo\":{\"type\":\"string\",\"enum\":[\"carrinho\"]," +
                    "\"description\":\"Carga de teste pronta, em vez de uma lista à mão. 'carrinho' = carrinho de brinquedo (6 primitivas).\"}," +
                    "\"planoXY\":{\"type\":\"integer\",\"minimum\":1,\"description\":\"Índice do RefPlane de normal Z (use se_planos). Só com 'exemplo'.\"}," +
                    "\"planoXZ\":{\"type\":\"integer\",\"minimum\":1,\"description\":\"Índice do RefPlane de normal Y (use se_planos). Só com 'exemplo'.\"}," +
                    "\"novaPeca\":{\"type\":\"boolean\",\"description\":\"true = cria uma PEÇA NOVA e modela nela (não salva). Padrão false = usa a peça ativa.\"}," +
                    "\"primitivas\":{\"type\":\"array\",\"description\":\"Os sólidos a criar, em MILÍMETROS.\",\"items\":{" +
                    "\"type\":\"object\",\"properties\":{" +
                    "\"kind\":{\"type\":\"string\",\"enum\":[\"caixa\",\"cilindro\"]}," +
                    "\"name\":{\"type\":\"string\",\"description\":\"Rótulo, aparece no log e no relatório.\"}," +
                    "\"sizeXMm\":{\"type\":\"number\",\"description\":\"Caixa: lado no eixo U do plano.\"}," +
                    "\"sizeYMm\":{\"type\":\"number\",\"description\":\"Caixa: lado no eixo V do plano.\"}," +
                    "\"diameterMm\":{\"type\":\"number\",\"description\":\"Cilindro: diâmetro.\"}," +
                    "\"heightMm\":{\"type\":\"number\",\"description\":\"Altura da extrusão, ao longo da normal do plano.\"}," +
                    "\"planeIndex\":{\"type\":\"integer\",\"minimum\":1,\"description\":\"RefPlane do esboço — define o EIXO. Ver se_planos.\"}," +
                    "\"extrudeSide\":{\"type\":\"integer\",\"enum\":[1,2,3],\"description\":\"1 = contra a normal, 2 = a favor, 3 = simétrico.\"}," +
                    "\"liftMm\":{\"type\":\"number\",\"minimum\":0,\"description\":\"DISTÂNCIA que a base se desloca na normal. Nunca negativa.\"}," +
                    "\"liftSide\":{\"type\":\"integer\",\"enum\":[1,2],\"description\":\"Sentido do deslocamento: 2 = a favor da normal, 1 = contra.\"}," +
                    "\"centerXMm\":{\"type\":\"number\",\"description\":\"Centro da seção no eixo U do plano.\"}," +
                    "\"centerYMm\":{\"type\":\"number\",\"description\":\"Centro da seção no eixo V do plano.\"}}," +
                    "\"required\":[\"kind\",\"heightMm\"],\"additionalProperties\":false}}}," +
                    "\"additionalProperties\":false}",
                Description =
                    "ESCREVE NA PEÇA. Cria sólidos primitivos (caixas e cilindros) a partir de uma lista declarativa, em " +
                    "MILÍMETROS. Exige PEÇA (.par) em modelagem SÍNCRONA — é o ambiente da receita de extrusão já validada no " +
                    "SE. Protrusões sucessivas FUNDEM no mesmo corpo, então primitivas que se tocam saem como um sólido único. " +
                    "Cada primitiva declara o plano do esboço (planeIndex, que define o EIXO — descubra com 'se_planos'), o " +
                    "sentido da extrusão, o deslocamento da base e o centro da seção. Valida TUDO antes de tocar na peça: se " +
                    "alguma primitiva estiver inválida, nada é criado. Uma primitiva que falhe no CAD não aborta as outras — o " +
                    "relatório diz qual falhou e por quê. Passe 'exemplo':'carrinho' para a carga de teste pronta (um carrinho " +
                    "de brinquedo), e 'novaPeca':true para modelar numa peça nova em vez da ativa. Não salva nada."
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
            },

            // ------------------------------------------ ambiente de modelagem (modo ESCRITA)
            new ToolSpec
            {
                Name = "se_trocar_ambiente",
                Writes = true,
                InputSchemaJson =
                    "{\"type\":\"object\",\"properties\":{" +
                    "\"ambiente\":{\"type\":\"string\",\"enum\":[\"sincrono\",\"ordenado\"],\"description\":\"O ambiente de destino.\"}}," +
                    "\"required\":[\"ambiente\"],\"additionalProperties\":false}",
                Description =
                    "ALTERA A PEÇA. Troca a PEÇA ativa entre modelagem SÍNCRONA e ORDENADA — o mesmo que o botão de ambiente " +
                    "da barra de status do Solid Edge. É um passo À PARTE, de propósito: nenhuma outra ferramenta troca o " +
                    "ambiente sozinha, porque a troca reconstrói o corpo e toda face/aresta lida antes vira proxy morto. Por " +
                    "isso esta ferramenta DESCARTA a seleção: troque PRIMEIRO e só então peça ao usuário para selecionar. " +
                    "Síncrono serve a 'se_criar_base', 'se_unir_superficies', 'se_modelar' e ao WEDM; ordenado serve a " +
                    "'se_aplicar_gap' e 'se_alojamento_oring'."
            },

            // ------------------------------------------------- botões: montagem
            new ToolSpec
            {
                Name = "se_criar_eletrodos",
                Writes = true,
                InputSchemaJson =
                    "{\"type\":\"object\",\"properties\":{" +
                    "\"confirmar\":{\"type\":\"boolean\",\"description\":\"false (padrão) = só a CONFERÊNCIA da queima detectada, nada é criado. true = cria.\"}}," +
                    "\"additionalProperties\":false}",
                Description =
                    "Botão 'Criar eletrodos'. MONTAGEM ativa. Sem 'confirmar' devolve só a CONFERÊNCIA — a queima detectada " +
                    "por cor e quantos eletrodos sairiam — sem criar nada: MOSTRE ao usuário e só chame com 'confirmar':true " +
                    "depois do sim dele (é a pergunta que o botão faz). Com 'confirmar' cria uma peça VAZIA por eletrodo na " +
                    "subpasta 'Eletrodos' e insere posicionada. Não salva a montagem."
            },
            new ToolSpec
            {
                Name = "se_criar_eletrodo_manual",
                Writes = true,
                InputSchemaJson = NoArgs,
                Description =
                    "Botão 'Criar eletrodo (manual)'. MONTAGEM ativa com a(s) FACE(s) do fundo do bolsão SELECIONADA(s) (o " +
                    "usuário clica na peça e clica de novo no mesmo ponto, ou segura Alt). Cria e posiciona UMA peça no " +
                    "centro XY e no Z mais fundo das faces, com as faces escolhidas COPIADAS para dentro dela (superfície 'Queima', " +
                    "sem vínculo com a cavidade, na cor de origem, peça em síncrono), e grava o Ra lido pela cor. Não salva a montagem."
            },
            new ToolSpec
            {
                Name = "se_nova_peca",
                Writes = true,
                InputSchemaJson =
                    "{\"type\":\"object\",\"properties\":{" +
                    "\"parte\":{\"type\":\"string\",\"enum\":[\"fixa\",\"movel\",\"extracao\"],\"description\":\"Série do código: fixa = .100, movel = .200, extracao = .300. Padrão: a última usada no projeto.\"}," +
                    "\"eixoAltura\":{\"type\":\"string\",\"enum\":[\"X\",\"Y\",\"Z\"],\"description\":\"Eixo da MONTAGEM que é a altura do molde. Padrão: o gravado para o projeto (Z se nunca escolhido).\"}," +
                    "\"origem\":{\"type\":\"string\",\"enum\":[\"baixo\",\"alto\"],\"description\":\"Origem no ponto mais baixo ou mais alto das faces, no eixo de altura. Padrão: o gravado para o projeto.\"}," +
                    "\"apenasPlanejar\":{\"type\":\"boolean\",\"description\":\"true = só diz o nome e a origem que sairiam, sem criar.\"}}," +
                    "\"additionalProperties\":false}",
                Description =
                    "Botão 'Nova peça' (grupo Molde). MONTAGEM ativa com a(s) FACE(s) de referência SELECIONADA(s). Cria uma " +
                    "peça VAZIA codificada '<código do molde>.<NNN>.par' no próximo número livre da série da parte (fixa .100, " +
                    "móvel .200, extração .300), na pasta da montagem, e a insere com a ORIENTAÇÃO DA MONTAGEM, com a origem " +
                    "no centro das faces nos dois eixos de planta e no ponto mais baixo/alto no eixo de altura. As escolhas " +
                    "ficam gravadas para o projeto. Não salva a montagem."
            },
            new ToolSpec
            {
                Name = "se_porta_molde_plano",
                Writes = false,
                InputSchemaJson = MoldBaseArgs,
                Description =
                    "SÓ LEITURA. Plano do botão 'Porta-molde' na MONTAGEM ativa (tipos: Polimold, MUD, Babyplast — 'familia'): lê a caixa das peças do 1º nível (as selecionadas, " +
                    "se houver seleção), escolhe no catálogo Polimold de 2 placas a menor série cuja área útil X×Y comporta as peças + " +
                    "margem da refrigeração, as espessuras de P1/P2 (sub-série) pelo fundo mínimo e o curso pela altura do produto, e " +
                    "devolve o código de pedido, a pilha de placas com os furos e as conferências (área, espessuras, curso, corredor " +
                    "dos canais de refrigeração, flexão da placa suporte). Com porta-molde já existente, mostra o atual conferido com " +
                    "as peças de agora (automatico=true recalcula). Nada é alterado."
            },
            new ToolSpec
            {
                Name = "se_porta_molde",
                Writes = true,
                InputSchemaJson = MoldBaseArgs,
                Description =
                    "Botão 'Porta-molde' (grupo Molde). MONTAGEM ativa e SALVA. Cria a submontagem 'Porta Molde - <montagem>/Porta Molde.asm' " +
                    "ao lado da montagem, com uma peça ORDENADA por placa (Polimold: PBS, P1, P2, PS, espaçadores, CPE, PE, PBI; MUD/gaveta: 100, 200, 201, 301, 302; Babyplast: 100, 101, 200, 201, 300, 301; Zamak: cavaletes, fixo, móvel, paralelos, extratoras 1 e 2) guiada por " +
                    "VARIÁVEIS da peça, mais colunas, buchas e pinos de retorno (Polimold) como peças com o código do catálogo ou as peças de biblioteca do padrão (MUD/Babyplast) por referência, e a posiciona na partição. Se o porta-molde já existe, TROCA de porta-molde nas mesmas peças " +
                    "(valores das variáveis, furos refeitos só quando mudam de forma, pilha reposicionada) e regrava as peças e a " +
                    "submontagem. Rode 'se_porta_molde_plano' antes e mostre o plano. Não salva a montagem ativa."
            },
            new ToolSpec
            {
                Name = "se_refrigeracao_plano",
                Writes = false,
                InputSchemaJson = CoolingArgs,
                Description =
                    "SÓ LEITURA. Plano da refrigeração na PEÇA ativa: lê as linhas retas do(s) esboço(s) 3D (da seleção, ou de " +
                    "todos), prolonga até a face os trechos que acabam em cantos dentro da placa (medido na peça), acha as bocas (chaves 'L1:I' = início da linha 1, 'L1:F' = fim), junta linhas colineares " +
                    "numa passada só e lista os furos que 'se_refrigeracao' criaria — entrada, profundidade, ponta/fundo, " +
                    "roscas — e os problemas. Mostra também as roscas de tubo da base de furos. Nada é alterado."
            },
            new ToolSpec
            {
                Name = "se_refrigeracao",
                Writes = true,
                InputSchemaJson = CoolingArgs,
                Description =
                    "Botão 'Refrigeração' (grupo Molde). PEÇA em modelagem ORDENADA. Cria os canais a partir das linhas do esboço " +
                    "3D: um FURO do Ø escolhido por passada de broca, no plano normal à linha na ponta de entrada (fica preso à " +
                    "linha), com sobrefuro e ponta de broca nos cruzamentos; e um furo roscado de tubo coaxial em cada ponta " +
                    "marcada como engate/tampão. Rode 'se_refrigeracao_plano' antes e mostre o plano ao usuário. Não salva."
            },
            new ToolSpec
            {
                Name = "se_duplicar_eletrodo",
                Writes = true,
                InputSchemaJson = NoArgs,
                Description =
                    "Botão 'Duplicar eletrodo'. MONTAGEM ativa com a OCORRÊNCIA de UM eletrodo já com GAP aplicado " +
                    "selecionada. Cria a cópia com o GAP no PRÓXIMO Ra da escada (desbaste) e a posiciona em TODAS as " +
                    "posições onde o original aparece. Não salva a montagem."
            },
            new ToolSpec
            {
                Name = "se_lista_corte",
                Writes = false,
                InputSchemaJson = NoArgs,
                Description =
                    "Botão 'Lista de corte', em texto. MONTAGEM ativa com as ocorrências de eletrodo selecionadas: uma linha " +
                    "por arquivo com as posições, o perfil de cobre do estoque identificado pelas medidas e a medida na " +
                    "serra já com o sobremetal. Não altera nada (a impressão continua na janela do botão)."
            },
            new ToolSpec
            {
                Name = "se_ordenar_pathfinder_plano",
                Writes = false,
                InputSchemaJson = NoArgs,
                Description =
                    "Plano do botão 'Ordenar PathFinder'. MONTAGEM ativa: a ordem atual das ocorrências do 1º nível e a ordem " +
                    "em sequência pelo código do arquivo, com os números comparados pelo valor (15335.99 < 15335.100 < 15335.101; " +
                    "posições do mesmo arquivo por :1, :2…). Diz quantas mudam de lugar. Não altera nada."
            },
            new ToolSpec
            {
                Name = "se_ordenar_pathfinder",
                Writes = true,
                InputSchemaJson = NoArgs,
                Description =
                    "Botão 'Ordenar PathFinder'. Reordena as ocorrências do 1º nível da MONTAGEM ativa na sequência de " +
                    "'se_ordenar_pathfinder_plano' e confere a árvore lida de volta. Só a ordem muda — posição, relações e " +
                    "arquivos ficam como estão; submontagens não são abertas. Não salva a montagem."
            },
            new ToolSpec
            {
                Name = "se_lista_modificacoes",
                Writes = false,
                InputSchemaJson = NoArgs,
                Description =
                    "Botão 'Lista de modificações', só a varredura. MONTAGEM do molde ativa: lista as peças do projeto com " +
                    "grupo \"Rev.N\" na árvore ordenada, as operações do grupo e o que o usuário já escreveu na folha. " +
                    "Não altera nada e não gera a planilha (isso continua na janela)."
            },
            new ToolSpec
            {
                Name = "se_desenho_2d_plano",
                Writes = false,
                InputSchemaJson = FaceZeroArg,
                Description =
                    "SÓ LEITURA. Plano do 'Des. 2D (Torno)' para a PEÇA torneada ativa: lê cada face (plano, cilindro, cone, " +
                    "toro) no sistema do eixo, resolve a cor pela tabela da ferramentaria (estilo de face '11 - MED CALIB', " +
                    "'20 - VEDAÇÃO'... ou o RGB) e lista as cotas que 'se_desenho_2d' criaria — posições no eixo a partir da face zero, diâmetros, " +
                    "raios, chanfros e ângulos — com a tolerância de cada uma — e o GD&T (referências A/B e quadros de 0,01). " +
                    "No fim lista as faces planas que podem ser a face zero, com o número de cada uma. Recusa peça que não é de revolução. Nada é alterado."
            },
            new ToolSpec
            {
                Name = "se_desenho_2d",
                Writes = true,
                InputSchemaJson = FaceZeroArg,
                Description =
                    "Botão 'Des. 2D (Torno)'. Cria um DESENHO NOVO (.dft, sem salvar) da PEÇA torneada ativa a partir do template " +
                    "PanFlight: vista lateral com o eixo na horizontal e a face zero virada para a DIREITA (como no torno), vista de topo, " +
                    "isométrica e as cotas do plano — as de comprimento partindo da face zero — com a " +
                    "tolerância da cor e o GD&T das faces de precisão. Toda cota é conferida contra o 3D; a que não bate é apagada e relatada. Não altera a peça."
            },
            new ToolSpec
            {
                Name = "se_ficha",
                Writes = true,
                InputSchemaJson = NoArgs,
                Description =
                    "Botão 'Ficha (spec-sheet)'. GRAVA ARQUIVOS (.txt e .csv) na pasta do projeto, não altera o modelo. " +
                    "MONTAGEM ativa: uma ficha por eletrodo com Ra, pegada, blank, offset por Ra e fixação; o texto volta junto."
            },

            // ---------------------------------------------------- botões: peça
            new ToolSpec
            {
                Name = "se_criar_base",
                Writes = true,
                InputSchemaJson =
                    "{\"type\":\"object\",\"properties\":{" +
                    "\"apenasPlanejar\":{\"type\":\"boolean\",\"description\":\"true = só dimensiona e lista os blanks que servem, sem modelar.\"}," +
                    "\"material\":{\"type\":\"string\",\"enum\":[\"Cobre\",\"CuW80\"],\"description\":\"Padrão Cobre.\"}," +
                    "\"blank\":{\"type\":\"integer\",\"minimum\":0,\"description\":\"Número do blank na lista do plano. 0 (padrão) = automático, o mais compacto.\"}," +
                    "\"afastamentoMm\":{\"type\":\"number\",\"minimum\":0,\"description\":\"Espaço entre o topo das superfícies e a base. Padrão 0.\"}," +
                    "\"alturaMm\":{\"type\":\"number\",\"minimum\":3,\"description\":\"Altura do bloco. Padrão 15.\"}," +
                    "\"fixacao\":{\"type\":\"boolean\",\"description\":\"Furos/eixo de fixação. Padrão true.\"}," +
                    "\"faixa\":{\"type\":\"boolean\",\"description\":\"Faixa de medição de 5 mm com a orientação. Padrão true.\"}}," +
                    "\"additionalProperties\":false}",
                Description =
                    "Botão 'Criar Base', sem a janela. PEÇA ativa em SÍNCRONO, com a superfície de queima copiada. Cria o " +
                    "bloco no blank do catálogo, a faixa de medição e a fixação. Chame primeiro com 'apenasPlanejar':true " +
                    "para ver a pegada, o bloco e os blanks numerados, e depois sem ele (com 'blank':N para fixar uma " +
                    "barra). Não salva."
            },
            new ToolSpec
            {
                Name = "se_unir_superficies",
                Writes = true,
                InputSchemaJson = NoArgs,
                Description =
                    "Botão 'Unir superfícies'. PEÇA ativa em SÍNCRONO, já com o bloco. Une a superfície de queima ao bloco " +
                    "num sólido único (fecha os vãos laterais com 'Limite' quando precisa). SÓ une: o GAP vem depois, em " +
                    "ordenado, com 'se_aplicar_gap'. Não salva."
            },
            new ToolSpec
            {
                Name = "se_aplicar_gap",
                Writes = true,
                InputSchemaJson =
                    "{\"type\":\"object\",\"properties\":{" +
                    "\"ra\":{\"type\":\"number\",\"description\":\"Ra em µm, da tabela. Omitido = o Ra gravado na peça.\"}}," +
                    "\"additionalProperties\":false}",
                Description =
                    "Botão 'Aplicar GAP', sem a janela. PEÇA ativa em ORDENADO, com o corpo já unido. Aplica o offset de " +
                    "faísca do Ra (Model.FaceOffsets, editável na árvore), pinta a cor do Ra e nomeia a feature. Sem 'ra' " +
                    "usa o Ra gravado na peça; se não houver, devolve a tabela de Ra/GAP para escolher. Selecione as faces " +
                    "de queima antes, ou chame logo depois de um 'se_unir_superficies' bem-sucedido. Não salva."
            },
            new ToolSpec
            {
                Name = "se_alojamento_oring",
                Writes = true,
                InputSchemaJson =
                    "{\"type\":\"object\",\"properties\":{" +
                    "\"apenasPlanejar\":{\"type\":\"boolean\",\"description\":\"true (PADRÃO) = só calcula e lista. false = corta.\"}," +
                    "\"arestas\":{\"type\":\"array\",\"items\":{\"type\":\"integer\",\"minimum\":1},\"description\":\"Números das arestas circulares (da lista devolvida) que viram alojamento.\"}," +
                    "\"tipo\":{\"type\":\"string\",\"enum\":[\"eixo\",\"furo\",\"face\"],\"description\":\"Omitido = o que a geometria da face sugere.\"}," +
                    "\"vedacao\":{\"type\":\"string\",\"enum\":[\"estatica\",\"reciproca\",\"rotativa\"],\"description\":\"Padrão estática.\"}," +
                    "\"elastomero\":{\"type\":\"string\",\"enum\":[\"nbr\",\"fkm\"],\"description\":\"Padrão NBR.\"}," +
                    "\"metricos\":{\"type\":\"boolean\",\"description\":\"Inclui os anéis milimétricos DL Seals (no composto escolhido). Padrão false.\"}," +
                    "\"pressao\":{\"type\":\"string\",\"enum\":[\"interna\",\"externa\"],\"description\":\"Só canal de face: lado da pressão. interna (PADRÃO) = anel apoia no Ø externo do canal; externa/vácuo = no Ø interno.\"}," +
                    "\"secaoMm\":{\"type\":\"number\",\"description\":\"Seção do cordão (d2). Omitido = cada aresta usa a que o diâmetro dela pede.\"}," +
                    "\"afastamentoMm\":{\"type\":\"number\",\"minimum\":0,\"description\":\"Eixo/furo: aresta → centro do canal, no eixo (padrão 5). Face: parede entre a aresta e o canal (padrão e mínimo 1).\"}," +
                    "\"anel\":{\"type\":\"string\",\"description\":\"Designação do anel, só com UMA aresta. Omitido = o melhor da lista. Um anel trocado à mão tem o canal desenhado PARA ELE (ver 'calculo').\"}," +
                    "\"folgaMm\":{\"type\":\"number\",\"minimum\":0,\"description\":\"Eixo/furo: folga DIAMETRAL até o conjugado (furo que recebe o eixo / eixo que entra no furo) — a altura do alojamento é medida até ele, folga maior = canal mais raso. Omitido/0 = lido em contexto de montagem, senão o mesmo Ø nominal. Face: vão entre as faces montadas.\"}," +
                    "\"pressaoBar\":{\"type\":\"number\",\"minimum\":0,\"description\":\"Pressão de trabalho (padrão 10 bar, água de refrigeração). Decide se a folga pede anel de apoio.\"}," +
                    "\"ajuste\":{\"type\":\"string\",\"description\":\"Ajuste ISO furo/eixo do pior caso (padrão H7/g6; aceita H/h/g/f nos graus 6–8). 'nenhum' desliga o pior caso.\"}," +
                    "\"tolCanalMm\":{\"type\":\"number\",\"minimum\":0,\"description\":\"Tolerância ± das cotas do canal no pior caso (padrão 0,03).\"}," +
                    "\"calculo\":{\"type\":\"string\",\"enum\":[\"auto\",\"tabela\",\"anel\"],\"description\":\"auto (PADRÃO) = recomendado pela tabela (ou pelo anel quando nenhum anel serve na tabela), anel trocado à mão pelo anel. tabela/anel forçam.\"}," +
                    "\"acabamento\":{\"type\":\"boolean\",\"description\":\"Corta o raio do fundo e a quebra de canto depois do canal (PADRÃO true). Se a SE recusar, o canal fica e a resposta diz o que faltou.\"}," +
                    "\"aceitarForaDaNorma\":{\"type\":\"boolean\",\"description\":\"Corta mesmo com alojamento fora da norma. Só com o de acordo do usuário.\"}}," +
                    "\"additionalProperties\":false}",
                Description =
                    "Botão 'Alojamento de O'ring', sem a janela. PEÇA ativa em ORDENADO, com a FACE de vedação selecionada " +
                    "(cilíndrica = eixo/furo, plana = de face). Lista as arestas circulares da face, numeradas, e calcula por " +
                    "ISO 3601 o anel do catálogo e o canal de cada uma (esmagamento, estiramento, preenchimento). O canal criado " +
                    "recebe o nome do anel com número de instância e é pintado de laranja (vedação). Por padrão " +
                    "só PLANEJA: mostre ao usuário e corte com 'apenasPlanejar':false. Alojamento fora da norma só é cortado " +
                    "com 'aceitarForaDaNorma':true. Não salva."
            },

            // ------------------------------------------------ botões: diagnóstico
            new ToolSpec
            {
                Name = "se_sonda_malha",
                Writes = false,
                InputSchemaJson =
                    "{\"type\":\"object\",\"properties\":{" +
                    "\"seccionamento\":{\"type\":\"boolean\",\"description\":\"Inclui o teste de seccionamento, que CRIA esboços (tenta apagar, nunca salva). Exige escrita liberada.\"}}," +
                    "\"additionalProperties\":false}",
                Description =
                    "Botão 'Sonda de malha' (Eng. Reversa). PEÇA ativa com a malha importada: mede o que a API de malha da " +
                    "Solid Edge entrega sobre ela (corpos de facetas, triângulos, caixa, membros da API existentes). Só lê — " +
                    "a não ser com 'seccionamento':true, que cria esboços e por isso exige a escrita liberada."
            },
            new ToolSpec
            {
                Name = "se_sonda_interpart",
                Writes = true,
                InputSchemaJson = NoArgs,
                Description =
                    "Botão 'Sonda inter-part'. MONTAGEM ativa com a ocorrência da cavidade selecionada. Testa as rotas de " +
                    "cópia de faces entre peças em peças DESCARTÁVEIS (cria documentos novos, não altera a cavidade, não " +
                    "salva a montagem) e devolve o que registrou."
            },
            new ToolSpec
            {
                Name = "se_sonda_rosca",
                Writes = true,
                InputSchemaJson =
                    "{\"type\":\"object\",\"properties\":{" +
                    "\"ligarExibicaoRosca\":{\"type\":\"boolean\",\"description\":\"Liga a opção GLOBAL do Solid Edge 'exibir roscas' se estiver desligada. Muda a SE inteira — só com o pedido do usuário.\"}}," +
                    "\"additionalProperties\":false}",
                Description =
                    "Botão 'Sonda de rosca'. Cria uma PEÇA NOVA descartável (não salva) com um bloco e quatro furos M6, " +
                    "um por receita da API de rosca, e mede o Ø real de cada um. Não precisa de documento aberto."
            },
            new ToolSpec
            {
                Name = "se_gravador_iniciar",
                Writes = false,
                InputSchemaJson = NoArgs,
                Description =
                    "Botão 'Iniciar gravação'. Tira a foto das features e superfícies do documento ativo ANTES " +
                    "de o usuário fazer uma ação à mão no Solid Edge. Depois da ação, chame 'se_gravador_gravar'. Não altera nada."
            },
            new ToolSpec
            {
                Name = "se_gravador_gravar",
                Writes = false,
                InputSchemaJson = NoArgs,
                Description =
                    "Botão 'Encerrar gravação'. Compara com a foto do 'se_gravador_iniciar' e devolve o tipo e as " +
                    "propriedades das features que o usuário criou à mão — é assim que uma ação manual vira receita COM. " +
                    "Não altera nada."
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
