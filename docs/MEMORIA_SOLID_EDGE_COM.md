# Memória Solid Edge COM — o que funciona, o que não, e como

> **Fonte de verdade COMPARTILHADA (Claude ↔ Kimi).** Este é o primeiro arquivo a
> consultar antes de escrever, corrigir ou analisar qualquer integração COM com o
> Solid Edge. Ele registra o **comportamento real** da API — não a teoria — validado
> run a run no SE 2023 real.
>
> **Escopo:** o que a API COM do SE **deixa** fazer, o que **recusa**, as assinaturas
> reais e as armadilhas de marshaling. Para *setup* (ProgID, registro, `.csproj`, TLBs)
> veja [`COM_INTEGRATION.md`](./COM_INTEGRATION.md); para o **catálogo completo** de
> tipos, veja [`api/`](./api/) e o dump `SE_API_dump_223.00.13.05.txt`.

---

## 0. Como usar e manter esta memória

**Regras (valem para Claude e Kimi):**

1. **Nunca inventar API.** Toda assinatura vem do **dump** da typelib. Se não está no
   dump, ou não existe com aquele nome, ou falta um dump completo — **marque, não conclua**.
2. **Todo item tem STATUS + EVIDÊNCIA.** Sem log/dump/arquivo que sustente, é 🟡 hipótese,
   não fato.
3. **Versão importa.** Tudo aqui é SE 2023 (`223.00.13.05`). Para outra versão, re-dumpar
   e reconferir (enums e assinaturas mudam entre versões).
4. **Atualize após cada log numerado** (`logs/AutoEDM NNN.log`): promova 🟡→✅ quando o SE
   real confirmar, 🟡→❌ quando falhar de forma reprodutível. Cite o run.

**Legenda de status:**

| Tag | Significado |
|---|---|
| ✅ **CONFIRMADO** | Exercitado no SE real e funcionou. Cita o run. |
| ❌ **NÃO FUNCIONA** | Exercitado e falhou de forma reprodutível. Cita HRESULT + run. |
| 🟡 **HIPÓTESE** | Plausível pelo dump/domínio, ainda **não** exercitado ou inconcluso. |
| ⛔ **BLOQUEADO** | Capacidade desejada **sem** caminho COM validado (várias tentativas falharam). |
| 📖 **NO DUMP** | Assinatura existe na typelib, mas ninguém exercitou ainda. |

**Referências de linha:** o dump é
`src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_223.00.13.05.txt`. Cite como `dump:NNNN`.

---

## 1. Fundamentos inegociáveis (erros previsíveis)

Cada um destes já custou pelo menos um run. Todos ✅.

| Regra | Detalhe | Por quê / erro se ignorado |
|---|---|---|
| **Unidades = METROS / RADIANOS** | 20 mm = `0.020`; leia range ×1000 para mm | Geometria 1000× errada, silenciosa |
| **Coleções 1-based** | `.Item(1)` é o primeiro; `.Count` | Índice fora dos limites |
| **x64 obrigatório** | SE 2023/2026 é 64-bit only | Processo x86 não conecta |
| **Thread STA** | UI WinForms já é STA; worker thread não é | COM marshaling quebra fora de STA |
| **Late binding (`dynamic`)** | compila sem a typelib instalada | portabilidade do build |
| **OLE Message Filter** | retry em `RPC_E_CALL_REJECTED` **e** `RPC_E_SERVERCALL_RETRYLATER` | SE ocupado/modal derruba a chamada |
| **Conectar via ROT** | `ComInterop.GetActiveObject` (P/Invoke) | `Marshal.GetActiveObject` não existe no .NET moderno |
| **`[out]`/VARIANT via `InvokeMember`** | `ParameterModifier` by-ref + `CultureInfo.InvariantCulture` | binder dinâmico não popula `[out]` nem coage VARIANT |
| **`cParams=0` num "método"** | é **propriedade-coleção**; a operação é `.Add(...)` | ex.: `CopySurfaces`, `OffsetSurfaces` |

---

## 2. Mapa de capacidades (o coração desta memória)

### 2.1. Conexão e documento ativo

