# Inglês e simplificação — back e banco — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** O back passa de 6 para 4 projetos, fica todo em inglês (código, banco, contrato) e sem comentários, sem mudar nenhuma regra.

**Architecture:** Dois PRs. PR 1 (`refactor/simplificar-projetos`) só move o `Ocorrencias.Domain` para dentro da Api; o contrato e o banco ficam iguais. PR 2 (`refactor/ingles`, criado em cima do PR 1) traduz tudo por camadas. Cada camada termina compilando e com os testes verdes. Três redes de proteção, criadas **antes** de qualquer rename, provam que só os nomes mudaram: CSV de referência do Simulator, "forma" do OpenAPI e migration com dados.

**Tech Stack:** .NET 10, EF Core 10 + Npgsql/PostGIS, xUnit + AwesomeAssertions, Testcontainers.

**Spec:** `docs/superpowers/specs/2026-10-29-ingles-e-simplificacao-design.md`. O glossário (§4), as rotas (§5) e os nomes do banco (§6) são normativos. Releia o spec antes de cada tarefa.

O front tem plano próprio, escrito depois do PR 2, a partir do `openapi.snapshot.json` novo.

## Global Constraints

- Nenhuma regra muda: motor, LGPD, arredondamento em 3 casas, rate limit, geocodificação, CORS e auth ficam iguais (spec §2).
- Nenhuma asserção de teste é removida ou afrouxada. Os testes são traduzidos, não reescritos.
- O Simulator só troca a referência e os nomes que chama. O CSV dele fica idêntico byte a byte. Ele e o `Simulator.Tests` mantêm os comentários e os identificadores em português.
- Migrations antigas (`20261016032542_Inicial` … `20261020185252_GruposDeCategoria`) não são tocadas. Valem como código gerado.
- Dados continuam em português: nomes de categoria, rótulos e chaves de grupo, chaves de ícone e mensagens de validação para o usuário.
- Todo commit compila, roda `dotnet test` verde e segue Conventional Commits em português.
- O motor (`Feature/Trust/Engine/`) só usa `System.*` e o próprio namespace, mais `Common` para os enums.

## Review Focus

1. **Banco de produção com dados reais aplicando `RenameToEnglish`**: tem que preservar todas as linhas, e a API sobe sem erro. É pinado pelo teste de migration com dados (Task 6).
2. **Variável de ambiente com nome antigo no Railway**: a config cai no padrão sem avisar. O caso mais grave é `Cors:Origins` vazio, que bloqueia o front em silêncio. Em `Production`, a API se recusa a subir sem `Cors:Origins` (Task 5).
3. **Claim `role` no token de admin**: `RequireClaim("role", "administrator")` tem que liberar `/admin/*`. Hoje `MapInboundClaims = false`, e isso precisa continuar assim. É pinado por um teste de admin que faz login real (Task 7c).
4. **SQL escrito à mão com nome antigo**: só quebra em tempo de execução. Cada consulta crua (histórico, calor, `FOR UPDATE` do voto, bloqueio de login, advisory lock) precisa ter um teste de integração que passe por ela (Task 6).
5. **Valor de enum no CSV do Simulator** (`status_fim_ataque`): tem que continuar `resolvida`/`ativa`. É pinado pelo CSV de referência (Task 1).

---

## PR 1 — `refactor/simplificar-projetos`

A branch atual `refactor/ingles-e-simplificacao`, que já tem o spec, é renomeada: `git branch -m refactor/simplificar-projetos`.

### Task 1: CSV de referência do Simulator

**Files:**
- Create: `tests/Ocorrencias.Simulator.Tests/ReferenciaCsvTests.cs`
- Create: `tests/Ocorrencias.Simulator.Tests/Referencia/*.csv` (gerados pelo código **atual**)
- Modify: `tests/Ocorrencias.Simulator.Tests/Ocorrencias.Simulator.Tests.csproj` (copiar `Referencia/**` para a saída)

**Interfaces:**
- Consumes: `Csv.Escrever(string dir, Resultados r)` e o mesmo `Executar()` de `DeterminismoTests` com `CenarioPequeno.Valor`, configs `A,B`, variantes `S1,S2,S3` e 2 sementes.
- Produces: `[Fact] Csv_igual_a_referencia`. As tasks seguintes precisam mantê-lo verde.

