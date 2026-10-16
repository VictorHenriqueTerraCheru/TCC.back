# Refatoração da arquitetura da API — padrão MedComite.Core

**Data:** 2026-10-16 · **Branch:** `refactor-arquitetura` (a partir de `etapa-7`) · **Status:** aprovado em conversa, aguardando revisão do spec

## 1. Objetivo

Reorganizar `src/Ocorrencias.Api` no padrão de vertical slice do projeto de referência
`MedComite.Core` (pasta por feature com `Abstractions/Services/Models/Validators/Errors`, endpoints em
`Modules/` e registro de dependências em `Infrastructure/`), resolvendo três incômodos:

1. Registros de serviço espalhados: 7 extensões `AddXxx` em pastas diferentes, mais 5 registros soltos no `Program.cs`.
2. `Features/Endpoints.cs` lista os 20 `MapXxx` à mão num arquivo central.
3. Cada caso de uso junta num arquivo só `Request`, `Response`, `Validator`, handler (com regra e acesso a dados) e `MapXxx`.

## 2. Restrições (o que NÃO muda)

- **Contrato HTTP idêntico.** Rotas, métodos, status, corpos, `type` dos erros, autenticação por endpoint, rate limit e
  headers (`Location`, `Retry-After`) continuam iguais. `docs/API.md` não muda.
- **OpenAPI semanticamente idêntico.** O `openapi.json` gerado depois da refatoração, canonicalizado (chaves ordenadas),
  é igual ao de antes. Só a ordem dos paths pode mudar. **Regra decorrente:** cada DTO desaninhado recebe exatamente o
  nome atual do seu schema (`ObterDetalheProblema.Response` → `ObterDetalheProblemaResponse`), porque o front gera os
  tipos desse arquivo.
- **Domain, Simulator, motor e banco ficam intactos.** Não muda nenhum arquivo em `src/Ocorrencias.Domain` nem em `src/Ocorrencias.Simulator`.
  Não há migration nova; as migrations existentes só trocam de pasta e de namespace.
- **Regras inegociáveis do `CLAUDE.md`** continuam valendo: voto guarda fatos, posição do votante descartada,
  `autor_id` nunca exposto, `TimeProvider` injetado, recálculo e ciclo de vida na mesma transação do voto, geocodificação só pelo servidor.
- **Identificadores em português.** As pastas usam os nomes do MedComite (`Feature`, `Modules`, `Infrastructure`,
  `Common`); classes, métodos e mensagens continuam em português.
- **Sem camada de repositório.** O service usa o `AppDbContext` direto: o EF já faz o papel de repositório e de unidade de trabalho.

## 3. Estrutura de pastas

```
src/Ocorrencias.Api/
  Program.cs
  Modules/
    AuthModule.cs  UsuarioModule.cs  CategoriaModule.cs  GeocodificacaoModule.cs  ProblemaModule.cs
    VotoModule.cs  IncidenteModule.cs  VerificacaoModule.cs  AdminModule.cs
  Feature/
    <Area>/
      Abstractions/I<Area>Service.cs
      Services/<Area>Service.cs
      Models/<Area>Dtos.cs            # requests, responses e enums de contrato da área
      Validators/<Area>Validators.cs
      Errors/Erros<Area>.cs
    Auth/Services/JwtTokenService.cs (+ JwtOptions em Models)
    Geocodificacao/Services/NominatimClient.cs, ServicoGeocodificacao.cs, LimitadorDeTaxa.cs
    Geocodificacao/Models/NominatimOptions.cs, NominatimResposta.cs, EnderecoGeocodificado.cs
    Confianca/Services/RecalculoProblema.cs, ProcessadorCicloDeVida.cs
    Confianca/Models/CicloDeVidaOptions.cs
    Confianca/Jobs/CicloDeVidaJob.cs
  Infrastructure/
    DependencyInjections/DependencyInjection.cs
    Configurations/AppConfiguration.cs
    Configurations/OpenApiConfig.cs
    Persistence/AppDbContext.cs, SeedDesenvolvimento.cs
    Persistence/Entidades/*.cs
    Persistence/Configurations/*.cs
    Persistence/Migrations/*.cs
  Common/
    Abstractions/IEndpointModule.cs
    Extensions/ClaimsPrincipalExtensions.cs, Validacao.cs, RegrasValidacao.cs,
               EndpointRouteBuilderExtensions.cs, ErroAplicacaoExtensions.cs
    Models/ErroAplicacao.cs, Resultado.cs, Paginacao.cs
    Statics/PoliticasAutorizacao.cs, PoliticasCors.cs, PoliticasLimite.cs
    Helpers/Geo.cs, RelogioPostgres.cs, JsonConfig.cs
```

