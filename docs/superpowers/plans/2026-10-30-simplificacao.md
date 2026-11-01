# Simplificação (back e front) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cortar exagero e testes de infraestrutura no back e no front, e reaproveitar componentes no front, sem mudar regra nem tela (fora F1).

**Architecture:** Três branches: back `refactor/simplificacao`, front `refactor/simplificacao`, front `refactor/componentes` (sobre a anterior). A maior parte é remoção; a rede de segurança são os testes de regra que ficam. Código novo (F1, F6, texto) nasce de teste vermelho.

**Tech Stack:** .NET 10, EF Core, xUnit + Testcontainers; React 19, TanStack Query, Zustand, Vitest + MSW.

**Spec:** `docs/superpowers/specs/2026-10-30-simplificacao-design.md`

## Global Constraints

- Nenhuma regra do `docs/MOTOR_CONFIANCA.md` ou do `docs/API.md` perde o único teste que a cobre.
- Texto e aparência das telas idênticos, exceto F1 e `categoria(s)` na lista de grupos.
- Sem comentários no código; nomes em inglês; textos de UI em pt-BR.
- Commits em Conventional Commits em português, com a linha `Claude-Session: https://claude.ai/code/session_01B9pmm7ec3W7X8NK2mx6PPG`.
- Back: `dotnet build` sem warnings (`TreatWarningsAsErrors`), `dotnet test` verde. Front: `npm run typecheck && npm run lint && npm run format:check && npm test`.
- Fica: B3 (geocodificação), B4 (OpenAPI), B5 (lockout, só o código), F5 (geolocalização), `scripts/e2e.mjs`, `npm run prints`, Simulator.

## Review Focus

- Visitante vota → entra → volta ao detalhe do mesmo problema (não ao mapa) e o voto não é enviado sozinho.
- Sessão vence no meio do registro → entra → volta ao mesmo passo com o rascunho preenchido.
- Token inválido ao votar → abre a folha "Entre para votar" (bug corrigido).
- Admin cria categoria com nome repetido (diferença de maiúscula) → 409 `duplicate-name`, sem a trava.
- Ícone remoto que a API do Iconify não tem → mostra o fallback, sem erro no console.

---

### Task 1: B1 — catálogo sem trava (back)

**Files:** Modify `src/UrbanReports.Api/Feature/Categories/Services/CategoryService.cs`, `Feature/Groups/Services/GroupService.cs`, `Feature/Categories/Errors/CategoryErrors.cs`, `docs/API.md`. Delete `Common/Helpers/CatalogLock.cs`. Test: `tests/UrbanReports.Api.Tests/Categories/CategoriesAdminTests.cs`, `Groups/GroupsTests.cs`.

- [ ] Remover `Concurrent_creations_get_distinct_sequential_ids` e `Parallel_rename_to_the_same_name_lets_only_one_win` (categorias) e `Parallel_rename_to_the_same_label_lets_only_one_win` (grupos).
- [ ] Tirar `BeginTransactionAsync`/`LockCatalogAsync`/`CommitAsync` dos dois services, o teste `short.MaxValue` e `CategoryLimitReached`; no `DeleteAsync` de grupo, tirar o `catch` de chave estrangeira. Apagar `CatalogLock.cs`; tirar a linha `category-limit-reached` do `docs/API.md`.
- [ ] Run: `dotnet test --filter "FullyQualifiedName~Categories|FullyQualifiedName~Groups"` — Expected: PASS.
- [ ] Commit `refactor(catalogo): cadastro de categorias e grupos sem trava de concorrência`.

### Task 2: B2 — limite de incidentes sem trava (back)

**Files:** Modify `Feature/Incidents/Services/IncidentService.cs`; Test `Incidents/IncidentsTests.cs`.

- [ ] Remover `Concurrent_reports_do_not_break_the_limit`.
- [ ] `CreateAsync`: uma contagem antes da geocodificação, sem transação, sem `pg_advisory_xact_lock`, sem `LockKey`.
- [ ] Run: `dotnet test --filter FullyQualifiedName~Incidents` — Expected: PASS (inclui `Eleventh_incident_in_24h_returns_429_and_is_released_later`).
- [ ] Commit `refactor(incidentes): limite diário sem trava por usuário`.

### Task 3: testes do back só de regra (T1–T8)

