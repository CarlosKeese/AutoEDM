# Catálogo de API COM do Solid Edge

Este diretório contém a documentação de API gerada a partir do dump da type library do Solid Edge.

## Type libraries disponíveis

- [`SolidEdgeFramework`](./SolidEdgeFramework.md) — 408 tipos
- [`SolidEdgePart`](./SolidEdgePart.md) — 858 tipos

## Constantes

- [Constantes e Enums consolidados](./constants.md)

## Notas

- Toda a geometria usa **metros/radianos** internamente.
- Coleções COM são **1-based** (primeiro elemento em `Item(1)`).
- A coluna `Retorno` mostra o tipo COM declarado na type library; em late binding pode ser necessário fazer cast/unmarshal manualmente.
- Para descobrir novos métodos ou comparar versões, reexecute `ComDiagnostics.DumpTypeLibraries` e regenere estes arquivos com `tools/generate_api_docs.py`.