# Plano de Validação no Solid Edge Real

> **Escopo:** validar as sequências COM que não podem ser testadas fora do Solid Edge: leitura de cor das faces, `Face` query type e o Inter-Part Copy associativo de 1 face.
>
> **Quem executa:** você (Carlos), na máquina com Solid Edge 2023/2026 licenciado.
>
> **O que me enviar:** log completo do console (copia/cola) + observações visuais (PathFinder, link, etc.).

---

## 1. Arquivos de teste necessários

Crie uma pasta, por exemplo `C:\AutoEDM_Tests\`, com os arquivos abaixo. Eles são **pequenos e artificiais** — o objetivo é isolar cada ponto de risco.

### 1.1 `cavidade_teste.par`

- Criar um bloco sólido simples, por exemplo **50 × 30 × 20 mm**.
- Pintar **apenas uma face** do bloco com a cor de queima da sua paleta (ex.: vermelho puro `RGB 255,0,0` ou uma das cores do `RaColorMap`).
- Salvar como `cavidade_teste.par`.

> Dica: use uma face plana (não curva) para simplificar a validação do Inter-Part Copy.

### 1.2 `eletrodo_destino.par`

- Criar um segundo bloco sólido, por exemplo **30 × 30 × 30 mm**.
- Não precisa pintar nada.
- Salvar como `eletrodo_destino.par`.

### 1.3 `teste_cor.par`

- Cópia de `cavidade_teste.par`.
- Serve só para o teste de seleção por cor, sem montagem.

### 1.4 `teste_ipc.asm`

- Nova montagem.
- Inserir `cavidade_teste.par`.
- Inserir `eletrodo_destino.par`.
- Salvar como `teste_ipc.asm`.

### 1.5 `teste_plan.asm`

- Nova montagem.
- Inserir `cavidade_teste.par`.
- Salvar como `teste_plan.asm`.

---

## 2. Teste A — Seleção por cor e Face Query Type

**Objetivo:** descobrir qual query type enumera faces no SE 2023/2026 e qual caminho lê a cor corretamente.

### Comando

```powershell
AutoEDM.exe faces "C:\AutoEDM_Tests\teste_cor.par" 255 0 0
```

> Use os valores RGB exatos da cor que você pintou na face.

### Resultado esperado

- O programa conecta ao Solid Edge.
- Log mostra algo como:

```text
Cor da face lida via "face.Style.FillColor" (primeira leitura).
Face query type 0 (6 faces).
Fixe ForcedFaceQueryType=0 para evitar o probing.
Selecionadas 1 face(s) em RGB(255,0,0).
  Body[0].Face[2] RGB(255,0,0) via face.Style.FillColor
```

### O que me enviar

1. O log completo do console.
2. A cor exata que você pintou (RGB) e se ela casa com a cor detectada.
3. Qual `FaceQueryType` funcionou (número e quantidade de faces).
4. Qual caminho de leitura de cor funcionou (`face.Style`, `face.FaceStyle`, `face.Color`, etc.).

---

## 3. Teste B — Plano de Eletrodo (modo `plan`)

**Objetivo:** validar se o `ElectrodeBuilder.PlanFromAssembly` encontra a ocorrência com faces de queima e calcula o plano não-destrutivo.

### Comando

```powershell
AutoEDM.exe plan "C:\AutoEDM_Tests\teste_plan.asm"
```

### Resultado esperado

```text
Ocorrência-alvo: 'cavidade_teste.par' com 1 região(ões) de queima.
Região Ra 3.2 µm  cor RGB(255,0,0)  1 face(s)  blank QUAD. XX x XX (cód XXXX)
    ELD-001_Ra32_ACAB.par: offset X.XXX mm
