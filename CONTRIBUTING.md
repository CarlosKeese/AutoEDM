# Contribuindo com o AutoEDM

Obrigado pelo interesse em contribuir! O AutoEDM é construído **ferramenta por ferramenta**, e cada contribuição bem enquadrada acelera a automação completa.

## Antes de começar

1. Leia o [`README.md`](README.md) para entender o propósito e arquitetura.
2. Leia o [`docs/PROJECT.md`](docs/PROJECT.md) para as regras internas, verdades do domínio e restrições de COM.
3. Leia a skill [`solid-edge-com`](.claude/skills/solid-edge-com/SKILL.md) se for trabalhar com a API COM do Solid Edge.

## Regras de ouro

- **Nunca invente API COM.** Toda assinatura de método deve vir do dump `SE_API_dump_*.txt` ou de introspecção COM ao vivo. Sem evidência → marque como "a validar".
- **Respeite as verdades do domínio.** Eletrodos são projetados em contexto de montagem, cores codificam Ra, offset é para dentro, entrega é `.par` nativo.
- **Mantenha o núcleo independente da UI.** Comandos da ribbon são cascas finas; a lógica fica em `AutoEDM.Core` com argumentos explícitos.
- **Uma ferramenta por vez.** Cada PR deve entregar uma ferramenta completa e testável, não um pedaço de automação.
- **Atualize a skill.** Se descobrir algo novo e validado sobre COM do Solid Edge, proponha a atualização de `.claude/skills/solid-edge-com/SKILL.md`.
- **Núcleo em inglês, usuário em português.** Identificadores de código — tipos, membros, parâmetros, enums — em **inglês** (`ORingGrooveCalculator.Compute`, `SealMotion.Static`, `GrooveBottomDiameter`): é a língua da API do Solid Edge que o núcleo espelha, e misturar as duas obriga a traduzir mentalmente a cada linha. Tudo que o **usuário lê** — rótulos de botão, mensagens de erro, tooltips, linhas de log, nomes de recurso criados na árvore (`"GAP: 0,05 - Ra: 0,8"`) — em **português**, com vírgula decimal e unidade explícita, porque quem lê está no chão de fábrica, não no editor. **Comentários e documentação em português**, e explicando o *porquê* (a armadilha de COM, a regra de EDM, a decisão) — o *o quê* já está no código.
- **Teste o que não precisa do Solid Edge.** A engenharia pura (cota de canal, tabela de Ra, catálogo, encadeamento de arestas) vive fora do COM justamente para ter teste automatizado: `dotnet test tests/AutoEDM.Core.Tests`. Ao mexer nessa parte, o teste vai junto no mesmo PR. O que só o SE responde fica marcado "a validar" e vai para o roteiro de teste ao vivo — nunca dado como pronto.

## Como contribuir

1. Fork o repositório.
2. Crie uma branch para sua ferramenta/correção: `git checkout -b feature/nome-da-ferramenta`.
3. Faça commits pequenos e descritivos.
4. Abra um Pull Request explicando:
   - O que a ferramenta faz.
   - Quais métodos COM usa e onde estão validados (dump/linha).
   - Como testar no Solid Edge.
   - Prints/logs de validação, se possível.

## Relatando problemas

Ao abrir uma issue, inclua:

- Versão do Solid Edge.
- Versão do .NET / Windows.
- Log relevante (`logs/AutoEDM NNN.log` ou `%LOCALAPPDATA%\AutoEDM\logs`).
- Passos para reproduzir.
- HRESULT do erro, se houver.

## Código de conduta

Mantenha o ambiente respeitoso, técnico e focado no problema. Dúvidas são bem-vindas; suposições sem evidência no dump não são.
