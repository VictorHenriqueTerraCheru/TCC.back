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
- PostgreSQL 16+ com PostGIS — **Supabase em produção**, `postgis/postgis` em Docker para dev e testes
- ASP.NET Core Identity (`IdentityUser<Guid>` customizado → tabela `usuarios`) + JWT Bearer
- FluentValidation, OpenAPI nativo (`Microsoft.AspNetCore.OpenApi`) + Scalar em `/scalar`
- xUnit + AwesomeAssertions (fork Apache-2.0 do FluentAssertions); Testcontainers (PostGIS) para testes de integração

## Arquitetura: Vertical Slice

```
Ocorrencias.slnx
src/
  Ocorrencias.Domain/          # motor de confiança + enums + tipos de valor. ZERO dependências (nem EF, nem ASP.NET)
    Confianca/MotorConfianca.cs, ConfiguracaoConfianca.cs, VotoFato.cs, ...
  Ocorrencias.Api/
    Program.cs
    Features/                  # uma pasta por área, um arquivo por caso de uso
      Auth/Cadastrar.cs, Entrar.cs
      Usuarios/ObterMe.cs, AtualizarMe.cs, ObterHistorico.cs
      Categorias/Listar.cs
      Problemas/ListarNaArea.cs, ObterDetalhe.cs, BuscarDuplicatas.cs, Registrar.cs, ListarRecentes.cs
      Votos/VotarExistencia.cs, VotarPersistencia.cs
      Incidentes/Registrar.cs, ObterCalor.cs, ObterArea.cs
      Verificacao/Solicitar.cs
      Geocodificacao/Reversa.cs
      Admin/ListarVerificacoes.cs, Aprovar.cs, Recusar.cs
    Infra/
      Data/AppDbContext.cs, Configurations/*.cs, Migrations/
      Auth/JwtTokenService.cs
      Jobs/CicloDeVidaJob.cs     # BackgroundService + PeriodicTimer (15 min)
      Geocodificacao/NominatimClient.cs, ServicoGeocodificacao.cs  # HttpClient tipado + IMemoryCache + fila de 1 req/s
    Common/                      # ProblemDetails, extensões de endpoint, IClock
  Ocorrencias.Simulator/         # console: cenários sintéticos A vs B (referencia SÓ o Domain)
tests/
  Ocorrencias.Domain.Tests/
  Ocorrencias.Api.Tests/
  Ocorrencias.Simulator.Tests/
cenarios/base.json             # parâmetros do cenário do Simulator (ver src/Ocorrencias.Simulator/README.md)
docs/
```

Cada arquivo de feature contém: `record Request`, `record Response`, `Validator` (FluentValidation), o handler (método estático ou classe) e a extensão `MapXxx(this IEndpointRouteBuilder)`. Sem MediatR, sem repositórios genéricos: o handler usa o `AppDbContext` direto.

## Regras inegociáveis

1. **O motor de confiança mora no `Ocorrencias.Domain`, é puro e testado.** A API nunca reimplementa fórmula; chama `MotorConfianca`. O Simulator chama as mesmas funções.
2. **O voto guarda fatos, não pesos**: `perfil_no_momento` e `distancia_metros` (nulo = sem GPS). A distância é calculada com `ST_Distance` (geography) e o `lat/lng` do votante é **descartado** — nunca persista nem logue a posição do votante (LGPD, RNF03).
3. **Incidente**: arredonde `lat/lng` para 3 casas **antes** de persistir; nunca exponha `autor_id` em resposta alguma.
4. A configuração ativa da API é a **B** (`ConfiguracaoConfianca.B`), via `IOptions` só para o nome; os valores vêm do Domain.
5. Use `IClock`/`TimeProvider` injetado — nunca `DateTime.Now` no código de negócio (os testes controlam o tempo).
6. Unicidade de voto: índice único `(problema_id, usuario_id, tipo)`; votar de novo faz UPSERT (substitui valor, perfil, distância e `registrado_em`).
7. Toda mudança de voto recalcula `confianca` e avalia o ciclo de vida **na mesma transação**.
8. Enums trafegam como string camelCase (`JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`).
9. **Geocodificação só pela API** (`IServicoGeocodificacao`): Nominatim com `User-Agent` próprio (`OcorrenciasUrbanasTCC/1.0 (<email de contato>)`), `accept-language=pt-BR`, no máximo **1 requisição/s** (um `SemaphoreSlim` + intervalo mínimo, ou `System.Threading.RateLimiting`), cache por coordenada em 4 casas por 7 dias. O endereço de problemas e o rótulo de incidentes são calculados **no servidor**, nunca aceitos do cliente. Falha do Nominatim nunca derruba um registro (ver `API.md`).
10. **CORS** restrito às origens de `Cors:Origens` (dev: `http://localhost:5173`; prod: o domínio do front). O front roda em HTTPS, porque o navegador só libera a localização em contexto seguro.

## Convenções

- Código e identificadores em **português** sem acento (`Problema`, `RegistrarProblema`), como no domínio do TCC. Mensagens de erro para o usuário em português.
- `record` para DTOs; `sealed` por padrão; nullable habilitado; `TreatWarningsAsErrors` no Domain.
- Commits em Conventional Commits, em português (`feat(votos): ...`). Uma branch `etapa-N` + PR por etapa do `PLAN.md`.
- Versões de pacotes centralizadas em `Directory.Packages.props`; SDK fixado em `global.json`.
- Nada de segredos no repo: `appsettings.Development.json` usa Docker local; produção via variáveis de ambiente (`ConnectionStrings__Default`, `Jwt__Chave`, `Cors__Origens__0`, `Nominatim__Email`).

## Comandos

```bash
docker compose up -d db                       # PostGIS local na porta 5432
dotnet ef migrations add <Nome> -p src/Ocorrencias.Api
dotnet ef database update -p src/Ocorrencias.Api
dotnet run --project src/Ocorrencias.Api      # Scalar em http://localhost:5080/scalar
dotnet test
dotnet run -c Release --project src/Ocorrencias.Simulator -- --config A,B,B-v3,B-v5 --variantes S1,S2,S3 --sementes 30 --saida resultados/ --cenario cenarios/base.json
```

## Supabase (produção)

- Ative a extensão: `create extension if not exists postgis;` (Database → Extensions).
- Conecte pela conexão direta ou pelo **Session pooler** (porta 5432). Evite o *Transaction pooler* (porta 6543): ele não preserva estado de sessão e costuma dar problema com Npgsql/EF (migrations, prepared statements).
- O Supabase é **só banco**: não use Supabase Auth, RLS nem Edge Functions.
