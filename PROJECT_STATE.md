# PROJECT_STATE — AutoEDM

> Fonte de verdade do estado deste projeto. Atualize ao fechar cada sessão,
> e reflita a mesma coisa na linha do `HUB.md`.

- **Pasta:** `/AutoEDM`
- **Tipo de tarefa:** paralelizável (cada comando da ribbon é uma ferramenta independente)
- **Repositório:** `CarlosKeese/AutoEDM` — **público** desde 2026-09-09
- **Licença:** MIT
- **Status:** em andamento — primeira versão compilada publicada

---

## O que é

Biblioteca C# + add-in COM que automatiza a extração de eletrodos de cavidades
de molde no Solid Edge, substituindo o add-on pago *Electrode Design*. Serve
também como referência prática de integração COM com o SE.

## Como está montado

| Projeto | Alvo | Papel |
|---|---|---|
| `src/AutoEDM.Core` | `net472` + `net8.0-windows` | Núcleo reaproveitável (late binding) |
| `src/AutoEDM.AddIn` | `net472` | Add-in COM, ribbon "AutoEDM" |
| `src/AutoEDM` | `net472` | GUI WinForms de debug (conecta via ROT) |
| `src/AutoEDM.Register` | `net472` | **Registra o add-in em HKCU, sem admin** |
| `tests/AutoEDM.Core.Tests` | `net8.0-windows` | Testes da lógica pura |

O duplo alvo do `Core` existe porque o add-in precisa rodar em `net472` dentro
do Solid Edge, mas os testes precisam rodar fora do CAD.

## Comandos para trabalhar aqui

```powershell
dotnet build AutoEDM.sln -c Release -p:Platform=x64
dotnet test  tests/AutoEDM.Core.Tests/AutoEDM.Core.Tests.csproj -c Release
pwsh tools\pack.ps1 -Version 2026.9.9                                 # gera dist\AutoEDM-2026.9.9.zip
src\AutoEDM.Register\bin\x64\Release\net472\AutoEDM.Register.exe      # registra
src\AutoEDM.Register\bin\x64\Release\net472\AutoEDM.Register.exe /u   # remove
```

Feche o Solid Edge antes de rodar o registrador de novo — com o SE aberto os
DLLs ficam travados.

## Decisão travada: o add-in nunca troca o ambiente de modelagem

Cada botão **declara** se exige síncrono ou ordenado, na tabela `CommandSpec`
de `ElectrodeRibbon`. No ambiente errado o botão fica cinza e explica o que
trocar. Quem troca é o usuário. Isso veio de dois estragos concretos: esboços
órfãos presos no nó "Ordenado" do PathFinder (`ProfileSets.Add()` cria esboço
ordenado mesmo em peça síncrona) e o "Aplicar GAP" falhando em peça síncrona,
porque a troca de ambiente reconstrói o corpo e mata as faces já lidas.

| Comando | Exige |
|---|---|
| Criar Base, Unir superfícies | síncrono |
| Aplicar GAP, Alojamento de O'ring | ordenado |
| Montagem e diagnóstico | qualquer |

## Estado por ferramenta

| Ferramenta | Estado |
|---|---|
| Relatório de coordenadas de queima | ✅ construído |
| Spec-sheet de eletrodos | ✅ construído |
| Criar eletrodos c/ blank | ✅ validado no SE |
| Criar Base | ✅ validado no SE |
| Criar eletrodo (manual) | ✅ validado no SE |
| Unir superfícies | ✅ validado no SE |
| Guarda de ambiente por comando | ✅ construído |
| Configuração externa (`config.json`) | ✅ construído, coberto por teste |
| Testes de unidade | ⚠️ 84 de 91 passando — as 7 falhas são de `ORingGrooveTests` |
| Alojamento de O'ring (ISO 3601) | 🚧 construído, **aguardando validação no SE** |
| Aplicar GAP | 🚧 corrigido, **aguardando confirmação final no SE** |
| Duplicar eletrodo p/ próximo Ra | 🚧 construído, **aguardando validação no SE** |
| Copiar superfícies (Inter-Part Copy) | 🚧 só em edição em contexto (in-place) |
| Rosca física no furo M6 | 🚧 sonda de diagnóstico pronta; receita definitiva em aberto |
| Orquestrador "gerar todos os eletrodos" | 📋 planejado |

## Regra de ouro

