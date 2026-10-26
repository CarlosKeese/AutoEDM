# CLAUDE.md — AutoEDM

Add-in COM do Solid Edge 2023/2026 (aba "AutoEDM") que automatiza eletrodos de EDM, porta-molde, WEDM,
engenharia reversa de malha e a ponte MCP que deixa o Claude Code dirigir o SE. Código e UI em português.
O README é a página pública (GitHub): não o use como bloco de notas.

## Stack
- `src/AutoEDM.Core`: lógica reaproveitável, late binding (`dynamic`); compila **net472** (carregado no SE) e
  **net8.0-windows** (testes). Pastas: `Com/ Config/ Assembly/ Selection/ Electrode/ Model/ Mcp/ Reporting/ Experiments/`
- `src/AutoEDM.AddIn` (net472): ribbon (`Ribbon.xml` + `ElectrodeRibbon`) e formulários
- `src/AutoEDM.Mcp` (net8.0): servidor MCP, fala com o add-in por named pipe; contrato e catálogo no Core
- `src/AutoEDM.Register`: registro HKCU sem admin · `src/AutoEDM`: GUI WinForms de debug (ROT)
- `tests/AutoEDM.Core.Tests`: lógica pura + ponte MCP num pipe real (sem SE)

## Build / test / deploy
```powershell
dotnet build AutoEDM.sln -c Release -p:Platform=x64
dotnet test  tests/AutoEDM.Core.Tests/AutoEDM.Core.Tests.csproj -c Release
pwsh -File tools\deploy-dev.ps1 -IncludeMcp      # add-in em %LOCALAPPDATA%\AutoEDM\addin, MCP em ...\mcp
powershell -ExecutionPolicy Bypass -File tools\pack.ps1 [-Version 2026.x.y]   # ZIP de distribuição
```
Atualizar o add-in exige o SE **fechado** (DLLs travados). Logs: `%LOCALAPPDATA%\AutoEDM\logs` (1ª linha = versão).
`AssemblyVersion` fica fixa em 1.0.0.0 de propósito; a versão vai em File/InformationalVersion.

## Regras que valem sempre
- **Regra de ouro:** nenhuma assinatura COM é inventada. Vem do dump `SE_API_dump_*.txt`, de `se_inspecionar_selecao`
  ou de introspecção ao vivo. Use a skill `solid-edge-com` e confira `docs/MEMORIA_SOLID_EDGE_COM.md` antes de afirmar
  o que a API faz.
- **Ambiente síncrono x ordenado:** nada troca o ambiente sozinho nem no meio de uma operação (a troca reconstrói o corpo
  e mata as faces lidas). Cada botão declara o ambiente em `CommandSpec` (`ElectrodeRibbon`).
- Handler de botão é casca fina: lê UI/seleção e chama o Core. Botão novo: `<button>` em `Ribbon.xml` (id = `CommandId`),
  tratar em `ElectrodeRibbon` e declarar em `CommandSpec`.
- MCP: ferramentas ficam **ao lado** dos handlers, sobre o Core (handlers terminam em `MessageBox`, que travaria o SE).
  Ponte nasce em somente-leitura; só o botão "Liberar escrita" libera; ferramenta de escrita tem `Writes = true` e o teste
  de trava cobre isso.
- COM trabalha em metros/radianos: converta só por `Model/Units`.
- `ProfileSets.Add()` cria esboço ordenado mesmo em peça síncrona (vira órfão): não use em operação síncrona.
- Funcionalidade nova ou validada no SE: atualizar a tabela "Status e Roadmap" do README e `docs/MANUAL.md`.

## Mais detalhes (ler só quando a tarefa pedir)

| Preciso de… | Onde |
|---|---|
| Estado do projeto, decisões e pendências do usuário | nota `_Gestao/Projetos/AutoEDM.md` (prevalece) |
| Como cada funcionalidade funciona por dentro, regras e valores; ferramentas MCP (seção 2.3) | `docs/MANUAL.md` |
| O que cada botão faz e o ambiente que exige | README, "Os comandos da faixa de opções" e "Ambiente de modelagem" |
| Análise de usinabilidade (canto vivo, alcance das fresas) | README, "Análise de usinabilidade" |
| Chaves do `config.json` | README, "Configuração" |
| O que a API COM aceita ou recusa, com evidência | `docs/MEMORIA_SOLID_EDGE_COM.md` |
| Cópia entre peças (Inter-Part): o que já falhou | `docs/INTER-PART.md` |
| Descobrir API por introspecção | `docs/GUIA_SOLID_EDGE_COM.md`, `docs/INDEX.md`, `docs/api/` |
| Porta-molde | `docs/PLANO_MOLDE.md` |
| Validações pendentes no SE | `docs/PLANO_TESTE_SE.md` |

Desatualizados (não confiar sem conferir): `docs/PROJECT.md` (roadmap de julho/2026) e a seção in-place de
`docs/COM_INTEGRATION.md` (`Occurrence.Activate = true` foi refutado).
