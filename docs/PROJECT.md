# AutoEDM — Automação de Eletrodos (Solid Edge COM API)

Aplicação C# (.NET Framework 4.7.2, x64, WinForms) que **extrai eletrodos de
cavidades de molde no Solid Edge 2023/2026 via COM Automation**, sem o add-on pago
*Electrode Design*. O objetivo final é uma **API/automação reaproveitável** para o
fluxo real de EDM da oficina, e o conhecimento de COM do Solid Edge é consolidado
em uma **skill** (`solid-edge-com`) para servir projetos futuros.

> **Este README é o documento de orientação da equipe** (humano + IAs colaboradoras).
> Antes de propor qualquer código ou solução, leia as seções
> [Direção atual](#direção-atual-add-in-modernizado-ferramenta-por-ferramenta),
> [Como descobrimos a API](#como-descobrimos-a-api-leia-antes-de-propor-qualquer-coisa),
> [Verdades do domínio](#verdades-do-domínio-processo-real-do-carlos) e
> [Regras para IAs colaboradoras](#regras-para-ias-colaboradoras).

---

## Direção atual: add-in modernizado, ferramenta por ferramenta

> **Decisão (2026-07-07).** O Solid Edge tem um módulo comercial de eletrodos, mas
> está defasado. Em vez de perseguir um único botão "faz tudo", construímos um
> **add-in modernizado como um conjunto de ferramentas**, uma de cada vez. Cada
> ferramenta é um comando da ribbon que faz **uma** operação do fluxo e entrega valor
> sozinha. A automação total ("projetar todos os eletrodos de uma vez") é a
> **orquestração** dessas ferramentas no fim — não um projeto separado.

**Por que isso chega mais rápido à automação:**

- Cada ferramenta interativa é um **bloco validado** do pipeline automático (a mesma
  operação COM que a automação vai chamar).
- Muda o loop de depuração: em vez de uma cadeia de 10 passos falhando às cegas no
  log, valida-se **uma** operação por vez, com o SE aberto e feedback visual.
- Entrega valor **incremental** (o desenhista já usa cada ferramenta pronta) em vez de
  "tudo ou nada".

**Regra de projeto que faz as ferramentas comporem em automação (obrigatória):** cada
comando da ribbon é uma **casca fina** sobre um método do `AutoEDM.Core` que recebe
**argumentos explícitos** (documento, ocorrência, faces, params) — nunca dependendo de
estado de UI/seleção interativa no miolo. Assim o orquestrador automático chama os
mesmos métodos sem a UI. Quebrar essa regra é o único jeito de o plano falhar.

**Ordem das ferramentas (roadmap):**

1. **Relatório de coordenadas de queima** — ✅ construído. Só leitura, risco zero; é o
   que o Carlos pediu ("relatório automático pro desenhista"). Núcleo:
   `ElectrodeBuilder.BuildBurnReport` + [`Reporting/`](src/AutoEDM.Core/Reporting/).
2. Selecionar/destacar faces de queima (cor→Ra) — núcleo pronto, falta o comando.
3. Criar eletrodo em contexto (`Occurrences.AddByTemplate`) — fronteira atual.
4. Copiar superfícies de queima (Inter-Part Copy) — isola o `E_FAIL` num só comando.
5. Offset por Ra → blank + fixação.
6. **Orquestrador**: roda 1→5 em todas as regiões = automação completa.

**Papéis:** Carlos **testa** no SE real (tem a licença); Claude **constrói** o código;
Kimi **pesquisa/lê/analisa** (sem escrever código). Ver
[Regras para IAs colaboradoras](#regras-para-ias-colaboradoras).

**Âncora:** modernizar ≠ copiar o módulo comercial. Ancore no fluxo real do Carlos
([Verdades do domínio](#verdades-do-domínio-processo-real-do-carlos)), não em paridade
de features.

---

## Estado atual

Duas frentes, **mesmo núcleo**: (1) um **add-in COM** que põe a aba **"AutoEDM"** com o
botão **"Criar eletrodos"** dentro da Solid Edge; (2) uma **GUI WinForms** externa de
debug (conecta à instância ativa via ROT). Ambos trabalham **em contexto da montagem
ativa** (não em peça isolada) e chamam o mesmo `ElectrodeBuilder`. Log em
`%LOCALAPPDATA%\AutoEDM\logs` (add-in) / `bin/.../logs` (GUI).

| Componente | Arquivo | Estado |
|---|---|---|
| Conexão COM (instância ativa via ROT) | [SolidEdgeConnector.cs](src/AutoEDM.Core/Com/SolidEdgeConnector.cs) + [ComInterop.cs](src/AutoEDM.Core/Com/ComInterop.cs) | ✅ Validado no SE 2023 |
| OLE message filter (retry em servidor ocupado) | [OleMessageFilter.cs](src/AutoEDM.Core/Com/OleMessageFilter.cs) | ✅ Validado |
| Introspecção COM + **dump da typelib** ("SDK offline") | [ComDiagnostics.cs](src/AutoEDM.Core/Com/ComDiagnostics.cs) | ✅ Validado (dump tipado grava parcial) |
| Montagem: ocorrências, transform, edição in-place | [AssemblyContext.cs](src/AutoEDM.Core/Assembly/AssemblyContext.cs) + [EditInPlaceScope.cs](src/AutoEDM.Core/Assembly/EditInPlaceScope.cs) | ✅ / 🚧 |
| Leitura de cor da face (`Style.Diffuse`) | [FaceStyleColorReader.cs](src/AutoEDM.Core/Selection/FaceStyleColorReader.cs) | ✅ Validado (cor = Ra) |
| Seleção de faces por cor → Ra | [FaceSelector.cs](src/AutoEDM.Core/Selection/FaceSelector.cs) + [RaColorMap.cs](src/AutoEDM.Core/Electrode/RaColorMap.cs) | ✅ Validado |
| Bounding box de face (GetRange → Vértices) | [FaceGeometry.cs](src/AutoEDM.Core/Selection/FaceGeometry.cs) | ✅ Validado (124/124) |
| Segmentação em detalhes (1 cor ≠ 1 região) | [RegionSplitter.cs](src/AutoEDM.Core/Selection/RegionSplitter.cs) | ✅ por proximidade |
| Planejamento do eletrodo por região | [ElectrodeBuilder.cs](src/AutoEDM.Core/Electrode/ElectrodeBuilder.cs) | ✅ plano não-destrutivo |
| **Add-in COM (ribbon "AutoEDM" → "Criar eletrodos")** | [src/AutoEDM.AddIn/](src/AutoEDM.AddIn/) | ✅ Validado in-process no SE 2023 |
| **Registro por usuário (HKCU, sem admin)** | [src/AutoEDM.Register/](src/AutoEDM.Register/) | ✅ Validado |
| **Relatório de coordenadas de queima** (ferramenta 1) | [Reporting/](src/AutoEDM.Core/Reporting/) + `ElectrodeBuilder.BuildBurnReport` | ✅ construído (só leitura) — validado no SE |
| **Spec-sheet de eletrodos** (ferramenta 2) | [ElectrodeBuilder.cs](src/AutoEDM.Core/Electrode/ElectrodeBuilder.cs) | ✅ validado no SE |
| **Criar eletrodos c/ blank** (ferramenta 3, `CreateElectrodesWithBlank`) | [ElectrodeBuilder.cs](src/AutoEDM.Core/Electrode/ElectrodeBuilder.cs) | ✅ validado no SE (peça standalone → bloco → `AddByFilename` → `PutTransform`) |
| Inter-Part Copy das faces de queima | [InterPartCopier.cs](src/AutoEDM.Core/Electrode/InterPartCopier.cs) | 🚧 só funciona em edição EM CONTEXTO (in-place); fora de contexto é bloqueado pela API COM — o desenhista copia manualmente e usa "Criar Base"/"Unir superfícies" a partir daí |
| **Criar Base** (ferramenta 4 — bloco + faixa de medição + fixação sobre a superfície copiada) | [SurfaceBlockBuilder.cs](src/AutoEDM.Core/Electrode/SurfaceBlockBuilder.cs) + [BlockOverSurfacesForm.cs](src/AutoEDM.AddIn/UI/BlockOverSurfacesForm.cs) | ✅ validado no SE |
| **Unir superfícies** (anexa a queima ao bloco + aplica o GAP) | [SurfaceBlockBuilder.cs](src/AutoEDM.Core/Electrode/SurfaceBlockBuilder.cs) (`TryUniteToBlock`/`TryApplyGapOffset`) | 🚧 união validada no SE (`Model.Attach`, síncrono); GAP corrigido (`FaceOffsets.AddEx` + `DispatchWrapper`), aguardando confirmação final no SE |
| GUI de monitoramento | [MainForm.cs](src/AutoEDM/UI/MainForm.cs) | ✅ |
| Módulo corte a fio (IGES) | — | 📋 Roadmap |

Legenda: ✅ funcionando/validado no SE real · 🚧 em andamento · 📋 planejado.

### Rodar o add-in (sem admin)

```powershell
dotnet build AutoEDM.sln -c Debug
src\AutoEDM.Register\bin\Debug\net472\AutoEDM.Register.exe        # registra em HKCU
#   → reabrir a Solid Edge → aba "AutoEDM" → "Criar eletrodos"
src\AutoEDM.Register\bin\Debug\net472\AutoEDM.Register.exe /u     # remove
```

O add-in roda **in-process** (recebe o `Application` direto, sem ROT). O registro é
**por usuário** (`HKCU\Software\Classes`), então não precisa de administrador.

---

## Como descobrimos a API (leia ANTES de propor qualquer coisa)

**A documentação da API do Solid Edge exige login e as Type Libraries não são
baixáveis. NÃO adivinhe assinaturas de métodos COM.** O SE registra as typelibs
localmente, então descobrimos a API **em tempo de execução, por introspecção**:

1. **Dump completo da typelib** — `ComDiagnostics.DumpTypeLibraries` sobe de um
   objeto vivo (`ITypeInfo → GetContainingTypeLib → ITypeLib`) e enumera TODAS as
   coclasses/interfaces/enums, com **nomes + tipos + direção (`[out]`/`[opt]`) dos
   parâmetros e valores de enum**. Gera `bin/.../logs/SE_API_dump_{versão}.txt`
   (as 4 libs: Framework, Assembly, Part, Geometry).
2. **Fonte da verdade da API = esse dump.** Consulte-o por `grep`, nunca o carregue
   inteiro. Se um método não está lá, **não existe com aquele nome** — peça um novo
   dump, não invente.
3. **Loop de validação** — quem tem licença do SE roda a GUI, gera um log numerado
   (`AutoEDM NNN.log`) e o dump, e envia de volta. Cada run responde **uma** dúvida.

Todo esse método (e as armadilhas de COM) está na skill **`solid-edge-com`**
(`.claude/skills/solid-edge-com/`). **Consulte a skill antes de escrever código de
automação.** Correções de conhecimento devem ir para a skill, não para docs avulsos.

Para uma referência técnica completa da integração COM (ProgID, type libraries,
registro Windows, add-ins, objetos/métodos principais e fluxo do AutoEDM), veja
[`docs/COM_INTEGRATION.md`](docs/COM_INTEGRATION.md). Para o catálogo completo de
API, métodos, parâmetros e constantes extraídos do dump, veja
[`docs/INDEX.md`](docs/INDEX.md).

### Restrições de COM que já custaram runs (previsíveis)

- **Unidades = METROS e RADIANOS** na API de geometria/modelagem. 20 mm = `0.020`.
- **Coleções são 1-based** (`.Item(1)`, `.Count`).
- **x64 obrigatório** e **thread STA** (a UI já é STA).
- **Late binding (`dynamic`)** para compilar sem as typelibs.
- **Parâmetros `[out]`** (ex.: `Face.GetRange`) NÃO populam em late binding sem
  `ParameterModifier` marcando by-ref.
- **Erro de conversão de VARIANT no binder dinâmico** → chame via `InvokeMember`
  (o IDispatch coage) e use `Type.Missing` para parâmetros de objeto opcionais.
- **`cParams=0` num "método"** normalmente significa que é uma **propriedade-coleção**
  (ex.: `Constructions.OffsetSurfaces`) e a operação é `.Add(...)` na coleção.
- **`RPC_E_CALL_REJECTED` / `RPC_E_SERVERCALL_RETRYLATER`** → o OleMessageFilter
  precisa dar retry (já tratado).

---

## Verdades do domínio (processo REAL do Carlos)

Estas são regras do fluxo de trabalho real. **Qualquer solução que as contrarie está
errada, por mais "correta" que pareça num tutorial genérico de CAD:**

- O eletrodo é projetado **EM CONTEXTO DE MONTAGEM** (`.asm`), não em peça isolada.
  A origem da montagem **é o zero-máquina**; a posição da ocorrência já é a
  coordenada de queima (base do relatório automático).
- **As cores pintadas nas faces codificam o Ra** (rugosidade), separando queima de
  não-queima. Mapa em `RaColorMap`. O CAM (NX) usa essas cores.
- **1 cor ≠ 1 região.** Uma mesma cor marca vários detalhes espalhados; cada detalhe
  (conjunto de faces) vira **um eletrodo**. Segmentação por **proximidade espacial**.
- Por região gera-se **1 eletrodo de DESBASTE** (Ra acima) **+ 1 de ACABAMENTO**
  (Ra da cor). Não existe eletrodo único com faces de Ra diferentes.
- A cópia das superfícies de queima é **Inter-Part Copy associativo**
  (`Constructions.CopySurfaces.Add`), preservando associatividade — **não** trazer a
  peça inteira.
- **O eletrodo ENCOLHE**: offset das faces de queima **para DENTRO**,
  `offset = f(GAP, Ra)` (`OffsetSurfaces.Add` com sentido interno). Desbaste = offset
  maior; acabamento = menor. GAP é só por Ra (não por material).
- Raios pequenos demais para produzir: **só detectar/sinalizar**, nunca consertar.
- Entrega do eletrodo: **`.par` nativo** (NX lê direto e preserva cor). **NÃO** há
  export Parasolid/STEP/`.x_t` no fluxo do eletrodo.
- Corte a fio (módulo separado): copia superfície, estende à base e à altura da
  máquina, adiciona GAP, gera perfis e exporta **IGES** (2 perfis cônico, 1 paralelo)
  para o Pitágoras.

Detalhes de dados já fornecidos (tabela de offset por Ra, catálogo de blanks,
padrão de fixação M6+2×Ø4) estão implementados no código do domínio.

---

## Regras para IAs colaboradoras

**Divisão de trabalho (2026-07-07):** **Carlos** roda e testa no Solid Edge real (tem a
licença e valida cada ferramenta). **Claude** constrói o código (núcleo + add-in + GUI).
**Kimi** ajuda **pesquisando, lendo e analisando** — **não escreve código**: lê o
dump/logs/código e produz análises que ajudam o Claude a decidir os próximos passos.
Regras comuns a todos, para pararmos de gastar ciclos com soluções que não rodam:

1. **Nunca invente API COM.** Toda assinatura vem do `SE_API_dump_*.txt` (grep) ou
   da skill `solid-edge-com`. Sem evidência no dump → marque como "a validar",
   não como fato. Pattern-matching de tutorial genérico é a causa nº 1 de erro
   (ex.: usar `AddCopiedPart` — que traz a peça inteira — onde o certo é
   `CopySurfaces.Add`, que copia faces).
2. **Respeite as [Verdades do domínio](#verdades-do-domínio-processo-real-do-carlos).**
   Contrariá-las (peça isolada, export `.x_t`, offset para fora, 1 cor = 1 região)
   invalida a proposta.
3. **Respeite as [restrições de COM](#restrições-de-com-que-já-custaram-runs-previsíveis)**
   (metros, 1-based, `[out]` por `ParameterModifier`, `InvokeMember` para VARIANT).
4. **Todo conhecimento reaproveitável vira skill**, não doc solto. Se descobrir algo
   novo e validado, proponha a edição da skill `solid-edge-com`.
5. **Não introduza dependências novas** (frameworks, linguagens, camadas) sem que
   sirvam diretamente à extração de eletrodo validada contra o SE real.

### Kimi — pesquisa e análise (não escreve código)

O papel do Kimi é **acelerar a decisão do Claude**, não produzir código. Tarefas úteis:

- **Ler e resumir o dump** (`SE_API_dump_*.txt`): achar assinaturas, tipos, direção de
  parâmetros e valores de enum de um método/coleção específico (ex.: `AddByTemplate`,
  `OffsetSurfaces.Add`), sempre por `grep`, nunca carregando o arquivo inteiro.
- **Analisar os logs numerados** (`logs/AutoEDM NNN.log`): identificar o HRESULT/erro
  real, correlacionar com o passo que falhou e propor a próxima hipótese a testar.
- **Comparar abordagens**: p.ex. como o módulo comercial de eletrodos do SE resolve um
  passo vs. o nosso, ou alternativas de API (`CopySurfaces` vs. `InterpartConstructions`
  vs. `CreateTopologyReference`) — com prós/contras.
- **Pesquisar a API do SE** (fóruns, Programmer's Guide, Solid Edge Community) para achar
  o nome/uso de um método, **sempre marcando "a validar no dump/SE"**.

Entregáveis do Kimi = **análises, tabelas comparativas, hipóteses priorizadas** em texto
(markdown), apontando arquivos/linhas e trechos do dump. **Nunca** editar código,
`.csproj`, ou criar arquivos de implementação. Se identificar uma correção,
**descreva-a** (arquivo, método, o que mudar e por quê) para o Claude aplicar.

Regras de qualidade (valem para qualquer pesquisa): **nunca invente API COM** — toda
assinatura vem do dump; **sinalize dependência de versão** do SE; e **respeite as
verdades do domínio** (a última rodada de sugestão trocaria `CopySurfaces` por
`AddCopiedPart` — o dump mostrou que são coisas diferentes).

---

## Build & Run

```powershell
dotnet build AutoEDM.sln -c Debug    # compila SEM o Solid Edge (late binding)
```

Para rodar de verdade é preciso Solid Edge 2023/2026 com licença **já aberto** com a
montagem ativa. Sem argumentos, sobe a GUI; com argumentos há modos console de debug
(`plan`/`faces`).

```powershell
AutoEDM.exe            # GUI (padrão)
```

> **Feche a GUI antes de recompilar** — o processo em execução trava o `.exe` de saída.

Fluxo do incremento atual (não-destrutivo): conecta ao SE → lê a montagem ativa →
varre faces → cor → Ra → segmenta em detalhes → imprime o **plano** de eletrodos
(desbaste+acabamento por detalhe). A geometria (cópia/offset/blank/save) está sendo
implementada com as assinaturas já descobertas.

## Early binding (opcional, produção)

Late binding (`dynamic`) é o padrão portável. As typelibs existem na instalação
(`C:\Program Files\Siemens\Solid Edge 2023\Program\*.tlb`); para produção pode-se
gerar interops (`TlbImp.exe` / *Add COM Reference* / NuGet `Interop.SolidEdge`) e
trocar por tipos fortes atrás de um `#if EARLY_BINDING`. Ver notas em
[AutoEDM.csproj](src/AutoEDM/AutoEDM.csproj).

## .NET 10

O connector já é portável (`ComInterop.GetActiveObject` via P/Invoke substitui o
removido `Marshal.GetActiveObject`), verificado compilando sob `net10.0-windows`.
Fica em `net472` por ora; migrar = trocar `TargetFramework` + pacote
`System.Drawing.Common`.