| Capacidade | Status | API real | Evidência |
|---|---|---|---|
| Conectar à instância aberta | ✅ | `ComInterop.GetActiveObject("SolidEdge.Application")` | run 2026-07-02 |
| Ler documento ativo | ✅ | `Application.ActiveDocument`; `.Type` (3 = `.asm`, 1 = part) | log 027:6 |
| Tipo do doc ativo | ✅ | `Application.ActiveDocumentType` | log 027:80 |
| Add-in in-process pega `Application` direto | ✅ | `OnConnection(Application, …)` — sem ROT | validação 2026-07-06 |

### 2.2. Leitura de faces (geometria de origem)

| Capacidade | Status | API real | Evidência |
|---|---|---|---|
| Percorrer faces | ✅ | `PartDocument.Models → Model.Body → Body.Faces[1] → Face`; **queryType 1 = igQueryAll** | fixado em `FaceSelector` |
| **Cor da face** | ✅ | `Face.Style.Diffuse{Red,Green,Blue}` (0..1, ×255); tb `Style.GetDiffuse(out r,g,b)` | logs 3/4; log 027:23-27 |
| **Bounding box da face** | ✅ | `Face.GetRange(MinPt, MaxPt)` / `GetExactRange` — 2 args **`[in,out]` SAFEARRAY(double)** de 3 doubles (metros) | dump:14-15; log 027:28 (124/124) |
| — marshaling do bbox | ✅ | late binding: **`new double[0]` + `ParameterModifier(2)` by-ref + `InvariantCulture`**. `new double[3]` dá `DISP_E_TYPEMISMATCH` | Log 11 |
| Fallback bbox por vértices | 📖 | `Face.Vertices → Vertex.GetPointData(out pt)` (`[out]`, metros) | dump; não precisou (GetRange resolveu) |
| Área | 📖 | `Face.Area -> double` | dump:12 |
| ID/Tag da face e aresta | ✅ | `Face.ID`, `Face.Tag`, `Edge.ID` (conectividade) | Runs 6/7 |
| Reference key da face | 📖 | `Face.GetReferenceKey(out key)` (SAFEARRAY byte) — insumo do TopologyReference | dump; log 027:8 |

### 2.3. Segmentação (detalhes → eletrodos)

| Capacidade | Status | Nota | Evidência |
|---|---|---|---|
| Conectividade por aresta compartilhada | ✅ (mas inadequada) | as 124 faces marrons formam **um** patch conexo → não separa "detalhes" | Runs 6/7 |
| **Segmentação por proximidade espacial** | ✅ | single-linkage por folga entre bboxes (`DetailGapMm`, default 1 mm) | Log 11; log 027:29 |
| 1 cor = vários detalhes | ✅ (domínio) | cada detalhe conexo/próximo = 1 eletrodo | correção do Carlos |

### 2.4. Contexto de montagem (ocorrências)

| Capacidade | Status | API real | Evidência |
|---|---|---|---|
| Listar ocorrências | ✅ | `AssemblyDocument.Occurrences` (1-based); nomes vêm com `:1` | run 2026-07-02 |
| Documento da ocorrência | ✅ | `Occurrence.OccurrenceDocument` (= a **peça**, sem `.Occurrences`) | Log 15 (bug corrigido) |
| **Posição/coordenada de queima** | ✅ | `Occurrence.GetTransform(out x,y,z, out ax,ay,az)` (metros/rad); cavidade na origem = zero-máquina | Log 21; relatório OK |
| Posicionar | ✅ | `Occurrence.PutOrigin(x,y,z)` / `PutTransform` | Log 21 |
| Opção global Inter-Part | ✅ | `Application.SetGlobalParameter(253,true)`+`(254,true)`; leia de volta com `ParameterModifier` no `[out] Value` | Log 24 (`Allow=True, CopyCmd=True`) |

### 2.5. Criar peça / geometria