Uma área só ganha a subpasta que vai usar: `Categorias` não tem `Errors` nem `Validators`, por exemplo.

**Namespaces:** um namespace curto por feature, que vale para todas as subpastas: `Ocorrencias.Api.Problemas`,
`Ocorrencias.Api.Votos`, etc. Para o resto:

| Pasta | Namespace |
|---|---|
| `Modules/` | `Ocorrencias.Api.Modules` |
| `Infrastructure/Persistence/**` | `Ocorrencias.Api.Persistence` |
| `Infrastructure/DependencyInjections` e `Configurations` | `Ocorrencias.Api.Infrastructure` |
| `Common/**` | `Ocorrencias.Api.Common` |

## 4. Mapeamento dos arquivos atuais

| Hoje | Depois |
|---|---|
| `Features/Endpoints.cs` | removido → `Common/Extensions/EndpointRouteBuilderExtensions.MapEndpoints` + `Modules/*` |
| `Features/Auth/Cadastrar.cs`, `Entrar.cs`, `RespostaAuth.cs` | `Feature/Auth` → `IAuthService.CadastrarAsync/EntrarAsync`; `AuthModule` |
| `Features/Usuarios/ObterMe.cs`, `AtualizarMe.cs`, `ObterHistorico.cs` | `Feature/Usuarios` → `IUsuarioService.ObterMeAsync/AtualizarMeAsync/ObterHistoricoAsync`; `UsuarioModule` |
| `Features/Categorias/Listar.cs`, `CategoriaResumo.cs` | `Feature/Categorias` → `ICategoriaService.ListarAsync`; `CategoriaModule` |
| `Features/Geocodificacao/Reversa.cs` | `GeocodificacaoModule` (chama o `IServicoGeocodificacao` que já existe; não há service novo) |
| `Features/Problemas/*` (5 casos + `ExibicaoConfianca`) | `Feature/Problemas` → `IProblemaService.RegistrarAsync/ObterDetalheAsync/BuscarDuplicatasAsync/ListarNaAreaAsync/ListarRecentesAsync`; `ExibicaoConfianca` em `Services/`; `ProblemaModule` |
| `Features/Votos/Votacao.cs`, `VotarExistencia.cs`, `VotarPersistencia.cs`, `ValoresVoto.cs` | `Feature/Votos` → `IVotoService.VotarExistenciaAsync/VotarPersistenciaAsync` (núcleo comum privado); enums em `Models/`; `VotoModule` |
| `Features/Incidentes/*` | `Feature/Incidentes` → `IIncidenteService.RegistrarAsync/ObterCalorAsync/ObterAreaAsync`; `IncidenteModule` |
| `Features/Verificacao/Solicitar.cs` | `Feature/Verificacao` → `IVerificacaoService.SolicitarAsync`; `VerificacaoModule` |
| `Features/Admin/*` (`Analise.cs` incluído) | `Feature/Admin` → `IAdminService.ListarVerificacoesAsync/AprovarAsync/RecusarAsync`; `AdminModule` |
| `Infra/Auth/AuthConfig.cs` | `AppConfiguration.AddAutenticacao`; constantes `Autenticado`/`Administrador` → `Common/Statics/PoliticasAutorizacao` |
| `Infra/Auth/JwtTokenService.cs`, `JwtOptions.cs` | `Feature/Auth/Services`, `Feature/Auth/Models` |
| `Infra/Data/**` | `Infrastructure/Persistence/**` |
| `Infra/Data/DadosConfig.cs` | `DependencyInjection.AddPersistencia` (privado) |
| `Infra/Geocodificacao/*` | `Feature/Geocodificacao/{Services,Models}`; `GeocodificacaoConfig` → `DependencyInjection.AddGeocodificacao` |
| `Infra/Confianca/*`, `Infra/Jobs/CicloDeVidaJob.cs` | `Feature/Confianca/{Services,Models,Jobs}`; `ConfiancaConfig` → `DependencyInjection.AddConfianca` |
| `Common/CorsConfig.cs`, `LimitesDeRequisicao.cs` | `AppConfiguration.AddCorsConfigurado`, `AppConfiguration.AddLimitesDeRequisicao`; constantes → `Common/Statics/PoliticasCors.Front`, `PoliticasLimite.Geocodificacao` |
| `Common/OpenApiConfig.cs` | `Infrastructure/Configurations/OpenApiConfig.cs` |
| `Common/Erros.cs` | removido → `Feature/<Area>/Errors/Erros<Area>.cs` |
| `Common/{ClaimsPrincipalExtensions,Validacao,RegrasValidacao}.cs` | `Common/Extensions/` |
| `Common/{Geo,RelogioPostgres,JsonConfig}.cs` | `Common/Helpers/` |
| `Common/Paginacao.cs` | `Common/Models/` |