**Files:** Delete `tests/UrbanReports.Api.Tests/Skeleton/` (com `openapi.snapshot.json`, `openapi.shape.txt`), `Trust/Engine/EnginePurityTests.cs`, `Trust/Engine/TrustConfigurationTests.cs`, `Common/*Tests.cs`, `Health/HealthTests.cs`, `Persistence/*Tests.cs`, `Geocoding/NominatimClientTests.cs`; `tests/Ocorrencias.Simulator.Tests/{OpcoesTests,GeracaoTests,DeterminismoTests}.cs`. Modify `Geocoding/{RequestThrottleTests,GeocodingServiceTests,AddressBuildingTests,ReverseTests}.cs`, `Auth/LoginLockoutTests.cs`, `Users/MeTests.cs`, demais arquivos com `Concurrent_*`/`Parallel_*` e validação repetida.

- [ ] Mover `Removed_user_token_gets_401` para `MeTests` antes de apagar `Skeleton/`.
- [ ] Apagar/encolher conforme a tabela de testes da spec §3 (T1–T8). T4 fica com `Second_call_waits_the_min_interval`, `Two_calls_to_the_same_point_make_a_single_request`, 3 casos de `AddressBuildingTests` (`Full_address`, `No_street_uses_neighborhood_and_city_in_label_and_ignores_number`, `No_address_falls_back_to_coordinates_text`), 2 de `ReverseTests` (`Visitor_gets_the_built_address`, `Nominatim_offline_returns_200_with_available_false`). T6 fica com `After_N_failures_the_account_locks_even_with_the_right_password`. T7: um teste de 400 por área.
- [ ] Apagar da infra de teste e do `.csproj` o que ficou sem uso (snapshots copiados, helpers).
- [ ] Run: `dotnet build -warnaserror && dotnet test` — Expected: PASS.
- [ ] Commit `test: fica só o que testa regra de negócio`.

### Task 4: docs do back

**Files:** `CLAUDE.md`, `docs/PLAN.md` (`Cors:Origens` → `Cors:Origins`).

- [ ] CLAUDE.md: regra 1 sem `EnginePurityTests`; árvore de `tests/`; convenção do OpenAPI vira "mudou o contrato → atualize `docs/API.md` e rode `npm run gen:api:live` no front".
- [ ] Commit `docs: CLAUDE.md sem os testes que saíram`; push; PR para `main`.

### Task 5: F1 — sem ação pendente depois do login (front)

**Files:** Modify `features/auth/stores/guard-store.ts`, `hooks/use-require-account.ts`, `hooks/use-after-login.ts`, `pages/login-page.tsx`, `pages/register-page.tsx`, `index.ts`, `app/layouts/root-layout.tsx`, `features/report/hooks/use-submission.ts`, `report/pages/issue-location-page.tsx`, `report/pages/*-review-page.tsx`, `features/issues/hooks/use-votes.ts`, `issues/api/use-vote.ts`. Delete `report/hooks/use-resume-report.ts`, `auth/hooks/use-discard-pending-action.ts`. Test: `src/test/flows/guard.test.tsx`, `features/issues/components/detail/issue-detail.test.tsx`.

**Interfaces:** Produces `askForAccount(reason: AccountReason, returnTo: string, expired?: boolean)`; `completeLogin(): string | null` devolve `returnTo`.

- [ ] Teste vermelho em `issue-detail.test.tsx`: `'invalid token on vote opens the login sheet'` — MSW responde 401 ao `PUT /issues/:id/vote` com sessão salva; espera `findByRole('dialog', { name: /Entre para votar/ })`.
- [ ] Ajustar `guard.test.tsx`: visitante vota → entra → está em `/map/issue/:id` e nenhum `PUT` de voto foi feito; registro com 401 → entra → volta a `/report/location` com o rascunho.
- [ ] Run `npm test -- issue-detail guard` — Expected: FAIL nos novos.
- [ ] Implementar: guard-store guarda só `returnTo` (persistido em `sessionStorage`); tratamento do 401 do voto no `onError` do `useMutation`; remover retomadas.
- [ ] Run `npm test` — Expected: PASS. Commit `refactor(auth): depois de entrar volta para a tela, sem repetir a ação`.

### Task 6: F2 — ícones remotos simples (front)