| Capacidade | Status | API real | Evidência |
|---|---|---|---|
| **Criar peça EM CONTEXTO** | ✅ | `Occurrences.AddByTemplate(novoPartPath, template)` — **arg1 = caminho do NOVO part**, arg2 = template | Log 21/22; log 027:74-77 |
| Template padrão de part | ✅ | `Application.GetDefaultTemplatePath(1)` (`1 = igPartDocument`) | Log 21 |
| — armadilha | ✅ | passar só o template (como arg1) insere o próprio template read-only como ocorrência **standalone** → cópia falha | Log 21 |
| Box (blank) | 🟡 | `Models.AddBoxByTwoPoints(x1,y1,z1,x2,y2,z2,dAngle,dDepth,pPlane,ExtentSide,vbKeyPointExtent,pKeyPointObj,pKeyPointFlags)` — 13 args, `pPlane = RefPlanes.Item(1)`, `ExtentSide` igLeft=1/igRight=2/igSymmetric=3 | Log 8 (assinatura); ainda `DISP_E_TYPEMISMATCH` no draw — **secundário** |
| **Bloco sólido via sketch+extrude** | ✅ | `PartDocument.ProfileSets.Add() → .Profiles.Add(RefPlanes.Item(1)=plano XY) → Lines2d.AddBy2Points(x1,y1,x2,y2)×4 → Profile.End(1) → Models.AddFiniteExtrudedProtrusion(1, Profile[] TIPADO, side, distMetros)`. ProfileArray precisa ser `SolidEdgePart.Profile[]` (SAFEARRAY(IDispatch), como o Face[]); object[] falha. **side = direção: 1=igLeft (−normal/−Z), 2=igRight (+normal/+Z), 3=simétrico** (Log 35). Modelar na peça é STANDALONE (não precisa in-place). Em SÍNCRONO, apagar a sketch depois (`ProfileSet.Delete()`). | **Logs 33–35 ✓** |
| Criar peça + inserir posicionada na montagem | ✅ | `app.Documents.Add("SolidEdge.PartDocument")` → modela/`SaveAs(path)`/`Close()` → `AssemblyDocument.Occurrences.AddByFilename(path)` → `Occurrence.PutOrigin(x,y,z)` (metros). Sem inter-part copy, sem in-place. | **Log 35 ✓** (6 eletrodos) |
| Box via `AddBoxByTwoPoints` | ❌ | `DISP_E_TYPEMISMATCH` recorrente — use sketch+extrude acima | Logs 8–30 |
| Cilindro (holder) | 📖 | `Models.AddCylinderByCenterAndRadius(x,y,z,dRadius,dDepth,pPlane,ExtentSide,vbKeyPointExtent,pKeyPointObj,pKeyPointFlags)` — 10 args | Log 8 |

### 2.6. Cópia de superfícies / Inter-Part (⛔ **o nó atual**)

Ver a saga completa e os caminhos ainda abertos na **§4**.

| Capacidade | Status | Resumo |
|---|---|---|
| Copiar faces de OUTRA peça (associativo) | ⛔ **BLOQUEADO** | `CopySurfaces.Add` aceita `Face[]` tipado mas dá **E_FAIL**; `InterpartConstructions`/`CreateReference` também recusam. Ver §4. |
| `CopySurfaces` como cópia **intra**-peça | 🟡 | provavelmente é isto que a coleção faz de verdade (Log 24) — não serve para inter-part |
| Offset (spark gap, encolhe) | 📖 | `Constructions.OffsetSurfaces.Add(Side, distance, FaceSet, Boundary)` — depende da cópia primeiro | dump/introspecção |
| Stitch em sólido | 📖 | `Constructions.StitchSurfaces.Add(n, SurfaceArray, Heal, Tolerance)` | introspecção |

### 2.7. Cor, save, relatório

| Capacidade | Status | API real | Evidência |
|---|---|---|---|
| Escrever cor de face | 🟡 | `FaceStyle.SetDiffuse(r,g,b)` (0..1) + `Body.SetFacesStyle`; `AssemblyDocument.FaceStyles` | lado da escrita conhecido, não exercitado no fluxo |
| Salvar `.par` nativo | 📖 | `Document.SaveAs(path)` (entrega é sempre `.par`) | domínio |
| **Relatório de coordenadas de queima** | ✅ | reusa `GetTransform` + bbox por detalhe; grava `.txt`+`.csv` | Log 21/024; **Ferramenta 1 pronta** |
| **Spec-sheet por eletrodo** | ✅ | reusa `PlanFromAssemblyDocument` (Ra/pegada/blank/offset por Ra/fixação); grava `.txt`+`.csv` (1 linha/passe) | **Ferramenta 2 validada (Log 30)** |