- [ ] **Step 1:** escrever `Csv_igual_a_referencia`. Ele roda o experimento pequeno num diretório temporário e, para cada arquivo de `Referencia/`, confere `File.ReadAllBytes(gerado).Should().Equal(File.ReadAllBytes(referencia))`. Também confere que o conjunto de nomes de arquivo é igual.
- [ ] **Step 2:** rodar com `Referencia/` vazia. Esperado: FAIL (nenhum arquivo de referência).
- [ ] **Step 3:** gerar os CSVs com o código atual, ainda sem mudança nenhuma, e copiá-los para `Referencia/`. A forma de gerar é livre (teste temporário, `dotnet run` do Simulator com os mesmos parâmetros).
- [ ] **Step 4:** `dotnet test tests/Ocorrencias.Simulator.Tests`. Esperado: PASS.
- [ ] **Step 5:** commit `test(simulator): CSV de referência para travar a saída`.

### Task 2: Domain dentro da Api

**Files:**
- Move: `src/Ocorrencias.Domain/Confianca/*.cs` → `src/Ocorrencias.Api/Feature/Confianca/Motor/` (namespace `Ocorrencias.Api.Confianca.Motor`)
- Move: `src/Ocorrencias.Domain/Enums.cs` → `src/Ocorrencias.Api/Common/Enums.cs` (namespace `Ocorrencias.Api.Common`)
- Move: `tests/Ocorrencias.Domain.Tests/Confianca/*.cs` → `tests/Ocorrencias.Api.Tests/Confianca/Motor/`
- Create: `tests/Ocorrencias.Api.Tests/Confianca/Motor/PurezaDoMotorTests.cs`
- Delete: `src/Ocorrencias.Domain/`, `tests/Ocorrencias.Domain.Tests/`
- Modify: `Ocorrencias.slnx`, `src/Ocorrencias.Api/Ocorrencias.Api.csproj` (sai a `ProjectReference` do Domain, entra `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`), `src/Ocorrencias.Simulator/Ocorrencias.Simulator.csproj` (referência para `..\Ocorrencias.Api\Ocorrencias.Api.csproj`), os `using Ocorrencias.Domain*` da Api, do Simulator e dos testes, `Dockerfile` (sai o `COPY` do csproj do Domain), `CLAUDE.md` (árvore e regra 1).

**Interfaces:**
- Produces: tipos do motor em `Ocorrencias.Api.Confianca.Motor` (`MotorConfianca`, `ConfiguracaoConfianca`, `VotoFato`, `EstadoProblema`, `ResultadoConfianca`) e enums em `Ocorrencias.Api.Common`.

- [ ] **Step 1:** escrever `PurezaDoMotorTests.Motor_so_usa_bcl`. Ele sobe pelos diretórios até achar o `.slnx`, lê cada `.cs` de `src/Ocorrencias.Api/Feature/Confianca/Motor/` e confere que toda linha `using X;` tem `X` começando com `System` ou sendo `Ocorrencias.Api.Common`. Também confere que a pasta tem pelo menos 5 arquivos, para o teste não passar vazio.
- [ ] **Step 2:** rodar. Esperado: FAIL (a pasta não existe).
- [ ] **Step 3:** mover os arquivos, ajustar namespaces, `using`s, csprojs, `.slnx` e `Dockerfile`. Se o `Program` do Simulator conflitar com o da Api (CS0436), resolver no Simulator sem renomear nada dele: por exemplo, `<NoWarn>` só para CS0436 ou `Program` com namespace explícito. Corrigir os avisos que o `TreatWarningsAsErrors` revelar na Api.
- [ ] **Step 4:** `dotnet build -c Release` sem avisos e `dotnet test`. Esperado: tudo verde, incluindo `Openapi_igual_ao_snapshot` **sem regravar** e `Csv_igual_a_referencia`.
- [ ] **Step 5:** `docker build .`. Esperado: imagem gerada.
- [ ] **Step 6:** commit `refactor: motor de confiança dentro da Api (6 → 4 projetos)`.

