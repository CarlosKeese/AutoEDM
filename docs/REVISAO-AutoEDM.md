# Revisão técnica — AutoEDM

*Revisão de arquitetura e código. 23/07/2026.*
*Escopo do que li: `README.md`, `docs/INDEX.md`, `docs/COM_INTEGRATION.md`, a skill `solid-edge-com`, e os fontes `Com/SolidEdgeConnector.cs` (280 linhas), `Selection/FaceSelector.cs` (348), `Electrode/ElectrodeBuilder.cs` (1.662). Não li o add-in, a GUI, o registrador nem o restante do Core — então trate isto como uma primeira passada, não como auditoria completa.*

---

## Veredito

O projeto está **estruturalmente correto e tecnicamente honesto**. A separação em quatro projetos (Core reaproveitável / add-in / GUI de debug / registrador) é a decisão certa e é o que a maioria dos projetos de automação CAD não faz — normalmente vira um add-in monolítico onde a lógica está grudada nos handlers de botão. Você acertou o osso.

Os problemas que encontrei são de **maturidade**, não de concepção: gestão de recursos COM, um arquivo que cresceu demais, e ausência de costura para teste. Nenhum deles exige refazer nada. Todos são incrementais.

---

## O que está certo (preserve)

**A regra de ouro.** "Nenhuma assinatura é inventada — tudo vem do dump ou de introspecção ao vivo." Essa é a diferença entre um projeto que funciona e um que funciona *até a próxima versão do Solid Edge*. Mantenha-a com rigor religioso.

**Late binding com `dynamic` para compilar sem as typelibs.** Decisão acertada e não óbvia. Significa que qualquer pessoa consegue clonar e compilar sem ter o Solid Edge instalado — o que é justamente o que permite que o projeto seja uma referência para a comunidade, e não só um binário seu.

**Registro em `HKCU`, sem admin.** Detalhe pequeno com impacto grande na adoção. Add-in que exige administrador não é instalado em empresa nenhuma.

**Os `try { } catch { }` em torno de leitura de propriedades COM.** Contraintuitivamente, aqui isso está *certo*: em introspecção COM, membros existem ou não conforme a versão, e sondar é legítimo. Só precisa de disciplina — ver P2.2.

**A documentação.** `COM_INTEGRATION.md` com os padrões de chamada (ParameterModifier para `[out]`, SAFEARRAY de `IDispatch`, filtro OLE para retry) vale mais para a comunidade que o próprio código de eletrodos. É o ativo mais reaproveitável do repositório.

---

## P1 — Resolver antes de crescer mais

### P1.1 — Ciclo de vida dos objetos COM

**Evidência:** `Marshal.ReleaseComObject` aparece **1 vez** em `SolidEdgeConnector.cs`, e **zero vezes** em `FaceSelector.cs` (32 usos de `dynamic`) e `ElectrodeBuilder.cs` (75 usos de `dynamic`).

**Por que importa:** cada acesso via `dynamic` a um objeto do Solid Edge cria um RCW (*Runtime Callable Wrapper*) que segura uma referência do lado do CAD. Num laço que percorre faces de uma cavidade — que podem ser milhares — você acumula milhares de referências vivas até o coletor de lixo decidir agir, o que pode demorar. Sintomas típicos, e que aparecem só em sessão longa (justamente a de trabalho real): o processo do Solid Edge não fecha depois que o usuário manda fechar; consumo de memória sobe ao longo do dia; erros esporádicos de RPC / "servidor ocupado" em operações demoradas.

**Recomendação:** não saia espalhando `ReleaseComObject` por toda parte — isso causa o problema oposto (liberar objeto compartilhado e derrubar o add-in). A regra prática que funciona em automação CAD:

- **Libere** o que você enumerou dentro de laços quentes: faces, arestas, ocorrências percorridas, corpos temporários. Em `finally`.
- **Nunca libere** o `Application`, o documento ativo, nem nada que você não obteve — quem obtém, libera.
- Considere um helper descartável para não poluir o código:

