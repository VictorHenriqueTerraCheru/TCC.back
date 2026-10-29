# Inglês e simplificação (back, banco, front) — design

Data: 2026-10-29. Repos: `TCC.back` (este) e `TCC.front`.

## 1. Intenção

Os professores vão abrir os dois repositórios e ler o código. O objetivo é que leiam como um código profissional, fácil de navegar e de seguir etapa por etapa (endpoint → service → motor → banco), **sem precisar de IA para entender**. O código atual já é bom: isto é polimento, não reescrita.

Três alavancas:

1. **Inglês**: código, banco e contrato HTTP em inglês.
2. **Sem comentários**: o nome explica. Saem todos os comentários de código, inclusive `///`.
3. **Menos peças**: menos projetos no back; componentes reaproveitados no front.

## 2. Objetivos, não objetivos, anti-objetivos

**Objetivos**
- Back de 6 para 4 projetos: `Api`, `Simulator`, `Api.Tests`, `Simulator.Tests`.
- Todo identificador de código, nome de arquivo e pasta, tabela, coluna, índice, rota, parâmetro, campo JSON, valor de enum no contrato e `type` de erro em inglês.
- Sem comentários no código de back e de front (C#, TS/TSX, CSS, testes).
- Front: componentes repetidos unificados onde ficar mais simples.
- Docs coerentes com o código: prosa em português, nomes de código em inglês, diagramas regerados.

**Não objetivos**
- **Nenhuma mudança de regra.** Fórmulas, parâmetros e ciclo de vida do motor (`docs/MOTOR_CONFIANCA.md`), LGPD (posição do votante descartada, incidente arredondado em 3 casas, `autor_id` nunca exposto), rate limit, geocodificação, CORS e autenticação ficam idênticos. Só mudam os nomes.
- **Simulator não é refatorado.** Ele só troca a referência `Domain → Api` e os nomes que chama do motor (rename mecânico). Pastas, identificadores internos, CSV de saída, `cenarios/base.json` e o README dele ficam como estão. Os projetos continuam com o nome `Ocorrencias.Simulator` e `Ocorrencias.Simulator.Tests`.
- Texto que o usuário final vê continua em português: telas, mensagens de erro de validação, nomes de categorias e de grupos.
- Dados continuam como estão: nomes de categoria, rótulos e chaves de grupo (`viaPublica`, `seguranca`...), chaves de ícone (`buraco`). São dados que o admin edita, não código.
- Monografia: fora deste trabalho.

**Anti-objetivos (falhou, mesmo se compilar)**
- Perder dados de produção: a migration só **renomeia**, nunca recria tabela nem coluna.
- Qualquer teste perder uma asserção ou deixar de cobrir um caso. Os testes são traduzidos, não reescritos.
- Mudar o comportamento observável além dos nomes.
- Abreviações ou nomes genéricos (`Mgr`, `Helper2`, `data`). O nome precisa substituir o comentário que saiu.

## 3. Estrutura do back

Mantém o padrão MedComite (decisão do usuário), só traduzido:

```
UrbanReports.slnx
src/
  UrbanReports.Api/
    Program.cs
    Modules/                      AuthModule.cs, UserModule.cs, CategoryModule.cs, GroupModule.cs, GeocodingModule.cs,
                                  IssueModule.cs, VoteModule.cs, IncidentModule.cs, VerificationModule.cs, AdminModule.cs, HealthModule.cs
    Feature/<Area>/               Abstractions/ Services/ Models/ Validators/ Errors/   (namespace UrbanReports.Api.<Area>)
    Feature/Trust/
      Engine/                     TrustEngine.cs, TrustConfiguration.cs, VoteFact.cs, TrustResult.cs, IssueState.cs  (ex-Ocorrencias.Domain)
      Services/                   IssueRecalculation.cs, LifecycleProcessor.cs
      Jobs/                       LifecycleJob.cs
      Models/                     LifecycleOptions.cs
    Common/                       Abstractions, Extensions, Helpers, Models (Result<T>, ApplicationError, Paged), Statics, Enums.cs
    Infrastructure/               Configurations/, DependencyInjections/, Persistence/{AppDbContext, Entities/, Configurations/, Migrations/}
  Ocorrencias.Simulator/          referência para UrbanReports.Api
tests/
  UrbanReports.Api.Tests/         inclui Trust/ (ex-Ocorrencias.Domain.Tests)
  Ocorrencias.Simulator.Tests/
```

- `Enums.cs` do Domain vai para `Common/Enums.cs` (são usados por várias áreas).
- O motor continua **puro**: os arquivos de `Feature/Trust/Engine/` não importam EF, ASP.NET nem nada de fora do BCL. Um teste de arquitetura garante isso (via reflexão, sem pacote novo). Esse teste substitui a garantia que o projeto separado dava.
- `TreatWarningsAsErrors`, que só valia no Domain, passa a valer no projeto Api inteiro. Se isso gerar avisos que hoje passam, eles são corrigidos.
- `Feature` (singular) continua igual ao MedComite.

## 4. Glossário

Regra geral: tradução direta para o termo usual em inglês, PascalCase em C#, camelCase em JSON e TS, snake_case no banco. Na dúvida, siga a tabela. Termo novo segue o mesmo estilo.

**Domínio**

| PT | EN |
|---|---|
| Ocorrência (geral) / `TipoOcorrencia` | Report / `ReportType` (`issue`, `incident`) |
| Problema | Issue |
| Incidente | Incident |
| Voto, `ValorVoto` (`Positivo`/`Negativo`) | Vote, `VoteValue` (`Yes`/`No`) |
| `ValorVotoContrato` (`sim`/`nao`) | `VoteAnswer` (`yes`/`no`) |
| Categoria | Category |
| `GrupoCategoria` (`chave`, `rotulo`, `cor`, `ordem`) | `CategoryGroup` (`key`, `label`, `color`, `sortOrder`) |
| `CorGrupo` (Laranja, Ocre, Marrom, Vermelho, Terracota, Azul, VerdeAgua, Roxo, Rosa, Cinza) | `GroupColor` (Orange, Ochre, Brown, Red, Terracotta, Blue, Teal, Purple, Pink, Gray) |
| Usuário | User |
| `Perfil` (Cidadao, CidadaoVerificado, Administrador) | `Role` (Citizen, VerifiedCitizen, Administrator) |
| `perfil_no_momento` | `role_at_vote` |
| `SolicitacaoVerificacao` | `VerificationRequest` |
| `TipoVinculo` (MoradorRegiao, TrabalhaRegiao, ServidorPublico, Outro) | `AffiliationType` (LivesInArea, WorksInArea, PublicServant, Other) |
| `StatusVerificacao` (Pendente, Aprovada, Recusada) | `VerificationStatus` (Pending, Approved, Rejected) |
| `StatusProblema` (Nova, Ativa, PossivelmenteResolvida, Resolvida) | `IssueStatus` (New, Active, PossiblyResolved, Resolved) |
| Confiança, índice de confiança | Trust, `trustScore` |
| `FaixaConfianca` (Baixa, Media, Alta) | `TrustBand` (Low, Medium, High) |
| `NivelConcentracao` | `ConcentrationLevel` (Low, Medium, High) |
| `MotorConfianca` / `ConfiguracaoConfianca` (A, B, B-v3, B-v5) | `TrustEngine` / `TrustConfiguration` (mesmos nomes de config) |
| `VotoFato` / `EstadoProblema` / `ResultadoConfianca` | `VoteFact` / `IssueState` / `TrustResult` |
| Ciclo de vida / `RecalculoProblema` / `ProcessadorCicloDeVida` | Lifecycle / `IssueRecalculation` / `LifecycleProcessor` |
| `ExibicaoConfianca` | `TrustDisplay` |
| Geocodificação / reversa / `EnderecoGeocodificado` / `ServicoGeocodificacao` / `LimitadorDeTaxa` | Geocoding / reverse / `GeocodedAddress` / `GeocodingService` / `RateLimiter` |
| Saúde | Health |
| Histórico | History |
| Duplicatas / Recentes / Calor | Duplicates / Recent / Heatmap |
| endereço, logradouro, número, bairro, cidade, uf, rótulo da área | address, street, number, neighborhood, city, state, areaLabel |
| `disponivel` | `available` |

**Infra e verbos**

| PT | EN |
|---|---|
| `Resultado<T>` / `ErroAplicacao` / `Paginado`, `pagina`, `tamanho`, `itens`, `total` | `Result<T>` / `ApplicationError` / `Paged`, `page`, `pageSize`, `items`, `total` |
| `RelogioPostgres` / `BloqueioDoCatalogo` / `ErrosDeBanco` | `PostgresClock` / `CatalogLock` / `DatabaseErrors` |
| `PoliticasAutorizacao` / `PoliticasCors` / `PoliticasLimite` | `AuthorizationPolicies` / `CorsPolicies` / `RateLimitPolicies` |
| `SeedDesenvolvimento` / `InicializacaoProducao` | `DevelopmentSeed` / `ProductionStartup` |
| `Validar<T>()` / `RegrasValidacao` | `Validate<T>()` / `ValidationRules` |
| `Erros<Area>` | `<Area>Errors` |
| Obter / Listar / Registrar / Atualizar / Cadastrar / Entrar / Solicitar / Aprovar / Recusar / Votar / Excluir | Get / List / Create / Update / Register / Login / Request / Approve / Reject / Vote / Delete |
| DTO `ObterDetalheProblemaResponse` | `GetIssueDetailResponse` (mesma regra: verbo + objeto + `Request`/`Response`) |

**Configuração** (`appsettings`/variáveis): `Cors:Origens` → `Cors:Origins`, `AdminInicial:{Email,Senha,Nome}` → `InitialAdmin:{Email,Password,Name}`, `Jwt:Chave` → `Jwt:Key`, `Nominatim:Email` fica. Os nomes das seções `ConnectionStrings:Default` e `Jwt` continuam. **As variáveis do Railway mudam junto** (ver §8).

## 5. Contrato HTTP

Base continua `/api/v1`. Rotas:

| Antes | Depois |
|---|---|
| `POST /auth/cadastro`, `POST /auth/login` | `POST /auth/register`, `POST /auth/login` |
| `GET/PATCH /me`, `GET /me/historico?filtro=tudo\|registros\|votos` | `GET/PATCH /me`, `GET /me/history?filter=all\|reports\|votes` |
| `GET /categorias`, `GET /grupos` | `GET /categories`, `GET /groups` |
| `GET /problemas?...&grupos&status`, `GET /problemas/{id}`, `GET /problemas/duplicatas`, `POST /problemas`, `GET /problemas/recentes?raioKm` | `GET /issues?...&groups&status`, `GET /issues/{id}`, `GET /issues/duplicates`, `POST /issues`, `GET /issues/recent?radiusKm` |
| `PUT /problemas/{id}/voto` | `PUT /issues/{id}/vote` |
| `POST /incidentes`, `GET /incidentes/calor?dias&grupos`, `GET /incidentes/area` | `POST /incidents`, `GET /incidents/heatmap?days&groups`, `GET /incidents/area` |
| `GET /geocodificacao/reversa` | `GET /geocoding/reverse` |
| `POST /verificacoes` | `POST /verifications` |
| `GET /admin/verificacoes`, `POST .../{id}/aprovar`, `.../{id}/recusar` | `GET /admin/verifications`, `POST .../{id}/approve`, `.../{id}/reject` |
| `GET /admin/usuarios?busca&perfil`, `PATCH /admin/usuarios/{id}/perfil` | `GET /admin/users?search&role`, `PATCH /admin/users/{id}/role` |
| `/admin/categorias`, `PATCH /admin/categorias/{id}/ativa` | `/admin/categories`, `PATCH /admin/categories/{id}/active` |
| `/admin/grupos/{chave}` | `/admin/groups/{key}` |
| `GET /saude`, `GET /saude/banco` | `GET /health`, `GET /health/database` |

`minLat/minLng/maxLat/maxLng/lat/lng` continuam. Paginação: `?page=1&pageSize=20` → `{ items, page, pageSize, total }`.

Erros (`type`): `email-already-registered`, `category-not-issue`, `category-not-incident`, `author-cannot-vote`, `issue-resolved`, `time-outside-window`, `daily-incident-limit`, `verification-already-pending`, `already-verified`, `duplicate-name`, `category-limit-reached`, `group-type-mismatch`, `duplicate-group-label`, `group-in-use`, `verification-already-reviewed`, `own-role`, `rate-limited`, `database-unavailable`. Status HTTP iguais aos de hoje.

Claim do JWT: `perfil` → `role`, com valores do enum em camelCase (`administrator`).

O snapshot `tests/.../openapi.snapshot.json` é regravado **uma vez**, no commit que traduz o contrato. Antes disso, ele trava a fase de mudança de estrutura (que não pode mexer no contrato). `docs/API.md` (os dois exemplares) e `docs/openapi.json` do front são atualizados juntos.

## 6. Banco

- Uma migration nova, `RenameToEnglish`, **escrita à mão** com `RenameTable`, `RenameColumn`, `RenameIndex` e o rename de PK/FK (`ALTER TABLE ... RENAME CONSTRAINT`). Nada de `Drop*`/`Create*`. Depois dela, `dotnet ef migrations has-pending-model-changes` precisa sair limpo. Isso prova que o modelo e o banco batem.
- Tabelas: `usuarios → users`, `usuarios_claims/logins/tokens → user_claims/user_logins/user_tokens`, `problemas → issues`, `incidentes → incidents`, `votos → votes`, `categorias → categories`, `grupos_categoria → category_groups`, `solicitacoes_verificacao → verification_requests`. Colunas, índices e constraints seguem o glossário em snake_case (`confianca → trust_score`, `perfil_no_momento → role_at_vote`, `ordem → sort_order`, `ix_problemas_localizacao → ix_issues_location`...).
- Enums são `smallint` no banco, então **nenhum dado muda**, só nomes.
- As migrations antigas ficam como estão, porque já rodaram em produção. Código gerado fica isento da regra de nomes e de comentários.
- SQL escrito à mão (`FromSql`, `SqlQuery`, `ExecuteSql` em `ProblemaService`, `RecalculoProblema`, `AuthService`, `UsuarioService`, `IncidenteService` e testes) é atualizado para os nomes novos. Os testes de integração com Testcontainers cobrem esse SQL.
- Teste novo: aplicar todas as migrations num banco com dados de exemplo inseridos **antes** da `RenameToEnglish` e conferir que as linhas continuam lá depois.

## 7. Front

- Pastas, arquivos, identificadores, rotas de URL do app (`/registrar → /report`, `/perfil → /profile`...), chaves de `localStorage`/`sessionStorage` (`ocorrencias.* → urbanReports.*`), fixtures e handlers do MSW e testes em inglês. O texto da UI continua em português.
- Tipos regerados do `docs/openapi.json` novo (`npm run gen:api`).
- Sem comentários, inclusive JSDoc.
- **Reaproveitamento de componentes**: fica numa etapa própria, depois da tradução. Primeiro sai uma lista de candidatos (componentes parecidos e o que viraria cada um). **O usuário aprova a lista antes de qualquer junção.** Critério: só une quando o resultado fica mais simples de ler. Componente genérico com muitas props não conta como simplificação. A tela tem que ficar igual (os prints de `npm run prints` servem de comparação).

## 8. Entrega e deploy

Branches e PRs (um por etapa, como sempre):

1. **back `refactor/simplificar-projetos`**: Domain entra na Api, Domain.Tests entra em Api.Tests e o Simulator passa a referenciar a Api. Contrato e banco não mudam (snapshot intacto), então pode ir para produção sozinho.
2. **back `refactor/ingles`**: tradução, sem comentários, migration `RenameToEnglish`, contrato novo, snapshot regravado e docs.
3. **front `refactor/ingles`**: tradução, sem comentários e contrato novo.
4. **front `refactor/componentes`**: reaproveitamento, depois da lista aprovada. Não muda contrato, então vai sozinho.

PRs 2 e 3 são mergeados **juntos**, num horário combinado com o usuário. Antes do merge:
- No Railway, renomear as variáveis da API (`Cors__Origens__0 → Cors__Origins__0`, `AdminInicial__* → InitialAdmin__*`, `Jwt__Chave → Jwt__Key`) e **gerar uma `Jwt__Key` nova**. Assim todo mundo faz login de novo, e não sobra token com o claim antigo.
- O `VITE_API_URL` do front não muda (base `/api/v1`).
- Ao subir, a API aplica a `RenameToEnglish` sozinha (`ProductionStartup`). Ficam alguns minutos fora do ar (aceito pelo usuário).
- Depois: smoke test em produção (login, mapa, detalhe, voto, registro, admin) e `GET /health/database`.

`CLAUDE.md` dos dois repos é atualizado: identificadores em inglês, sem comentários, nova árvore, novos nomes de variáveis.

## 9. Fica para o builder

- Nomes não listados no glossário, seguindo o mesmo estilo.
- Ordem interna dos commits dentro de cada PR, desde que cada commit compile e os testes passem.
- Como fazer o rename em massa (rename do IDE/Roslyn, scripts). O que vale é o resultado.

## 10. Escolhas minhas (para o usuário conferir)

- Nome da solution e do namespace: `UrbanReports`.
- `Problema → Issue` (e não "Problem"). `Ocorrência → Report`.
- `TipoVinculo → AffiliationType`.
- O Simulator mantém o nome `Ocorrencias.Simulator` e os identificadores internos em português, já que não será refatorado agora.
- Rotas de URL do front também em inglês. Links antigos salvos (ex.: `/problemas/{id}`) deixam de abrir.
- Rotacionar `Jwt__Key` no deploy (todos fazem login de novo).
- Migrations antigas intocadas.
- `TreatWarningsAsErrors` no projeto Api inteiro.
- **CSV do Simulator idêntico byte a byte.** Onde ele escreve valor de enum do motor (ex.: `status_fim_ataque` via `ToString()`), ganha um mapeamento local para o texto antigo (`resolvida`, `ativa`...). Assim os resultados que a monografia já usa continuam reproduzíveis. Os testes de determinismo do Simulator travam isso: rodar com a mesma semente antes e depois do refactor dá o mesmo CSV.
- **Simulator e Simulator.Tests mantêm os comentários.** São exceção explícita à regra "sem comentários", registrada no `CLAUDE.md`.
- **Variáveis de configuração**: todas as seções de `*Options` seguem o glossário (`Jwt:Emissor/Audiencia → Jwt:Issuer/Audience`, `Confianca:Configuracao → Trust:Configuration`, opções do ciclo de vida etc.). Antes do merge, a lista real de variáveis do serviço `api` no Railway é conferida, e cada uma que existir é renomeada. Pelo `CLAUDE.md`, hoje são `ConnectionStrings__Default`, `Jwt__Chave`, `Cors__Origens__0`, `ASPNETCORE_FORWARDEDHEADERS_ENABLED`, `AdminInicial__*` e `Nominatim__Email`.
- **Alcance dos docs**: são atualizados os docs vivos (`CLAUDE.md`, `README`, `docs/API.md`, `docs/MOTOR_CONFIANCA.md`, `docs/PLAN.md`, diagramas, `Bruno/` e, no front, `docs/PLAN.md` e `docs/TELAS.md`). Os históricos em `docs/superpowers/` ficam como estão, porque são registro de como o trabalho foi feito. O `MOTOR_CONFIANCA.md` ganha uma tabela PT → EN no topo (`perfil_no_momento → role_at_vote`, `MotorConfianca → TrustEngine`...), para quem lê a monografia achar cada coisa no código.