**Files:** `common/lib/iconify.ts`, `common/components/remote-icon.tsx`, `features/admin/components/icon-picker.tsx`; test `common/lib/iconify.test.ts` (encolhe).

- [ ] `loadIcon(name)` vira um `fetch` de `${HOST}/${PREFIX}.json?icons=${iconifyName(name)}` → `{ paths, size }` ou `null`; `RemoteIcon` usa `useQuery({ queryKey: ['remote-icon', name], staleTime: Infinity })`. Saem fila, `localStorage` e regex de validação; `fetchIcons` (busca do seletor) fica.
- [ ] Run `npm test` — Expected: PASS. Commit `refactor(icones): ícone remoto buscado direto, com cache do TanStack Query`.

### Task 7: F3, F4 e texto `categoria(s)` (front)

**Files:** `main.tsx`, `infrastructure/env.ts`, `infrastructure/mocks/browser.ts` (apaga), `vite.config.ts`, `.env.example`/README se citarem `VITE_USE_MOCKS`; `report/pages/incident-success-page.tsx`; `admin/components/group-list.tsx`; test `admin/pages/admin-categories-page.test.tsx`.

- [ ] Teste vermelho: aba Grupos mostra `'1 categoria'` / `'N categorias'`. Run — FAIL.
- [ ] Corrigir o texto; tirar `plural` fixo; tirar o modo mock do navegador.
- [ ] Run gate — PASS. Commit `fix(admin): contagem de categorias em português` e `refactor: sem dados falsos no navegador e sem nomes fixos de categoria`.

### Task 8: testes e ferramentas do front (FT1–FT4), e2e, docs

**Files:** conforme spec §4 "Testes e ferramentas"; `package.json` (scripts), `scripts/e2e.mjs`, `CLAUDE.md`, `README.md`, `docs/TELAS.md`, `docs/PLAN.md`.

- [ ] Apagar arquivos de FT1–FT4; tirar dependências que ficarem sem uso (`pixelmatch`, `pngjs` se só a comparação usava).
- [ ] `e2e.mjs`: depois de entrar, tocar de novo em votar/enviar.
- [ ] Run gate + `node scripts/e2e.mjs` contra o back da Task 4 — Expected: PASS (inclui o passo do token inválido).
- [ ] Commit `test: fica só o que testa regra de tela`; push; PR.

### Task 9: F6 — `LoadError` (front, branch `refactor/componentes`)

**Files:** Create `common/components/load-error.tsx`; modify as 11 telas da spec.

**Interfaces:** `LoadError({ message: string, onRetry(): void })`.

- [ ] Teste vermelho `load-error.test.tsx`: mostra a mensagem em `role="alert"` e o botão chama `onRetry`. Implementar; trocar as 11 cópias quando a marcação for a mesma (as que diferem ficam e vão para o relatório).
- [ ] Gate PASS. Commit `refactor(ui): LoadError no lugar das 11 cópias`.

### Task 10: F6 — admin (front)

**Files:** Create `admin/components/admin-list-row.tsx`, `form-parts.tsx` (`FormSection`, `TypeField`, `FormActions`), `common/components/confirm-sheet.tsx`; modify listas, formulários, `user-card`, `request-card`, `settings-page` onde a confirmação for igual.

- [ ] Os testes de tela do admin já cobrem; rodar antes (PASS), refatorar, rodar depois (PASS). Junção que não encolher o código não entra.
- [ ] Commit `refactor(admin): linha de lista, partes de formulário e confirmação compartilhadas`.

### Task 11: F6 — registro e diálogos (front)

**Files:** Create `report/components/review-step.tsx`; modify as duas revisões; avaliar `NoticeDialog`/`SubmissionFailedDialog`/`location-help-dialog`.

- [ ] Fluxos de registro cobrem; PASS antes e depois. `npm run prints` e conferir a olho as telas tocadas.
- [ ] Commit `refactor(registro): revisão compartilhada entre problema e incidente`; push; PR.

### Final

- [ ] Revisão independente das 3 branches (subagente, modelo mais capaz) com este plano, a spec e o Review Focus.
- [ ] Corrigir Critical/Important com teste vermelho → verde.
- [ ] Merge dos 3 PRs (pedido pelo usuário) na ordem back → front simplificação → front componentes; conferir deploy no Railway (health, bundle novo).