```

### O que me enviar

1. Log completo.
2. Se o blank foi selecionado corretamente.
3. Se o bounding box das faces foi lido (ou se apareceu o warning "Bounding box das faces não lido").
4. Se as dimensões do blank fazem sentido (pegada + margem de 5 mm por lado).

---

## 4. Teste C — Inter-Part Copy de 1 Face (o mais importante)

**Objetivo:** validar se o `EditInPlaceScope` ativa o eletrodo e se o `InterPartCopier` consegue copiar 1 face associativamente.

> **Antes de rodar:** o `InterPartCopier.CopyBurnFaces` ainda está como `NotImplementedException`. Você precisa pedir ao Claude para implementar o teste mínimo primeiro, OU implementar manualmente um método que:
> 1. Ative `eletrodo_destino.par` para edição in-place.
> 2. Selecione a face pintada de `cavidade_teste.par`.
> 3. Execute o comando de Inter-Part Copy.
> 4. Pare e salve.

### Comando (quando implementado)

```powershell
AutoEDM.exe copy-test "C:\AutoEDM_Tests\teste_ipc.asm" "cavidade_teste" "eletrodo_destino" 255 0 0
```

### Resultado esperado

- Nenhuma exceção.
- No PathFinder de `eletrodo_destino.par`, aparece uma nova superfície copiada.
- Aparece o **símbolo de corrente/link** indicando associatividade.
- A face copiada está no sistema de coordenadas correto (sobreposta à face original da cavidade).

### O que me enviar

1. Log completo.
2. Se apareceu o link no PathFinder.
3. Se a face foi copiada como superfície (surface body).
4. Se houve algum diálogo do Solid Edge que bloqueou a automação.
5. Se a operação foi rápida ou lenta.

---

## 5. Procedimento de coleta de logs

O projeto já usa `AutoEDM.Diagnostics.Log`. Para garantir que nada se perca, rode os comandos redirecionando a saída:

```powershell
AutoEDM.exe faces "C:\AutoEDM_Tests\teste_cor.par" 255 0 0 *> C:\AutoEDM_Tests\log_faces.txt
AutoEDM.exe plan "C:\AutoEDM_Tests\teste_plan.asm" *> C:\AutoEDM_Tests\log_plan.txt
```

Envie os arquivos `.txt` para análise.

---

## 6. Checklist de sucesso

| Item | Teste | Critério de sucesso |
|---|---|---|
| Face query type | A | Um dos candidatos retorna o número correto de faces |
| Leitura de cor | A | A cor detectada casa com a cor pintada (dentro da tolerância) |
| Encontrar ocorrência | B | `plan` identifica `cavidade_teste.par` como alvo |
| Bounding box | B | Dimensões coerentes com o bloco de teste |
| Ativação in-place | C | `EditInPlaceScope` ativa `eletrodo_destino.par` sem erro |
| Inter-Part Copy | C | Face copiada como superfície associativa |
| Link no PathFinder | C | Símbolo de corrente visível |

---

## 7. Se algo falhar

### Falha na leitura de cor

- Verifique se a cor foi aplicada como **Face Color** e não como Body Color / Appearance.
- Teste com cores da `RaColorMap` atual (azul, vermelho, marrom, verde, amarelo).
- Aumente a tolerância no `ElectrodeParams.ColorTolerance`.

### Falha no Inter-Part Copy

- Verifique se o Solid Edge está **visível** (`makeVisible: true`).
- Verifique se `eletrodo_destino.par` foi inserido na montagem (não aberto isoladamente).
- Verifique se a opção **Inter-Part Copy** está habilitada nas opções da montagem.

### Falha no bounding box

- O método `GetRange` pode não estar disponível na face. Anote o erro e envie o log.
- Alternativa: iterar vértices da face para calcular o bounding box.

---

## 8. Próximo passo após este plano

Assim que os Testes A e B passarem, fixamos:

1. `FaceSelector.ForcedFaceQueryType`
2. `ProbingFaceColorReader` → leitor direto simplificado

Assim que o Teste C passar, destravamos:

1. Offset das faces de queima.
2. Criação do blank + holder + furos.
3. Duplicação para desbaste/acabamento.
4. Relatório de erosão.