O mapeamento dos erros segue a feature dona:

| Arquivo | Erros |
|---|---|
| `ErrosAuth` | `EmailJaCadastrado`, `CredenciaisInvalidas` |
| `ErrosProblema` | `CategoriaInexistente`, `CategoriaNaoEProblema` |
| `ErrosVoto` | `AutorNaoVotaExistencia`, `PersistenciaIndisponivelEmNova`, `ProblemaResolvido` |
| `ErrosIncidente` | `CategoriaNaoEIncidente`, `HorarioForaDaJanela`, `LimiteDiarioIncidentes` (e o erro de campo de categoria) |
| `ErrosVerificacao` | `VerificacaoPendenteExistente`, `JaVerificado` |
| `ErrosAdmin` | `VerificacaoJaAnalisada` |

Os erros comuns (`NaoEncontrado`, `NaoAutorizado`) ficam como fábricas em `ErroAplicacao`.

## 5. Fluxo de uma requisição

```
HTTP → Module (filtro Validar<T>, lê usuário do ClaimsPrincipal)
     → I<Area>Service (EF + Domain; devolve Resultado<T>)
     → Module traduz Resultado<T> → Results<...> tipado de hoje → HTTP
```

### 5.1 Tipos comuns (`Common/Models`)

```csharp
public enum TipoErro { NaoEncontrado, NaoAutorizado, Campo, Regra }

public sealed record ErroAplicacao(TipoErro Tipo, int Status, string? Type, string? Title, string? Campo = null)
{
    public static ErroAplicacao NaoEncontrado { get; } = new(TipoErro.NaoEncontrado, 404, null, null);
    public static ErroAplicacao NaoAutorizado { get; } = new(TipoErro.NaoAutorizado, 401, null, null);
    public static ErroAplicacao DeCampo(string campo, string mensagem) => new(TipoErro.Campo, 400, null, mensagem, campo);
    public static ErroAplicacao Regra(int status, string? type, string title) => new(TipoErro.Regra, status, type, title);
}

public readonly struct Resultado<T>
{
    public T? Valor { get; }
    public ErroAplicacao? Erro { get; }
    public bool Sucesso => Erro is null;
    public static implicit operator Resultado<T>(T valor);
    public static implicit operator Resultado<T>(ErroAplicacao erro);
}
```

Operações sem valor de retorno, como aprovar e recusar, usam `Resultado<Vazio>`, com `readonly record struct Vazio` em `Common/Models`.

### 5.2 Tradução para HTTP (`Common/Extensions/ErroAplicacaoExtensions`)