```csharp
// uso: using (var f = Com.Scoped(face)) { ... }
internal static class Com
{
    public static ScopedCom Scoped(object o) => new ScopedCom(o);
}

internal sealed class ScopedCom : IDisposable
{
    private object _o;
    public ScopedCom(object o) { _o = o; }
    public dynamic Value => _o;
    public void Dispose()
    {
        if (_o != null && Marshal.IsComObject(_o))
        {
            try { Marshal.ReleaseComObject(_o); } catch { }
            _o = null;
        }
    }
}
```

Comece pelos laços de `FaceSelector` — é onde o volume está. Meça antes e depois: abra uma montagem grande, rode a seleção dez vezes seguidas e observe a memória do processo do Solid Edge.

### P1.2 — Fronteira de unidades: metros no COM, milímetros no domínio

**Evidência:** a API do Solid Edge trabalha em **metros e radianos** (`Face.GetRange`, `Occurrence.PutOrigin`, `GetTransform`), enquanto o seu domínio é em **milímetros** (`ApplyOffset(..., double inwardOffsetMm)`, `CheckMinimumRadii(..., double minRadiusMm)`, tabela de Ra, spark gap).

**Por que importa:** este é o tipo de erro que não estoura em exceção — ele produz um resultado **plausível e errado**. Um fator 1000 aplicado a um offset de eletrodo não gera stack trace; gera aço usinado errado. É a categoria de falha mais cara que existe num projeto como este, e a mais difícil de pegar em revisão de código, porque `double` é `double`.

**Recomendação:** um único ponto de conversão, e tipos que não deixam misturar. O mínimo viável:

```csharp
public readonly struct Mm
{
    public readonly double Value;
    public Mm(double v) { Value = v; }
    public double ToMeters() => Value / 1000.0;
    public static Mm FromMeters(double m) => new Mm(m * 1000.0);
    public override string ToString() => $"{Value:0.###} mm";
}
```

Se achar que envolver tudo é invasivo agora, faça pelo menos o seguinte hoje: **sufixe toda variável e todo parâmetro com a unidade** (`offsetMm`, `rangeM`, `zMinM`) sem exceção, e concentre as conversões em uma classe `Units`. Você já faz isso em alguns parâmetros — falta ser sistemático. Custa uma tarde e elimina uma classe inteira de erro silencioso.

---

## P2 — Resolver quando puder

### P2.1 — `ElectrodeBuilder` virou classe-deus

**Evidência:** 1.662 linhas, ~40 métodos, 63 blocos `catch`, 86 KB num arquivo.

Olhando os nomes dos seus próprios métodos, as costuras já estão desenhadas — só falta cortar:

| Nova classe | Métodos que vão para lá |
|---|---|
| `ElectrodePlanner` | `PlanFromAssembly`, `PlanFromAssemblyDocument`, `BuildRegionPlan`, `MakePass`, `AnalyzeElectrodesByZ*` |
| `InContextPartFactory` | `CreateInContextPart`, `CreateAndPlaceElectrode`, `ResolvePartTemplate`, `EnableInterPartCopy` |
| `ElectrodeGeometryOps` | `ApplyOffset`, `CreateBlankAndHolder`, `CheckMinimumRadii`, `TryGetBurnBoundingBox` |
| `ElectrodeNaming` | `ResolveElectrodeFolder`, `SafeDocName`, `SafeName`, `NextElectrodeIndex`, `SafeDoc` |
| `ElectrodeDiagnostics` | `DiagnoseNoBurn`, `DumpFaceColorSources`, `DumpFeatureInfo`, `Fmt` |

Comece por **`ElectrodeDiagnostics`**: são ~150 linhas puramente de depuração, sem acoplamento com o fluxo principal, e o corte é seguro. É o refactor de menor risco e maior alívio imediato. Depois `ElectrodeNaming`, que é quase todo estático e puro.

O `ElectrodeBuilder` que sobrar vira o que ele deveria ser: um **orquestrador** que chama os outros — provavelmente umas 300 linhas.

### P2.2 — `catch { }` silencioso e inconsistente

**Evidência:** o padrão está misturado. Em alguns pontos você registra (`catch (Exception ex) { Log.Warn(...) }`), em outros engole em silêncio (`try { docName = (string)fdoc.Name; } catch { }` nas linhas 592-593; `catch { }` na 782).