Nenhuma assinatura COM é inventada. Toda assinatura vem do dump da typelib
(`SE_API_dump_*.txt`) ou de introspecção ao vivo. O botão **Inspecionar
seleção** da ribbon é a ferramenta que alimenta esse dump.

## Distribuição

- **`tools/pack.ps1`** monta o ZIP portátil: compila em Release, tira os `.pdb`,
  junta os arquivos do operador de `tools/dist/` (`instalar.cmd`,
  `desinstalar.cmd`, `LEIAME.txt`) e zipa em `dist/`.
- **GitHub Releases** — o link `releases/latest` sempre resolve para a versão
  mais nova.
- **kenatec.com/downloads** — card do AutoEDM apontando para `releases/latest`.
  A entrada vive em `kenatec-web/src/lib/downloads/catalogo.ts`; ao publicar uma
  versão nova, atualize o campo `versao` lá.

## Histórico

- **2026-08-07** — commit `1064304`: config externa de eletrodos
  (`AutoEdmConfig`), suíte de testes, `ElectrodeListForm`,
  `ElectrodeDiagnostics`, `ElectrodeNaming`, `FaceColorPainter`, `ComLifetime`
  e `Units`.
- **2026-09-09** — commit `073406a`: **alojamento de O'ring** pela ISO 3601, com
  seleção por etapas e vários furos por vez.
- **2026-09-09** — commit `701725f`: cada comando **declara** síncrono/ordenado;
  o add-in nunca mais troca o ambiente sozinho.
- **2026-09-09** — README reescrito para o GitHub (instalação, os 14 comandos da
  ribbon, ambiente, configuração, build, empacotamento, problemas comuns), com
  destaque para o `AutoEDM.Register.exe` como o programa que registra o add-in.
  Descoberto e corrigido que a regra `dist/` do `.gitignore` também casava com
  `tools/dist/` — por isso o `instalar.cmd`, o `desinstalar.cmd` e o `LEIAME.txt`
  que o README já documentava nunca tinham sido commitados, e o `pack.ps1`
  quebrava. Regra trocada para `/dist/` e os três arquivos criados.
  Repositório tornado **público**; primeira **release compilada** publicada;
  card criado na página de downloads do kenatec.com.

## Dívida conhecida: as 7 falhas de `ORingGrooveTests`

Já existiam em `073406a` e o commit `701725f` as registra explicitamente. Os
testes foram escritos contra uma especificação **anterior** à implementação que
ficou, e as duas divergiram:

| Teste | Espera | Implementação entrega |
|---|---|---|
| `CatalogoEmbutido_temAsTresSecoesDeMoldeETodasAConferir` | 3 seções: 1,78 / 2,62 / 3,53 | 5 seções — o catálogo ganhou 1,02 / 1,27 / 1,52 |
| `CanalEstatico_temProfundidade80PorCento...` (×3) | profundidade = 0,80·d2, largura = 1,31·d2 | valores da `ORingHousingTable`, que não seguem uma razão fixa |
| `Preenchimento_ficaNoAlvoEAbaixoDoLimite` | 0,70–0,80 | 0,6947 |
| `Fkm_deixaOCanalMaisFolgadoQueNbr` | FKM mais folgado que NBR | não é o que sai |
| `CatalogoEmbutido_asProgressoesBatemComOsCodigosConhecidos` | códigos que existiam antes | `Sequence contains no matching element` |

**Decidir qual lado é a verdade é uma questão de domínio, não de código:** ou a
`ORingHousingTable` (tabelas Parker/DL Seals, medidas reais de catálogo) está
certa e os testes precisam ser reescritos contra ela, ou as razões da ISO 3601
(80 % / 131 %) são o alvo e a tabela precisa ceder. **Resolver isso junto com a
validação da ferramenta no SE** — não antes, para não travar uma fórmula que a
peça real ainda vai contestar.

## Próxima ação

Validar no Solid Edge, com uma montagem real, as três ferramentas construídas
que ainda não passaram por confirmação no CAD: **Alojamento de O'ring**,
**Aplicar GAP** e **Duplicar eletrodo**. Na do O'ring, resolver junto a dívida
dos 7 testes acima. Depois disso, fechar a receita da rosca
física M6 (rodar a *Sonda de rosca* e ler o log), os ícones da ribbon (recurso
BITMAP Win32, via `tools/make_ribbon_res.ps1`) e o orquestrador completo.
