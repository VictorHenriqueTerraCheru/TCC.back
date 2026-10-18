# CLAUDE.md — API de Ocorrências Urbanas (TCC)

API .NET do TCC "Sistema colaborativo de registro e validação de ocorrências urbanas" (Gustavo Brandão e Victor Henrique Terra Cherú — Sistemas de Informação, Unilasalle-RJ, orientadora Claudia Abreu Paes). O front é uma aplicação web em **React + TypeScript** (mobile-first, responsiva), em outro repositório, e consome esta API pelo navegador.

**O objeto de estudo do TCC é o motor de confiança.** O front é o meio. Tudo que toca em peso de voto, índice de confiança ou ciclo de vida segue `docs/MOTOR_CONFIANCA.md` à risca.

## Documentos deste repo (leia antes de codar)

- `docs/MOTOR_CONFIANCA.md` — fórmulas, parâmetros e casos de teste do motor (normativo)
- `docs/API.md` — contrato HTTP compartilhado com o front (normativo)
- `docs/PLAN.md` — plano de implementação por etapas, com critérios de aceite
- `docs/diagramas/*.puml` — caso de uso, domínio, modelo de dados, sequência, estado, implantação

Se uma tarefa exigir mudar a regra ou o contrato, **pare e pergunte** — essas regras estão escritas no texto do TCC.

## Stack

- .NET 10 (LTS), C# 14, ASP.NET Core **Minimal APIs**
- EF Core 10 + `Npgsql.EntityFrameworkCore.PostgreSQL` + `.NetTopologySuite`; `EFCore.NamingConventions` (snake_case)
- PostgreSQL 16+ com PostGIS — **PostgreSQL + PostGIS no Railway em produção**, `postgis/postgis` em Docker para dev e testes
- ASP.NET Core Identity (`IdentityUser<Guid>` customizado → tabela `usuarios`) + JWT Bearer
- FluentValidation, OpenAPI nativo (`Microsoft.AspNetCore.OpenApi`) + Scalar em `/scalar`
- xUnit + AwesomeAssertions (fork Apache-2.0 do FluentAssertions); Testcontainers (PostGIS) para testes de integração

## Arquitetura: vertical slice por feature (padrão MedComite.Core)

```
Ocorrencias.slnx
src/
  Ocorrencias.Domain/          # motor de confiança + enums + tipos de valor. ZERO dependências (nem EF, nem ASP.NET)
    Confianca/MotorConfianca.cs, ConfiguracaoConfianca.cs, VotoFato.cs, ...
  Ocorrencias.Api/
    Program.cs                 # AddAplicacao() + configurações de borda + pipeline + MapEndpoints()
    Modules/                   # um IEndpointModule por área (descobertos por reflexão): só HTTP
      AuthModule.cs, UsuarioModule.cs, CategoriaModule.cs, GeocodificacaoModule.cs, ProblemaModule.cs,
      VotoModule.cs, IncidenteModule.cs, VerificacaoModule.cs, AdminModule.cs,
      SaudeModule.cs           # /api/v1/saude e /saude/banco (health checks, anônimos)
    Feature/<Area>/            # namespace Ocorrencias.Api.<Area>
      Abstractions/I<Area>Service.cs
      Services/<Area>Service.cs        # regra + EF direto; devolve Resultado<T>
      Models/<Area>Dtos.cs             # requests/responses (nome = nome do schema no OpenAPI)
      Validators/<Area>Validators.cs   # FluentValidation
      Errors/Erros<Area>.cs            # ErroAplicacao com o type de docs/API.md
    Feature/Geocodificacao/Services/   # NominatimClient, ServicoGeocodificacao (cache + 1 req/s)
    Feature/Confianca/                 # RecalculoProblema, ProcessadorCicloDeVida, Jobs/CicloDeVidaJob (15 min)
    Infrastructure/
      DependencyInjections/DependencyInjection.cs   # AddAplicacao: tudo que é interno
      Configurations/AppConfiguration.cs            # CORS, autenticação/policies, rate limit
      Configurations/OpenApiConfig.cs
      Persistence/AppDbContext.cs, Entidades/, Configurations/, Migrations/, SeedDesenvolvimento.cs
    Common/                    # Resultado<T>, ErroAplicacao, IEndpointModule, Validar<T>(), Paginado, Geo, RelogioPostgres, Statics
  Ocorrencias.Simulator/       # console: cenários sintéticos A vs B (referencia SÓ o Domain)
tests/
  Ocorrencias.Domain.Tests/
  Ocorrencias.Api.Tests/       # Esqueleto/ContratoTests + openapi.snapshot.json travam o contrato
  Ocorrencias.Simulator.Tests/
cenarios/base.json
docs/
```

Fluxo: **Module** (valida com `.Validar<T>()`, lê o usuário do token, monta `Location`) → **Service** (EF + Domain, recebe `Guid usuarioId`, devolve `Resultado<T>` com `ErroAplicacao`) → **Module** traduz para o `Results<...>` tipado (o OpenAPI deriva dessa assinatura). Sem MediatR e sem repositório: o service usa o `AppDbContext` direto (o EF já é repositório/unidade de trabalho).

## Regras inegociáveis

