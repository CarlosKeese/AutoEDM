# Catálogo de API COM do Solid Edge

Referência de objetos, métodos, parâmetros e enums da API COM do Solid Edge 2023
(`223.00.13.05`). **Para os PADRÕES e armadilhas** (out-params, late binding, sync×ordered,
receitas de modelagem, erros→causa→fix), leia primeiro o
[**Guia — Pedra de Roseta**](../GUIA_SOLID_EDGE_COM.md).

## Type libraries disponíveis

Gerados **do dump da typelib** (`tools/generate_api_docs.py`):

- [`SolidEdgeFramework`](./SolidEdgeFramework.md) — Application, Documents, add-ins, eventos, SelectSet…
- [`SolidEdgePart`](./SolidEdgePart.md) — PartDocument, Models, Constructions, features, furos…

Gerados **por reflexão do `Interop.SolidEdge`** (`tools/reflect_api_docs.ps1` — a lib de
geometria não é capturada pelo walk do dump; a reflexão é a única fonte completa):

- [`SolidEdgeGeometry`](./SolidEdgeGeometry.md) — **Body / Face / Edge / Vertex / Loop / Shell** + enums de query topológica (onde vivem `GetRange`, `GetRGBAVals`, `GetPointData`).
- [`SolidEdgeAssembly`](./SolidEdgeAssembly.md) — Occurrences, transforms, relations (mates), padrões.

## Constantes

- [Constantes e Enums consolidados](./constants.md) (do dump; enums de Geometry/Assembly também estão nos arquivos de módulo acima).

## Notas

- Toda a geometria usa **metros/radianos** internamente.
- Coleções COM são **1-based** (primeiro elemento em `Item(1)`).
- Nos arquivos de reflexão, `[out]`/`[ref]` marcam parâmetros de saída — em **late binding** eles precisam de `ParameterModifier` by-ref (ver o Guia).
- A coluna `Retorno` mostra o tipo COM declarado na type library; em late binding pode ser necessário fazer cast/unmarshal manualmente.
- **Atualizar/comparar versões:** dump → `python tools/generate_api_docs.py`; reflexão → `pwsh tools/reflect_api_docs.ps1`.