### Task 3: PR 1

- [ ] **Step 1:** commitar spec e plano (`docs(superpowers): spec e plano de inglês e simplificação`).
- [ ] **Step 2:** push e `gh pr create` (conta `VictorHenriqueTerraCheru`, via script Python, conforme a memória do fluxo git). Esperar o CI verde.
- [ ] **Step 3:** perguntar ao usuário se pode mergear. Esse PR pode ir para produção sozinho.

---

## PR 2 — `refactor/ingles` (base: `refactor/simplificar-projetos`)

### Task 4: Redes de proteção, nomes de projeto e fim dos comentários

**Files:**
- Create: `tests/Ocorrencias.Api.Tests/Esqueleto/FormaOpenapiTests.cs`, `tests/Ocorrencias.Api.Tests/Esqueleto/openapi.forma.txt`
- Rename: `Ocorrencias.slnx` → `UrbanReports.slnx`; `src/Ocorrencias.Api` → `src/UrbanReports.Api` (csproj, `RootNamespace`, namespaces `Ocorrencias.Api*` → `UrbanReports.Api*`); `tests/Ocorrencias.Api.Tests` → `tests/UrbanReports.Api.Tests`
- Modify: `InternalsVisibleTo`, referência do Simulator, `Dockerfile` (caminhos e `ENTRYPOINT ["dotnet", "UrbanReports.Api.dll"]`), CI se citar caminho
- Modify: todo `.cs` de `src/UrbanReports.Api` (menos `Migrations/`) e de `tests/UrbanReports.Api.Tests`, mais `*.csproj`, `Directory.*.props`, `Dockerfile`, `docker-compose.yml` e `.github/workflows/ci.yml`: remover comentários

**Interfaces:**
- Produces: `[Fact] Forma_do_openapi_preservada`. As tasks 5 a 7 regravam o snapshot de nomes, mas **nunca** este arquivo de forma.

- [ ] **Step 1:** escrever `Forma_do_openapi_preservada`. Ele lê o OpenAPI gerado e calcula uma "forma" sem nomes:
  - para cada operação: método HTTP, quantidade de segmentos da rota, quantidade de parâmetros por local (`path`/`query`), se tem corpo, o conjunto de status de resposta e se exige autenticação;
  - para cada schema: quantidade de propriedades, o multiconjunto de tipos (`string`, `integer`, `number`, `boolean`, `array`, `$ref`, `nullable`) e a quantidade de valores de enum.

  As linhas são ordenadas e comparadas com `openapi.forma.txt`. Variável `ATUALIZAR_FORMA_OPENAPI=1` regrava o arquivo, só neste step.
- [ ] **Step 2:** gerar `openapi.forma.txt` com o código atual e rodar. Esperado: PASS.
- [ ] **Step 3:** renomear solution, projetos, pastas e namespaces raiz. Rodar `dotnet test`. Esperado: verde, sem regravar snapshot nenhum (namespace não aparece no OpenAPI).
- [ ] **Step 4:** remover comentários com uma ferramenta que entende a sintaxe. Para C#, usar Roslyn (trivia `SingleLineComment`, `MultiLineComment`, `SingleLineDocumentationComment`, `MultiLineDocumentationComment`), para não estragar `//` dentro de strings como URLs e o `User-Agent`. Para os arquivos de config, remover à mão. Ficam: diretivas `#pragma` e `<auto-generated>` nas migrations.
- [ ] **Step 5:** `rtk proxy grep -rnE '^\s*(//|/\*|\*)' src/UrbanReports.Api tests/UrbanReports.Api.Tests --include='*.cs' | grep -v Migrations`. Esperado: vazio. Depois `dotnet build -c Release` e `dotnet test`: verdes, com os 3 snapshots intactos.
- [ ] **Step 6:** commits separados: `refactor: solution e projetos UrbanReports` e `refactor: remove comentários do back`.

### Task 5: Motor, Common, enums e configuração em inglês