---

## 3. Edição in-place — entendimento **corrigido** (2026-07-08)

> ⚠️ **Isto corrige** o que dizem [`COM_INTEGRATION.md`](./COM_INTEGRATION.md) e o briefing
> do Kimi ("`Occurrence.Activate = true` entra em edição in-place"). Aquela afirmação é
> **provavelmente falsa** e nunca foi verificada com o sinal certo.

**O que sabemos (dump + log 027):**

- `Occurrence.Activate` é uma **propriedade booleana** (`put Activate(bool)` — dump:6888).
  Na semântica do SE isso é o flag de **carregar/ativar a ocorrência** (gestão de montagem
  grande), **não** necessariamente "editar a peça in-place".
- Durante a "cópia", `Application.ActiveDocument` continuava sendo a **montagem** (`Type=3`,
  log 027:80). **Isso não é prova de falha**: ao editar uma peça in-place, a janela
  hospedeira continua sendo o `.asm`. É um proxy fraco.
- Os sinais **autoritativos** existem no dump e **nunca foram lidos**:
  - `AssemblyDocument.ModelingInAssembly -> bool` (dump:7408)
  - `AssemblyDocument.InPlaceActivated -> bool` (dump:7225)
  - relacionados: `EditAssembly()` (dump:7410), `EditAssemblyWithOptions(...)` (dump:7411)

**✅ VEREDITO (Log 29, 2026-07-08): `Occurrence.Activate=true` NÃO entra em edição in-place.**
`InterPartCopier.LogInPlaceState` logou, durante a cópia, **`ModelingInAssembly=False,
InPlaceActivated=False`** (log 029:81) mesmo com a ocorrência do eletrodo "criada in-place"
e `Activate=true`. Confirmado: `Activate` é só **carregar/ativar a ocorrência**; o
`EditInPlaceScope` **não** coloca a peça em edição in-place. **Este é o motivo do E_FAIL do
`CopySurfaces.Add`**: a cópia inter-part roda fora do modo de modelagem-em-montagem.

Consequência: falta a API/gesto que realmente faz `ModelingInAssembly=True`. Candidatos
**não** validados: comando nativo "Editar in-place" via `Application.StartCommand` +
`SelectSet` (interativo); ativar o `OccurrenceDocument` do eletrodo como janela de edição.
`AssemblyDocument.EditAssembly`/`EditAssemblyWithOptions` editam a **montagem**, não a peça.

Arquivo afetado: [`EditInPlaceScope.cs`](../src/AutoEDM.Core/Assembly/EditInPlaceScope.cs)
(o comentário das linhas 11-14 está incorreto).

---

## 4. Inter-Part Copy — o beco documentado (⛔ BLOQUEADO)

Objetivo: copiar **associativamente** as faces de queima da cavidade para o eletrodo,
com o eletrodo editado em contexto. ~10 runs; tudo abaixo é reprodutível.

**Fato firme:** `CopySurfaces.Add` **aceita** o tipo certo (`Face[]` tipado →
`SAFEARRAY(IDispatch)`; `object[]` dava `DISP_E_TYPEMISMATCH`), mas **falha na operação
com E_FAIL**. Assinatura real (introspecção, Log 17):
`CopySurfaces.Add(NumberOfFaces: int, FaceArray: SAFEARRAY(IDispatch)*, [opt]InternalBoundary, [opt]ExternalBoundary) -> CopySurface*`.

### 4.1. O que já falhou

