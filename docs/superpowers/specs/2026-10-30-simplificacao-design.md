# Simplificação (back e front) — design

Data: 2026-10-30. Repos: `TCC.back` (este) e `TCC.front`. Continua `2026-10-29-ingles-e-simplificacao-design.md`, que deixou de fora de propósito o corte de código e de testes ("testes traduzidos, não reescritos").

## 1. Intenção

Os professores vão ler o código. Depois da tradução, o que ainda pesa é o código que trata casos que quase nunca acontecem no uso real (duas pessoas no mesmo milissegundo, sessão vencendo no meio do registro) e os testes que vigiam infraestrutura em vez de regra. O objetivo é que o código mostre o sistema e que os testes mostrem as regras do TCC.

Três alavancas:

1. **Cortar exagero**: tratamento de concorrência e retomadas automáticas que o uso real não pede.
2. **Testes só de regra de negócio**: motor, ciclo de vida, votos, registros, verificação, admin, autenticação. Sai o que vigia contrato, configuração e infraestrutura.
3. **Front com componentes reaproveitados**: blocos repetidos viram um componente.

## 2. Objetivos, não objetivos, anti-objetivos

**Objetivos**
- Os cortes das seções 3 e 4, um a um.
- Menos código e menos testes, sem perder nenhuma regra do `docs/MOTOR_CONFIANCA.md` nem do `docs/API.md`.

**Não objetivos** (decisão do usuário: fica como está)
- **B3** geocodificação: cache por coordenada em 4 casas por 7 dias, junção de chamadas simultâneas, limite do cache e fila de 1 requisição/s com tempo máximo.
- **B4** configuração do OpenAPI, inclusive a ordem das tags.
- **B5** bloqueio de login após 5 falhas (só o código; os testes encolhem).
- **F5** geolocalização que lembra a permissão já dada (Safari/iPhone).
- Regras do motor, LGPD (posição do votante descartada, incidente com 3 casas, autor nunca exposto), rate limit, CORS, contrato HTTP. Só sai o erro `category-limit-reached` (B1).
- Simulator: o código não muda.
- `scripts/e2e.mjs` e `npm run prints` ficam (o e2e é o teste de fumaça antes de subir; os prints servem à monografia).

**Anti-objetivos (falhou, mesmo se compilar)**
- Uma regra de negócio perder o único teste que a cobre.
- Uma tela mudar de aparência ou de texto, fora as mudanças listadas (F1 e o texto `categoria(s)` da lista de grupos).
- Componente "genérico" que precisa de mais props do que as duas cópias que ele substitui. Junção só entra se o resultado for menor e mais fácil de ler.

## 3. Back

### Código

- **B1 — catálogo sem trava.** `CategoryService` e `GroupService` deixam de abrir transação e de chamar `LockCatalogAsync`. Ficam a checagem prévia (nome/rótulo repetido, grupo inexistente, tipo do grupo) e o `catch` de unicidade (o índice único continua sendo a garantia). Sai `CatalogLock.cs`, a checagem de `short.MaxValue` e o erro `category-limit-reached` (de `CategoryErrors`, `docs/API.md` e do front, se houver). No apagar grupo, fica a checagem "grupo em uso" e sai o `catch` de chave estrangeira.
- **B2 — limite de incidentes sem trava.** `IncidentService.CreateAsync` conta uma vez e grava. Saem a transação, o `pg_advisory_xact_lock`, a segunda contagem e `LockKey`. Envios no mesmo instante podem passar de 10; aceito.

### Testes

Ficam (com o que já testam): `TrustEngineTests` (22 casos do documento), `ComplementaryRulesTests`, `PropertiesTests`, `LifecycleTests`, `VotesTests`, `IssuesTests`, `IncidentsTests`, `VerificationTests`, `AdminTests`, `AdminUsersTests`, `AuthTests`, `MeTests`, `HistoryTests`, `CategoriesTests`, `CategoriesAdminTests`, `GroupsTests`, `TrustDisplayTests`. Dentro deles saem os testes listados abaixo.