**Files:**
- Rename: `Feature/Confianca/` → `Feature/Trust/` (`Engine/TrustEngine.cs`, `TrustConfiguration.cs`, `VoteFact.cs`, `IssueState.cs`, `TrustResult.cs`; `Services/IssueRecalculation.cs`, `LifecycleProcessor.cs`; `Jobs/LifecycleJob.cs`; `Models/LifecycleOptions.cs`). Os testes vão junto para `tests/UrbanReports.Api.Tests/Trust/`.
- Modify: `Common/**` (glossário: `Result<T>`, `ApplicationError`, `Paged`, `PostgresClock`, `CatalogLock`, `DatabaseErrors`, `Validate<T>()`, `ValidationRules`, `*Policies`), `Common/Enums.cs` (todos os enums e membros do glossário), `JwtOptions`/`NominatimOptions`/`LifecycleOptions` (`Section` e propriedades), `Infrastructure/**` (menos `Persistence/`)
- Modify: `appsettings*.json` (`Cors:Origins`, `Jwt:{Key,Issuer,Audience}`, `Trust:Configuration`, seção do ciclo de vida)
- Modify: `src/Ocorrencias.Simulator/Saida/Csv.cs` (mapeamento local de `IssueStatus` para o texto antigo), mais os nomes chamados pelo Simulator e pelo `Simulator.Tests`
- Modify: `PurezaDoMotorTests` → `Trust/EnginePurityTests.cs` (`Engine_uses_only_bcl`, pasta `Feature/Trust/Engine/`, namespace `UrbanReports.Api.Common`)
- Test: `tests/UrbanReports.Api.Tests/Common/StartupConfigurationTests.cs`

**Interfaces:**
- Produces: `TrustEngine`, `TrustConfiguration.{A,B,ByName(string)}`, `IssueStatus`, `TrustBand`, `Role`, `VoteValue`, `ReportType`, `GroupColor`, `AffiliationType`, `VerificationStatus`, `ConcentrationLevel`, `Result<T>`, `ApplicationError`, `Paged<T>`, `IssueRecalculation.Apply(...)`, `LifecycleProcessor`. As tasks 6 e 7 dependem desses nomes.

- [ ] **Step 1:** escrever `StartupConfigurationTests.Production_without_cors_origins_fails_to_start`: subir a `ApiFactory` em ambiente `Production` sem `Cors:Origins`; esperado: exceção ao iniciar, com mensagem citando `Cors:Origins`. Escrever também `Production_reads_cors_origins`, que sobe com `Cors:Origins:0` e espera que inicie. Rodar: o primeiro FAIL.
- [ ] **Step 2:** traduzir motor, Common, enums, Options e Infrastructure; ajustar todos os usos (os nomes das áreas ficam em português até a Task 7). Em `Production`, `Cors:Origins` vazio lança `InvalidOperationException`. A claim do JWT vira `role`, com valor do enum em camelCase.
- [ ] **Step 3:** `dotnet test`. Esperado: verde, menos `Openapi_igual_ao_snapshot` (os enums mudaram de nome). Regravar com `ATUALIZAR_SNAPSHOT_OPENAPI=1 dotnet test --filter Openapi_igual_ao_snapshot`. Conferir no diff que só mudaram nomes de enum e de schema de enum. `Forma_do_openapi_preservada` e `Csv_igual_a_referencia` ficam verdes sem regravar.
- [ ] **Step 4:** commit `refactor(trust): motor, common e enums em inglês`.

### Task 6: Persistência e migration `RenameToEnglish`

**Files:**
- Rename: `Infrastructure/Persistence/Entidades/` → `Entities/` (`User`, `Issue`, `Incident`, `Vote`, `Category`, `CategoryGroup`, `VerificationRequest`); `Configurations/*Configuration.cs`; `SeedDesenvolvimento` → `DevelopmentSeed`; `InicializacaoProducao` → `ProductionStartup` (lê `InitialAdmin:{Email,Password,Name}`)
- Create: `Infrastructure/Persistence/Migrations/<timestamp>_RenameToEnglish.cs` (+ Designer e snapshot atualizado)
- Modify: SQL escrito à mão em `ProblemaService`, `IssueRecalculation`, `AuthService`, `UsuarioService`, `IncidenteService`, `CatalogLock` e nos testes (`LoginBloqueioTests`)
- Test: `tests/UrbanReports.Api.Tests/Persistence/RenameToEnglishMigrationTests.cs`