| Método | Saída | Equivale hoje a |
|---|---|---|
| `ParaProblema()` | `ProblemHttpResult` | `TypedResults.Problem(statusCode, type, title)` |
| `ParaValidacao()` | `ValidationProblem` | `Erros.Campo(campo, mensagem)` |
| `ParaNaoEncontrado()` | `NotFound` | `TypedResults.NotFound()` |
| `ParaNaoAutorizado()` | `UnauthorizedHttpResult` | `TypedResults.Unauthorized()` |

Os corpos e os status precisam sair byte a byte iguais aos de hoje.

### 5.3 Module

- Cada module implementa `IEndpointModule` (`static abstract void Map(IEndpointRouteBuilder api)`).
- `MapEndpoints()` cria o grupo `/api/v1` com `RequireAuthorization(PoliticasAutorizacao.Autenticado)`, encontra os módulos por
  reflexão no assembly e chama `Map(grupo)` em cada um. A autenticação continua sendo o padrão; endpoints de visitante
  seguem com `AllowAnonymous()` no próprio module.
- A assinatura de cada handler mantém exatamente o `Results<...>` atual. O OpenAPI deriva as respostas dela.
- O module só cuida de HTTP: extrai `usuarioId` do `ClaimsPrincipal`, monta o `Location` do 201, aplica
  `.Validar<T>()`, `.WithTags()`, rate limit e `AllowAnonymous()` como hoje, e traduz o erro com um `switch` sobre `Erro.Tipo`.

```csharp
private static async Task<Results<Created<ObterDetalheProblemaResponse>, ValidationProblem, ProblemHttpResult>> Registrar(
    RegistrarProblemaRequest request, ClaimsPrincipal principal, IProblemaService service, CancellationToken ct)
{
    var r = await service.RegistrarAsync(request, principal.ObterUsuarioId(), ct);
    return r.Erro switch
    {
        null => TypedResults.Created($"/api/v1/problemas/{r.Valor!.Id}", r.Valor),
        { Tipo: TipoErro.Campo } e => e.ParaValidacao(),
        var e => e.ParaProblema(),
    };
}
```

### 5.4 Service

- É `sealed`, registrado como scoped, com construtor primário e dependências do jeito que já são injetadas hoje
  (`AppDbContext`, `TimeProvider`, `ConfiguracaoConfianca`, `IServicoGeocodificacao`, `UserManager<Usuario>`, `JwtTokenService`, ...).
- Recebe `Guid usuarioId`, nunca `ClaimsPrincipal` ou `HttpContext`. Não referencia `Microsoft.AspNetCore.Http.HttpResults`.
- **Dependência entre services:** a consulta do detalhe recalculado (hoje `ObterDetalheProblema.ConsultarAsync`) passa a
  ser `IProblemaService.ObterDetalheAsync`. O `IVotoService` injeta `IProblemaService`. Os dois usam o mesmo
  `AppDbContext` scoped, então a transação do voto (`FOR UPDATE` → UPSERT → recálculo → commit) continua num único
  método do `VotoService`, sem mudança de semântica.
- `RecalculoProblema` e `ProcessadorCicloDeVida` continuam como estão hoje (classes concretas da feature Confiança),
  só que em outra pasta.

## 6. DI e `Program.cs`

