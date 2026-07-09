# Análise de Falhas e Desvios de Escopo no MCP do Kimi

Este documento apresenta uma análise técnica dos problemas encontrados na **Skill do Kimi** (`.kimi/skills/solid-edge-com/SKILL.md`) e no **MCP Server** (`mcp/solid-edge-mcp/`) criados no projeto AutoEDM.

---

## 🚨 1. Desvio Crítico de Escopo (Foco do Negócio)

Conforme as diretrizes principais do projeto em [README.md](../README.md):
> *"O objetivo do projeto **não** é um servidor MCP genérico de CAD."*

*   **O que o MCP do Kimi faz:** Cria blocos paramétricos (`create_block_with_hole`, `create_cylinder`), gera montagens genéricas (`place_parts_linear`), exporta para formatos avulsos (`export_step`, `export_pdf`).
*   **O que o projeto AutoEDM precisa:** Automação de **extração de eletrodos** para moldes de eletroerosão (EDM) **em contexto de montagem**. O NX lê arquivos nativos `.par` diretamente (mantendo cores/Ra); exportar STEP/Parasolid é fora de escopo.
*   **A "Falsa" Implementação de Eletrodo:** A ferramenta `create_electrode_from_cavity` do MCP do Kimi cria geometrias de bloco e cilindro ilustrativos (placeholders) em vez de derivar as faces de queima da cavidade real da montagem ativa.

---

## 🐛 2. Bugs Críticos de COM no Código Python (`win32com`)

Ao testar a ferramenta `create_block_with_hole` de Kimi, ocorrem dois erros fundamentais que impedem qualquer execução:

### A. Passagem de Parâmetro Incorreta (TypeError em `AddFiniteExtrudedProtrusion`)
Na ferramenta `tools/geometry.py` de Kimi, a extrusão é chamada assim:
```python
doc.Models.AddFiniteExtrudedProtrusion(
    doc.ProfileSets.Count,
    profile_set.Profiles.Count, # <--- BUG CRÍTICO (Passa um inteiro count como array)
    0,
    h
)
```
*   **O Bug:** O segundo parâmetro (`ProfileArray`) exige um **array/coleção de objetos `Profile`**. Kimi passou `.Count` (um inteiro).
*   **O Efeito:**
    *   Se usar **Early Binding** (`EnsureDispatch`), o Python valida a assinatura e lança:
        `TypeError: Objects for SAFEARRAYS must be sequences (of sequences), or a buffer object.`
    *   Se usar **Late Binding** (`Dispatch` padrão do Kimi), o runtime do Solid Edge COM rejeita a chamada com:
        `DISP_E_TYPEMISMATCH` (HRESULT `0x80020005`).

### B. Ausência de Validação de Perfil (`Profile.End`)
Mesmo corrigindo o parâmetro acima para passar a lista de perfis `[profile]` ou `(profile,)`, a chamada de extrusão falha internamente com:
`com_error: -2147467259 (E_FAIL / Unspecified error)`
*   **O Bug:** No Solid Edge, sketches/perfis criados por código precisam ser obrigatoriamente fechados/salvos chamando o método `.End(ProfileValidationType)` antes de serem consumidos por operações de modelagem física.
*   **A Correção:** Adicionar o fechamento do perfil antes da extrusão:
    ```python
    profile.End(2) # 2 = igProfileClosed
    ```

---

## 🚫 3. Falta de OLE Message Filter (Travamentos da UI)

O Solid Edge é um software CAD pesado. Durante operações geométricas complexas ou quando avisa erros através de popups/caixas de diálogo, o servidor COM do Solid Edge responde ao cliente com `RPC_E_SERVERCALL_RETRYLATER` (Servidor Ocupado).

*   **O Bug no MCP do Kimi:** O MCP em Python faz requisições diretas ao Solid Edge sem registrar um filtro de mensagens COM single-threaded. Qualquer processamento mais longo no CAD fará o servidor MCP Python travar ou encerrar abruptamente com exceções não tratadas.
*   **Contraste com C#:** O aplicativo correspondente em C# resolve isso nativamente com a classe `OleMessageFilter.cs`.

---

## 🏗️ 4. Duplicação de Esforço Físico (Arquitetura)

Manter uma suíte complexa de modelagem e conversão em Python (`mcp/solid-edge-mcp`) paralelamente ao núcleo principal desenvolvido em C# (.NET Framework 4.7.2) no arquivo `AutoEDM.sln` gera uma divisão desnecessária de código.

1.  **Dificuldade de Marshalling em Python:** O C# gerencia a conversão de arrays nativos para SAFEARRAYS e a passagem de parâmetros dinâmicos via `InvokeMember` + `ParameterModifier` de forma robusta e estável. No Python (`win32com`), isso exige conversões manuais complexas (`VARIANT(VT_ARRAY | VT_DISPATCH, ...)`) que quebram facilmente entre builds.
2.  **Solução Ideal:** A lógica de CAD (cópia de faces, cálculo de gaps, encolhimento e modelagem do blank) deve ser 100% resolvida na biblioteca C# (`src/AutoEDM`). O MCP Server, se necessário no futuro, deve atuar apenas como uma ponte de API finíssima que chama o executável ou a DLL do `AutoEDM`.

---

## 💡 5. Problemas Identificados na Skill (`SKILL.md`)

A skill `.kimi/skills/solid-edge-com/SKILL.md`:
*   Ensina a criar primitivas genéricas e sketches (que contêm os bugs de parâmetros demonstrados acima).
*   Não fornece diretivas para o fluxo de **Inter-Part Copy associativo** (`Constructions.CopySurfaces.Add`), que é a única forma de obter as faces da cavidade de molde na peça do eletrodo em contexto de montagem.
