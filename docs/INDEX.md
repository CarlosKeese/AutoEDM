# Índice da Documentação do AutoEDM

Documentação completa e atualizada sobre o desenvolvimento de integrações com o **Siemens Solid Edge** no contexto do projeto AutoEDM.

> **Direção atual (2026-07-07):** construímos um **add-in modernizado, ferramenta por
> ferramenta** (cada comando da ribbon = uma casca fina sobre um método do `AutoEDM.Core`);
> a automação total é a orquestração dessas ferramentas. Papéis: **Carlos testa** no SE
> real, **Claude constrói** o código, **Kimi pesquisa/analisa** (sem escrever código). O
> texto completo está em [`README.md` → Direção atual](../README.md#direção-atual-add-in-modernizado-ferramenta-por-ferramenta).

## Documentação de introdução

| Documento | Descrição |
|---|---|
| [`README.md`](../README.md) | Visão geral do projeto, propósito, estado atual, build & run. |
| [`docs/INDEX.md`](./INDEX.md) | Este índice. |

## ⭐ Ler primeiro — Memória compartilhada SE-COM

| Documento | Descrição |
|---|---|
| [`docs/MEMORIA_SOLID_EDGE_COM.md`](./MEMORIA_SOLID_EDGE_COM.md) | **Fonte de verdade compartilhada (Claude ↔ Kimi):** o que a API COM do SE **deixa** ou **recusa** fazer, com status (✅/❌/🟡/⛔) + evidência (run/dump) em cada item. Consultar antes de escrever, corrigir ou analisar código COM. Corrige entendimentos superados (edição in-place, `CopySurfaces`). |

## API, SDK, funções, parâmetros e métodos

| Documento | Descrição |
|---|---|
| [`docs/api/README.md`](./api/README.md) | Catálogo de API COM gerado a partir do dump da type library. |
| [`docs/api/SolidEdgeFramework.md`](./api/SolidEdgeFramework.md) | Tipos da type library `SolidEdgeFramework` (Application, Documents, add-ins, eventos, etc.). |
| [`docs/api/SolidEdgePart.md`](./api/SolidEdgePart.md) | Tipos da type library `SolidEdgePart` (PartDocument, Models, Constructions, features, etc.). |
| [`docs/api/constants.md`](./api/constants.md) | **231 enums e constantes** COM consolidadas com valores. |
| [`docs/SolidEdge_API_COM_Referencia_Completa.md`](./SolidEdge_API_COM_Referencia_Completa.md) | Catálogo AMPLO de tipos da API (nomes + descrições de todas as classes, do Programmer's Guide; pesquisa do Kimi). Bom mapa de descoberta — mas **sem assinaturas nem valores de enum**: para esses, use o dump ou reflita `Interop.SolidEdge`. |
| [`docs/COM_INTEGRATION.md`](./COM_INTEGRATION.md) | Guia de integração COM: arquitetura, ProgID, type libraries, configuração do projeto, objetos principais. |
| [`docs/MAPEAMENTO_INTEGRACAO_COM.md`](./MAPEAMENTO_INTEGRACAO_COM.md) | Mapeamento das funções COM usadas em cada estágio do núcleo AutoEDM. |

## Processos e fluxos

| Documento | Descrição |
|---|---|
| [`docs/MAPEAMENTO_INTEGRACAO_COM.md`](./MAPEAMENTO_INTEGRACAO_COM.md) | Fluxo completo de extração de eletrodos: seleção → planejamento → Inter-Part Copy → offset → stitch → blank → furos → relatório. |
| [`docs/PLANO_TESTE_SE.md`](./PLANO_TESTE_SE.md) | Procedimentos de teste manual no Solid Edge. |

## Arquitetura e decisões

| Documento | Descrição |
|---|---|
| [`docs/recomendacoes_arquitetura.md`](./recomendacoes_arquitetura.md) | Recomendações de arquitetura para o núcleo COM. |
| [`docs/analise_mcp_kimi.md`](./analise_mcp_kimi.md) | Análise sobre MCP genérico de CAD e alinhamento com o projeto. |

## Colaboração / IAs

| Documento | Descrição |
|---|---|
| [`.kimi/skills/solid-edge-com/SKILL.md`](../.kimi/skills/solid-edge-com/SKILL.md) | **Briefing do Kimi**: papel de pesquisa/leitura/análise (sem escrever código) + referência de fatos SE-COM validados para consulta. |
| [`README.md` → Regras para IAs colaboradoras](../README.md#regras-para-ias-colaboradoras) | Divisão de trabalho (Carlos testa · Claude constrói · Kimi pesquisa) e regras comuns. |

## Fontes da verdade

- **Dump da type library:** `src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_223.00.13.05.txt`
- **Gerador de documentação:** `tools/generate_api_docs.py`
- **Código-fonte:** `src/AutoEDM.Core/`, `src/AutoEDM.AddIn/`, `src/AutoEDM/`

## Convenções importantes

- Toda a geometria COM usa **metros/radianos** internamente.
- Coleções COM são **1-based** (`Item(1)` é o primeiro elemento).
- O dump reflete a versão do Solid Edge **2023 (`223.00.13.05`)**. Para outras versões, reexecute `ComDiagnostics.DumpTypeLibraries` e regenere o catálogo com `tools/generate_api_docs.py`.

## Como atualizar esta documentação

1. Execute o AutoEDM para gerar um novo dump da type library (se necessário):
   ```bash
   # O dump é gerado automaticamente em src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_<versão>.txt
   ```
2. Regenere o catálogo de API:
   ```bash
   python tools/generate_api_docs.py
   ```
3. Revise e commit os arquivos em `docs/api/` e os documentos manuais atualizados.