Para sondagem COM, engolir é defensável. O problema é a **inconsistência**: quando algo falha em produção, você não sabe se não logou porque era esperado ou porque o `catch` mudo comeu a informação.

**Recomendação:** duas regras simples.
1. Sondagem de membro que pode legitimamente não existir → `catch (COMException) { }`, **tipado**, com um `Log.Debug` de uma linha. Nunca `catch` pelado.
2. Qualquer outra coisa → `catch (Exception ex)` com `Log.Warn`, como você já faz.

Um `catch` sem tipo também engole `OutOfMemoryException`, `StackOverflow` (parcialmente) e erros de programação seus. Narrar o tipo é barato.

---

## P3 — Dívida que vale registrar

### P3.1 — Nada é testável sem o Solid Edge instalado

O `dynamic` em todo lugar é o preço aceito por compilar sem typelibs — tudo bem. Mas isso significa que **lógica pura** (segmentação por proximidade no `RegionSplitter`, cálculo de Ra, montagem do plano, escolha de nomes) só pode ser exercitada com uma instalação licenciada aberta. Isso trava o desenvolvimento em máquina sem SE e impede regressão automatizada.

**Recomendação barata:** não precisa de mock do Solid Edge inteiro. Basta extrair as funções puras para classes que recebem tipos seus (`SelectedFace` com cor, bounding box e chave — não o `Face` do COM). Aí `RegionSplitter` vira testável com dados sintéticos, sem CAD nenhum. Pelo que vi na assinatura de `TryGetBurnBoundingBox(IReadOnlyList<SelectedFace>, ...)`, você já começou a fazer isso — é só levar adiante.

### P3.2 — Placeholder no README

`git clone https://github.com/seu-usuario/AutoEDM.git` — trocar por `CarlosKeese`. Alguém vai copiar e colar.

---

## Sequência que eu seguiria

1. **README** (2 minutos, e é a primeira coisa que qualquer visitante vê).
2. **Sufixos de unidade + classe `Units`** (uma tarde, elimina a classe de bug mais cara).
3. **Extrair `ElectrodeDiagnostics` e `ElectrodeNaming`** (corte seguro, alívio imediato no arquivo grande).
4. **`ScopedCom` nos laços do `FaceSelector`**, medindo memória antes e depois.
5. Só então o resto do fatiamento do `ElectrodeBuilder`.

---

## Uma observação de coautor, não de revisor

O `docs/COM_INTEGRATION.md` e o dump da typelib são, na minha leitura, mais valiosos para o mundo do que o automatizador de eletrodos em si. Existe muito projetista tentando automatizar Solid Edge e apanhando exatamente dos problemas que você já resolveu e documentou — `ParameterModifier` para `[out]`, SAFEARRAY de `IDispatch`, filtro OLE para retry.

Vale considerar promover essa parte: um repositório próprio, ou pelo menos um título e um README que digam claramente que ali dentro existe um **SDK offline não oficial do Solid Edge**. Do jeito que está, isso é um detalhe interno de um projeto de eletrodos, e quem mais precisa dele nunca vai encontrar por busca.

---
---

# Adendo — Revisão do add-in

*Segunda passada, 23/07/2026. Lidos: `ElectrodeAddIn.cs` (108 linhas), `ElectrodeRibbon.cs` (483), `Ribbon.xml` (71), `AutoEDM.AddIn.csproj` (44), `UI/RaGapPickerForm.cs` (70), `UI/BlockOverSurfacesForm.cs` (261).*

## Veredito

**O add-in é a parte mais bem construída do projeto.** Se o `ElectrodeBuilder` mostra um projeto que cresceu rápido demais, o add-in mostra alguém que já apanhou do Solid Edge e aprendeu. Vários detalhes aqui só aparecem em código de quem sangrou; listo abaixo porque merecem ser preservados conscientemente.

## O que está notavelmente bom

**`BuildStamp()`.** Isto é o melhor código do repositório, e não é sobre eletrodos. Você registra no log a data de modificação dos assemblies **em memória** para confirmar qual build o Solid Edge está de fato rodando — porque o SE mantém o add-in carregado in-process e recompilar sem reiniciar continua executando o código antigo. O comentário inclusive data o dia em que isso te enganou. É diagnóstico nascido de dor, e é exatamente o tipo de coisa que economiza um dia inteiro de alguém.

