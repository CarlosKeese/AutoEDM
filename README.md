# AutoEDM — API de Automação para Eletrodos no Solid Edge

[![Solid Edge](https://img.shields.io/badge/Solid%20Edge-2023%2F2026-blue)](https://plm.sw.siemens.com/en-US/solid-edge/)
[![.NET](https://img.shields.io/badge/.NET-Framework%204.7.2%20%7C%20NET%2010%2B-purple)](https://dotnet.microsoft.com/)
[![COM](https://img.shields.io/badge/COM-Late%20Binding%20%7C%20Early%20Binding-orange)](./docs/COM_INTEGRATION.md)
[![Download](https://img.shields.io/github/v/release/CarlosKeese/AutoEDM?label=baixar&color=success)](https://github.com/CarlosKeese/AutoEDM/releases/latest)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

> **Uma API funcional e open-source para extrair eletrodos de cavidades de molde no Solid Edge — sem depender do add-on pago *Electrode Design*.**

AutoEDM é uma biblioteca C# (.NET, x64, STA) que expõe, de forma reaproveitável, as operações COM do Solid Edge usadas no fluxo real de EDM: leitura de faces por cor/Ra, segmentação por proximidade, criação de peças em contexto, Inter-Part Copy, offset por rugosidade e geração de relatórios. Além da automação, ela serve como **referência prática de integração COM com o Solid Edge** para desenvolvedores que querem construir seus próprios add-ins.

---

## Instalar (versão compilada)

Baixe o ZIP na página de **[releases](https://github.com/CarlosKeese/AutoEDM/releases/latest)** — ou pela [página de downloads da Kenatec](https://kenatec.com/downloads).

1. **Feche o Solid Edge.** Com ele aberto os DLLs ficam travados e a instalação não completa.
2. **Extraia o ZIP numa pasta do usuário** — por exemplo `C:\Users\<você>\AutoEDM`. **Não** extraia em `C:\Arquivos de Programas`: o registro é por usuário e a pasta de programas exige admin.
3. **Execute `instalar.cmd`** (dois cliques).
4. **Abra o Solid Edge.** A aba **AutoEDM** aparece na faixa de opções.

### Qual programa registra o add-in

> **`AutoEDM.Register.exe`** — é ele que registra o add-in no Solid Edge.

O `instalar.cmd` é um invólucro que faz três coisas antes de chamá-lo, e cada uma existe por causa de uma falha real:

| O que o `instalar.cmd` faz | Por quê |
|---|---|
| Recusa rodar com o Solid Edge aberto | Os DLLs ficariam travados e o usuário seguiria na versão antiga achando que atualizou. |
| Roda `Unblock-File` nos binários | ZIP vindo de e-mail, rede ou download carrega o *Mark of the Web*, e o CLR se recusa a carregá-lo in-process. O add-in some da ribbon **sem erro nenhum** — é a causa nº 1 de "instalei e não apareceu". |
| Chama o `AutoEDM.Register.exe` | O registro em si. |

Se preferir rodar o registrador direto, ele funciona sozinho — só desbloqueie o ZIP antes (botão direito → Propriedades → **Desbloquear**, no ZIP, *antes* de extrair):

```powershell
.\AutoEDM.Register.exe        # registra
.\AutoEDM.Register.exe /u     # remove
```

**Não precisa de administrador.** O registrador faz duas coisas:

1. **Copia** o `AutoEDM.AddIn.dll` e as dependências para `%LOCALAPPDATA%\AutoEDM\addin\` — uma pasta estável, fora da árvore de build. É de lá que o Solid Edge carrega o add-in, então dá para recompilar o projeto com o SE aberto; só é preciso fechá-lo para **atualizar** o add-in.
2. **Grava as chaves COM em `HKCU\Software\Classes`** (usuário atual, não a máquina): `CLSID\{guid}\InprocServer32`, `Implemented Categories\{CATID_SolidEdgeAddIn}`, `Environment Categories` e `AutoConnect`. O COM e o Solid Edge consultam o HKCU antes do HKLM, então o add-in é descoberto sem escrever em área de máquina e sem `regasm`.

Saída esperada:

```text
Deploy: 12 dll(s) -> C:\Users\<você>\AppData\Local\AutoEDM\addin
Add-in REGISTRADO no usuário (HKCU), sem admin.
  CLSID:    {....}
Reinicie a Solid Edge. Aba 'AutoEDM' > 'Criar eletrodos'.
```

### Atualizar e remover

| Operação | Como |
|---|---|
| **Atualizar** | Fechar a SE, extrair a versão nova, rodar `instalar.cmd` de novo. Não precisa desinstalar antes. |
| **Remover** | Fechar a SE e rodar `desinstalar.cmd` (= `AutoEDM.Register.exe /u`). A pasta `%LOCALAPPDATA%\AutoEDM\` pode ser apagada à mão depois. |
| **Config do usuário** | `%LOCALAPPDATA%\AutoEDM\config.json` — criado sozinho no 1º uso, com os defaults. |
| **Logs para suporte** | `%LOCALAPPDATA%\AutoEDM\logs` — a 1ª linha traz a versão e o carimbo dos binários carregados. |

**Requisitos:** Windows x64, Solid Edge 2023+ com licença, e .NET Framework 4.7.2 (já vem no Windows 10 1803+ e no 11).

---

## O que este projeto faz

No fluxo real de usinagem por eletroerosão (EDM), o desenhista precisa:

1. Identificar as faces de queima pintadas no molde (as cores codificam o **Ra**, ou seja, a rugosidade desejada).
2. Agrupar faces próximas em **regiões / detalhes** (uma cor pode marcar vários eletrodos distantes).
3. Para cada região, criar um eletrodo em contexto da montagem.
4. Copiar as faces de queima associativamente para a peça do eletrodo.
5. Aplicar **offset para dentro** (spark gap) segundo a tabela de Ra.
6. Gerar blank, fixação e relatório de coordenadas para o CAM.

AutoEDM automatiza esse pipeline como uma **coleção de ferramentas individuais** (cada uma é um comando de add-in) que, no futuro, serão orquestradas em um fluxo "gerar todos os eletrodos".

---

## Os comandos da faixa de opções

A aba **AutoEDM** tem quatro grupos. A coluna **Ambiente** é levada a sério: cada botão **declara** o que exige e, no lugar errado, fica cinza — ver [Ambiente de modelagem](#ambiente-de-modelagem-síncrono-x-ordenado).

### Eletrodos — documento de montagem

| Comando | Ambiente | O que faz |
|---|---|---|
| **Analisar (Z)** | qualquer | Lê a peça **sem alterar nada** e propõe quantos eletrodos existem, por nível de profundidade, com a posição de cada um. Junto, faz a [análise de usinabilidade](#análise-de-usinabilidade): o que a sua ferramentaria não consegue produzir e por isso exige erosão. **Selecione a peça a analisar** antes de clicar — sem seleção ele mira pela cor de queima, e o eletrodo também tem faces pintadas. |
| **Criar eletrodos** | qualquer | Cria uma peça por eletrodo com o bloco da base (holder) modelado e posicionado na montagem. Não salva a montagem. |
| **Criar eletrodo (manual)** | qualquer | Um clique por eletrodo: selecione a(s) face(s) do fundo do bolsão a erodir e ele cria **um** eletrodo, centrado em XY e no Z mais fundo das faces. Identifica o Ra pela cor e grava numa variável da peça. |
| **Duplicar eletrodo** | qualquer | Selecione um eletrodo que já tem GAP aplicado: gera a cópia de desbaste no **próximo Ra da escada** e a posiciona em todas as ocorrências daquele eletrodo na montagem. |

### Relatórios — documento de montagem

| Comando | Ambiente | O que faz |
|---|---|---|
| **Coordenadas** | qualquer | Lista os eletrodos selecionados com posição (mesma leitura de *Propriedades de Ocorrência*) e o GAP/Ra gravados na peça. Não altera nada. |
| **Ficha (spec-sheet)** | qualquer | Gera a ficha por eletrodo — Ra, pegada, blank, offset por Ra, fixação — em `.txt` e `.csv`. |

### Peça — documento de peça

| Comando | Ambiente | O que faz |
|---|---|---|
| **Criar Base** | **síncrono** | Cria a base (bloco no material mais próximo) com a **faixa de medição** (chanfro X+/Y− em quadrado/retângulo, flat em Y− no redondo) e a **fixação** (M6 roscado + 2×Ø4, ou eixo). Janela de parâmetros com preview. |
| **Unir superfícies** | **síncrono** | Une a superfície de queima ao bloco num sólido único. Isolado de propósito: só une, sem GAP nem cor. |
| **Aplicar GAP** | **ordenado** | Escolha o Ra numa lista (pré-selecionada pelo Ra gravado na peça): aplica o offset de faísca via `Model.FaceOffsets`, pinta a cor do Ra e nomeia a feature na árvore. |
| **Alojamento de O'ring** | **ordenado** | Canal de anel O'ring pela **ISO 3601**. Capture uma face (cilíndrica = eixo/furo; plana = vedação de face) e uma aresta circular: ele mede o diâmetro, acha o anel mais próximo no catálogo, dimensiona o canal pelo tipo de vedação (estática / recíproca / rotativa) e pelo elastômero (NBR / FKM Viton) e corta. Mostra **esmagamento, estiramento e preenchimento antes de cortar**, e avisa quando algum sai da norma — quem decide é você. |

### Diagnóstico — qualquer documento

| Comando | O que faz |
|---|---|
| **Inspecionar seleção** | SPY: dumpa tipo, propriedades, métodos e coleções do objeto COM selecionado — e soma a type library ao mapa acumulado `SE_API_dump_<versão>.txt`. |
| **Iniciar leitura de ação manual** | Snapshot das features **antes** de você fazer a operação à mão no SE. |
| **Gravar log da leitura** | Diff contra o snapshot: grava o tipo e as propriedades das features que você criou, para reproduzir por COM. |
| **Sonda de rosca (M6)** | Cria uma peça descartável com cinco furos M6, cada um por uma receita diferente da API de rosca. O log traz o `HoleData`, o `Status` da feature e o erro da rosca física — para descobrir qual receita produz a hélice cortada de verdade. |

---

## Análise de usinabilidade

O **Analisar (Z)** não decide o que vai a erosão pela sua tinta — ele mede a peça e responde com o que a **sua** ferramentaria consegue produzir. A regra é uma só, aplicada a dois sólidos: o que a fresa não alcança na cavidade é o que exige eletrodo; o que ela não alcança no eletrodo é um eletrodo que não dá para fabricar.

### O que ele reprova

| Achado | Por quê | Veredito |
|---|---|---|
| **Canto vivo** (R0) entre duas paredes verticais | Toda fresa deixa ali o próprio raio — no mínimo R0,5 com a Ø1. Canto vivo nenhuma entrega, em profundidade nenhuma. | Erosão, e ferramenta mais longa não ajuda |
| **Raio menor que a menor fresa** | Nada abaixo de R0,5 existe na ferramentaria. | Erosão |
| **Fundo demais para a fresa do raio** | O raio aceita uma fresa, mas nenhuma delas é longa o bastante. | Erosão *ou* uma fresa mais longa |
| **Furo fundo demais para fresa** | Dá a volta completa: é furo, e furo se faz com broca. | Não é erosão — confira a broca |

O canto **piso↔parede** também é vivo e **não** é reprovado: a fresa de topo reto varre o fundo e encosta na parede. Só o canto **vertical** parede↔parede exige erosão — é a diferença que separa um bolsão de fundo chato, que se fresa todo dia, de um canto de projeto sem raio.

### A ferramentaria

Os limites saem da lista de ferramentas, não de um número fixo no código. Com a ferramentaria de fábrica (fresas de Ø1 a Ø6, topo reto e esférica, incluindo os pescoços longos, e brocas DIN 338 de Ø1 a Ø19):

| Raio exigido pela região | Alcance da fresa |
|---|---|
| menor que R0,5 | nenhuma fresa — é erosão |
| R0,5 a R1,0 | 10 mm |
| R1,0 ou mais | 20 mm |

Das 18 fresas, apenas **6** mudam essa resposta: as outras servem o mesmo raio sem ir mais fundo, e existem por rigidez, carga de cavaco e tempo de ciclo. O relatório diz, por região, qual ferramenta ela exige — então uma região que só sobrevive graças a um pescoço longo fica visível.

### O que ele ainda não faz

A análise mede **faces curvas e arestas vivas** — o raio exato, direto do modelo, sem discretizar nada. Ela ainda **não** enxerga rasgo estreito entre duas paredes planas nem região sem acesso de cima: isso exige varrer o vazio da cavidade, e é o próximo passo. Também não separa face côncava de convexa, então um pino fino é relatado junto com um canto fechado — os dois são problema de fabricação, mas o motivo sai genérico.

---

## Ambiente de modelagem: síncrono x ordenado

O add-in **nunca troca o ambiente da peça sozinha.** Quem troca é você, no Solid Edge. Isso é uma decisão de projeto, tomada depois de dois estragos concretos:

- **Esboços presos entre os ambientes.** `ProfileSets.Add()` cria esboço **ordenado** mesmo numa peça síncrona. Consumido por um recurso síncrono, ele fica órfão no nó "Ordenado" do PathFinder — e o usuário não consegue apagar pela interface.
- **"Aplicar GAP" falhando em peça síncrona.** Ele lia as faces selecionadas, trocava para ordenado e só então pintava/offsetava. A troca reconstrói o corpo, e as faces já lidas viram *proxies* mortos.

Por isso cada botão declara o ambiente que exige, numa tabela única (`CommandSpec`, em `ElectrodeRibbon`). No ambiente errado o botão fica cinza; se clicado assim mesmo, ele explica como trocar. As razões, comando a comando:

| Comando | Exige | Porque |
|---|---|---|
| **Criar Base** | síncrono | Extrusão e furação da família síncrona (`BlankModeler`). |
| **Unir superfícies** | síncrono | "Limite", Costurar, Anexar e a booleana de união são síncronas. Em ordenado o botão criava só uma feature de costura, e não unia. |
| **Aplicar GAP** | ordenado | `Model.FaceOffsets` — o GAP que fica **editável na árvore** — só existe em ordenado. |
| **Alojamento de O'ring** | ordenado | Em ordenado o esboço é filho legítimo do recurso e some junto quando você apaga o canal. Em síncrono viraria órfão. |

Os comandos de montagem e os de diagnóstico não dependem do ambiente: eles só leem, ou criam documento próprio.

---

## Configuração

Na primeira execução o add-in grava `%LOCALAPPDATA%\AutoEDM\config.json` com os defaults de fábrica. Editar esse arquivo muda o comportamento sem recompilar:

```json
{
  "ElectrodeNamePrefix": "ELD",
  "Material": "Cobre",
  "ColorTolerance": 8,
  "DetailGapMm": 1.0,
  "HolderHeightMm": 15.0,
  "HolderBaseClearanceMm": 1.0,
  "RaOffsetBands": null,
  "RaColorEntries": null
}
```

| Chave | O que controla |
|---|---|
| `ElectrodeNamePrefix` | Prefixo do nome das peças de eletrodo. |
| `Material` | Material gravado na ficha (cobre, grafite…). |
| `ColorTolerance` | Tolerância RGB (0–255) ao casar a cor da face com uma entrada da paleta de Ra. |
| `DetailGapMm` | Distância máxima entre faces para elas contarem como o **mesmo** detalhe na segmentação. |
| `HolderHeightMm` / `HolderBaseClearanceMm` | Altura do bloco da base e a folga sob ele. |
| `RaOffsetBands` | Tabela `Ra máximo → offset (mm)`. `null` ou lista vazia = tabela de fábrica. |
| `RaColorEntries` | Paleta `RGB → Ra alvo`. `null` ou lista vazia = paleta de fábrica. |

Quem nunca tocar no arquivo tem exatamente o comportamento que vivia fixo no código.

---

## Por que isso importa para a comunidade Solid Edge

A documentação COM do Solid Edge exige login, as type libraries não são distribuíveis e muitas assinaturas só são descobertas por introspecção em tempo de execução. Este projeto contribui com:

- **Uma API funcional e testada no SE 2023** — métodos reais, com parâmetros reais, validados contra uma instalação licenciada.
- **Um "SDK offline" gerado por introspecção** — o dump `SE_API_dump_*.txt` lista coclasses, interfaces, enums, direção de parâmetros (`[out]`, `[opt]`) e valores de enum.
- **Padrões de chamada COM robustos** — late binding, marshaling de `SAFEARRAY`, `ParameterModifier` para `[out]`, `InvokeMember` para VARIANT, OLE message filter para retry.
- **Add-in moderno (COM) sem admin** — registro por usuário (`HKCU`), ribbon nativa e carregamento in-process.
- **Código dividido em camadas** — `AutoEDM.Core` (lógica reaproveitável), `AutoEDM.AddIn` (ribbon), `AutoEDM` (GUI de debug) e `AutoEDM.Register` (registro HKCU).

Se você programa integrações com Solid Edge, pode usar este projeto como **ponto de partida**, **catálogo de referência** ou **biblioteca base** para seus próprios add-ins.

---

## Arquitetura

```text
AutoEDM.sln
├── src/AutoEDM.Core           # Núcleo reaproveitável (late binding + early binding opcional)
│   ├── Com/                   # Conexão COM, introspecção, OLE filter, ComLifetime, helpers
│   ├── Config/                # AutoEdmConfig — config.json externo, com defaults de fábrica
│   ├── Assembly/              # Contexto de montagem, ocorrências, edição in-place
│   ├── Selection/             # Leitura de cor, seleção de faces, geometria, segmentação
│   ├── Electrode/             # Eletrodos: plano, Ra, offset, Inter-Part Copy, nomes, blank
│   ├── Model/                 # Units — conversão metro/milímetro num lugar só
│   ├── Reporting/             # Relatórios de coordenadas (.txt / .csv)
│   └── Experiments/           # Probes de validação de API
├── src/AutoEDM.AddIn          # Add-in COM (ribbon "AutoEDM" + formulários)
├── src/AutoEDM               # GUI WinForms de debug (conecta via ROT)
├── src/AutoEDM.Register      # >>> Registrador/desregistrador HKCU (sem admin) <<<
└── tests/AutoEDM.Core.Tests  # Testes de unidade da lógica pura do núcleo
```

`AutoEDM.Core` compila para **dois** alvos: `net472` (o que o add-in carrega dentro do Solid Edge) e `net8.0-windows` (o que os testes rodam, fora do CAD).

---

## API em uso: o que você encontra aqui

A biblioteca trabalha principalmente com as type libraries do Solid Edge: `SolidEdgeFramework`, `SolidEdgeAssembly`, `SolidEdgePart` e `SolidEdgeGeometry`. Os membros mais usados no fluxo atual incluem:

| Objeto / Coleção | Membro | Uso no AutoEDM |
|---|---|---|
| `Application` | `GetActiveObject` / ROT | Conectar à instância aberta do SE |
| `Application` | `GetDefaultTemplatePath(1)` | Template padrão de `.par` (`igPartDocument = 1`) |
| `AssemblyDocument` | `Occurrences` | Acessar/montar ocorrências |
| `Occurrences` | `AddByTemplate(path, template)` | Criar peça **em contexto** (in-place) |
| `Occurrence` | `GetTransform` / `PutOrigin` | Posicionar ocorrência (metros, radianos) |
| `Occurrence` | `Activate` (bool) | Ativar/desativar ocorrência |
| `Occurrence` | `CreateTopologyReference(key)` | Referência de topologia entre peças |
| `Face` | `Style.Diffuse{Red,Green,Blue}` | Ler cor da face (0..1 → ×255) |
| `Face` | `GetRange(MinPt, MaxPt)` | Bounding box (metros) |
| `Face` | `GetReferenceKey` | Chave para `TopologyReference` |
| `Constructions` | `CopySurfaces.Add(...)` | Inter-Part Copy associativo de faces |
| `Constructions` | `OffsetSurfaces.Add(...)` / `StitchSurfaces.Add(...)` | Offset das faces por Ra/spark gap; consolidar faces soltas numa superfície coesa |
| `Model` | `Attach(nObjects, objects, bAdd, fpcSide)` | Anexar a superfície de queima ao sólido do bloco (síncrono, sem feature na árvore) |
| `Model` | `FaceOffsets.AddEx(...)` | Aplicar o GAP (spark gap) nas faces já unidas ao bloco (ordenado) |
| `Documents` / `SelectSet` | `StartCommand` | Fallback para comandos nativos interativos |

> **Regra de ouro deste projeto:** nenhuma assinatura é inventada. Toda assinatura vem do dump da typelib (`SE_API_dump_*.txt`) ou de introspecção COM ao vivo. Veja [`docs/api/`](docs/api/) e a skill [`solid-edge-com`](.claude/skills/solid-edge-com/SKILL.md).

---

## Como as chamadas funcionam

O núcleo usa **late binding** (`dynamic`) para compilar sem as typelibs do Solid Edge instaladas:

```csharp
// Conectar à instância ativa do SE via ROT
dynamic app = Marshal.GetActiveObject("SolidEdge.Application");

// Acessar documento e ocorrências
dynamic doc = app.ActiveDocument;
dynamic occurrences = doc.Occurrences;

// Criar peça em contexto (assinatura real do dump)
dynamic occurrence = occurrences.AddByTemplate(newPartPath, templatePath);
```

Para métodos com parâmetros `[out]` ou arrays tipados, usamos `InvokeMember` + `ParameterModifier`:

```csharp
// Face.GetRange precisa de by-ref em late binding
var args = new object[] { new double[0], new double[0] };
var mods = new ParameterModifier[2];
mods[0][0] = true; mods[1][0] = true;
face.GetType().InvokeMember("GetRange", BindingFlags.InvokeMethod, null, face, args, mods, null, null);
```

Para arrays de `Face` que devem marshalar como `SAFEARRAY(IDispatch)` (ex.: `CopySurfaces.Add`), usamos tipagem forte via `Interop.SolidEdge`:

```csharp
var faces = new SolidEdgeGeometry.Face[] { face1, face2 };
copySurfaces.Add(faces.Length, faces, Type.Missing, Type.Missing);
```

Detalhes completos em [`docs/COM_INTEGRATION.md`](docs/COM_INTEGRATION.md) e [`docs/INDEX.md`](docs/INDEX.md).

---

## Build a partir do código

Precisa do **.NET SDK 10** (ou 8+): ele compila os dois alvos, `net472` e `net8.0-windows`. Não precisa do Visual Studio nem das type libraries do Solid Edge instaladas — o núcleo é late-bound e o `Interop.SolidEdge` vem do NuGet.

```powershell
git clone https://github.com/CarlosKeese/AutoEDM.git
cd AutoEDM
dotnet build AutoEDM.sln -c Release -p:Platform=x64
dotnet test  tests/AutoEDM.Core.Tests/AutoEDM.Core.Tests.csproj -c Release
```

Para registrar a sua build local, rode o registrador de onde ele saiu — a pasta já tem o add-in e as dependências que ele vai copiar:

```powershell
src\AutoEDM.Register\bin\x64\Release\net472\AutoEDM.Register.exe
```

Para **rodar de verdade** é preciso o **Solid Edge 2023/2026** aberto com uma montagem ativa. Sem o SE o projeto compila e os testes passam, mas as operações COM só funcionam com a licença.

### Testes

`tests/AutoEDM.Core.Tests` cobre a lógica que não depende do CAD: leitura de `config.json` e o fallback para os defaults, mapa de cor → Ra, política da tabela de offset por Ra, biblioteca de blanks padrão, conversão de unidades e a guarda de ambiente de modelagem. Rodam em `net8.0-windows`, sem Solid Edge instalado.

> **Estado atual: 84 de 91 passando.** As 7 falhas são todas de `ORingGrooveTests`, e são **conhecidas**: os testes do alojamento de O'ring foram escritos contra uma especificação anterior à implementação que ficou — o catálogo embutido ganhou seções (1,02 / 1,27 / 1,52 mm) que o teste não espera, e as fórmulas de profundidade e largura do canal divergem das constantes 80 % / 131 % que ele assume. É por isso que a ferramenta de O'ring está marcada como *aguardando validação* no roadmap. As demais ferramentas não são afetadas.

---

## Empacotar para distribuição

O add-in é COM in-process com registro **por usuário** (`HKCU`), então não há MSI nem `regasm`: o pacote é um **ZIP portátil** que instala sem senha de administrador.

```powershell
pwsh tools\pack.ps1                    # versão = data de hoje, ex. dist\AutoEDM-2026.9.9.zip
pwsh tools\pack.ps1 -Version 2026.9.20
```

O script compila em Release, remove `*.pdb`, junta `instalar.cmd` / `desinstalar.cmd` / `LEIAME.txt` (de [`tools/dist/`](tools/dist/)) e zipa. A versão vira `FileVersion`/`InformationalVersion` e aparece na primeira linha do log do add-in — é assim que se descobre, num suporte remoto, qual build a máquina está rodando. A `AssemblyVersion` fica fixa em `1.0.0.0` de propósito: ver [`Directory.Build.props`](Directory.Build.props).

Assinatura Authenticode só se algum antivírus corporativo passar a barrar o `Register.exe`; MSI/Inno com GPO só se o parque passar de ~10 máquinas.

---

## Como criar um add-in com esta biblioteca

1. **Clone e build** (acima).
2. **Registre** com o `AutoEDM.Register.exe` (acima).
3. **Crie um novo comando:** adicione um `<button>` em [`src/AutoEDM.AddIn/Ribbon.xml`](src/AutoEDM.AddIn/Ribbon.xml) — o `id` do botão é o `CommandId` que chega em `OnControlClick` — trate esse id em `ElectrodeRibbon` e **declare o ambiente** dele na tabela `CommandSpec`.
4. **Mantenha o handler como casca fina:** ele lê a UI/seleção e chama o núcleo com argumentos explícitos. A lógica mora no `AutoEDM.Core`.
5. **Use o núcleo no seu próprio projeto:** referencie `AutoEDM.Core.dll` e use `SolidEdgeConnector`, `FaceSelector`, `RegionSplitter`, `ElectrodeBuilder` etc.

---

## Status e Roadmap

| Ferramenta | Estado |
|---|---|
| Relatório de coordenadas de queima | ✅ construído |
| Spec-sheet de eletrodos (Ra/pegada/blank/fixação) | ✅ construído |
| Criar eletrodos c/ blank (`CreateElectrodesWithBlank`) | ✅ validado no SE |
| Criar Base (bloco + faixa de medição + fixação) | ✅ validado no SE |
| Criar eletrodo (manual) a partir da seleção | ✅ validado no SE |
| Unir superfícies (anexar a queima ao bloco) | ✅ validado no SE |
| Guarda de ambiente síncrono/ordenado por comando | ✅ construído |
| Configuração externa (`config.json`) | ✅ construído, coberto por teste |
| Análise de usinabilidade: raio mínimo, alcance e canto vivo | ✅ construído, coberto por teste — validado no SE (raio); canto vivo a confirmar |
| Análise de usinabilidade: rasgo estreito e acesso (varredura do vazio) | 📋 planejado |
| Alojamento de O'ring pela ISO 3601 | 🚧 construído, aguardando validação no SE |
| Aplicar GAP (offset + cor + nome da feature) | 🚧 corrigido, aguardando confirmação final no SE |
| Duplicar eletrodo p/ o próximo Ra | 🚧 construído, aguardando validação no SE |
| Copiar superfícies (Inter-Part Copy) | 🚧 só funciona em edição em contexto (in-place) |
| Rosca física no furo M6 | 🚧 sonda de diagnóstico pronta; receita definitiva em aberto |
| Orquestrador completo ("gerar todos os eletrodos") | 📋 planejado |

Legenda: ✅ funcionando · 🚧 em andamento · 📋 planejado.

---

## Problemas comuns

| Sintoma | Causa provável |
|---|---|
| A aba **AutoEDM** não aparece | O `instalar.cmd` / `AutoEDM.Register.exe` não foi executado, ou o Solid Edge não foi reiniciado depois. |
| Instalou, sem erro nenhum, e a aba não aparece | *Mark of the Web*: o ZIP veio da internet e os DLLs continuam bloqueados. Rode o `instalar.cmd` (ele desbloqueia), ou desbloqueie o ZIP nas Propriedades **antes** de extrair. |
| O registrador diz "N travado(s)" | O Solid Edge estava aberto e segurava os DLLs. Feche o SE e rode de novo. |
| O botão está **cinza** | Ambiente ou tipo de documento errado — veja [Ambiente de modelagem](#ambiente-de-modelagem-síncrono-x-ordenado). Clique nele mesmo assim: ele explica o que trocar. |
| A cor da face não vira Ra | Aumente `ColorTolerance` no `config.json`, ou acrescente a sua cor em `RaColorEntries`. |
| Preciso reportar um erro | Mande o log de `%LOCALAPPDATA%\AutoEDM\logs` — a primeira linha traz a versão do build. |

---

## Documentação

- [`docs/PROJECT.md`](docs/PROJECT.md) — direção interna, verdades do domínio e regras da equipe.
- [`docs/COM_INTEGRATION.md`](docs/COM_INTEGRATION.md) — guia técnico de integração COM com o Solid Edge.
- [`docs/GUIA_SOLID_EDGE_COM.md`](docs/GUIA_SOLID_EDGE_COM.md) — a "pedra de roseta": como descobrir a API do SE por introspecção.
- [`docs/INDEX.md`](docs/INDEX.md) — catálogo de API, métodos e constantes do dump.
- [`docs/api/`](docs/api/) — referências markdown por namespace do Solid Edge.
- [`docs/REVISAO-AutoEDM.md`](docs/REVISAO-AutoEDM.md) — revisão de código e decisões pendentes.
- [`.claude/skills/solid-edge-com/SKILL.md`](.claude/skills/solid-edge-com/SKILL.md) — skill com os fatos validados de COM do SE.

---

## Contribuição

Contribuições são bem-vindas! Leia [`docs/PROJECT.md`](docs/PROJECT.md) para entender as regras do projeto e [`CONTRIBUTING.md`](CONTRIBUTING.md) para o fluxo de colaboração.

---

## Licença

Este projeto é licenciado sob a [MIT License](LICENSE).

---

**AutoEDM** — feito para modernizar o fluxo de eletrodos no Solid Edge e devolver à comunidade uma API de integração COM bem documentada e testada na prática. Parte das ferramentas abertas da [Kenatec](https://kenatec.com).