| # | Sai |
|---|---|
| T1 | `Skeleton/` inteiro (`ContractTests`, `OpenapiShapeTests`, `SkeletonTests`) com `openapi.snapshot.json` e `openapi.shape.txt`. O caso `Removed_user_token_gets_401` vai para `MeTests`. |
| T2 | `EnginePurityTests`, `TrustConfigurationTests`. A pureza do motor vira convenção escrita no CLAUDE.md. |
| T3 | `PostgresClockTests`, `StartupConfigurationTests`, `ProductionStartupTests`, `ApplicationErrorTests`, `ValidationMessagesTests`, `HealthTests`, `RenameToEnglishMigrationTests`. |
| T4 | `NominatimClientTests`, `RequestThrottleTests`, `GeocodingServiceTests` saem, exceto 2 casos que guardam a regra 9: `Second_call_waits_the_min_interval` e `Two_calls_to_the_same_point_make_a_single_request`. `AddressBuildingTests` fica com 3 casos (endereço completo, sem rua, sem endereço). `ReverseTests` fica com o caso do visitante e o do Nominatim fora do ar. |
| T5 | Todo teste `Concurrent_*` e `Parallel_*` (acompanha B1 e B2). |
| T6 | `LoginLockoutTests`: fica só `After_N_failures_the_account_locks_even_with_the_right_password`. |
| T7 | Validação repetida: em cada área fica 1 teste de corpo inválido → 400. |
| T8 | `Ocorrencias.Simulator.Tests`: ficam `ReferenciaCsvTests` e `AvaliadorTests`; saem `OpcoesTests`, `GeracaoTests`, `DeterminismoTests` (o CSV de referência já trava geração e determinismo). |

Infra de teste (`ApiFactory`, `FakeNominatim`, `Scenarios`) encolhe junto com o que deixar de ser usado.

### Docs do back
- `CLAUDE.md`: regra 1 sem `EnginePurityTests`; seção de convenções sem o snapshot do OpenAPI (mudou o contrato → `docs/API.md` e `npm run gen:api:live` no front); árvore de `tests/`.
- `docs/API.md`: sem `category-limit-reached`.

## 4. Front

### Código

- **F1 — sem ação pendente depois do login.** Hoje: visitante toca em votar → entra → o voto é enviado sozinho; a sessão vence no meio do registro → entra → o passo continua sozinho. Depois: a folha "Entre para votar/registrar" continua; depois de entrar, a pessoa volta para a mesma tela (`returnTo`) e toca de novo. O rascunho do registro continua salvo (`sessionStorage`). Saem `pendingAction`/`consumeAction` do `guard-store`, `use-resume-report`, `use-discard-pending-action` e o que só existia para eles em `use-after-login`, `use-require-account`, `use-submission`, `use-votes`, `issue-location-page`, páginas de login e cadastro e `root-layout`.
  - Junto: corrige o bug do token inválido ao votar (a folha não abria). O tratamento do 401 sai do `mutate(..., { onError })` e vai para o `onError` do `useMutation`, que roda mesmo com o card desmontado pelo `resetQueries`.
- **F2 — ícones remotos simples.** `iconify.ts` perde a fila em lote, o cache próprio no `localStorage` e a validação manual do SVG. Cada ícone é buscado por nome com TanStack Query (`staleTime: Infinity`); a busca do seletor de ícones continua igual. O React já escreve o `d` do `<path>` como atributo, sem risco de injeção.
- **F3 — sem dados falsos no navegador.** Saem o bloco de `main.tsx`, `env.mocksEnabled`/`VITE_USE_MOCKS`, `mocks/browser.ts` e o plugin `msw` do `vite.config.ts`. Os handlers continuam para os testes.
- **F4 — sucesso de incidente sem nomes fixos.** Sai `plural = { 7: 'Roubos', 8: 'Acidentes' }`; usa o nome da categoria.
- **Texto em inglês na tela** (achado do levantamento, veio do refactor): a lista de grupos mostra `category/categories`; volta a ser `categoria/categorias`.

### F6 — componentes reaproveitados

