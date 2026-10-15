# AGENTS.md

## Review guidelines

### Idioma
- Escreva **todo** o review em português do Brasil: títulos, explicações e sugestões.
- Mantenha em inglês apenas nomes de código (funções, variáveis, arquivos) e termos técnicos consagrados.

### Formato de cada apontamento
- O título de cada apontamento **deve começar** com exatamente uma das tags abaixo, entre colchetes e em minúsculo:

| Tag | Quando usar |
|---|---|
| `[bug]` | Comportamento incorreto, crash, erro de lógica, condição de corrida |
| `[seguranca]` | Injeção (SQL, XSS etc.), segredo no código, falha de autenticação/autorização, dado sensível exposto |
| `[spec]` | Implementação diverge da especificação do projeto (pasta `specs/`, quando existir) |
| `[padrao]` | Fere uma convenção do projeto: arquitetura, nomenclatura, estrutura de pastas, padrões descritos neste arquivo |
| `[observabilidade]` | Falta de log, log sem contexto, erro engolido, ausência de métricas ou rastreio |
| `[performance]` | Consulta N+1, loop desnecessário, re-render excessivo, falta de paginação ou cache |
| `[teste]` | Falta de teste para regra nova ou alterada, teste frágil ou que não verifica nada |
| `[legibilidade]` | Código confuso, nome ruim, duplicação, complexidade desnecessária |

- Exemplo de título: `[seguranca] Texto do comentário é renderizado como HTML (XSS)`
- Corpo do apontamento, nesta ordem:
  1. **Problema:** o que está errado e em qual cenário acontece.
  2. **Impacto:** o que o usuário ou o sistema sofre.
  3. **Sugestão:** como corrigir, com trecho de código curto quando ajudar.

### Critérios
- Seja direto. Não elogie código trivial nem repita o que o diff já mostra.
- Na dúvida entre duas tags, use a mais grave (ordem: `[seguranca]` > `[bug]` > `[spec]` > as demais).
- Quando houver especificação em `specs/`, confira se o PR implementa o que ela pede e aponte divergências com `[spec]`.
