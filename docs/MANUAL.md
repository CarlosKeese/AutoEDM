# AutoEDM — Manual de funcionamento

> **O que é:** o manual único do projeto — a camada COM, as duas APIs (Core e MCP), a skill `solid-edge-com` e as 25 funcionalidades entregues, com as regras de negócio e seus valores exatos.
> **Para quem:** quem vai manter ou estender o AutoEDM (pessoa ou agente), e quem precisa saber *por que* um número é aquele antes de mudá-lo.
> **Como ler:** cada afirmação traz `arquivo:linha`. O código é a fonte da verdade; quando este manual divergir dele, o código vence e o manual é que está errado.
> **Levantado em:** 2026-09-21, contra o commit `20c58ee`. Conferido nesta data: 25 botões na ribbon, 12 ferramentas MCP (3 de escrita), 369 casos de teste, `GuiVersion = 16`.

---

## Índice

| Parte | Assunto |
|---|---|
| [0](#parte-0--o-terreno) | O terreno: projetos, glossário, onde ficam os arquivos |
| [1](#parte-1--com-a-camada-solid-edge) | **COM** — conexão, registro, ciclo de vida, geometria, escopos, armadilhas |
| [2](#parte-2--api-o-core-e-a-ponte-mcp) | **API** — o Core em C# e as 12 ferramentas MCP |
| [3](#parte-3--a-skill-solid-edge-com) | **SKILL** — `solid-edge-com`: o que impõe e quando carrega o quê |
| [4](#parte-4--funcionalidades) | **Funcionalidades** — os 25 comandos e suas regras de negócio |
| [5](#parte-5--operação) | Operação — build, registro, deploy, o ciclo com a SE aberta |
| [A](#apêndice-a--estado-da-documentação-2026-09-21) | Apêndice — estado da documentação |

---

# Parte 0 — O terreno

## 0.1 O que o AutoEDM é

Um add-in COM que vive **dentro do processo da Solid Edge** (`Edge.exe`) e automatiza o trabalho de ferramentaria de moldes: eletrodos de EDM, corte a fio, alojamento de anel de vedação, listas de corte e a folha de revisões. Cinco projetos na solução:

| Projeto | Alvo | Papel |
|---|---|---|
| `AutoEDM.Core` | `net472` **+** `net8.0-windows` | Toda a lógica. Multi-target porque o add-in exige Framework e o servidor MCP exige .NET moderno — o contrato e o catálogo são escritos uma vez só |
| `AutoEDM.AddIn` | `net472` | A ribbon, as janelas, o hospedeiro da ponte. É o artefato principal |
| `AutoEDM.Register` | `net472` | Registra o add-in em `HKCU` e faz o deploy para `%LOCALAPPDATA%` |
| `AutoEDM.Mcp` | `net8.0-windows` | Servidor MCP em stdio; fala com o add-in por named pipe |
| `AutoEDM` | `net472` | GUI de depuração, fora do CAD |

**Restrição de linguagem:** C# **7.3** no `Core` — sem `switch` de expressão, sem `new` de tipo-alvo, sem tupla nomeada atravessando `dynamic` (o nome do elemento não existe em runtime).

**Convenção de idioma** (`CONTRIBUTING.md`): identificadores em inglês, tudo que a pessoa lê — mensagem, log, comentário — em português.

## 0.2 Glossário

| Termo | O que é |
|---|---|
| **Eletrodo** | Peça de cobre que queima a cavidade por EDM. Sai subdimensionada pelo GAP |
| **GAP** (faísca) | Folga entre eletrodo e aço. Depende do Ra desejado: de 0,05 a 0,30 mm |
| **Ra** | Rugosidade alvo em µm. Determina o GAP e, no AutoEDM, é codificado por **cor de face** |
| **Blank** | Barra de cobre bruta do estoque, de seção padronizada. **Não leva sobremetal** |
| **Bloco / holder** | Corpo do eletrodo acima da queima; é por ele que a máquina segura |
| **Faixa de medição** | Degrau menor no topo do bloco, com chanfro de orientação, usado para zerar |
| **Pegada** | Caixa envolvente XY da região de queima |
| **Desbaste × acabamento** | Dois eletrodos do mesmo detalhe em Ra diferentes; o de desbaste é o mais grosso |
| **Postiço** | Inserto do molde |
| **WEDM** | Corte a fio. O AutoEDM exporta o perfil em `.igs`, um por altura Z |
| **Pitágoras** | O CAM de corte a fio da casa, consumidor final dos `.igs` |
| **Síncrono × ordenado** | Os dois ambientes de modelagem da SE. Métodos COM **diferentes**; o AutoEDM nunca troca no meio de uma operação — só `se_trocar_ambiente`, como passo à parte |
| **Ponte** | O named pipe que liga um agente à Solid Edge viva |

## 0.3 Onde ficam os arquivos

| O quê | Onde |
|---|---|
| Add-in instalado (CodeBase registrado) | `%LOCALAPPDATA%\AutoEDM\addin` |
| Configuração | `%LOCALAPPDATA%\AutoEDM\config.json` |
| Log da sessão | `%LOCALAPPDATA%\AutoEDM\logs\AutoEDM_{yyyyMMdd_HHmmss}.log` |
| Mapa da API COM (cumulativo) | `%LOCALAPPDATA%\AutoEDM\logs\SE_API_dump_<versão>.txt` |
| Catálogo de anéis de vedação | `%LOCALAPPDATA%\AutoEDM\oring-catalog.txt` |
| Eletrodos gerados | subpasta `Eletrodos` ao lado da montagem |
| Perfis WEDM | pasta da peça, `{peça} Z = XX.XX.igs` |
| Folha de revisões | `.xlsx` onde o usuário escolher; estado em `{montagem}_revisoes.json` ao lado da montagem |

---

# Parte 1 — COM (a camada Solid Edge)

Toda a integração é **late binding com `dynamic`**: compila e roda sem as type libraries da SE instaladas (`src/AutoEDM.Core/Com/SolidEdgeConnector.cs:11-15`). O preço é que nenhum erro de assinatura aparece na compilação — daí a regra-mãe do projeto: **nunca inventar assinatura**; introspeccionar antes de chamar.

## 1.1 Conexão

| Cenário | Como |
|---|---|
| Fora do CAD (console, GUI de depuração) | `new SolidEdgeConnector().Connect(...)` — registra o message filter, procura na ROT, e só então inicia instância nova |
| Dentro do add-in | `SolidEdgeConnector.Attach(app)` — o `Application` chega pronto no `OnConnection` |

`Marshal.GetActiveObject` não existe no .NET moderno; há P/Invoke próprio de `CLSIDFromProgID` + `GetActiveObject` em `src/AutoEDM.Core/Com/ComInterop.cs:19-45`. O `PreserveSig=false` converte `MK_E_UNAVAILABLE (0x800401E3)` em `COMException`, lido como "nenhuma SE rodando" (`SolidEdgeConnector.cs:185-190`).

**A thread tem de ser STA** (`SolidEdgeConnector.cs:23-24`). No add-in tudo já roda na thread da própria SE.

## 1.2 As leis invioláveis

Seis restrições que, violadas, **não estouram exceção — produzem resultado errado em silêncio**:

| # | Lei | Onde |
|---|---|---|
| 1 | **A API é em METROS e RADIANOS**; o domínio é em milímetros. Converter só por `Units.MmToM` / `Units.MToMm` — nunca `/1000.0` solto | `Model/Units.cs:3-22` |
| 2 | **Toda coleção COM é 1-based** | marcado `// 1-based` em cada laço |
| 3 | **x64 obrigatório**, **STA obrigatório** | `Directory.Build.props`, `AutoEDM.Register/Program.cs:20` |
| 4 | **Parâmetro `[out]` exige `ParameterModifier`** marcando by-ref; sem isso a leitura volta **vazia**, sem erro | `Selection/FaceGeometry.cs:238-249` |
| 5 | **Array passado ao COM tem de ser TIPADO** (`SolidEdgeGeometry.Face[]`, `SolidEdgePart.Profile[]`). `object[]` marshala como `SAFEARRAY(VARIANT)` e a SE rejeita | `Electrode/BlankModeler.cs:16-17` |
| 6 | **O message filter é obrigatório** em processo externo: sem ele qualquer operação demorada derruba a chamada com `RPC_E_CALL_REJECTED` | `Com/OleMessageFilter.cs:6-19` |

Sobre ângulos **não há regra única**: `GetTransform` devolve radianos, `HoleData.BottomAngle` é em **graus**, e só `ThreadTaperAngle` volta a ser radiano (`BlankModeler.cs:706-707`). Confira a propriedade, não o objeto.

## 1.3 Ciclo de vida dos objetos COM

A regra está escrita em `Com/ComLifetime.cs:6-17`:

> Cada acesso via `dynamic` cria um RCW que segura uma referência do lado do CAD. Num laço sobre milhares de faces sem soltar, a SE acumula memória e handles e pode não fechar direito.

**Quem obtém, libera:**
- **Libere** o que você enumerou e descartou num laço quente — faces reprovadas, corpos, coleções temporárias.
- **NUNCA** libere o que o chamador ainda vai usar (a face vencedora), nem o `Application`, nem o documento ativo.

`ComLifetime.Release(o)` só age se `Marshal.IsComObject(o)` e nunca lança. `ComLifetime.Scoped(o)` devolve um descartável para `using`.

`Marshal.FinalReleaseComObject` no `Application` **só quando o AutoEDM iniciou a instância** — num add-in in-process o `Application` pertence à Solid Edge (`SolidEdgeConnector.cs:262-267`).

### Proxies mortos — a outra metade

Depois de um `Feature.Add` a peça regenera, e a chamada seguinte pode falhar:

| HRESULT | Quando | Tratamento |
|---|---|---|
| `0x80010114` "o objeto não existe" | logo após um `Add` bem-sucedido | `RetryStaleCom` com backoff `{200, 400, 800, 1500}` ms — uma retentativa de 250 ms **não bastava** (`BlankModeler.cs:956-982`) |
| `RPC_E_DISCONNECTED (0x80010108)` | após `Holes.AddSync`; ao trocar `ModelingMode` | re-adquirir `app.ActiveDocument` e reconstruir plano e perfil a partir dele |

Passar um `dynamic` como **argumento** amarra o call site ao proxy **antes** de o método rodar — com o proxy morto isso lança `RPC_E_DISCONNECTED` sem sequer entrar no método. Daí o cast `(object)` em `Sealing/ORingGrooveModeler.cs:214-219`.

## 1.4 Geometria: os padrões de acesso

**Caminho canônico até as faces:**

```
PartDocument.Models → Model.Item(i) → Model.Body → Body.Faces[queryType] → Face
```

`Body.Faces` é **indexada por tipo de consulta de topologia**, não por posição, e o valor de `igQueryAll` **varia entre versões da SE**. O projeto validou `1` no SE 2023 e fixou `FaceSelector.ForcedFaceQueryType = 1` (`Selection/FaceSelector.cs:40-52`); pôr `null` reativa a sondagem automática em outra versão.

**Caixa envolvente** (`Selection/FaceGeometry.cs`) — três executores, com ordens diferentes de propósito:

| Método | Ordem das tentativas | Por quê |
|---|---|---|
| `TryGetRangeMm` | `GetRange` → `GetExactRange` → varredura de vértices | quem agrupa detalhe por proximidade prefere errar **para maior** |
| `TryGetExactRangeMm` | `GetExactRange` primeiro | em aresta **B-spline** o `GetRange` infla a caixa **±0,005 mm**; o exato dá Δ=0,00000 mm (medido 2026-09-15) |
| `TryGetBodyRangeMm` | o corpo inteiro | rede de segurança: o bbox por-face perde faces ilegíveis e já subestimou o topo Z em 0,2 mm. Usado só para **expandir**, nunca encolher |

**Identidade de face:** `Face.ID`, lido com `Convert.ToString` porque o tipo não é assumido. É a chave do mapa `faceID → cor` (`FaceSelector.cs:265`) e o que liga a face de uma feature à face do corpo — é assim que a folha de revisões pinta o que mudou.

**Cor de face — três camadas, nesta ordem** (`FaceSelector.EnumerateColoredFaces`):
1. `face.Style.Diffuse{Red,Green,Blue}` — canais 0..1.
2. `Face.GetRGBAVals([out] R,G,B,A)` — é a cor do **corpo**.
3. **Mapa por FEATURE** — a cor aplicada pelo menu de pintura da SE **não aparece** em nenhuma das duas; só em `feature.GetStyle()`. A cor de feature sobrepõe a do corpo (`FaceSelector.cs:211-280`).

Para **pintar**, o caminho é `Body.SetFacesStyle`: escrever em `Face.Style.Diffuse*` falha em silêncio porque `Face.Style` vem nulo. Só faces entram (a seleção costuma trazer um item que não é face), uma chamada por corpo, e o resultado é conferido lendo `Face.Style.StyleName` de volta (`Electrode/FaceColorPainter.cs`).

**Aresta** não se pega pelo `SelectSet`: a ferramenta Selecionar da SE, em ambiente de peça, só localiza face e feature. É preciso um comando **nosso** com filtro de localização — `Application.CreateCommand` → `Command.Start()` → `Mouse.AddToLocateFilter` → evento `MouseClick`, cujo último argumento **é** o objeto clicado (`AddIn/UI/SePicker.cs:7-26`).

## 1.5 Os três escopos

### `SketchScope` (`IDisposable`) — o dono dos esboços temporários

Garante que todo esboço e plano criado por código seja apagado no fim, **conferido pela contagem da coleção** e, se resistir, logado como ERRO com instrução ao usuário.

Existe por três motivos concretos (`Com/SketchScope.cs:12-29`): (a) `ProfileSets.Add()` cria esboço **ORDENADO mesmo em peça síncrona** — consumido por um recurso síncrono, vira órfão que a interface da SE se recusa a apagar; (b) cada corte criava até cinco esboços, e não dá para juntá-los porque **um `ProfileSet` aceita UM perfil** (o segundo `Profiles.Add` dá `E_FAIL`); (c) todos os `Delete()` estavam em `catch` mudo.

- `AddProfileSet()` — use sempre, em vez de `doc.ProfileSets.Add()`.
- **`Release(item)`** — tira do escopo: o item **fica** na peça. Em ambiente ordenado, esboço e plano são **filhos do recurso**; apagá-los mata o recurso recém-nascido.
- `DeleteVerified(...)` — um `Delete()` que não lança **pode não ter apagado nada**; confira contando a coleção antes e depois.

### `ModelingEnvironment` — uma porta, não um escopo

**Premissa do projeto** (`Com/ModelingEnvironment.cs:17-37`): **nenhum comando troca o `ModelingMode` da peça do usuário.** A troca reconstrói o corpo e transforma toda Face/Edge já lida em proxy morto — era o bug do "Aplicar GAP".

Em vez de trocar, **cada comando declara** o ambiente que exige. Na ribbon isso é a tabela `Specs` (`ElectrodeRibbon.cs:982-1032`), aplicada em dois níveis: um relógio de 750 ms mantém o botão **cinza** no ambiente errado, e o clique ainda passa por `AllowedHere(..., explain: true)`, que explica em vez de só falhar.

### `EditInPlaceScope` — ⚠ não usar em produção

Documenta uma **premissa falsa**, confirmada em ~11 runs: `Occurrence.Activate = true` **NÃO** entra em edição in-place — é a flag de carga da ocorrência. Os sinais reais são `AssemblyDocument.ModelingInAssembly` e `.InPlaceActivated`. Consequência: **Inter-Part Copy por COM está bloqueada**, e o fluxo real cria o eletrodo como peça standalone + `AddByFilename` + `PutOrigin`. A classe sobrevive só para experimentos.

## 1.6 Receita mínima

O menor trecho que respeita as convenções da casa: abrir, pegar uma face, medir e criar geometria.

```csharp
// Fora do add-in: Connect() registra o OleMessageFilter e lê a ROT. Thread STA.
// Dentro do add-in: SolidEdgeConnector.Attach(app) — o Application é da SE e NÃO se libera.
using (var connector = new SolidEdgeConnector())
{
    dynamic app = connector.Connect(startIfNotRunning: true, makeVisible: true);
    dynamic doc = connector.OpenDocument(@"C:\jobs\cavidade.par");

    // Porta de ambiente: sketch + extrusão é receita SÍNCRONA. Nunca trocar o modo no meio.
    if (!ModelingEnvironment.Require(doc, ModelingEnv.Synchronous, "Receita mínima")) return;

    dynamic models = doc.Models;
    dynamic model  = models.Item(1);   // 1-based
    dynamic body   = model.Body;       // referência COM própria: sobrevive ao Model
    dynamic faces  = body.Faces[1];    // INDEXADA POR QUERY TYPE (1 = igQueryAll no SE 2023)

    object face = null;
    int n = (int)faces.Count;
    for (int i = 1; i <= n; i++)       // 1-based
    {
        object f = faces.Item(i);
        if (face == null) face = f;    // a que fica viva é do chamador
        else ComLifetime.Release(f);   // laço quente: solte o que descartou
    }
    ComLifetime.Release((object)faces);
    ComLifetime.Release((object)model);
    ComLifetime.Release((object)models);

    // [out] em late binding exige ParameterModifier — FaceGeometry encapsula isso E o mm.
    double[] minMm, maxMm;
    if (!FaceGeometry.TryGetRangeMm(face, out minMm, out maxMm)) return;  // nunca chute um range

    double hx = Units.MmToM(maxMm[0] - minMm[0]) / 2.0;
    double hy = Units.MmToM(maxMm[1] - minMm[1]) / 2.0;
    double cx = Units.MmToM((minMm[0] + maxMm[0]) / 2.0);
    double cy = Units.MmToM((minMm[1] + maxMm[1]) / 2.0);

    using (var scope = new SketchScope(doc, "Receita mínima"))
    {
        dynamic profileSet = scope.AddProfileSet();                          // nunca doc.ProfileSets.Add()
        dynamic profile    = profileSet.Profiles.Add(doc.RefPlanes.Item(1)); // UM perfil por ProfileSet

        dynamic lines = profile.Lines2d;
        lines.AddBy2Points(cx - hx, cy - hy, cx + hx, cy - hy);
        lines.AddBy2Points(cx + hx, cy - hy, cx + hx, cy + hy);
        lines.AddBy2Points(cx + hx, cy + hy, cx - hx, cy + hy);
        lines.AddBy2Points(cx - hx, cy + hy, cx - hx, cy - hy);
        profile.End(1);                                                      // 1 = perfil FECHADO

        // Array TIPADO: object[] viraria SAFEARRAY(VARIANT) e a SE rejeita.
        var arr = new SolidEdgePart.Profile[] { (SolidEdgePart.Profile)profile };

        ((object)doc.Models).GetType().InvokeMember(
            "AddFiniteExtrudedProtrusion", BindingFlags.InvokeMethod, null, doc.Models,
            new object[] { 1, arr, 2, Units.MmToM(10.0) });   // side: 1=-normal, 2=+normal, 3=simétrico
        // Em ORDENADO seria scope.Release(profileSet): lá o esboço é filho do recurso.
    }

    ComLifetime.Release(face);
}
```

No add-in, troque o bloco de conexão por `Attach` e ponha o corpo dentro do wrapper `Run(...)` (`ElectrodeRibbon.cs:1137-1154`), que já confere documento e ambiente, loga início e fim, e captura tudo em `Fail`: **nenhuma exceção deve subir para a Solid Edge**.

## 1.7 Armadilhas — sintoma → causa → correção

As que custaram rodada de teste. A tabela completa (25 linhas) está na skill, em `errors.md`.

| Sintoma | Causa | Correção |
|---|---|---|
| `[out]` volta vazio (`Face.GetRange` zerado, cavidade sempre em 0,0,0) | late binding não popula `[out]` sozinho | `ParameterModifier` by-ref; placeholder `new double[0]` — `double[3]` semeado dá `DISP_E_TYPEMISMATCH` |
| `DISP_E_TYPEMISMATCH` passando array | `object[]` = `SAFEARRAY(VARIANT)` | array tipado |
| `DISP_E_TYPEMISMATCH` passando `null` | vira `VT_EMPTY`, não `VT_DISPATCH` nulo | `new DispatchWrapper(null)` |
| `Add` "deu certo" e a geometria está errada | a SE não lança em feature falha, e ainda mescla os **defaults de diálogo salvos** do usuário | checar `Status == igFeatureOK (1216476310)` como uint32; `IgnoreSavedDefaultValues = true`; **medir** a geometria |
| Rastro de features vermelhas ao repetir um `Add` | a SE não faz rollback | `Delete()` entre tentativas |
| `0x80010114` logo após um `Add` | a regeneração desconectou o proxy | retry com backoff; recriar refs de coleção (não cachear `model.Holes`) |
| `RPC_E_DISCONNECTED` dentro do próprio binder `dynamic` | um dos **argumentos** é proxy morto | tipar os locais; nunca apagar a única coisa que referencia um plano recém-criado |
| Furo M6 sai liso, sem rosca | `HoleType` de furo roscado é `igRegularHole (33)`, **não** `igTappedHole (37)` — o 37 é `TreatmentType`; errado, a SE cai nos defaults salvos em silêncio | usar 33. Custou meses de M6 sem rosca |
| `Documents.Open` devolve **null** após um `File.Copy` | a cópia byte a byte carrega o ID interno do original | `SaveCopyAs` — **nunca** `SaveAs`, que renomeia o documento dentro da montagem do usuário |
| `E_INVALID_MODELING_MODE`, ou no-op silencioso | método da família errada (`AddThickenFeature` só existe em ordenado) | ramificar por `ModelingMode` |
| `TypeInitializationException` no `System.Text.Json` dentro do add-in | hospedado pelo `Edge.exe`, o `.config` que vale é o **da SE**, sem os binding redirects | `AssemblyRedirect` no `AssemblyResolve` (ver §5.2) |
| `RuntimeBinder: não contém definição para 'ToList'` | LINQ sobre retorno de chamada `dynamic`; o DLR não acha métodos de extensão | variável local tipada quebra a cadeia |
| Ribbon não muda depois de alterar o XML | a SE cacheia o layout por add-in | incrementar `AddInEx.GuiVersion` |
| O código novo não roda | a SE carrega do CodeBase registrado, não do `bin/` | `tools\deploy-dev.ps1` com o `Edge.exe` fechado |

---

# Parte 2 — API: o Core e a ponte MCP

O AutoEDM tem **duas superfícies de programação**: o `AutoEDM.Core` em C#, usado pela ribbon e por quem escreve código novo; e as **12 ferramentas MCP**, usadas por um agente conversando com a Solid Edge viva.

## 2.1 A API do Core — pontos de entrada por tarefa

Todo namespace do Core é `AutoEDM.<pasta>`. Os pontos de entrada que valem conhecer:

| Tarefa | Chamada |
|---|---|
| Conectar / anexar | `Com.SolidEdgeConnector.Connect` / `.Attach` |
| Declarar ambiente exigido | `Com.ModelingEnvironment.Require(doc, env, what)` |
| Converter unidade | `Model.Units.MmToM` / `.MToMm` |
| Soltar RCW | `Com.ComLifetime.Release` / `.Scoped` |
| Esboço temporário | `new Com.SketchScope(doc, what)` |
| Ler faces por cor | `Selection.FaceSelector.SelectByRaColorMap` / `.SelectByColor` |
| Medir face / aresta | `Selection.FaceGeometry.TryGetRangeMm` / `.TryGetExactRangeMm`; `Selection.EdgeGeometry` |
| Analisar eletrodos por Z | `Electrode.ElectrodeBuilder.AnalyzeElectrodesByZWithSource` |
| Criar eletrodos | `Electrode.ElectrodeBuilder.CreateElectrodesWithBlank` / `.CreateElectrodeFromSelection` |
| Dimensionar bloco | `Electrode.SurfaceBlockBuilder.Plan` → `.Build` |
| Aplicar GAP | `Electrode.SurfaceBlockBuilder.ApplyGapToUnitedSurfaces` |
| Planejar corte de barra | `Electrode.SawCutPlanner.Identify` / `.ForBlank` |
| Varrer revisões | `Revisions.RevisionScanner.Scan` |
| Gerar a folha `.xlsx` | `Reporting.ChangeReportXlsx.Build` → `Reporting.Xlsx.XlsxWriter.Save` |
| Curvas e perfis de corte a fio | `Wedm.SurfaceRimCurveBuilder.Build`; `Wedm.WedmProfileExporter.Export` |
| Alojamento de vedação | `Sealing.ORingGrooveCalculator.Rank`/`.Compute` → `Sealing.ORingGrooveModeler.Cut` |
| Reconhecer malha | `Reverse.SurfaceRecognizer.Recognize` (puro, sem COM) |
| Introspeccionar objeto vivo | `Com.ComDiagnostics.DumpObject` |
| Configuração | `Config.AutoEdmConfig.Load()` |

**Divisão de responsabilidade:** a lógica pura (tabelas, cálculo, formatação) fica no Core e **é testável sem CAD** — é por isso que existem 369 testes. O que toca COM fica isolado nos arquivos que dizem isso no nome (`*Modeler`, `*Builder`, `*Scanner`, `*Probe`).

## 2.2 A ponte MCP — arquitetura

```
Claude Code (agente)
   │  JSON-RPC 2.0, uma mensagem por LINHA, em stdio
   ▼
AutoEDM.Mcp.exe                       processo separado, net8.0-windows, x64
   │  BridgeClient.Call(tool, argsJson)
   ▼
named pipe "AutoEDM.Bridge.v1"        1 instância, DACL do usuário atual, linha-JSON UTF-8 sem BOM
   ▼
BridgeServer                          thread de fundo DENTRO do Edge.exe
   │  Control.BeginInvoke  (Control oculto criado na thread da SE)
   ▼
SeToolRunner.Execute                  thread STA da Solid Edge, COM ao vivo
```

**Por que dois processos:** o add-in é obrigatoriamente `net472` (a SE hospeda .NET Framework in-process) e um servidor MCP precisa de .NET 8+. O contrato e o catálogo moram no `Core`, que multitargeta os dois — escritos uma vez, sem duas cópias divergindo.

| Parâmetro | Valor | Onde |
|---|---|---|
| Nome do pipe | `AutoEDM.Bridge.v1` (fixo: o servidor não teria como descobrir um nome sorteado) | `Mcp/BridgeProtocol.cs:29` |
| Versão do contrato | `1` — divergência é **recusada** com explicação | `BridgeProtocol.cs:34` |
| Enquadramento | uma linha de JSON compacto por mensagem; texto multilinha viaja escapado | `BridgeProtocol.cs:36-45` |
| Timeout de conexão | 3.000 ms | `BridgeClient.cs:26` |
| Timeout de uma operação na SE | 120.000 ms | `McpBridgeHost.cs:29` |

**Serialização:** a thread do pipe fica **bloqueada** até a SE responder. É o desejado — nenhum agente consegue pedir duas modelagens ao mesmo tempo.

**Nada nunca lança para fora:** `BridgeClient.Call` e `SeToolRunner.Execute` devolvem `Ok=false` com texto acionável. Uma exceção subindo morreria na thread da SE e o agente só veria o pipe fechar. Quando a ponte não responde, a mensagem ensina o passo a passo de ligá-la.

**A armadilha do stdout:** stdout **é** o transporte JSON-RPC, e o `Log` do Core escreve com `Console.WriteLine`. A primeira linha do `Main` guarda o stdout real e faz `Console.SetOut(Console.Error)`. Sem isso, a primeira linha de log corromperia a sessão.

### As duas travas

1. **A ponte não sobe sozinha.** Só pelo botão "Ligar ponte" na ribbon.
2. **Nasce sempre em SOMENTE-LEITURA.** "Liberar escrita" pede confirmação, e o modo **volta a somente-leitura** quando a ponte é desligada ou a SE fecha.

**Nenhuma ferramenta MCP alcança essa chave** — ela é só da ribbon. Um teste garante que *toda* ferramenta de escrita seja recusada em modo leitura, e que a recusa diga o que fazer.

## 2.3 Referência das 28 ferramentas

14 de leitura, 14 de escrita (a seção 2.3.1 traz as 15 que espelham os botões da ribbon, mais a troca de ambiente). Fonte: `Mcp/ToolCatalog.cs` (schema) e `Mcp/SeToolRunner.cs` (implementação e guardas).

### Leitura

| Ferramenta | Exige | Parâmetros | Devolve |
|---|---|---|---|
| `se_status` | nada | — | versão da SE, **modo da ponte**, documento ativo e tipo, caminho, ambiente de modelagem, tamanho da seleção |
| `se_inspecionar_selecao` | documento | `profundidade` int 0–3, **padrão 1** | introspecção ao vivo de cada item selecionado: tipo COM real, propriedades com valor, métodos, coleções |
| `se_arvore` | documento | — | montagem: as ocorrências (até 500). Peça: corpos, features na ordem da árvore, superfícies e curvas de construção |
| `se_medir_selecao` | documento | — | por objeto: tipo de geometria traduzido e `min`/`max`/`tamanho` **em mm**. Tenta `GetExactRange` primeiro |
| `se_analisar_z` | **montagem** | — | eletrodos propostos por nível de Z, contagem de faces, laudo de detecção por cor e a seção de **usinabilidade** |
| `se_coordenadas` | **montagem** | — | por eletrodo selecionado: X, Y, Z em mm, giro Z em graus, Ra e GAP gravados, área de queima em cm² |
| `se_planos` | **peça** | — | os `RefPlanes` por índice, com nome e a normal quando legível |
| `se_log` | nada | `linhas` int 1–2000, **padrão 200** | as últimas N linhas do log desta sessão da SE, e o caminho do arquivo |
| `se_reconhecer_malha` | **peça** | ver abaixo | planos e cilindros ajustados sobre os triângulos, cada um com o **RMS do próprio ajuste**; a área que sobra como "livre" |

Parâmetros de `se_reconhecer_malha` — todos opcionais e **clampados** na faixa, nunca viram erro:

| Nome | Unidade | Padrão | Faixa |
|---|---|---|---|
| `corpo` | índice 1-based | 1 | 1…1000 |
| `toleranciaMm` | mm (tesselação) | 0,01 | 0,0001…5,0 |
| `anguloPlanoGraus` | graus | 2 | 0,1…30,0 |
| `distanciaPlanoMm` | mm | 0,05 | 0,0001…5,0 |
| `anguloQuinaGraus` | graus | 35 | 1,0…89,0 |

> **Limitação medida:** `RefPlane.Normal` **não é legível** — os três planos base respondem `DISP_E_UNKNOWNNAME`. Na prática `se_planos` entrega índice e nome, não o eixo. Mapeamento validado em peça nova padrão: **XY = 1, XZ = 2**.

### Escrita (recusadas em modo somente-leitura)

**`se_modelar`** — exige **peça em ambiente SÍNCRONO** (verificado por dentro, porque com `novaPeca=true` o documento nasce durante a chamada).

| Parâmetro | Tipo | Significado |
|---|---|---|
| `exemplo` | `"carrinho"` | carga de teste pronta, 6 primitivas |
| `planoXY` / `planoXZ` | int | índices dos planos — só com `exemplo` (padrão 1 e 2) |
| `novaPeca` | bool | cria peça nova e modela nela; **não salva** |
| `primitivas` | array | os sólidos a criar, **em milímetros** |

Cada primitiva (`kind` e `heightMm` obrigatórios): `kind` `"caixa"`/`"cilindro"`, `name`, `sizeXMm`, `sizeYMm`, `diameterMm`, `heightMm`, `planeIndex` (é o que **define o eixo**), `extrudeSide` 1=contra a normal / 2=a favor / 3=simétrico, `liftMm` (≥0, distância — o sentido é `liftSide`), `liftSide` 1/2, `centerXMm`, `centerYMm`.

**Valida tudo antes de tocar na peça**: se alguma primitiva for inválida, nada é criado. Uma que falhe no CAD **não aborta as outras**, e o relatório diz qual falhou e por quê. Protrusões sucessivas fundem no mesmo corpo.

**`se_curvas_superficies`** — peça síncrona. Cria curvas derivadas sobre as extremidades paralelas a XY das superfícies (selecionadas, ou todas as de construção). Nascem como `WEDM Z = XX.XX`; rodar de novo **substitui** as anteriores.

**`se_exportar_perfis_wedm`** — peça síncrona **já salva**. Grava um `.igs` por altura Z ao lado da peça. **Escreve em disco, não altera o modelo.**

### 2.3.1 Os botões da ribbon e a troca de ambiente

Cada botão virou uma ferramenta que chama **o mesmo método do Core**. Só a borda muda: onde o botão abre janela ou pergunta Sim/Não, a ferramenta recebe a resposta como argumento. Nada abre diálogo. Fonte: `Mcp/SeToolRunner.Buttons.cs`.

| Ferramenta | Botão | Exige | Escreve | Argumentos |
|---|---|---|---|---|
| `se_trocar_ambiente` | (barra de status da SE) | peça | sim | `ambiente` `sincrono`/`ordenado` — **descarta a seleção** |
| `se_criar_eletrodos` | Criar eletrodos | montagem | sim | `confirmar` — sem ele devolve só a conferência da queima |
| `se_criar_eletrodo_manual` | Criar eletrodo (manual) | montagem + faces selecionadas | sim | — |
| `se_duplicar_eletrodo` | Duplicar eletrodo | montagem + 1 ocorrência | sim | — |
| `se_lista_corte` | Lista de corte | montagem + ocorrências | não | — (texto; a impressão fica na janela) |
| `se_lista_modificacoes` | Lista de modificações | montagem | não | — (só a varredura; o .xlsx fica na janela) |
| `se_ficha` | Ficha (spec-sheet) | montagem | arquivos | — |
| `se_criar_base` | Criar Base | peça **síncrona** | sim | `apenasPlanejar`, `material`, `blank` (nº da lista), `afastamentoMm`, `alturaMm`, `fixacao`, `faixa` |
| `se_unir_superficies` | Unir superfícies | peça **síncrona** | sim | — |
| `se_aplicar_gap` | Aplicar GAP | peça **ordenada** | sim | `ra` — omitido = o Ra gravado na peça |
| `se_alojamento_oring` | Alojamento de O'ring | peça **ordenada** + a FACE selecionada | sim | `apenasPlanejar` (**padrão true**), `arestas`, `tipo`, `vedacao`, `elastomero`, `pressao`, `secaoMm`, `afastamentoMm`, `anel`, `aceitarForaDaNorma` |
| `se_sonda_malha` | Sonda de malha | peça | só com `seccionamento` | `seccionamento` — conferido contra a chave de escrita na hora |
| `se_sonda_interpart` | Sonda inter-part | montagem | peças descartáveis | — |
| `se_sonda_rosca` | Sonda de rosca (M6) | nada | peça descartável | `ligarExibicaoRosca` (opção global da SE) |
| `se_gravador_iniciar` / `se_gravador_gravar` | Iniciar / Gravar leitura | documento | não | — (snapshot próprio, separado do da ribbon) |

**O O'ring sem clique:** a janela coleta a aresta com um clique no modelo, e a ferramenta Selecionar da SE normalmente não deixa selecionar aresta. Por isso a entrada é **só a face**: as arestas circulares dela saem numeradas, e `arestas` escolhe quais viram alojamento.

**Fora de propósito:** os cinco botões do grupo MCP. A chave de escrita é do usuário.

**A troca de ambiente é um passo à parte.** A regra de nunca trocar **no meio** de uma operação continua valendo, e nenhuma outra ferramenta troca. `se_trocar_ambiente` limpa o `SelectSet` antes (a troca reconstrói o corpo, e o que estava selecionado vira proxy morto), desliga `DisplayAlerts` só durante a troca e relê o ambiente por um documento fresco. Fluxo típico: trocar → o usuário seleciona → a ferramenta do botão.

## 2.4 Como adicionar uma ferramenta

1. Definir nome, descrição e `InputSchemaJson` em `Mcp/ToolCatalog.cs`. Marcar `Writes = true` se alterar o modelo ou gravar arquivo.
2. Declarar o pré-requisito de documento e ambiente na tabela `Requirements` de `SeToolRunner.cs`.
3. Implementar em `SeToolRunner`. **Nunca deixar exceção subir**; devolver texto que o agente possa agir em cima.
4. Recompilar **os dois processos**: `pwsh tools\deploy-dev.ps1 -Configuration Release -IncludeMcp`, com a SE **e** o Claude Code fechados. O agente lê o catálogo do **servidor**, não do add-in, e cada um carrega sua própria cópia do `Core` — esquecer o `-IncludeMcp` é a causa nº 1 de "minha ferramenta nova não aparece".

## 2.5 Testar a ponte sem CAD

- **Sem SE nenhuma:** `tests/AutoEDM.Core.Tests/McpBridgeTests.cs` sobe servidor e cliente num pipe real, com nome único por teste e um marshaler falso no lugar da SE — cobre ida e volta, reconexão, segundo hospedeiro recusado e versão de contrato incompatível.
- **Servidor sozinho:** como é stdio linha a linha, dá para alimentar `initialize` / `tools/list` / `tools/call` por stdin. Sem a SE, a chamada devolve o texto que ensina a ligar a ponte.
- **À unha:** `NamedPipeClientStream(".", "AutoEDM.Bridge.v1", PipeDirection.InOut)`, escrever uma linha `{"id":1,"tool":"se_status","args":"{}","v":1}` e ler uma linha de resposta. O `v` tem de ser **1**.
- **Num piscar:** o botão "Status da ponte" mostra se está no ar, o nome do pipe, a versão do contrato, o modo, quantos pedidos foram atendidos e a última ferramenta pedida.

---

# Parte 3 — A skill `solid-edge-com`

Uma skill de projeto, em `.claude/skills/solid-edge-com/`. Existe porque **cada assinatura chutada custa um round-trip humano dentro de uma Solid Edge licenciada** — o agente não pode testar sozinho.

## 3.1 Quando dispara

O `description` do frontmatter é um gatilho: enumera as tarefas (ler faces, cores e geometria; criar features; malha e engenharia reversa; edição em contexto; add-ins de ribbon) e os erros COM pelo nome (`RPC_E_CALL_REJECTED`, `RPC_E_DISCONNECTED`, `DISP_E_TYPEMISMATCH`, `E_NOINTERFACE`, erros do RuntimeBinder, marshaling de `[out]`). Manda disparar **mesmo que o usuário nunca mencione o SDK, e mesmo que a pergunta pareça C# puro**.

## 3.2 Os arquivos e quando carregar cada um

`SKILL.md` (228 linhas) é a entrada: regras duras + índice. Os apoios carregam **um de cada vez**, conforme a tarefa:

| Carregue | Quando |
|---|---|
| `api-signatures.md` (520 l.) | precisa de assinatura ou enum confirmado: cor de face, bbox, transform de ocorrência, in-place, **grupos da árvore ordenada**, propriedades de documento, cache de `GuiVersion` |
| `modeling-recipes.md` (342 l.) | vai construir geometria: sketch+extrusão, cilindros, furos, roscas, canais anulares, síncrono × ordenado, colocar peça em montagem |
| `discovery.md` (153 l.) | vai montar a descoberta: dump da typelib, SPY, gravador de ação, tabelas DATA do install |
| `addin-ribbon.md` (151 l.) | botão dentro da SE: registro, XML da ribbon, ícones, HKCU, pasta de deploy, diálogos modeless, escolher face ou aresta |
| `edm-electrode.md` (134 l.) | fluxo do eletrodo: cópia da superfície de queima, costura, anexar ao bloco, offset de GAP |
| `mesh-reverse.md` (124 l.) | malha: ler facetas, o que a SE ajusta e o que não, seccionar, escrever reconhecedor |
| `errors.md` (67 l.) | um HRESULT específico, erro do binder, ou "funcionou e nada aconteceu" |

> `SKILL.md` referencia os apoios como `references/<arquivo>.md`. No repositório eles estão na **raiz** da pasta da skill; a subpasta `references/` só existe dentro do pacote `solid-edge-com.skill`. `OLD/` guarda a versão monolítica anterior (756 linhas num arquivo só).

## 3.3 O que a skill impõe ao agente

**A regra-mãe:** nunca deduzir uma assinatura da memória. Introspecção antes de chamar.

As restrições não-negociáveis repetem as leis da Parte 1 (metros, 1-based, x64, STA, late binding, message filter, ROT por P/Invoke). Além delas, a skill lista o que **gera resultado errado em silêncio**: checar `Status` da feature e apagá-la antes de repetir; `ParameterModifier` no `[out]`; SAFEARRAY tipado; **nunca usar método ordenado em peça síncrona** (pode *suceder* no ambiente errado); **nunca trocar o `ModelingMode` do documento do usuário**; desabilitar o botão no ambiente errado em vez de só falhar; ler as propriedades de volta depois de um `Add` e **medir** a geometria; `.Type` de Face/Edge é topologia — a forma está em `.Geometry.Type`; enum que **indexa** coleção ≠ enum que a propriedade **devolve**.

E dois laços de trabalho: o de **descoberta** em 6 passos (sondas não-destrutivas primeiro) e o de **trabalho com o humano operando a SE** em 7 passos — **uma pergunta por rodada**, logs numerados, fechar a GUI antes de recompilar, carimbar o build com o caminho completo, isolar operação arriscada do entregável.

## 3.4 Como manter

Política escrita no fim do `SKILL.md`: registrar **data e fonte** de cada confirmação; o **dump ao vivo vence** reflexão e documentação; e manter os erros corrigidos marcados como corrigidos, para ninguém contornar um problema que já não existe.

Na prática: quando uma sessão descobre algo no CAD, o achado entra em `api-signatures.md` (com a data da medição) ou em `errors.md` (sintoma → causa → correção), e a linha do índice no `SKILL.md` é atualizada se um assunto novo passou a existir.

---

# Parte 4 — Funcionalidades

## 4.1 Os 25 comandos

A fonte canônica é dupla: `Ribbon.xml` define id, rótulo e grupo; a tabela `Specs` (`ElectrodeRibbon.cs:982-1032`) define o pré-requisito. **No ambiente errado o botão fica cinza** (relógio de 750 ms) e, se clicado, explica.

| id | Rótulo | Grupo | Documento | Ambiente |
|---|---|---|---|---|
| 3 | Analisar (Z) | Eletrodos | montagem | qualquer |
| 1 | Criar eletrodos | Eletrodos | montagem | qualquer |
| 10 | Criar eletrodo (manual) | Eletrodos | montagem | qualquer |
| 12 | Duplicar eletrodo | Eletrodos | montagem | qualquer |
| 2 | Coordenadas | Relatórios | montagem | qualquer |
| 16 | Lista de corte | Relatórios | montagem | qualquer |
| 25 | Lista de modificações | Relatórios | montagem | qualquer |
| 4 | Ficha (spec-sheet) | Relatórios | montagem | qualquer |
| 5 | Criar Base | Peça | peça | **SÍNCRONO** |
| 7 | Unir superfícies | Peça | peça | **SÍNCRONO** |
| 11 | Aplicar GAP | Peça | peça | **ORDENADO** |
| 14 | Alojamento de anel | Peça | peça | **ORDENADO** |
| 18 | Curvas das superfícies | WEDM | peça | **SÍNCRONO** |
| 17 | Exportar perfis (IGES) | WEDM | peça | **SÍNCRONO** |
| 24 | Sonda de malha | Eng. Reversa | peça | qualquer |
| 19 / 21 / 22 / 23 / 20 | Ligar ponte / Status / Liberar escrita / Somente leitura / Desligar | MCP | — | — |
| 6 | Inspecionar seleção | Diagnóstico | qualquer | qualquer |
| 8 / 9 | Iniciar leitura de ação manual / Gravar log da leitura | Diagnóstico | qualquer | qualquer |
| 15 | Sonda inter-part | Diagnóstico | montagem | qualquer |
| 13 | Sonda de rosca (M6) | Diagnóstico | qualquer | qualquer |

Os cinco comandos MCP ficam **fora** da tabela `Specs` de propósito: a ponte precisa subir com a SE vazia.

## 4.2 Eletrodos

### Analisar (Z) — id 3
Lê a cavidade **sem alterar nada** e propõe quantos eletrodos existem, segmentando as faces de queima por nível de Z. Saída em janela e log; nenhum arquivo.

| Regra | Valor | Onde |
|---|---|---|
| Mesmo nível de Z se o `MinZ` difere em até | **1,0 mm** | `ElectrodeZAnalyzer.cs:19` |
| Mesmo eletrodo dentro do nível se as regiões distam em XY até | **50,0 mm** (o tamanho dos blanks) | `ElectrodeZAnalyzer.cs:26` |
| Origem do eletrodo | **Z mínimo** — o fundo do bolsão, não o topo | `ElectrodeBuilder.cs:797-799` |
| Tolerância de cor por canal | **8** (0–255) | `RaColorMap.cs:29` |

O alarme de usinabilidade só aparece **quando reprovou algo**: silêncio é resultado bom. Há também o aviso "cor dominante não mapeada" — existe cor não mapeada com mais faces que a queima detectada, sinal de que se ia criar eletrodo sobre cor residual.

### Criar eletrodos — id 1
Cria uma peça `.par` por eletrodo proposto e a posiciona na montagem. Exige **confirmação explícita** ("A queima detectada está CORRETA?") antes de criar.

- Saída: subpasta **`Eletrodos`** ao lado da montagem, nome `{montagem}_EE##`. O índice é o maior `EE##` já existente **+1** — não é contador estático, sobrevive entre sessões.
- A montagem **não é salva** automaticamente.
- As coordenadas da análise são locais da cavidade e passam pelo transform da ocorrência, **translação e rotação**; sem a rotação o bloco sai atravessado. Para cavidade inclinada, prefere-se a pose completa (`GetMatrix`).

| Parâmetro | Valor | Configurável |
|---|---|---|
| Altura do holder | 15,0 mm | `HolderHeightMm` |
| Folga do fundo do holder sobre o zero-máquina | 1,0 mm | `HolderBaseClearanceMm` |
| Folga de segmentação por proximidade | 1,0 mm | `DetailGapMm` |
| Margem do blank por lado | **0,0 mm** — o blank não leva sobremetal | `ElectrodeParams.cs:73` |

### Criar eletrodo (manual) — id 10
Mesmo pipeline, mas o candidato vem do bounding box das **faces selecionadas à mão**. Tenta identificar o Ra pela cor e grava na peça.

O Ra é gravado como **`Variable.Formula` (string), nunca `Variable.Value`** — `Value` é escalado pela unidade do documento. Nome da variável: **`AutoEDM_Ra`**.

### Duplicar eletrodo — id 12
A partir de um eletrodo com GAP aplicado, gera a cópia de **desbaste** no próximo Ra da escada (mais grosso) e a insere em **todas** as posições onde o original aparece — molde multicavidade.

- Nome: `{base}_Ra{X.X}.par`; se existir, `_2`, `_3`…
- No topo da escada não há mais grosso: devolve o mesmo.
- Casamento do Ra gravado com a tabela: tolerância **0,05 µm** (o número vai e volta de string).

### Criar Base — id 5 (peça, **síncrona**)
Dimensiona e modela o bloco, a faixa de medição e a fixação, sobre as superfícies de queima já copiadas na peça. Janela com resumo ao vivo e Preview destrutível.

| Regra | Valor | Onde |
|---|---|---|
| **Sobremetal do bloco sobre a pegada** | **+0,5 mm por lado** | `SurfaceBlockBuilder.cs:45` |
| Espaço topo-da-superfície → base-do-bloco | **0,0 mm** (vão solto atrapalha a união) | `SurfaceBlockBuilder.cs:33` |
| Altura do bloco | 15,0 mm | `:36` |
| Faixa de medição | altura **5,0 mm**, margem **0,5 mm/lado**, chanfro de orientação **3,0 mm a 45°** no canto **X+ Y−** | `:59`, `:62`, `:68` |
| Em blank redondo | o mesmo valor vira a profundidade do **flat**, voltado para **Y−** | `:64-67` |
| Comprimento máximo de barra | 500 mm | `:25` |

**Fixação — furos ou eixo** (`BlankModeler.FixationHolesFit`), com borda mínima de 2,0 mm:
- `span = 15 + 4 + 4 = 23 mm` (eixo dos dois pinos Ø4)
- `cross = 4 + 4 = 8 mm`
- `diag = 15/√2 + 4 + 4 = 18,61 mm` (arranjo a 45°)
- Cabem furos se `(maior ≥ 23 e menor ≥ 8)` **ou** `menor ≥ 18,61`. Senão, **eixo** no topo.

**Padrão de fixação** (`FixationPattern.cs`): rosca central **M6**, broca-guia **Ø5,0**, profundidade do furo **10,0 mm** até o ombro, profundidade da **rosca 6,0 mm** — menor de propósito, o macho não chega ao fundo; ponta da broca 118°; chanfro de entrada 0,5 × 45°; pinos **2 × Ø4,0 × 9,0 mm de profundidade, 15,0 mm entre centros**. Eixo alternativo: **Ø9,6** padrão, **Ø6,1** só quando o 9,6 não cabe, altura 20,0 mm.

**Escolha do blank:** o mais **compacto** primeiro (área `BlockX·BlockY`), desempate pela menor altura imposta. Barra redonda **não deita**. Corte deitado arredonda para mm inteiro. Blank de material especial (CuW80) só entra se a peça pedir aquele material.

**Catálogo de barras de cobre** (conferido contra o estoque real, `IBlankLibrary.cs:285-305`): QUAD 19 / 32 / 50 · RED 6 CuW80, 10, 12 CuW80, 12,7, 16, 16 CuW80, 25,4, 38 · RET 25,4×12,7, 38×16, 50×19, 102×25,4.

### Unir superfícies — id 7 (peça, **síncrona**)
Une a superfície de queima ao sólido do bloco, fechando os vãos laterais com patch de contorno. **Isolado do "Criar Base"** porque o thicken, ao falhar, envenenava o documento e derrubava a fixação.

- Duas extremidades a ≤ **0,01 mm** são o mesmo ponto (encadeamento de arestas abertas).
- **Só vira patch o contorno fechado que tenha pelo menos uma aresta vertical**: o rim de topo e o de fundo também são fechados, mas todo horizontal — fechá-los taparia a queima.
- Aresta é "horizontal" se ΔZ < 0,05 mm.

### Aplicar GAP — id 11 (peça, **ordenada**)
Aplica o offset de faísca **para dentro** nas faces de queima, pinta a cor do Ra e nomeia a feature na árvore como `GAP: {gap} - Ra: {ra}`.

**A tabela que é o coração do add-in** (`IOffsetPolicy.cs:53-59`) — o Ra é o limite **superior** da faixa:

| Ra (µm) | Offset para dentro |
|---|---|
| ≤ 0,8 | **0,05 mm** |
| ≤ 1,6 | **0,10 mm** |
| ≤ 3,2 | **0,20 mm** |
| ≤ 6,3 | **0,30 mm** |
| > 6,3 | usa a maior faixa |

**Mapa cor → Ra** (`RaColorMap.cs:74-81`): azul → 6,3 · vermelho → 3,2 · marrom → 1,6 · verde → 0,8 · amarelo → 0,1. Tolerância de 8 por canal.

O sinal é sempre para **dentro**: o eletrodo é subdimensionado. `OffsetOverrideMm > 0` prevalece sobre a tabela.

**Exige ordenado e nunca troca sozinho** — `Model.FaceOffsets` só existe lá, e trocar o ambiente mataria os proxies das faces já lidas.

### Coordenadas — id 2
Lista os eletrodos selecionados com posição, GAP e Ra gravados, e a **área da secção de queima** em cm². Somente leitura; a janela copia para a área de transferência.

- A secção é medida num plano horizontal **no meio da altura** das faces de GAP, no corpo **já com o offset**.
- Face de queima plana em Z (sem "meio"): corta **0,05 mm acima** do plano.
- Quem é malhado é o **corpo sólido**, fechado por construção — malhar as faces de queima dava contorno aberto e a coluna vinha vazia (corrigido em 2026-09-02).

## 4.3 Lista de corte — id 16

Uma linha por **arquivo** de eletrodo: quantas posições ocupa na montagem, o perfil de cobre identificado pelas medidas, a orientação e a medida de corte na serra.

| Regra | Valor |
|---|---|
| **Sobremetal de serra** | **+5,0 mm** |
| **Faceamento no centro de usinagem** | **+1,0 mm**, só no caso **deitado**, em que a altura vem da seção da barra e não passa pela serra — não se faceia material que não existe |
| Medida na serra | `Math.Ceiling(comprimento − 0,01) + 5` — o −0,01 existe porque a caixa do SE chega como `42,000000001` e não pode virar 43 |
| Folga para dizer que a medida da peça **é** a da barra | **0,3 mm** — é o que separa RED 12 de RED 12,7 |
| Barra inteira | 500 mm; corte maior vira aviso |

**A regra de material, dita pelo Carlos:** *o material só não pode ser MENOR que o modelo* — sobra vira cavaco, falta perde o eletrodo.

**Critério de escolha da barra**, nesta ordem: (1) o material que a peça pede (CuW80 pedido = 0, cobre padrão = 1, especial não pedido = 2); (2) **menos material gasto** = área da seção × corte; (3) em pé antes de deitado. Empate real vira a nota "gasta o mesmo que X — confira". Nada serve ⇒ "comprar material (ou escolher o perfil à mão)", e a coluna Perfil passa a mostrar **as medidas X×Y×Z da peça** — quem vai ao almoxarifado precisa delas.

Barra **redonda** comporta a pegada se a caixa é o próprio cilindro (Ø×Ø, ±0,3) ou se a **diagonal** da pegada cabe no Ø. Redondo não deita.

Detalhes que custaram correção: as posições são contadas na **montagem inteira**, não na seleção; a caixa envolvente usa `GetExactRange` primeiro; o aviso é escrito "ATENÇÃO:" e não "⚠" porque o símbolo não existe em toda fonte de impressora; e o clipboard grava os **bytes** UTF-8, não a string — dentro do `Edge.exe` o WinForms grava ANSI e o Excel mostra `Posi綷s`.

A miniatura impressa tem 1 polegada de lado e a câmera vem **de baixo** (Z−), do canto X+ Y− — o do chanfro —, porque os eletrodos têm a figura embaixo.

## 4.4 Lista de modificações — id 25

Varre as peças **do projeto** na montagem, acha em cada uma o grupo `Rev.N` mais recente da árvore ordenada (ou a propriedade de revisão, no caso de peça nova), e gera a folha de revisões no layout da folha MD.

**Como uma peça entra na lista:**
- **Alterada:** tem um **grupo da árvore ordenada** nomeado `Rev.N`. O grupo é reconhecido pelo `SystemName` começando com `"Group_"`, independentemente do nome que o usuário deu. O nome é lido tolerantemente: `Rev.2`, `Rev 2`, `REV-2`, `Revisão 3`, `rev2`. O texto depois do número vira rascunho da descrição.
- **Nova:** tem a **propriedade de arquivo** de revisão preenchida. Vale número sozinho ("2", "02"); **zero, vazio e negativo não contam** — um projeto novo nasce com tudo em 0, e é exatamente isso que separa a peça nova.
- Empate entre as duas fontes fica com o grupo. Vale sempre a revisão **mais recente** do projeto.

> ⚠ **Nunca usar a propriedade padrão "Número da Revisão" da Solid Edge**: é contador de salvamentos. Casar por pedaço do nome trouxe 43 peças como novas e a revisão do relatório virou 153. A comparação é do **nome inteiro** (acento e caixa não importam), contra a lista `RevisionPropertyNames` = `["Revisão", "Revision"]`.

**Preenchimento automático das caixas** — o único que o add-in faz, e é a regra binária do Carlos:
- peça **nova** ⇒ marca `FABRICAR, QUANTIDADE` com as posições na montagem **e** `VERIF. MATERIAL EM ESTOQUE / COMPRAR`;
- peça **alterada** ⇒ marca `RECUPERAR PEÇA NÃO CONFORME`.

O que estiver no `.json` manda sobre isso.

**Ações indicadas:** só a feature que o usuário **numerou** ("1 - Ajustar a chaveta") vira ação e ganha cor e balão na miniatura. Nome automático da SE ("Recorte 6") fica de fora.

**Miniaturas:** duas por peça, isométricas de **cima (Z+)** e de **baixo (Z−)**, 300 px — a alteração pode estar de qualquer lado. As faces das features da revisão saem em **roxo** RGB(198,76,196), o mesmo que ele usa à mão, com balões numerados ligados por linha ao **maior blob conectado** da feature (não ao centro da peça). As imagens flutuam nas colunas C e D, ao lado das caixas de seleção, para não esticar a linha.

**Saídas:**
- `.xlsx` — nome sugerido `MD-{molde} Rev.{N} - lista de modificações.xlsx`.
- `{montagem}_revisoes.json` ao lado da montagem, **por revisão**: a Rev.3 nasce em branco em vez de herdar o texto da Rev.2, e o histórico fica no arquivo. As caixas são guardadas pelo **rótulo**, não pelo índice, porque a lista pode crescer.
- **Nada é alterado no modelo** — o varredor é só leitura e nunca chama `Ungroup()`.

Os códigos do cabeçalho saem do **caminho**: a pasta `MD-14309 [PN-13972] [PA-10925]` é lida **do fim para o começo**, porque projeto dentro de projeto vale o mais próximo.

O `.xlsx` é escrito **à mão** (zip + XMLs) para não carregar ClosedXML ou EPPlus dentro do processo da SE — seria o mesmo conflito de binding que derrubou o `System.Text.Json`. Texto vai como `inlineStr`; imagem flutuante em `oneCellAnchor`; 9525 EMU por pixel. Num intervalo mesclado, **as células cobertas recebem o estilo**, senão a borda fecha só em volta da primeira e o contorno sai pela metade.

## 4.5 Corte a fio (WEDM)

### Curvas das superfícies — id 18 (peça, síncrona)
Cria na peça uma curva derivada sobre cada extremidade paralela ao plano XY — o contorno de fundo (Z mín.) e o de topo (Z máx.), por onde o fio corta. Nascem como **`WEDM Z = XX.XX`**; rodar de novo substitui as anteriores.

- Aresta é horizontal se ΔZ ≤ **0,001 mm**; arestas a ≤ **0,005 mm** são o mesmo nível.
- **Só o Z mínimo e o máximo viram curva**; degrau horizontal no meio da parede é descartado e contado.
- Contorno **aberto é normal** em corte reto; só vira ATENÇÃO quando as pontas ficam a menos de **1,0 mm** uma da outra — aí é encadeamento quebrado, não abertura de projeto.
- Se o rename falhar, a próxima rodada **não** substituiria e o perfil sairia duplicado no `.igs`. A janela avisa.

### Exportar perfis (IGES) — id 17 (peça, síncrona)
Lê as curvas de construção **visíveis** (o sólido é ignorado), agrupa por altura e grava **um `.igs` por nível**, em mm e nas coordenadas da própria peça.

- Entidades IGES 5.3: **110** reta, **100** arco/círculo anti-horário visto de +Z, **126** B-spline com polos e nós **originais**.
- Unidade MM, escala 1.0, **sem matriz de transformação** — cada ponto exatamente onde está no modelo. Resolução mínima 0,0001 mm, só ASCII.
- Rótulo Z com 2 casas, arredondando para longe do zero, nunca `-0.00`. Dois níveis que arredondam para o mesmo nome são **fundidos num arquivo só**.
- Curva sem leitura exata sai como polilinha de 0,001 mm.
- Exige a peça **salva**: os `.igs` vão na pasta dela, com o nome dela.

Cadeia validada ponta a ponta até o Pitágoras em 2026-09-16.

## 4.6 Alojamento de anel de vedação — id 14 (peça, **ordenada**)

Captura uma face (cilíndrica = eixo ou furo; plana = vedação de face) e arestas circulares, mede o diâmetro, escolhe o anel no catálogo, dimensiona o canal e corta como retângulo revolvido 360°. É a **única janela modeless** do add-in, porque a seleção de aresta precisa que o usuário clique na SE com a janela aberta.

**Tabela primeiro, cálculo depois:** a cota vem da tabela Parker quando a seção está tabelada e o movimento não é rotativo; a origem (tabela ou cálculo) sai escrita no relatório. Catálogo: 349 medidas do Parker 001-5 BR / SAE AS 568-A, em `%LOCALAPPDATA%\AutoEDM\oring-catalog.txt`.

| Regra | Estática | Recíproca | Rotativa |
|---|---|---|---|
| Esmagamento alvo (fração de d2) | **0,20** (0,15–0,30) | **0,15** (0,10–0,20) | **0,05** (0,02–0,08) |
| Preenchimento alvo | **0,75** | **0,72** | **0,65** |
| Estiramento máximo (canal de eixo), NBR | 0,05 | 0,05 | **0,00** |

- **FKM subtrai 0,03** do preenchimento (dilata mais) e baixa o estiramento máximo para 0,03.
- **Rotativa tem estiramento ZERO** por causa do efeito Gow-Joule: borracha esticada contrai ao esquentar e aperta o eixo até queimar. O alvo em rotativa é **−0,02**.
- Preenchimento máximo admissível: **0,90** — acima disso é ERRO, o próprio anel arrebenta o alojamento.
- Trava de largura: entre **1,10·d2** e **1,70·d2**. Raio do fundo `0,10·d2` limitado a [0,20; 0,50] mm; quebra de canto na entrada `0,05·d2` limitada a [0,10; 0,30] mm.
- **Afinamento por estiramento:** `d2_efetivo = d2·(1 − 0,5·estiramento)`, e é o d2 efetivo que entra no esmagamento.
- **Parede mínima num canal de face: 1,0 mm**, medida até a **borda** do canal, não até o centro.
- O perfil de corte ultrapassa a superfície em **0,05 mm**: sem essa sobra o corte fica tangente e o modelador pode não abrir o canal.
- **Canal de face apoia o anel do lado da pressão** (combo *Pressão*, só habilitado em canal de face): pressão **interna** encosta o Ø externo do anel na parede externa do canal (Ø ext. do canal = Ø ext. do anel − 1 %); pressão **externa**/vácuo encosta o d1 na parede interna (Ø int. do canal = d1 + 1 %). A folga fica toda do lado de onde vem a pressão. Canal de eixo/furo não muda: o anel já se apoia no próprio diâmetro.
- **A feature recebe o nome do anel**: `O'ring <código> - d2 <seção> x d1 <Ø interno> - <n>`. O `n` é o maior em uso **para o mesmo anel** + 1, lido da árvore na hora (apagar o `- 1` não faz o próximo herdar o nome).
- **As faces do canal saem pintadas de laranja** (vedação): usa o estilo de face `Orange` da peça (ou `Laranja`), sem alterá-lo; se a peça não tiver nenhum, cria `AutoEDM_Vedacao` em RGB 255,128,0. Nome e cor são cosméticos — se falharem, o canal fica e o log diz por quê.
- A janela abre encostada no **lado direito** da tela onde está o mouse, centrada na altura.
- **Vínculo com a peça:** o canal é um **recorte extrudado de uma coroa circular**, com o esboço preso a uma face plana que a aresta toca e os dois círculos **concêntricos à aresta** (com cota de Ø), para acompanhar a peça quando ela é editada (furo movido ou Ø alterado). Se a aresta não tocar face plana perpendicular ao eixo, cai no corte revolvido com o esboço num plano **normal à aresta clicada** (`RefPlanes.AddNormalToCurve`), que acompanha o furo movido mas não a mudança de Ø. Ser ordenado não basta: o esboço antigo (plano base + linhas em coordenada absoluta) não tinha referência nenhuma à peça; sem plano amarrado, o log avisa "NÃO vai acompanhar". Validado ao vivo em 2026-09-21: o revolvido acompanha o furo movido e **falha** na mudança de Ø; o **extrudado** (coroa concêntrica, perfil aninhado `End(1|8|8192)`, extrusão simétrica — a distância é o total) cria o canal de face e o de eixo com a cota medida certa.

**Filosofia: avisar, não recusar** — quem decide cortar é o operador. Candidato reprovado vai para o fim da fila, mas continua na lista.

## 4.7 Engenharia reversa de malha — id 24

Sonda **só-leitura** sobre malha importada: quantos corpos e faces de malha existem, quantos triângulos cada rota entrega, a caixa envolvente, e **quais membros da API existem nesta versão da SE** — conferidos por introspecção antes de chamar. O teste de seccionamento (o único que escreve) é opcional e perguntado.

**Por que existe:** o dump da typelib provou que a SE **não expõe por COM** nenhum ajuste de plano ou cilindro sobre região de malha, nem segmentação, nem seleção de região. Ela **lê** malha e **reconstrói** corpo a partir dela, mas não a **entende** por você. Logo o ajuste tem de ser nosso.

**A ordem do reconhecimento não é estilo, é correção** (`SurfaceRecognizer.cs:85-101`): solda de vértices → quebra por **quina** (ângulo diedral) → ajuste do componente **inteiro**. Tentar planos primeiro faz cada faceta de um cilindro tesselado virar um "plano" perfeito — um Ø20 sairia como 64 plaquinhas.

- Tolerâncias: ângulo de plano **2,0°**, distância ao plano **0,05 mm**, ângulo de quina **35,0°**, mínimo **2 triângulos** por região, cilindro com tolerância relativa `raio × 0,02` e piso de 0,02 mm.
- Eixo do cilindro = autovetor de menor autovalor de `Σ área·n·nᵀ` — numa superfície cilíndrica toda normal é perpendicular ao eixo.
- Componente misto (filete tangente, que não gera quina) é **descascado**: crescem-se os planos de dentro e ajusta-se o resto.
- **Toda superfície sai com o RMS do próprio ajuste** — sem esse número o reconhecimento seria opinião.
- A **área classificada como livre** é o número que decide se a rota por primitivas basta ou se um kernel free-form é inevitável.

## 4.8 Usinabilidade (dentro do "Analisar (Z)")

Responde, só por leitura do B-Rep, o que a ferramentaria **não** consegue produzir e por isso exige erosão.

**Escada de fresas real da casa** (`ToolLadder.Shop()`) — topo reto Ø1×3, Ø1×10 longneck, Ø1,5×4, Ø2×6, Ø2,5×6, Ø3×8, Ø4×11, Ø5×12, Ø6×15; esférica Ø1×4, Ø1×10 longneck, Ø1,5×4, Ø2×6, Ø2×20 longneck, Ø3×6, Ø4×8, Ø5×10, Ø6×12. Daí saem o **raio mínimo de 0,5 mm** e os tetos de alcance de 10 e 20 mm.

**A calibração que decide tudo:** partir de "Ø1×3" marcaria como EDM uma pilha de bolsões que hoje se fresa. O limite a R0,5 é **10 mm**, não 3.

- Um ponto é usinável se **qualquer** fresa da escada o alcança — o comprimento útil é o daquela fresa, não o da cavidade.
- Vereditos separados de propósito: `BelowMinimumRadius` (geometria pura, nenhuma fresa existe) × `BeyondReach` (às vezes se resolve comprando ferramenta mais longa, não queimando).
- **O falso positivo corrigido:** um Ø8 × 25 mm saiu como "só EDM" porque a fresa mais longa para em 20 mm — sendo furo trivial, broca DIN 338 Ø8 tem 75 mm de canal. Daí o jogo de brocas Ø1–Ø19 no cálculo, consultado pelo diâmetro tabelado **imediatamente abaixo** (erra para o lado conservador). *Cair nesse falso positivo é o que faz o usuário parar de abrir o relatório.*
- **Canto vivo:** não tem face curva para medir — é uma aresta entre dois planos, raio zero. Toda fresa deixa ali o próprio raio. É EDM por definição, e ferramenta mais longa não resolve. Só conta canto **vertical** (a aresta pode fugir de Z em até 20°, o que cobre saída de molde de 1 a 3° com folga).
- **A concavidade é calibrada, não chutada:** o sinal do produto vetorial depende de uma convenção da SE que não está no dump, então as arestas verticais na borda da caixa envolvente — convexas por construção — servem de referência. Se elas não concordarem entre si, a análise **se recusa a classificar** e diz isso no log.

## 4.9 Diagnóstico

| Comando | O que faz |
|---|---|
| **Inspecionar seleção** (6) | SPY sobre o objeto COM selecionado; cada clique **engorda** o `SE_API_dump_<versão>.txt` |
| **Gravador** (8 + 9) | snapshot das coleções → o usuário faz a operação à mão na SE → diff → dump `[REC]`/`[SPY]`. É como se descobriu a receita de costura das superfícies |
| **Sonda inter-part** (15) | mapeia as rotas de cópia inter-part em peças descartáveis; não toca na cavidade |
| **Sonda de rosca M6** (13) | peça descartável 120×40×15 com 4 furos M6, uma receita de API por furo. Ø real esperado **5,0 mm** (broca de M6), não 6,0. Detecta e oferece ligar a opção global "exibir roscas" — com ela desligada, um furo roscado correto desenha liso |

## 4.10 Configuração e logs

`%LOCALAPPDATA%\AutoEDM\config.json`. Se não existir, o add-in devolve os defaults **e grava o arquivo**, para o usuário achar e editar em vez de descobrir o formato sozinho. **Nunca lança**: erro de leitura vira aviso no log e segue com o default. Lido uma vez por sessão.

| Chave | Default | Efeito |
|---|---|---|
| `ElectrodeNamePrefix` | `"ELD"` | prefixo de fallback (hoje o nome real vem da montagem) |
| `Material` | `"Cobre"` | filtra o catálogo de blanks |
| `ColorTolerance` | `8` | tolerância por canal RGB |
| `DetailGapMm` | `1.0` | folga da segmentação por proximidade |
| `HolderHeightMm` | `15.0` | altura do holder |
| `HolderBaseClearanceMm` | `1.0` | folga do fundo do holder sobre o zero-máquina |
| `RevisionPropertyNames` | `["Revisão","Revision"]` | nomes **inteiros** da propriedade de revisão de peça nova. Lista vazia = modo diagnóstico |
| `RaOffsetBands` | `null` | null ⇒ tabela de fábrica de Ra → offset |
| `RaColorEntries` | `null` | null ⇒ paleta de fábrica de cor → Ra |

**Log:** um arquivo por sessão, `AutoEDM_{yyyyMMdd_HHmmss}.log`, append, UTF-8 sem BOM, com flush automático — se a SE morrer, o log sobrevive. Linha: `yyyy-MM-dd HH:mm:ss.fff [Nível] mensagem`. **A primeira linha traz a versão e o caminho dos binários carregados** — é o que responde "você está rodando o build que pensa que está?". O caminho do log aparece em toda mensagem de erro.

## 4.11 Testes

**369 casos** (261 métodos: 230 `[Fact]` + 31 `[Theory]`), em 27 arquivos, rodando **sem Solid Edge**:

```
dotnet test tests/AutoEDM.Core.Tests/AutoEDM.Core.Tests.csproj
```

Distribuição: `ORingGroove` 30 · `PrimitiveModel` 21 · `McpBridge` 16 · `SawCutPlanner` 16 · `ToolLadder` 14 · `SurfaceRecognizer` 13 · `XlsxWriter` 13 · `OpenEdgeLoops` 12 · `WireCurve` 12 · `ElectrodeThumbnail` 11 · `NewPartRevision` 10 · `ChangeReport` 9 · `OccurrenceTransform` 9 · `DrillSet` 8 · `RevisionName` 8 · `ChangeReportStore` 7 · `SawCutReportFormatter` 7 · `SectionAreaCalculator` 7 · `AutoEdmConfig` 6 · `ModelingEnvironment` 6 · `IgesWriter` 5 · `StandardBlankLibrary` 5 · `SurfaceRims` 4 · `WedmLevels` 4 · `RaColorMap` 3 · `Units` 3 · `RaOffsetTablePolicy` 2.

**Lacunas conhecidas de cobertura:** não há teste dedicado para `ElectrodeZAnalyzer` (a segmentação por nível de Z — as regras de 1,0 mm e 50 mm), nem para `ElectrodeSpecSheet`, `BurnReportFormatter` e `FixationPattern`.

---

# Parte 5 — Operação

## 5.1 Build e instalação

```powershell
dotnet build AutoEDM.sln -c Release -p:Platform=x64      # x64 é obrigatório
pwsh tools\pack.ps1 -Version 2026.9.18                   # gera dist\AutoEDM-<versão>.zip
```

O ZIP traz `instalar.cmd`, que roda o `AutoEDM.Register`: ele copia tudo para `%LOCALAPPDATA%\AutoEDM\addin` e registra em **HKCU** (sem admin) — `InprocServer32` com `mscoree.dll` e `CodeBase` apontando para a pasta de deploy, `AutoConnect=1`, as categorias de add-in e de ambiente, e o ProgId.

Se `AutoEDM.AddIn.dll` estiver em uso, o registrador **falha com exit 1** em vez de registrar a versão velha — decisão explícita, porque "instalou com sucesso" e rodar o código antigo é pior que não instalar.

> `pack.ps1` **não** empacota o servidor MCP. Quem usa a ponte compila o `AutoEDM.Mcp` em Release x64 e aponta o `.mcp.json` para ele.

## 5.2 O ciclo de desenvolvimento com a SE

```powershell
pwsh tools\deploy-dev.ps1                                 # SE fechada
pwsh tools\deploy-dev.ps1 -Configuration Release -IncludeMcp   # + Claude Code fechado
```

Quatro coisas que custaram rodada de teste:

1. **`dotnet build` sozinho não muda nada do que a SE executa.** Ela carrega do CodeBase registrado, `%LOCALAPPDATA%\AutoEDM\addin`. Confira a linha "Build carregado" do log.
2. **O executável da Solid Edge chama-se `Edge.exe`**, não `msedge.exe`. Com ele aberto, os DLLs estão travados.
3. **Mudou a ribbon? Incremente `AddInEx.GuiVersion`** (`ElectrodeAddIn.cs:44`). Sem isso a SE serve o layout em cache e o botão novo aparece no grupo errado — ou não aparece.
4. **Mudou o catálogo MCP? Use `-IncludeMcp`.** O agente lê a lista de ferramentas do **servidor**, e os dois processos carregam cópias diferentes do `Core`.

**`AssemblyRedirect`** (`AddIn/AssemblyRedirect.cs`) é a primeiríssima linha do `OnConnection`, antes do `base`. Hospedado in-process pelo `Edge.exe`, o `.config` que vale é o **da Solid Edge**, e o `bindingRedirect` que o MSBuild geraria num `.exe` próprio não existe: o `System.Text.Json` pedia `System.Runtime.CompilerServices.Unsafe 4.0.4.1` enquanto o NuGet implantava 6.0.0.0, o construtor estático morria, e **o `config.json` nunca era lido nem gravado — o AutoEDM rodava nos defaults em silêncio**. O gancho resolve por nome simples e nunca lança.

## 5.3 Subir a ponte MCP

1. Solid Edge aberta com o add-in ativo.
2. Aba AutoEDM ▸ grupo MCP ▸ **"Ligar ponte"**.
3. Opcional: **"Liberar escrita"** (confirma com Sim/Não). Vale só até a ponte cair ou a SE fechar.
4. O Claude Code, na raiz do repositório, sobe o `AutoEDM.Mcp.exe` pelo `.mcp.json`.
5. Conferência: **"Status da ponte"**.

## 5.4 Quando algo der errado

| Sintoma | Primeira coisa a olhar |
|---|---|
| O botão não faz o que eu mudei | a linha "Build carregado" na 1ª linha do log — provavelmente falta deploy |
| O botão está cinza | o ambiente: a peça é síncrona e o comando pede ordenado (ou o contrário) |
| O botão apareceu no grupo errado | `GuiVersion` não foi incrementado |
| A ferramenta MCP nova não aparece | faltou `-IncludeMcp` |
| A ponte não responde | ela não sobe sozinha: o botão "Ligar ponte" |
| Uma escrita MCP foi recusada | a ponte nasce em somente-leitura, por desenho |
| Qualquer erro numa janela | o caminho do log vem junto na mensagem; `se_log` traz as últimas linhas sem sair do agente |

---

# Apêndice A — Estado da documentação (2026-09-21)

Esta revisão conferiu os documentos existentes contra o código. O que segue é o resultado, para ninguém tratar como corrente um documento que é histórico.

## A.1 Documentos correntes

| Documento | Papel |
|---|---|
| `README.md` | vitrine e manual do usuário final: instalação, catálogo dos comandos em linguagem de ferramentaria |
| `PROJECT_STATE.md` | estado por ferramenta, decisões travadas, histórico datado |
| `docs/MANUAL.md` | **este arquivo** — o funcionamento técnico |
| `docs/GUIA_SOLID_EDGE_COM.md` | a "pedra de Roseta": 12 receitas COM validadas, reaproveitáveis fora deste projeto |
| `docs/MEMORIA_SOLID_EDGE_COM.md` | mapa de capacidades com evidência (log:linha) por item |
| `docs/INTER-PART.md` | post-mortem do inter-part: 13 tentativas, e o que **nunca** foi executado |
| `docs/api/` | gerado da typelib e por reflexão — não editar à mão |
| `CONTRIBUTING.md` | as regras de equipe |

## A.2 Documentos históricos — ler com data na mão

`docs/PROJECT.md` (roadmap de julho, fala em **um** botão), `docs/MAPEAMENTO_INTEGRACAO_COM.md` (auto-declarado "parcialmente superado"; a tabela de "camada MCP futura" propõe ferramentas que **não existem** — o MCP foi construído com outros 12 nomes e outra arquitetura), `docs/PLANO_TESTE_SE.md` (02/07/2026; cita o modo `copy-test`, que não existe mais), `docs/recomendacoes_arquitetura.md` (estágio de stubs; recomenda `AddCopiedPart`, que o `PROJECT.md` chama de causa nº 1 de erro), `docs/REVISAO-AutoEDM.md` (revisão externa de 23/07; várias recomendações já implementadas) e `docs/AutoEDM_Logs_Consolidated_Analysis.md` (arqueologia dos runs 001–047).

## A.3 Divergências conhecidas, não corrigidas nesta passagem

1. **`docs/COM_INTEGRATION.md:212`** afirma que `Occurrence.Activate = true` entra em edição in-place. **É falso** — refutado em `MEMORIA_SOLID_EDGE_COM.md:157-190` e no código (`EditInPlaceScope.cs:6-17`). O mesmo documento diz que "add-ins não fazem parte do MVP", quando o add-in é hoje o artefato principal.
2. **Dez referências** a `src/AutoEDM/bin/.../SE_API_dump_223.00.13.05.txt` como "fonte da verdade" apontam para um arquivo que o `.gitignore` barra: **quem clona o repositório não o tem**. O caminho atual é `%LOCALAPPDATA%\AutoEDM\logs\SE_API_dump_<versão>.txt`, e ele se gera clicando em "Inspecionar seleção".
3. **`MEMORIA_SOLID_EDGE_COM.md:127`** ainda marca o inter-part como bloqueado sem apontar para o `INTER-PART.md`, três meses mais novo, que mostra rotas inteiras nunca executadas.
4. **`docs/MAPEAMENTO_INTEGRACAO_COM.md:35-43`** ainda chama de "bloqueio técnico atual" o `DISP_E_TYPEMISMATCH` em `Face.GetRange` — resolvido há muitas sessões — e diz que o `InterPartCopier` lança `NotImplementedException`, o que não é verdade desde então.
5. **`docs/REVISAO-AutoEDM.md`** cita tamanhos de arquivo e contagens de julho (`ElectrodeRibbon.cs` com 483 linhas, hoje 1.210; doze comandos, hoje 25). Vale como backlog técnico, não como retrato.

## A.4 Corrigido nesta passagem

| Arquivo | O que mudou |
|---|---|
| `PROJECT_STATE.md` | contagem de testes na tabela de estado (272 → **369**); a descrição do `SeToolRunner` deixou de citar um número de ferramentas, que envelhecia a cada ferramenta nova; a nota do `pack.ps1` perdeu a condição que já tinha caducado |
| `README.md` | entrou o comando **"Sonda inter-part"**, que existia na ribbon e não estava documentado; a Sonda de rosca dizia "cinco furos M6" e são **quatro**; entrou a chave **`RevisionPropertyNames`**, a única de configuração que faltava — e a mais fácil de errar; a lista de documentação ganhou ordem de leitura e a ressalva sobre o in-place |
| `docs/INDEX.md` | passou a listar o manual, o `PROJECT_STATE.md` e o `CONTRIBUTING.md`, e a linha da skill agora nomeia os sete arquivos de apoio em vez de tratá-la como arquivo único |

**Deliberadamente não alterado:** as entradas datadas do `PROJECT_STATE.md` (por exemplo `GuiVersion = 15` na seção de 2026-09-17, ou "272 testes" no histórico). Eram verdade naquela data; reescrevê-las falsificaria o registro. O valor corrente está na tabela de estado e neste manual.