**`AddInEx.GuiVersion = 9`, com o comentário de incrementar ao mudar a ribbon.** Pega-ratão clássico do SE: a faixa de opções fica em cache e não atualiza se a versão não subir. Você não só sabe, como documentou no lugar certo.

**Log em `%LOCALAPPDATA%`.** Você percebeu que, in-process, o diretório de trabalho é a pasta do Solid Edge em Program Files, sem permissão de escrita. Muita gente descobre isso em produção.

**A conferência antes de criar (`CriarEletrodos`).** Este é o melhor design de segurança do projeto: você roda a análise **não destrutiva** primeiro, mostra ao usuário qual queima foi detectada, **avisa quando a maior região colorida está fora do mapa de cores**, pede confirmação explícita e ainda diz que a montagem não será salva sozinha. Uma ferramenta que escreve na montagem de produção de alguém tem obrigação de fazer isso, e quase nenhuma faz. Não mexa nisso.

**O csproj.** `PlatformTarget x64` explícito (obrigatório para add-in in-process no SE 2023/2026), `ComVisible false` no assembly com atributos por tipo — em vez de expor tudo —, e o recurso Win32 dos ícones gerado por script PowerShell para não depender do `rc.exe`. Tudo correto e nada acidental.

**O grupo "Diagnóstico" já separado no `Ribbon.xml`.** Eu ia sugerir isso e fui conferir antes: já está feito. Inspecionar seleção, iniciar leitura e gravar log estão isolados dos comandos de produção.

## Achados

### A1 — Dois padrões concorrentes para a mesma coisa (P2, o principal)

Mapeei os doze comandos:

| Padrão | Comandos |
|---|---|
| `Run(...)` | `AnalisarZ`, `GerarRelatorioCoordenadas`, `GerarSpecSheet` |
| `TryAssembly` + try/catch manual | `CriarEletrodos`, `CriarEletrodoManual`, `CriarBase`, `UnirSuperficies`, `AplicarGap`, `DuplicarEletrodo` |
| Nenhum dos dois | `IniciarLeitura`, `GravarLeitura`, `InspecionarSelecao` |

Você escreveu o `Run()` justamente para eliminar a repetição — e depois seis comandos não o usaram. Provavelmente porque `Run()` assume montagem, e os comandos de peça precisavam de `TryPart()`.

O custo não é a duplicação de oito linhas. É que **o próximo comando que você escrever no padrão manual pode esquecer o `try/catch`, ou os marcadores de início/fim no log** — e aí uma exceção sobe direto para o Solid Edge, que trata isso do jeito dele.

Correção, meia hora:

```csharp
private enum DocKind { Assembly = 3, Part = 1 }

private void Run(string title, DocKind kind,
                 Action<SolidEdgeConnector, dynamic, ElectrodeParams> body)
{
    dynamic app = ElectrodeAddIn.Current?.App;
    if (app == null) { MessageBox.Show("Add-in não inicializado.", "AutoEDM"); return; }

    dynamic doc = app.ActiveDocument;
    if (doc == null || (int)doc.Type != (int)kind)
    {
        MessageBox.Show(kind == DocKind.Assembly
            ? "Abra uma MONTAGEM (.asm) ativa (a cavidade no zero-máquina)."
            : "Abra uma PEÇA (.par) ativa, com as faces de queima já copiadas nela.",
            "AutoEDM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
    }

    try
    {
        Log.Info($"===== {title} (add-in) =====");
        body(SolidEdgeConnector.Attach(app), doc, LoadParams());
        Log.Info($"===== FIM ({title}) =====");
    }
    catch (Exception ex) { Fail(title.ToLowerInvariant(), ex); }
}
```

Os doze comandos passam a ter um caminho só. O `CriarEletrodos`, que tem a lógica de confirmação, continua sendo um `body` maior — mas dentro do mesmo trilho.

### A2 — A mensagem de erro que chega ao usuário é crua (P2)