| # | Tentativa | Resultado | Run |
|---|---|---|---|
| 1 | Peça standalone (`AddByFilename`+`SaveAs`) + `CopySurfaces.Add` | **E_FAIL** — peça não está em contexto | Log 18 |
| 2 | Ligar opção global Inter-Part (253/254 = True) | opção **confirmada True/True**, mas E_FAIL persiste → não era a opção | Log 24 |
| 3 | Peça in-place via `AddByTemplate` + `CopySurfaces.Add(Face[])` | cria peça in-place OK; cópia **E_FAIL** | Log 22; log 027:82-84 |
| 4 | `CopySurfaces.Add(1 face)` e `(todas)` | **E_FAIL** nas duas | log 027:81-84 |
| 5 | `InterpartConstructions.Add(occ / doc / body)` via InvokeMember | **E_NOINTERFACE** (0x80004002) | Log 25 |
| 6 | `InterpartConstructions.Add2(eletrodo, occ / doc)` | **E_FAIL** | Log 25 |
| 7 | `CreateReference(occ, face)` → consumidores | `CreateReference` **OK** (retorna `__ComObject`); mas `CopySurfaces.Add(ref)`=`DISP_E_TYPEMISMATCH`, `IPC.Add(ref)`=`E_UNEXPECTED`, `Add2(eletrodo, ref)`=`E_FAIL` | Log 26 |
| 8 | `InterpartConstructions.Add2(eletrodo, Face[])` e `Add(Face[])` | **DISP_E_TYPEMISMATCH** | log 027:85-88 |

**Conclusões firmes:** (a) `CopySurfaces` quer `Face` mesmo (rejeita `Reference`), mas
não copia entre peças → é **intra-peça**. (b) `InterpartConstructions` não aceita
occ/doc/body/face/`Reference` como `AsmSource` testados até aqui. (c) A opção global e o
tipo do array **não** são a causa.

### 4.2. Fatos do dump relevantes

- `CopySurfaces` e `InterpartConstructions` **NÃO estão no dump** (parcial — o walk de
  tipos crasha antes de terminar a lib Assembly). As assinaturas vieram de **introspecção
  ao vivo**. Não conclua a partir da ausência.
- O que **está** no dump: `AssemblyDocument.CreateReference(Occurrence, Entity: VARIANT) -> IDispatch`
  (dump:7252), `CreateReference2(Object, Entity) -> IDispatch` (dump:7339),
  `Occurrence.CreateTopologyReference(ReferenceKey: SAFEARRAY(byte)) -> TopologyReference`
  (dump:7444), `Face.GetReferenceKey`.

### 4.3. Caminhos ainda abertos (🟡, ordenados por custo/promessa)

1. ~~**Diagnóstico in-place** (§3)~~ ✅ **FEITO (Log 29): `ModelingInAssembly=False`** — o
   problema É o contexto: nunca entramos em edição in-place. Falta o gesto que faz
   `ModelingInAssembly=True` (ver §3). As tentativas 2–4 abaixo só valem depois disso.
2. **TopologyReference** (Kimi 2.5, intocado): `Face.GetReferenceKey(out key)` na cavidade
   → `Occurrence.CreateTopologyReference(key)` no contexto do eletrodo. É a API dedicada de
   referência de topologia in-context.
3. **Comando nativo do SE**: `AssemblyDocument.SelectSet` + `Application.StartCommand`.
   Ressalva: `StartCommand` é **interativo/assíncrono** — difícil dirigir 100% headless;
   tende ao fluxo **semi-automático**.
4. **Semi-automático (bridge pragmático)**: o desenhista faz o Inter-Part Copy no clique
   nativo; a ferramenta faz offset/blank/fixação/recolor/save. Desbloqueia o resto do
   pipeline sem resolver o nó COM.

> **Decisão registrada (Carlos):** após a última tentativa via COM, **pivotar** para
> outras ferramentas independentes do blocker (destacar faces, spec-sheet, checagem de
> raio, export da lista). Não ficar preso no nó.

---

## 5. Tabela mestra: erro → causa → correção