**Interfaces:**
- Consumes: enums e `Result<T>` da Task 5.
- Produces: entidades e propriedades em inglês (`Issue.TrustScore`, `Issue.Location`, `Vote.RoleAtVote`, `Vote.DistanceMeters`, `Vote.RegisteredAt`, `CategoryGroup.{Key,Label,Color,SortOrder}`...). A Task 7 usa essas propriedades.

- [ ] **Step 1:** escrever `RenameToEnglishMigrationTests.Rename_keeps_every_row`. Ele cria um banco PostGIS novo (Testcontainers), aplica as migrations até `GruposDeCategoria` (`IMigrator.MigrateAsync("20261020185252_GruposDeCategoria")`) e insere com SQL cru nos nomes **antigos** 1 usuário, 1 grupo, 1 categoria, 1 problema com `confianca`, 1 voto com `perfil_no_momento` e `distancia_metros`, 1 incidente e 1 solicitação de verificação. Depois aplica o resto (`MigrateAsync()`) e confere pelo `AppDbContext` novo que cada linha existe com os mesmos valores (ex.: `Vote.RoleAtVote == Role.VerifiedCitizen`, `Issue.TrustScore == 82.00m`, a localização igual).
- [ ] **Step 2:** escrever `Rename_leaves_no_pending_model_changes`: `db.Database.HasPendingModelChanges().Should().BeFalse()`. Rodar os dois. Esperado: FAIL (a migration não existe).
- [ ] **Step 3:** traduzir entidades e configurações, gerar a migration com `dotnet ef migrations add RenameToEnglish` e **reescrever o `Up`/`Down` à mão** só com `RenameTable`, `RenameColumn` e `RenameIndex`, mais `migrationBuilder.Sql("ALTER TABLE … RENAME CONSTRAINT …")` para PK/FK. Os nomes vêm do spec §6 em snake_case. Nenhum `Drop*`/`Create*`/`AddColumn`. O `Down` desfaz exatamente o `Up`.
- [ ] **Step 4:** atualizar o SQL escrito à mão. Conferir que cada consulta crua tem um teste que passa por ela: histórico (`HistoricoTests`), calor (`IncidentesTests`), `FOR UPDATE` (`VotosTests`), bloqueio de login (`LoginBloqueioTests`), limite diário de incidentes (`IncidentesTests`), advisory lock do catálogo (`CategoriasAdminTests`/`GruposTests`). Se faltar algum, criar.
- [ ] **Step 5:** `dotnet test`. Esperado: verde. `rtk proxy grep -nE 'Drop|Create|AddColumn' <migration>.cs`: vazio. Snapshot OpenAPI: sem mudança (entidades não aparecem no contrato). Se mudou, algo vazou e precisa ser corrigido.
- [ ] **Step 6:** commit `refactor(persistence): entidades e banco em inglês (migration RenameToEnglish)`.

### Task 7: Áreas, módulos e contrato

Três commits, um por grupo de áreas. Cada um compila e fica verde. Em cada área: `Feature/<Area>/{Abstractions,Services,Models,Validators,Errors}`, `Modules/<Area>Module.cs`, rotas e parâmetros do spec §5, DTOs pela regra de verbo do glossário, `type` de erro do spec §5 e testes da área em `tests/UrbanReports.Api.Tests/<Area>/`.

**Interfaces:**
- Consumes: Tasks 5 e 6.
- Produces: o contrato final (`openapi.snapshot.json` regravado).

- [ ] **7a — Auth, Users, Health, Verification, Admin.** Pasta `Usuarios/` → `Users/`, `Saude/` → `Health/`, `Verificacao/` → `Verification/`. Rotas: `/auth/register`, `/me/history?filter=all|reports|votes`, `/health`, `/health/database`, `/verifications`, `/admin/verifications/{id}/approve|reject`, `/admin/users?search&role`, `/admin/users/{id}/role`. Paginação `page`/`pageSize`/`items`.
  - Teste novo, `AdminTests.Admin_token_from_real_login_opens_admin_routes`: cadastrar, promover pelo banco, fazer login por `POST /auth/login` e chamar `GET /admin/users`. Esperado: `200`. Com token de cidadão, esperado: `403`.
