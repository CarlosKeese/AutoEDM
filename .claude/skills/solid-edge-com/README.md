# Skill: solid-edge-com

Skill do Claude Code com o método e as lições de **automação do Solid Edge (2023/2026) via
COM** sem o SDK instalado — descobrir a API por introspecção (dump da typelib, reflexão do
`Interop.SolidEdge`, e o "SPY" ao vivo) em vez de adivinhar assinaturas. Encapsula os erros
que ela prevê (unidades em metros, coleções 1-based, OLE message filter, out-params por-ref,
`RPC_E_DISCONNECTED` pós-`Holes.AddSync`, marshaling de SAFEARRAY com arrays tipados, furo
roscado M6, a armadilha do deploy do add-in, etc.).

## Como replicar em outro projeto

Uma skill é uma pasta com um `SKILL.md` (frontmatter `name` + `description` que dizem ao
Claude quando ativá-la). Para usar noutro projeto, copie **a pasta** para um destes locais —
o Claude Code descobre skills automaticamente:

- **Por projeto:** `<seu-projeto>/.claude/skills/solid-edge-com/`
- **Por usuário (todos os projetos):** `~/.claude/skills/solid-edge-com/`
  (no Windows: `C:\Users\<voce>\.claude\skills\solid-edge-com\`)

Depois é só pedir algo de Solid Edge/COM (ou os gatilhos do `description`) que a skill entra
em contexto. O `SKILL.md` é a fonte de verdade — mantido aqui e no `~/.claude/skills/` deste
usuário.