`Fail()` mostra `ex.GetBaseException().Message` direto no `MessageBox`. Quando a origem é COM, isso costuma virar algo como *"Exceção de HRESULT: 0x80004005"* — que não diz nada a um ferramenteiro, e ainda passa a impressão de que a ferramenta quebrou sem motivo.

Você já tem o caminho do log em `_logSink.FilePath`. Exponha-o e use:

```csharp
MessageBox.Show(
    $"Não foi possível {what}.\n\nDetalhes técnicos no log:\n{ElectrodeAddIn.Current?.LogPath}",
    "AutoEDM — erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
```

Melhor ainda: um botão que abre a pasta do log. Custa três linhas e muda completamente a experiência de quem não é você usando a ferramenta.

### A3 — Não existe configuração do usuário (P2, e vira demanda no dia 1)

Todo handler faz `new ElectrodeParams { ElectrodeName = "ELD" }`. Ou seja: prefixo de nome, tabela de Ra, offsets e mapa de cores estão fixos no código.

Enquanto o usuário é você, tudo bem. No instante em que a segunda pessoa instalar isso, as primeiras três perguntas serão: *como mudo o prefixo?*, *minha tabela de Ra é diferente*, e *aqui a cor de queima é outra*.

Recomendo `%LOCALAPPDATA%\AutoEDM\config.json` carregado num `LoadParams()`, com um botão "Configurações" na ribbon — ou, se preferir manter tudo num só lugar e já que LiteDB é o seu padrão em .NET, um arquivo LiteDB guardando config, tabela de Ra e histórico de eletrodos gerados. Essa é a fronteira entre uma automação pessoal e uma ferramenta que outra pessoa usa, e vale a pena atravessá-la enquanto o código ainda é pequeno.

### A4 — Multi-target no Core para destravar os testes (P3)

O add-in é `net472` com `LangVersion 7.3`, e tem que ser mesmo — o Solid Edge hospeda .NET Framework. Mas se o `AutoEDM.Core` também está preso nisso, você paga o preço duas vezes: sem ergonomia moderna e, principalmente, **sem poder rodar testes num runner atual**, que era o achado P3.1 da primeira passada.

Se o Core não usa nada exclusivo do Framework, experimente:

```xml
<TargetFrameworks>net472;net8.0</TargetFrameworks>
```

O add-in continua consumindo o `net472`; um projeto de testes consome o `net8.0` e exercita a lógica pura (segmentação de regiões, cálculo de Ra, montagem do plano, nomenclatura) sem Solid Edge nenhum na máquina. É o menor caminho entre onde você está e ter regressão automatizada.

### A5 — `ElectrodeAddIn.Current` estático (P3, registrar e seguir)

Singleton mutável global, zerado no `OnDisconnection`. Se algum handler rodar depois da desconexão, dá referência nula. O `TryAssembly` já checa, então o risco real hoje é baixo — mas com o `Run()` unificado do A1, garanta que a checagem continue lá.

### A6 — Convenção de idioma: documentar antes que derive (P3)

Hoje: comandos da ribbon em português (`CriarEletrodos`, `AnalisarZ`), API do núcleo em inglês (`CreateElectrodesWithBlank`, `AnalyzeElectrodesByZ`).

Isso me parece **certo**, não errado: a UI fala com o ferramenteiro brasileiro, o núcleo fala com a comunidade internacional que você quer atingir com o dump da API. Mas convenção não escrita vira bagunça em seis meses. Registre num `CONTRIBUTING.md`: *núcleo e API pública em inglês; camada de UI e mensagens ao usuário em português.*

## Sequência atualizada

Somando as duas passadas, a ordem que eu seguiria:

1. README (o placeholder `seu-usuario`)
2. Sufixos de unidade + classe `Units` — **o risco mais caro do projeto**
3. `Run()` unificado com `DocKind` — meia hora, elimina uma classe de erro futuro
4. Mensagem de erro com caminho do log
5. Extrair `ElectrodeDiagnostics` e `ElectrodeNaming` do `ElectrodeBuilder`
6. `ScopedCom` nos laços do `FaceSelector`, medindo memória
7. Multi-target do Core + primeiro projeto de testes
8. Configuração externa (`config.json` ou LiteDB)