1. **O motor de confiança mora no `Ocorrencias.Domain`, é puro e testado.** A API nunca reimplementa fórmula; chama `MotorConfianca`. O Simulator chama as mesmas funções.
2. **O voto guarda fatos, não pesos**: `perfil_no_momento` e `distancia_metros` (nulo = sem GPS). A distância é calculada com `ST_Distance` (geography) e o `lat/lng` do votante é **descartado** — nunca persista nem logue a posição do votante (LGPD, RNF03).
3. **Incidente**: arredonde `lat/lng` para 3 casas **antes** de persistir; nunca exponha `autor_id` em resposta alguma.
4. A configuração ativa da API é a **B** (`ConfiguracaoConfianca.B`), via `IOptions` só para o nome; os valores vêm do Domain.
5. Use `IClock`/`TimeProvider` injetado — nunca `DateTime.Now` no código de negócio (os testes controlam o tempo).
6. Unicidade de voto: índice único `(problema_id, usuario_id, tipo)`; votar de novo faz UPSERT (substitui valor, perfil, distância e `registrado_em`).
7. Toda mudança de voto recalcula `confianca` e avalia o ciclo de vida **na mesma transação**.
8. Enums trafegam como string camelCase (`JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`).
9. **Geocodificação só pela API** (`IServicoGeocodificacao`): Nominatim com `User-Agent` próprio (`OcorrenciasUrbanasTCC/1.0 (+<url do projeto>[; <email>])` — sem User-Agent a resposta é 403; e-mail opcional via `Nominatim__Email`), `accept-language=pt-BR`, no máximo **1 requisição/s** (um `SemaphoreSlim` + intervalo mínimo, ou `System.Threading.RateLimiting`), cache por coordenada em 4 casas por 7 dias. O endereço de problemas e o rótulo de incidentes são calculados **no servidor**, nunca aceitos do cliente. Falha do Nominatim nunca derruba um registro (ver `API.md`).
10. **CORS** restrito às origens de `Cors:Origens` (dev: `http://localhost:5173`; prod: o domínio do front). O front roda em HTTPS, porque o navegador só libera a localização em contexto seguro.

## Convenções

- Código e identificadores em **português** sem acento (`Problema`, `RegistrarProblema`), como no domínio do TCC. Mensagens de erro para o usuário em português.
- `record` para DTOs; `sealed` por padrão; nullable habilitado; `TreatWarningsAsErrors` no Domain.
- Um service por área (`I<Area>Service`, scoped, registrado em `DependencyInjection.AddServicos`), um module por área. Endpoint novo nasce exigindo autenticação (grupo `/api/v1`); visitante só com `AllowAnonymous()` explícito.
- DTOs de contrato são `record`s de topo com o nome do schema no OpenAPI (`ObterDetalheProblemaResponse`). Mudou o OpenAPI? `ContratoTests.Openapi_igual_ao_snapshot` falha; regrave com `ATUALIZAR_SNAPSHOT_OPENAPI=1 dotnet test --filter Openapi_igual_ao_snapshot` **só** se a mudança de contrato foi aprovada (e replique em `docs/API.md` e no front).
- Commits em Conventional Commits, em português (`feat(votos): ...`). Uma branch `etapa-N` + PR por etapa do `PLAN.md`.
- Versões de pacotes centralizadas em `Directory.Packages.props`; SDK fixado em `global.json`.
- Nada de segredos no repo: `appsettings.Development.json` usa Docker local; produção via variáveis de ambiente (`ConnectionStrings__Default`, `Jwt__Chave`, `Cors__Origens__0`, `Nominatim__Email` opcional).

## Comandos

```bash
docker compose up -d db                       # PostGIS local na porta 5432
dotnet ef migrations add <Nome> -p src/Ocorrencias.Api -o Infrastructure/Persistence/Migrations
dotnet ef database update -p src/Ocorrencias.Api
dotnet run --project src/Ocorrencias.Api      # Scalar em http://localhost:5080/scalar
dotnet test
dotnet run -c Release --project src/Ocorrencias.Simulator -- --config A,B,B-v3,B-v5 --variantes S1,S2,S3 --sementes 30 --saida resultados/ --cenario cenarios/base.json
```

## Banco em produção (Railway)

- O serviço de banco precisa ter a extensão PostGIS **instalada** no servidor: use o template de PostGIS do Railway ou um serviço com a imagem `postgis/postgis:16-3.4` (a mesma do `docker-compose.yml`) e um volume em `/var/lib/postgresql/data`. A migration `Inicial` roda `CREATE EXTENSION postgis`, que falha se a extensão não existir na imagem.
- A API lê `ConnectionStrings__Default` no formato chave=valor do Npgsql (não aceita a URL `postgresql://` da `DATABASE_URL`). Monte com as referências do Railway: `Host=${{Postgres.PGHOST}};Port=${{Postgres.PGPORT}};Database=${{Postgres.PGDATABASE}};Username=${{Postgres.PGUSER}};Password=${{Postgres.PGPASSWORD}};GSS Encryption Mode=Disable` (troque `Postgres` pelo nome do serviço). O `GSS Encryption Mode=Disable` evita o erro inofensivo `libgssapi_krb5.so.2` no log (a imagem não tem Kerberos). Dentro do Railway isso usa a rede privada.
- Inicialização (`Persistence/InicializacaoProducao.cs`, só em `Production`): ao subir, a API aplica as migrations pendentes e, se não houver nenhum administrador, cria um com `AdminInicial__Email` e `AdminInicial__Senha` (opcional `AdminInicial__Nome`); se o e-mail já tiver conta, só promove. Os usuários de exemplo (`tcc12345`) **nunca** vão para produção.
- API em container: `Dockerfile` na raiz (escuta na porta 8080). Variáveis: `ConnectionStrings__Default`, `Jwt__Chave` (≥ 32 caracteres, aleatória), `Cors__Origens__0` (domínio do front), `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` (IP real do cliente para o rate limit), `AdminInicial__Email`, `AdminInicial__Senha` e, opcional, `Nominatim__Email`.
- É um PostgreSQL comum: nada de recurso específico de provedor.
