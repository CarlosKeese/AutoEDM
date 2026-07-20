# Recomendações e Observações de Arquitetura — Projeto AutoEDM

Este documento consolida a análise de arquitetura, pontos de atenção identificados e recomendações técnicas para o desenvolvimento dos stubs de automação do Solid Edge no projeto **AutoEDM** (automação de extração de eletrodos via COM).

O objetivo é servir de base para discussão e validação conjunta entre os membros do time (Claude e equipe de engenharia).

---

## 🔍 1. Pontos Observados na Arquitetura Atual

### A. Uso de Late Binding (`dynamic`)
*   **Vantagem:** O compilador não exige as bibliotecas de tipo (Type Libraries) do Solid Edge instaladas. Facilita o desenvolvimento em máquinas sem licenciamento e em pipelines de build automatizados.
*   **Risco/Atenção:** Em tempo de execução, chamadas dinâmicas perdem checagem de tipo estático e possuem penalidade de performance devido ao dispatch por reflexão do DLR.

### B. OLE Message Filter (`OleMessageFilter.cs`)
*   **Excelente implementação:** Impede que o aplicativo quebre com exceções de "Servidor Ocupado" (`0x8001010A - RPC_E_SERVERCALL_RETRYLATER`) enquanto o Solid Edge estiver realizando cálculos pesados de geometria ou reconstruindo a árvore.

### C. Captura de Bounding Box (`FaceGeometry.TryGetRangeMm`)
*   **Ponto de Risco:** O uso de `GetRange` e `GetExactRange` com arrays por referência (`out`) em chamadas dinâmicas exige o uso de `BindingFlags.InvokeMethod` e `ParameterModifier` explícitos. Esta implementação atual é correta e inovadora, mas a disponibilidade dos métodos depende da versão da API exposta pelo Solid Edge real.

### D. Separação de Regiões de Queima (`RegionSplitter.cs`)
*   **Estratégias de Divisão:** A divisão de faces da mesma cor em eletrodos independentes é resolvida de duas formas:
    1.  **Conectividade (Arestas):** Requer introspecção das propriedades `Edge.ID` ou `Edge.Tag`.
    2.  **Proximidade Espacial (AABB Gap):** Resolve casos onde as arestas não são lidas, medindo a folga tridimensional entre as caixas de contorno de cada face.

---

## 💡 2. Recomendações de Implementação para os Stubs (`ElectrodeBuilder`)

### A. Estratégia Híbrida de Build (Early vs Late Binding)
Recomenda-se adicionar suporte a compilação condicional para uso de **Early Binding** durante o desenvolvimento local ativo.
*   **Como fazer:**
    ```csharp
    #if EARLY_BINDING
        using SolidEdgeFramework;
        using SolidEdgePart;
        // Permite IntelliSense completo
    #else
        // Mantém dynamic para builds portáveis
    #endif
    ```

### B. Sequência do Inter-Part Copy (`InterPartCopier.CopyBurnFaces`)
A cópia associativa de faces da cavidade para o eletrodo exige que o documento do eletrodo esteja ativo em contexto na montagem:
1.  **In-Place Edit:** Ativar o arquivo do eletrodo (`eletrodo_destino.par`) dentro da montagem ativa.
2.  **Criação da Cópia:** Inserir a cópia de parte utilizando o método `AddCopiedPart` na coleção `Models` do documento ativo:
    ```csharp
    electrodePart.Models.AddCopiedPart(FileName: assemblyOccurrencePath, ...);
    ```
3.  **Preservação das Cores:** Caso as faces fiquem com a cor cinza padrão na cópia, mapear topologicamente os índices das faces no eletrodo de destino para reaplicar o estilo de face/cor correspondente à rugosidade $Ra$.

### C. Algoritmo de Offset Dinâmico (Spark Gap)
O deslocamento dimensional (offset) das faces de queima para modelar o spark gap varia conforme o ambiente do arquivo editado:
*   **Ordered (Tradicional):** Utilizar `PartDocument.Constructions.OffsetSurfaces.Add` passando as faces copiadas e o valor negativo do offset ($offset = f(Ra)$).
*   **Synchronous:** Utilizar transformações de deslocamento direto na geometria do corpo manipulando `Body.Faces`.

### D. Estratégia de Fallback para Bounding Box
Se os métodos `GetRange` / `GetExactRange` falharem na máquina com Solid Edge real devido a problemas de Binder COM, sugerimos uma rotina de fallback que varra os vértices da face:
1.  Obter a coleção `face.Vertices`.
2.  Iterar e ler as coordenadas `X, Y, Z` de cada vértice (objeto `Vertex`).
3.  Construir as coordenadas mínimas e máximas da AABB local via C#. Embora com mais chamadas COM, garante compatibilidade total.

### E. Configuração do OLE Message Filter
Permitir o ajuste do tempo limite de retry ou a capacidade de interrupção (cancelamento) por meio da interface gráfica (`MainForm`), para evitar travamento em loops infinitos caso o Solid Edge exiba caixas de diálogo modais bloqueantes de erro geométrico.