- [ ] **7b — Categories, Groups, Geocoding.** Rotas `/categories`, `/groups`, `/admin/categories`, `/admin/categories/{id}/active`, `/admin/groups/{key}` e `/geocoding/reverse`. Campos `available`, `address`, `street`, `number`, `neighborhood`, `city`, `state`, `areaLabel`. Os dados do seed continuam em português.
- [ ] **7c — Issues, Votes, Incidents.** `Problemas/` → `Issues/`, `Votos/` → `Votes/`, `Incidentes/` → `Incidents/`, `ExibicaoConfianca` → `TrustDisplay`. Rotas `/issues`, `/issues/{id}`, `/issues/duplicates`, `/issues/recent?radiusKm`, `/issues/{id}/vote`, `/incidents`, `/incidents/heatmap?days&groups`, `/incidents/area`. Corpo do voto `{ "value": "yes"|"no", "lat", "lng" }`.
- [ ] **Em cada sub-task:** traduzir, rodar `dotnet test`, regravar `Openapi_igual_ao_snapshot` e conferir no diff que sumiram/apareceram só nomes (mesmas contagens). `Forma_do_openapi_preservada` fica verde sem regravar. Commit `refactor(<areas>): áreas e contrato em inglês`.
- [ ] **Ao final:** `rtk proxy grep -rniE '\b(problema|incidente|voto|usuario|categoria|grupo|confianca|perfil|pagina|tamanho|endereco)' src/UrbanReports.Api --include='*.cs' | grep -v Migrations` deve listar só strings de dados e mensagens para o usuário (seed, validação). Qualquer identificador que aparecer volta para tradução.

### Task 8: Docs e coleção Bruno

**Files:**
- Modify: `docs/API.md` (rotas, campos, enums e tabela de `type`; prosa em português), `docs/MOTOR_CONFIANCA.md` (nomes e tabela PT → EN no topo), `docs/PLAN.md`, `README.md`, `CLAUDE.md` (árvore, convenções — identificadores em inglês, sem comentários, exceção do Simulator —, variáveis do Railway, comandos com `src/UrbanReports.Api`), `docs/diagramas/*.puml` + `*.png`, `Bruno/**`
- Não mexer: `docs/superpowers/**` (histórico)

- [ ] **Step 1:** atualizar os docs. Regerar os PNG com `docker run --rm -v "$PWD/docs/diagramas:/data" plantuml/plantuml -tpng /data/*.puml`, ou com o jar do PlantUML.
- [ ] **Step 2:** conferir que todo exemplo JSON de `docs/API.md` bate com o `openapi.snapshot.json`: cada campo e cada rota citados existem no snapshot.
- [ ] **Step 3:** commit `docs: API, motor, diagramas e Bruno com os nomes em inglês`.

### Task 9: Verificação final e PR 2

- [ ] **Step 1:** `dotnet build -c Release` (zero avisos) e `dotnet test` (tudo verde). Anotar a quantidade de testes, que tem que ser ≥ a quantidade antes da Task 4 mais os novos.
- [ ] **Step 2:** subir um banco real com os dados antigos. Fazer checkout da `main` em um worktree, `docker compose up -d db`, aplicar as migrations e rodar o seed de desenvolvimento pela API antiga. Depois, nesta branch, `dotnet run --project src/UrbanReports.Api`: a migration aplica e `GET /api/v1/health/database` → `200`. Pelo Bruno: login com usuário de exemplo, `GET /issues` na área do seed, voto e `GET /admin/users` com admin.
- [ ] **Step 3:** push, `gh pr create` com base `refactor/simplificar-projetos`. No corpo, a lista de variáveis do Railway para renomear e o passo de gerar `Jwt__Key` nova (spec §8, §10). **Não mergear.** Ele sobe junto com o PR do front.
- [ ] **Step 4:** revisão final da branch inteira por um revisor independente no modelo mais capaz.
