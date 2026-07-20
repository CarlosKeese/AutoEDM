# Índice da Documentação do AutoEDM

Documentação completa e atualizada sobre o desenvolvimento de integrações com o **Siemens Solid Edge** no contexto do projeto AutoEDM.

> **Direção atual (2026-07-07):** construímos um **add-in modernizado, ferramenta por
> ferramenta** (cada comando da ribbon = uma casca fina sobre um método do `AutoEDM.Core`);
> a automação total é a orquestração dessas ferramentas. Papéis: **Carlos testa** no SE
> real, **Claude constrói** o código. O
> texto completo está em [`docs/PROJECT.md` → Direção atual](./PROJECT.md#direção-atual-add-in-modernizado-ferramenta-por-ferramenta).

## Documentação de introdução

| Documento | Descrição |
|---|---|
| [`README.md`](../README.md) | Visão geral do projeto, propósito, estado atual, build & run. |
| [`docs/INDEX.md`](./INDEX.md) | Este índice. |

## ⭐ Ler primeiro — Solid Edge por COM

| Documento | Descrição |
|---|---|
| [`docs/GUIA_SOLID_EDGE_COM.md`](./GUIA_SOLID_EDGE_COM.md) | **A "Pedra de Roseta": guia de aprendizados** para automatizar o SE por COM sem o SDK — introspecção, out-params (`ParameterModifier`), late binding, unidades/STA/message filter, receitas de modelagem (sketch+extrusão, furos, superfícies intra-peça), síncrono×ordenado, in-place bloqueado, add-in de ribbon, tabela **erro→causa→fix**. Comece por aqui. |
| [`docs/MEMORIA_SOLID_EDGE_COM.md`](./MEMORIA_SOLID_EDGE_COM.md) | **Fonte de verdade:** o que a API COM do SE **deixa** ou **recusa** fazer, com status (✅/❌/🟡/⛔) + evidência (run/dump) em cada item. Consultar antes de escrever, corrigir ou analisar código COM. Corrige entendimentos superados (edição in-place, `CopySurfaces`). |

## API, SDK, funções, parâmetros e métodos

| Documento | Descrição |
|---|---|
| [`docs/api/README.md`](./api/README.md) | Catálogo de API COM gerado a partir do dump da type library. |
| [`docs/api/SolidEdgeFramework.md`](./api/SolidEdgeFramework.md) | Tipos da type library `SolidEdgeFramework` (Application, Documents, add-ins, eventos, etc.). |
| [`docs/api/SolidEdgePart.md`](./api/SolidEdgePart.md) | Tipos da type library `SolidEdgePart` (PartDocument, Models, Constructions, features, etc.). |
| [`docs/api/SolidEdgeGeometry.md`](./api/SolidEdgeGeometry.md) | **Body / Face / Edge / Vertex / Loop / Shell** + enums de query topológica (`igQueryCylinder`…). Onde vivem `GetRange`, `GetRGBAVals`, `GetPointData`. Gerado por reflexão do interop (o dump não pega a lib de geometria). |
| [`docs/api/SolidEdgeAssembly.md`](./api/SolidEdgeAssembly.md) | `Occurrence`/`Occurrences` (transforms `GetTransform`/`PutTransform`), `Relations3d` (mates), padrões. Gerado por reflexão do interop. |
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

## Colaboração / IAs

| Documento | Descrição |
|---|---|
| [`.claude/skills/solid-edge-com/SKILL.md`](../.claude/skills/solid-edge-com/SKILL.md) | Skill com os fatos validados de COM do SE (assinaturas, armadilhas, receitas de modelagem). |
| [`docs/PROJECT.md` → Regras para IAs colaboradoras](./PROJECT.md#regras-para-ias-colaboradoras) | Divisão de trabalho (Carlos testa · Claude constrói) e regras comuns. |

## Fontes da verdade

- **Dump da type library (CUMULATIVO, 2026-07-17):** `%LOCALAPPDATA%\AutoEDM\logs\SE_API_dump_<versão>.txt` — cresce sozinho a cada clique em **"Inspecionar seleção"** no add-in (cada objeto tocado soma a lib dele ao arquivo, sem sobrescrever o que já foi capturado; dedup por GUID no cabeçalho `[guid]` de cada seção). É a fonte MAIS confiável para Geometry/Assembly: como vem de objeto AO VIVO (não de reflexão do interop), não lista tipos que o objeto real não expõe (ver `ComDiagnostics.HarvestTypeLibs`/`ResolveDumpPath`). O antigo `src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_223.00.13.05.txt` (gerado por `ModelingProbe`, seeds fixos) continua existindo e também é lido pelo gerador.
- **Interop tipado (todas as libs):** `Interop.SolidEdge.dll` (NuGet v219 = SE 2023) — reflita quando o dump ainda não cobrir algo; mas desconfie: a reflexão lista TIPOS declarados na typelib que o objeto VIVO pode não expor (memória do projeto, Log 2026-07-15).
- **Geradores de documentação:** `tools/generate_api_docs.py` (do dump — agora acha sozinho o dump mais recente entre LOCALAPPDATA e bin/Debug, ou aceita um caminho explícito) · `tools/reflect_api_docs.ps1` (por reflexão do interop → Geometry/Assembly, só como fallback)
- **Código-fonte:** `src/AutoEDM.Core/`, `src/AutoEDM.AddIn/`, `src/AutoEDM/`

## Convenções importantes

- Toda a geometria COM usa **metros/radianos** internamente.
- Coleções COM são **1-based** (`Item(1)` é o primeiro elemento).
- O dump cumulativo não tem uma "versão fixa" — cada `SE_API_dump_<versão>.txt` corresponde à versão do SE que gerou aquele arquivo (`Application.Version`); ao trocar de instalação do SE, um arquivo NOVO começa sozinho (nome diferente), sem misturar com o antigo.

## Como atualizar esta documentação

1. No Solid Edge, com o add-in AutoEDM carregado: selecione features/faces/arestas variadas (quanto mais tipos diferentes de objeto — furo, superfície, ocorrência de montagem, face — melhor a cobertura) e clique **"Inspecionar seleção"** algumas vezes. Isso vai enchendo `%LOCALAPPDATA%\AutoEDM\logs\SE_API_dump_<versão>.txt` sozinho, sem precisar de nenhum passo manual extra.
2. Regenere o catálogo de API (acha o dump mais recente automaticamente):
   ```bash
   python tools/generate_api_docs.py
   ```
3. Revise e commit os arquivos em `docs/api/` e os documentos manuais atualizados.