| Sintoma | Causa | Correção |
|---|---|---|
| `RuntimeBinder: não contém 'ToList'/'X'` | LINQ/membro em resultado `dynamic` COM, ou objeto errado (peça vs. montagem) | tipar a variável antes; `(object)` p/ ligação estática; conferir o objeto |
| `RPC_E_CALL_REJECTED` / `RPC_E_SERVERCALL_RETRYLATER` | SE ocupado/modal | `OleMessageFilter.Register()` (retry nos dois) |
| `[out]` volta vazio (ex.: `GetRange`) | by-ref não marcado no late binding | `new double[0]` + `ParameterModifier` by-ref + `InvariantCulture` |
| `DISP_E_TYPEMISMATCH` (0x80020005) | VARIANT errado — `object[]` onde quer `SAFEARRAY(IDispatch)`, ou placeholder `[out]` errado | array **tipado** (`Face[]`); ou placeholder `[out]` correto |
| `DISP_E_PARAMNOTOPTIONAL` (0x8002000F) | param obrigatório omitido (`Type.Missing` recusado) | passar `null` no objeto obrigatório; conferir aridade no dump |
| `E_FAIL` (0x80004005) no `CopySurfaces.Add` | operação inválida no contexto (ver §4) — **não** é tipo nem opção global | investigar in-place real (§3) / mudar de API |
| `E_NOINTERFACE` (0x80004002) | objeto não expõe a interface que o método quer (ex.: `IPC.Add(occ)`) | passar o tipo certo de `AsmSource` (provável `Reference`/topology) |
| `InvalidCastException` no `IPC.Add(occ)` | binder dinâmico não coage o COM | chamar via **`InvokeMember`** (marshaling robusto) |
| `cParams=0` num método real | é **coleção-propriedade** | `.Add(...)` na coleção |
| Enum errado entre versões | valores são por versão | ler o valor **no dump** desta versão |
| `AccessViolation` no dump tipado | walk de ITypeInfo crasha na lib Assembly | dump grava **parcial** via StreamWriter AutoFlush; não é bug do fluxo |

---

## 6. Limites conhecidos (o que HOJE não dá / não sabemos)

- ⛔ **Inter-Part Copy via COM** — sem caminho validado (§4). Plano B = semi-automático.
- ❌ **`Occurrence.Activate=true` NÃO entra em edição in-place** — confirmado Log 29
  (`ModelingInAssembly=False`). Falta a API real de in-place; sem ela o Inter-Part não roda (§3).
- 🟡 **Box via `AddBoxByTwoPoints`** — assinatura conhecida, ainda `DISP_E_TYPEMISMATCH`;
  há fallback sketch+extrude. Secundário.
- 🟡 **Escrever cor de face no fluxo** — API do lado da escrita conhecida, não exercitada.
- 📖 **Offset / Stitch** — assinaturas conhecidas, mas **dependem** da cópia funcionar
  primeiro; não exercitados.
- ⚠️ **Dump tipado incompleto** — crasha antes de terminar a lib Assembly; por isso
  `CopySurfaces`/`InterpartConstructions` não aparecem nele. Um dump completo é uma
  pendência que ajudaria a fechar §4.

---

## 7. Como consultar o dump (grep primeiro, sempre)

```bash
# assinatura de um método (nome + tipos + direção)
grep -n "AddByTemplate"   src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_*.txt
grep -n "CreateReference" src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_*.txt

# valores de um enum
grep -n "igPartDocument"  src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_*.txt
grep -n "ExtentSide"      src/AutoEDM/bin/Debug/net472/logs/SE_API_dump_*.txt
```

Para uma assinatura só (sem dump inteiro), o código tem
`ComDiagnostics.LogSignatures(obj, "Metodo", …)` — lê nome + params + tipos + direção via
ITypeInfo, ao vivo.

---

## 8. Documentos irmãos

| Doc | Papel |
|---|---|
| [`COM_INTEGRATION.md`](./COM_INTEGRATION.md) | **Setup**: ProgID, registro, TLBs, `.csproj`, STA, OLE filter |
| [`api/`](./api/) + dump | **Catálogo completo** de tipos/assinaturas/constantes |
| [`MAPEAMENTO_INTEGRACAO_COM.md`](./MAPEAMENTO_INTEGRACAO_COM.md) | Mapa por estágio do pipeline (⚠️ seções de in-place e CopySurfaces **superadas** por esta memória — §3/§4) |
| [`.kimi/skills/solid-edge-com/SKILL.md`](../.kimi/skills/solid-edge-com/SKILL.md) | Briefing/papel do Kimi (pesquisa, sem código) |
| [`README.md`](../README.md) | Direção, verdades do domínio, papéis |

> **Ao divergir:** esta memória (§3/§4) vence os docs mais antigos, porque reflete os logs
> 18–27. Se corrigir algo aqui, deixe o doc antigo apontar para cá em vez de repetir.