### 6.1 `Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAplicacao(builder.Configuration)
    .AddCorsConfigurado(builder.Configuration)
    .AddOpenApiConfigurado()
    .AddAutenticacao()
    .AddLimitesDeRequisicao(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(PoliticasCors.Front);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapHealthChecks("/health");
app.MapEndpoints();

if (app.Environment.IsDevelopment())
{
    await app.InicializarBancoDesenvolvimentoAsync();
}

await app.RunAsync();
```

A ordem do pipeline é a mesma de hoje.

### 6.2 `Infrastructure/DependencyInjections/DependencyInjection.cs`

O único método público é `AddAplicacao(this IServiceCollection, IConfiguration)`. Ele encadeia, nesta ordem, os métodos privados:

| Método | O que faz |
|---|---|
| `AddTratamentoDeErros()` | `AddProblemDetails()` |
| `AddJson()` | `ConfigureHttpJsonOptions(JsonConfig.Configurar)` |
| `AddRelogio()` | `TimeProvider` → `RelogioPostgres` |
| `AddValidacao()` | `Validacao.ConfigurarGlobal()` + `AddValidatorsFromAssemblyContaining<Program>()` |
| `AddPersistencia(config)` | conteúdo atual de `AddDados`, incluindo `AddHealthChecks().AddDbContextCheck<AppDbContext>()` |
| `AddGeocodificacao()` | conteúdo atual |
| `AddConfianca(config)` | conteúdo atual (options, configuração ativa, `CicloDeVidaJob`) |
| `AddServicos()` | lista explícita dos `AddScoped<IXxxService, XxxService>()`, sem varredura do assembly |

### 6.3 `Infrastructure/Configurations/AppConfiguration.cs`

- `AddCorsConfigurado(config)`: política `PoliticasCors.Front` (valor `"Front"`, inalterado).
- `AddAutenticacao()`: JWT Bearer com `LifetimeValidator` usando `TimeProvider`, as policies de `PoliticasAutorizacao` (`Autenticado`, `Administrador`).
- `AddLimitesDeRequisicao(config)`.

O OpenAPI fica em `Infrastructure/Configurations/OpenApiConfig.cs`, com o conteúdo atual (`AddOpenApiConfigurado`, `NomeSchema`, transformers).

## 7. Testes e verificação

1. **Antes de qualquer mudança:** gerar `openapi.json` da `etapa-7` (`GET /openapi/v1.json` via teste ou
   `dotnet run`) e salvar como referência no scratchpad, fora do repo.
2. **Suítes existentes verdes, sem mudar asserções:** 149 da API, 71 do Domain e 42 do Simulator. Nos testes só mudam
   `using`s e nomes de tipo (por exemplo, `ObterHistorico.Item` → `ObterHistoricoItem`).
3. **Testes novos:**
   - **Módulos:** `MapEndpoints` registra todas as rotas atuais. Compara o conjunto (método + rota) com uma lista fixa das 21 operações de `docs/API.md`.
   - **`ErroAplicacao` → HTTP:** status, `type`, `title` e o dicionário `errors` do `ValidationProblem` iguais aos de hoje.
4. **OpenAPI:** depois da refatoração, gerar de novo e comparar com a referência, ambos canonicalizados (`json.dumps(sort_keys=True)` e paths ordenados). **A diferença tem que ser vazia.**
5. **CI verde** na branch.

## 8. Documentação

- **`CLAUDE.md`:** a seção "Arquitetura" passa a descrever esta estrutura (árvore + fluxo Module → Service → `Resultado<T>`).
  Na seção "Convenções", sai "cada arquivo de feature contém Request/Response/Validator/handler/MapXxx… o handler usa o
  `AppDbContext` direto" e entra "service por área usa o `AppDbContext` direto; sem repositório; DTO com o nome do schema no OpenAPI".
- **`docs/PLAN.md`:** atualizar os caminhos citados (`Infra/Geocodificacao` → `Feature/Geocodificacao`, `Infra/Jobs/CicloDeVidaJob` → `Feature/Confianca/Jobs/CicloDeVidaJob`).
- **`docs/API.md`, `docs/MOTOR_CONFIANCA.md`, diagramas:** não mudam.
- **`TCC.front/docs/openapi.json`:** só é regenerado se a ordem dos paths mudar. O conteúdo é semanticamente o mesmo, então é opcional.

## 9. Entrega

- Um PR, `refactor-arquitetura` → `etapa-7`, que entra na pilha depois do #9.
- Commits pequenos em Conventional Commits, um por fase do plano de implementação. Exemplos:
  `refactor(infra): centraliza registro de dependências`, `refactor(problemas): extrai service e module`.
- O build e os testes têm que passar a cada commit, para dar para revisar fase por fase.

## 10. Fora de escopo

- Camada de repositório, MediatR e mudança de ORM.
- Mudança em regra de negócio, contrato, banco ou motor.
- Renomear identificadores para inglês.
- Logs novos nos services. Se entrar algum depois, a regra de LGPD continua: nunca registrar posição de votante.