| Junção | Onde está repetido hoje |
|---|---|
| `LoadError` (mensagem + "Tentar novamente") | 11 telas: listas do admin, fila de verificação, usuários, mapa, perfil, histórico, categoria, detalhe, recentes, área de incidentes, `admin-guard`. |
| `AdminListRow` (ícone, título, linha de detalhes, ações) e mensagem "Nenhum … com esses filtros" | `category-list`, `group-list`. |
| `FormSection` (rótulo + conteúdo), `TypeField` (tipo travado ou segmentado + ajuda), `FormActions` (erro + Cancelar/Salvar) | `category-form`, `group-form`. |
| `ConfirmSheet` (pergunta + Cancelar/ação destrutiva) | apagar grupo e onde mais houver confirmação (`user-card`, `request-card`, configurações). |
| Revisão do registro: botão Enviar, linha de categoria, erro de regra e `SubmissionFailedDialog` num só componente | `issue-review-page`, `incident-review-page`. |
| Diálogo de aviso (`NoticeDialog`, `SubmissionFailedDialog`, ajuda da localização) num só, se couber sem prop extra | `auth`, `report`, `location`. |

A lista é a de partida; na execução cada junção só entra se cumprir o anti-objetivo de tamanho. O que for descartado vai para o relatório do PR.

### Testes e ferramentas

| # | Sai |
|---|---|
| FT1 | `scripts/ui-strings.mjs`, `scripts/compare-prints.mjs` e o baseline; `ui-strings.txt` e os mapas do refactor ficam como histórico em `docs/` (não rodam). |
| FT2 | `scripts/contrast.test.mjs`, `scripts/map-style.test.mjs`, `scripts/icons.test.mjs`. |
| FT3 | `mocks/handlers/report-admin-category.test.ts` e o snapshot do `issue-marker`. |
| FT4 | Testes de unidade de detalhe: `stores/*.test.ts` (guard, session, draft, recent, geolocation), `*/errors.test.ts`, `infrastructure/api/client.test.ts`, `use-sync-cache.test.tsx`, `router.test.tsx`, `common/lib/*.test.ts`, `use-theme.test.tsx`, `use-admin-categories.test.tsx`, testes de componentes visuais (`compass-button`, `remote-icon`, `vector-map`, `app-layout`, `issue-layer`, `issue-marker`, `report-map`). Ficam os fluxos de `src/test/flows/` e os testes de tela com regra: mapa, detalhe e cards de voto, registro (tipo/categoria), admin (categorias, usuários), login/cadastro, recentes, e os de modelo que guardam regra (`votes`, `filters`, `heatmap`, `time`, `history`, `category`). Um teste novo cobre o bug do token inválido (F1). |

`scripts/e2e.mjs` é ajustado ao F1 (depois de entrar, toca de novo).

### Docs do front
`CLAUDE.md`, `README.md`, `docs/TELAS.md`, `docs/PLAN.md` sem as ferramentas e fluxos que saíram.

## 5. Entrega

1. **back `refactor/simplificacao`**: B1, B2, testes, docs. CI verde.
2. **front `refactor/simplificacao`**: F1–F4, texto `categoria(s)`, testes e ferramentas, docs. Gate: typecheck, lint, format, `npm test`, e2e contra o back do item 1 rodando local.
3. **front `refactor/componentes`**: F6, sobre o item 2. Mesmo gate, mais `npm run prints` conferido a olho nas telas tocadas.

Os três são independentes no deploy (nenhum muda o contrato além do erro que sai). Merge só quando o usuário pedir.

## 6. Escolhas minhas (para o usuário conferir)

- T4: guardei 2 testes da geocodificação porque a regra 9 (1 req/s, cache por ponto) está no CLAUDE.md como inegociável.
- T8: saem os testes do Simulator que o CSV de referência já cobre.
- F1: corto as duas retomadas (voto e registro). O bug do token inválido é corrigido no caminho, não em PR separado.
- FT4: o critério é "o teste falharia se uma regra da tela mudasse?". Se só falharia por detalhe de implementação, sai.
