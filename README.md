# AutoEDM — API de Automação para Eletrodos no Solid Edge

[![Solid Edge](https://img.shields.io/badge/Solid%20Edge-2023%2F2026-blue)](https://plm.sw.siemens.com/en-US/solid-edge/)
[![.NET](https://img.shields.io/badge/.NET-Framework%204.7.2%20%7C%20NET%2010%2B-purple)](https://dotnet.microsoft.com/)
[![COM](https://img.shields.io/badge/COM-Late%20Binding%20%7C%20Early%20Binding-orange)](./docs/COM_INTEGRATION.md)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

> **Uma API funcional e open-source para extrair eletrodos de cavidades de molde no Solid Edge — sem depender do add-on pago *Electrode Design*.**

AutoEDM é uma biblioteca C# (.NET, x64, STA) que expõe, de forma reaproveitável, as operações COM do Solid Edge usadas no fluxo real de EDM: leitura de faces por cor/Ra, segmentação por proximidade, criação de peças em contexto, Inter-Part Copy, offset por rugosidade e geração de relatórios. Além da automação, ela serve como **referência prática de integração COM com o Solid Edge** para desenvolvedores que querem construir seus próprios add-ins.

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
├── src/AutoEDM.Core          # Núcleo reaproveitável (late binding + early binding opcional)
│   ├── Com/                  # Conexão COM, introspecção, OLE filter, helpers
│   ├── Assembly/             # Contexto de montagem, ocorrências, edição in-place
│   ├── Selection/            # Leitura de cor, seleção de faces, geometria, segmentação
│   ├── Electrode/            # Lógica de eletrodos: plano, Ra, offset, Inter-Part Copy
│   ├── Reporting/            # Relatórios de coordenadas (.txt / .csv)
│   └── Experiments/          # Probes de validação de API
├── src/AutoEDM.AddIn         # Add-in COM (ribbon "AutoEDM")
├── src/AutoEDM               # GUI WinForms de debug (conecta via ROT)
└── src/AutoEDM.Register      # Registrador/desregistrador HKCU (sem admin)
```

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

## Como criar um add-in com esta biblioteca

1. **Clone e build:**
   ```powershell
   git clone https://github.com/seu-usuario/AutoEDM.git
   cd AutoEDM
   dotnet build AutoEDM.sln -c Debug
   ```

2. **Registre o add-in (sem admin):**
   ```powershell
   src\AutoEDM.Register\bin\Debug\net472\AutoEDM.Register.exe
   # reabra o Solid Edge → aba "AutoEDM" → "Criar eletrodos"
   ```

3. **Crie um novo comando no add-in:**
   - Adicione um botão na ribbon em `src/AutoEDM.AddIn/`.
   - No handler, chame um método do `AutoEDM.Core` passando o `Application` do SE.
   - Mantenha o handler como **casca fina**: ele lê a UI/seleção e chama o núcleo com argumentos explícitos.

4. **Use o núcleo no seu próprio projeto:**
   Referencie `AutoEDM.Core.dll` e use classes como `SolidEdgeConnector`, `FaceSelector`, `RegionSplitter`, `ElectrodeBuilder` etc.

---

## Status e Roadmap

| Ferramenta | Estado |
|---|---|
| Relatório de coordenadas de queima | ✅ construído |
| Spec-sheet de eletrodos (Ra/pegada/blank/fixação) | ✅ construído |
| Criar eletrodos c/ blank (`CreateElectrodesWithBlank`) | ✅ validado no SE |
| Criar Base (bloco + faixa de medição + fixação sobre a superfície copiada) | ✅ validado no SE |
| Unir superfícies (anexar a queima ao bloco + GAP) | 🚧 união validada no SE; GAP corrigido, aguardando confirmação final |
| Copiar superfícies (Inter-Part Copy) | 🚧 só funciona em edição em contexto (in-place) |
| Orquestrador completo | 📋 planejado |

Legenda: ✅ funcionando · 🚧 em andamento · 📋 planejado.

---

## Documentação

- [`docs/PROJECT.md`](docs/PROJECT.md) — direção interna, verdades do domínio e regras da equipe.
- [`docs/COM_INTEGRATION.md`](docs/COM_INTEGRATION.md) — guia técnico de integração COM com o Solid Edge.
- [`docs/INDEX.md`](docs/INDEX.md) — catálogo de API, métodos e constantes do dump.
- [`docs/api/`](docs/api/) — referências markdown por namespace do Solid Edge.
- [`.claude/skills/solid-edge-com/SKILL.md`](.claude/skills/solid-edge-com/SKILL.md) — skill com os fatos validados de COM do SE.

---

## Build

```powershell
dotnet build AutoEDM.sln -c Debug
```

Para rodar é preciso o **Solid Edge 2023/2026** aberto com uma montagem ativa. Sem o SE, o projeto compila, mas as operações COM só funcionam com a licença.

---

## Contribuição

Contribuições são bem-vindas! Leia [`docs/PROJECT.md`](docs/PROJECT.md) para entender as regras do projeto e [`CONTRIBUTING.md`](CONTRIBUTING.md) para o fluxo de colaboração.

---

## Licença

Este projeto é licenciado sob a [MIT License](LICENSE).

---

**AutoEDM** — feito para modernizar o fluxo de eletrodos no Solid Edge e devolver à comunidade uma API de integração COM bem documentada e testada na prática.
