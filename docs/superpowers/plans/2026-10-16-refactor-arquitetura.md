# Refatoração da arquitetura (padrão MedComite.Core) — plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** reorganizar `src/Ocorrencias.Api` em `Feature/<Area>/{Abstractions,Services,Models,Validators,Errors}` + `Modules/` + `Infrastructure/`, sem mudar nenhum comportamento HTTP nem o OpenAPI.

**Architecture:** primeiro uma rede de segurança: snapshot do OpenAPI, tabela de rotas/políticas e testes de Location/401/404. Depois vêm os tipos base (`Resultado<T>`, `ErroAplicacao`, `IEndpointModule`), a mudança de pastas da infra e a centralização do DI. Por fim, as áreas migram uma a uma do arquivo único por caso de uso (`Features/`) para service + module. Durante a transição, `MapEndpoints()` registra os módulos novos e, logo depois, chama `MapFeaturesLegadas()` com o que ainda não migrou.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, EF Core 10 + Npgsql/NetTopologySuite, FluentValidation, Microsoft.AspNetCore.OpenApi 10 (Microsoft.OpenApi 2.x), xUnit + AwesomeAssertions, Testcontainers (Docker precisa estar rodando para `tests/Ocorrencias.Api.Tests`).

**Spec:** `docs/superpowers/specs/2026-10-16-refactor-arquitetura-design.md`

## Global Constraints

- **Contrato HTTP idêntico:** rotas, métodos, status, corpos, `type`/`title` dos erros, autenticação por endpoint, rate limit, `Location` e `Retry-After`. `docs/API.md` não muda.
- **OpenAPI semanticamente idêntico.** Cada DTO desaninhado recebe exatamente o nome atual do schema (ex.: `ObterDetalheProblema.Response` → `ObterDetalheProblemaResponse`). O snapshot da Task 1 é o árbitro.
- **Domain e Simulator intocados.** Nenhum arquivo muda em `src/Ocorrencias.Domain` nem em `src/Ocorrencias.Simulator`. Não há migration nova; as existentes só trocam de pasta e de namespace.
- **Regras inegociáveis do `CLAUDE.md`:**
  - o voto guarda fatos e a posição do votante é descartada;
  - `autor_id` nunca é exposto;
  - `TimeProvider` sempre injetado;
  - recálculo e ciclo de vida acontecem na mesma transação do voto;
  - geocodificação só pelo servidor.
- **Nomes:** identificadores em português. Pastas `Feature`, `Modules`, `Infrastructure`, `Common`.
- **Namespaces:**

  | Pasta | Namespace |
  |---|---|
  | `Feature/<Area>/**` | `Ocorrencias.Api.<Area>` |
  | `Modules/` | `Ocorrencias.Api.Modules` |
  | `Infrastructure/Persistence/**` | `Ocorrencias.Api.Persistence` |
  | `Infrastructure/DependencyInjections`, `Infrastructure/Configurations` | `Ocorrencias.Api.Infrastructure` |
  | `Common/**` | `Ocorrencias.Api.Common` |

  **Exceção:** as migrations ficam em `Ocorrencias.Api.Infrastructure.Persistence.Migrations`. O `dotnet ef` deduz a pasta de novas migrations pelo namespace do snapshot, e esse namespace casa com a pasta real.
- **Sem repositório:** o service usa o `AppDbContext` direto. Sem MediatR.
- **Service:**
  - devolve `Resultado<T>` quando tem caminho de erro; quando não pode falhar, devolve o tipo puro (`List<T>`, `Paginado<T>`);
  - recebe `Guid usuarioId`, nunca `ClaimsPrincipal`/`HttpContext`;
  - não referencia `Microsoft.AspNetCore.Http.HttpResults`.
- **Module:**
  - mantém exatamente a assinatura `Results<...>` atual de cada handler;
  - aplica `.Validar<T>()`, `.AllowAnonymous()`, `.RequireAuthorization(...)`, `.RequireRateLimiting(...)`, `.ProducesProblem(...)` e `.WithTags(...)` como hoje.
- **Comentários `/// <summary>`:** acompanham o código para onde ele vai (handler → método do service).
- **Commits:** Conventional Commits em português, um por task. Build e testes verdes em todo commit.

## Review Focus

1. **`Location` dos 201.** O do incidente monta `lat`/`lng` arredondados em cultura invariante: com cultura pt-BR, uma vírgula quebraria a URL. Cadastro e verificação apontam para `/api/v1/me`. → Task 1 (`Location_*`).
2. **Política por rota ao mover para módulos.** Esquecer `AllowAnonymous()` ou `RequireAuthorization(Administrador)` muda quem acessa. → Task 1 (`Rotas_e_politicas_do_contrato`).
3. **Token válido de usuário removido** precisa continuar dando 401 (e não 500 ou 404) em `GET/PATCH /me`, `POST /verificacoes` e no voto. → Task 1 (`Token_de_usuario_removido_recebe_401`).
4. **Id inexistente** precisa continuar dando 404 sem corpo de regra em detalhe, nos dois votos e em aprovar/recusar. → Task 1 (`Recursos_inexistentes_devolvem_404`).
5. **Deriva do modelo EF** ao trocar o namespace das entidades: o snapshot de migrations guarda o nome CLR completo. → Task 1 (`Modelo_do_ef_nao_tem_mudancas_pendentes`) + Task 3 (`dotnet ef migrations has-pending-model-changes`).

---

## Mapa de renomeações de tipos (usado pelas Tasks 5–13 e pelos testes)

| Hoje | Depois | Namespace novo |
|---|---|---|
| `ListarCategorias.Response` | `ListarCategoriasResponse` | `Ocorrencias.Api.Categorias` |
| `CategoriaResumo` | `CategoriaResumo` | `Ocorrencias.Api.Categorias` |
| `Cadastrar.Request` / `Entrar.Request` | `CadastrarRequest` / `EntrarRequest` | `Ocorrencias.Api.Auth` |
| `RespostaAuth` (+ `RespostaAuth.UsuarioResumo`) | igual | `Ocorrencias.Api.Auth` |
| `ObterMe.Response` / `ObterMe.VerificacaoResumo` | `ObterMeResponse` / `ObterMeVerificacaoResumo` | `Ocorrencias.Api.Usuarios` |
| `AtualizarMe.Request` | `AtualizarMeRequest` | `Ocorrencias.Api.Usuarios` |
| `ObterHistorico.Request` / `.Item` / `.TipoItem` / `.ValorItem` / `.Linha` | `ObterHistoricoRequest` / `ObterHistoricoItem` / `ObterHistoricoTipoItem` / `ObterHistoricoValorItem` / `LinhaHistorico` | `Ocorrencias.Api.Usuarios` |
| `Reversa.Request` / `Reversa.Response` | `ReversaRequest` / `ReversaResponse` | `Ocorrencias.Api.Geocodificacao` |
| `RegistrarProblema.Request` | `RegistrarProblemaRequest` | `Ocorrencias.Api.Problemas` |
| `BuscarDuplicatas.Request` / `.Response` | `BuscarDuplicatasRequest` / `BuscarDuplicatasResponse` | `Ocorrencias.Api.Problemas` |
| `ListarProblemasNaArea.Request` / `.Response` | `ListarProblemasNaAreaRequest` / `ListarProblemasNaAreaResponse` | `Ocorrencias.Api.Problemas` |
| `ListarProblemasRecentes.Request` / `.Item` | `ListarProblemasRecentesRequest` / `ListarProblemasRecentesItem` | `Ocorrencias.Api.Problemas` |
| `ObterDetalheProblema.Response` / `.Avaliacao` / `.ContagemExistencia` / `.ContagemPersistencia` / `.MeusVotos` | `ObterDetalheProblemaResponse` / `ObterDetalheProblemaAvaliacao` / `ObterDetalheProblemaContagemExistencia` / `ObterDetalheProblemaContagemPersistencia` / `ObterDetalheProblemaMeusVotos` | `Ocorrencias.Api.Problemas` |
| `ValorExistencia` / `ValorPersistencia` | igual | `Ocorrencias.Api.Votos` |
| `VotarExistencia.Request` / `VotarPersistencia.Request` | `VotarExistenciaRequest` / `VotarPersistenciaRequest` | `Ocorrencias.Api.Votos` |
| `RegistrarIncidente.Request` | `RegistrarIncidenteRequest` | `Ocorrencias.Api.Incidentes` |
| `ObterCalorIncidentes.Request` / `.Response` | `ObterCalorIncidentesRequest` / `ObterCalorIncidentesResponse` | `Ocorrencias.Api.Incidentes` |
| `ObterAreaIncidentes.Request` / `.Response` / `.Contagem` | `ObterAreaIncidentesRequest` / `ObterAreaIncidentesResponse` / `ObterAreaIncidentesContagem` | `Ocorrencias.Api.Incidentes` |
| `ObterAreaIncidentes.Nivel(int)` | `IncidenteService.Nivel(int)` | `Ocorrencias.Api.Incidentes` |
| `SolicitarVerificacao.Request` / `.Response` | `SolicitarVerificacaoRequest` / `SolicitarVerificacaoResponse` | `Ocorrencias.Api.Verificacao` |
| `ListarVerificacoes.Request` / `.Item` / `.UsuarioResumo` | `ListarVerificacoesRequest` / `ListarVerificacoesItem` / `ListarVerificacoesUsuarioResumo` | `Ocorrencias.Api.Admin` |
| `ListarVerificacoes.Iniciais(string)` | `AdminService.Iniciais(string)` | `Ocorrencias.Api.Admin` |
| `RecusarVerificacao.Request` | `RecusarVerificacaoRequest` | `Ocorrencias.Api.Admin` |

**Atenção nos testes:** dentro de `namespace Ocorrencias.Api.Tests.X`, um nome qualificado como `Auth.RespostaAuth` resolve primeiro para `Ocorrencias.Api.Tests.Auth`. Sempre adicione `using Ocorrencias.Api.<Area>;` no topo do arquivo e use o nome simples. Os qualificados atuais `Features.Auth.RespostaAuth` (`Infra/Cenarios.cs`) e `Features.Categorias.CategoriaResumo` (`Problemas/ProblemasTests.cs`) viram `RespostaAuth` e `CategoriaResumo`.

---

### Task 1: Rede de segurança do contrato

**Files:**
- Create: `tests/Ocorrencias.Api.Tests/Esqueleto/ContratoTests.cs`
- Create: `tests/Ocorrencias.Api.Tests/Esqueleto/openapi.snapshot.json` (gerado pelo próprio teste)

**Interfaces:**
- Consumes: `ApiFactory`, `ApiCollection.Nome`, `ClienteApi` (`PostarCadastroAsync`, `EmailUnico`, `Json`, `LerAsync`), `Cenarios` (`NovaPessoaAsync`, `RegistrarProblemaAsync`, `VotarExistenciaAsync`, `VotarPersistenciaAsync`), `fabrica.NoBancoAsync`, `fabrica.Relogio`. Tudo já existe.
- Produces: o teste `ContratoTests.Openapi_igual_ao_snapshot` e o arquivo `openapi.snapshot.json`, que todas as tasks seguintes precisam manter verdes. A variável de ambiente `ATUALIZAR_SNAPSHOT_OPENAPI=1` regrava o snapshot.

- [ ] **Step 1: Escrever os testes (devem passar no código atual — são a referência)**

```csharp
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ocorrencias.Api.Tests.Infra;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Tests.Esqueleto;

/// <summary>Trava o contrato HTTP durante refatorações: OpenAPI, rotas/políticas, Location, 401 e 404.</summary>
[Collection(ApiCollection.Nome)]
public sealed class ContratoTests(ApiFactory fabrica)
{
    private const string Justificativa = "Moro em Botafogo há dez anos e circulo pelo bairro todos os dias.";

    private static readonly string[] RotasEsperadas =
    [
        "POST /api/v1/auth/cadastro [anonimo]",
        "POST /api/v1/auth/login [anonimo]",
        "GET /api/v1/me",
        "PATCH /api/v1/me",
        "GET /api/v1/me/historico",
        "GET /api/v1/categorias [anonimo]",
        "GET /api/v1/geocodificacao/reversa [anonimo]",
        "GET /api/v1/problemas [anonimo]",
        "GET /api/v1/problemas/recentes [anonimo]",
        "GET /api/v1/problemas/duplicatas",
        "GET /api/v1/problemas/{id:guid} [anonimo]",
        "POST /api/v1/problemas",
        "PUT /api/v1/problemas/{id:guid}/votos/existencia",
        "PUT /api/v1/problemas/{id:guid}/votos/persistencia",
        "POST /api/v1/incidentes",
        "GET /api/v1/incidentes/calor [anonimo]",
        "GET /api/v1/incidentes/area [anonimo]",
        "POST /api/v1/verificacoes",
        "GET /api/v1/admin/verificacoes [admin]",
        "POST /api/v1/admin/verificacoes/{id:guid}/aprovar [admin]",
        "POST /api/v1/admin/verificacoes/{id:guid}/recusar [admin]",
    ];

    [Fact]
    public async Task Openapi_igual_ao_snapshot()
    {
        var atual = Canonico(await fabrica.CreateClient().GetStringAsync("/openapi/v1.json"));
        var caminho = CaminhoSnapshot();
        if (Environment.GetEnvironmentVariable("ATUALIZAR_SNAPSHOT_OPENAPI") == "1")
        {
            await File.WriteAllTextAsync(caminho, atual);
        }

        var esperado = Canonico(await File.ReadAllTextAsync(caminho));

        atual.Should().Be(esperado, "o contrato do front sai do OpenAPI; regrave o snapshot só para mudança de contrato aprovada");
    }

    [Fact]
    public void Rotas_e_politicas_do_contrato()
    {
        var rotas = fabrica.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/v1", StringComparison.Ordinal) == true)
            .SelectMany(e =>
            {
                var anonimo = e.Metadata.GetMetadata<IAllowAnonymous>() is not null ? " [anonimo]" : "";
                var admin = e.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => a.Policy == "Administrador") ? " [admin]" : "";
                return e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods
                    .Select(m => $"{m} {e.RoutePattern.RawText}{anonimo}{admin}");
            });

        rotas.Should().BeEquivalentTo(RotasEsperadas);
    }

    [Fact]
    public async Task Modelo_do_ef_nao_tem_mudancas_pendentes() =>
        (await fabrica.NoBancoAsync(db => Task.FromResult(db.Database.HasPendingModelChanges()))).Should().BeFalse();

    [Fact]
    public async Task Location_do_cadastro_e_da_verificacao_aponta_para_me()
    {
        var cadastro = await fabrica.CreateClient().PostarCadastroAsync(ClienteApi.EmailUnico());
        var pessoa = await fabrica.NovaPessoaAsync();
        var verificacao = await pessoa.Cliente.PostAsJsonAsync(
            "/api/v1/verificacoes", new { tipoVinculo = "moradorRegiao", justificativa = Justificativa }, ClienteApi.Json);

        cadastro.StatusCode.Should().Be(HttpStatusCode.Created);
        cadastro.Headers.Location!.ToString().Should().Be("/api/v1/me");
        verificacao.StatusCode.Should().Be(HttpStatusCode.Created);
        verificacao.Headers.Location!.ToString().Should().Be("/api/v1/me");
    }

    [Fact]
    public async Task Location_do_incidente_usa_o_ponto_arredondado_em_cultura_invariante()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        var (baseLat, baseLng) = ApiFactory.AreaIsolada();
        var (lat, lng) = (baseLat + 0.00041, baseLng - 0.00067);
        var culturaOriginal = CultureInfo.CurrentCulture;
        HttpResponseMessage resposta;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
            resposta = await pessoa.Cliente.PostAsJsonAsync(
                "/api/v1/incidentes",
                new { categoriaId = 7, lat, lng, ocorridoEm = fabrica.Relogio.GetUtcNow().AddMinutes(-30) },
                ClienteApi.Json);
        }
        finally
        {
            CultureInfo.CurrentCulture = culturaOriginal;
        }

        var latArredondada = Math.Round(lat, 3, MidpointRounding.AwayFromZero);
        var lngArredondada = Math.Round(lng, 3, MidpointRounding.AwayFromZero);
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        resposta.Headers.Location!.ToString().Should().Be(
            FormattableString.Invariant($"/api/v1/incidentes/area?lat={latArredondada}&lng={lngArredondada}"));
    }

    [Fact]
    public async Task Token_de_usuario_removido_recebe_401()
    {
        var autor = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.PontoUnico();
        var problema = await autor.RegistrarProblemaAsync(lat, lng);
        var removido = await fabrica.NovaPessoaAsync();
        await fabrica.NoBancoAsync(db => db.Users.Where(u => u.Id == removido.Id).ExecuteDeleteAsync());

        (await removido.Cliente.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await removido.Cliente.PatchAsJsonAsync("/api/v1/me", new { nome = "Outro Nome" }, ClienteApi.Json))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await removido.Cliente.PostAsJsonAsync("/api/v1/verificacoes", new { tipoVinculo = "moradorRegiao", justificativa = Justificativa }, ClienteApi.Json))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await removido.VotarExistenciaAsync(problema.Id, "confirma")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Recursos_inexistentes_devolvem_404()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var id = Guid.NewGuid();

        (await fabrica.CreateClient().GetAsync($"/api/v1/problemas/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await pessoa.VotarExistenciaAsync(id, "confirma")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await pessoa.VotarPersistenciaAsync(id, "continua")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.Cliente.PostAsync($"/api/v1/admin/verificacoes/{id}/aprovar", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.Cliente.PostAsJsonAsync($"/api/v1/admin/verificacoes/{id}/recusar", new { motivo = "Sem vínculo com a região." }, ClienteApi.Json))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static string CaminhoSnapshot([CallerFilePath] string arquivo = "") =>
        Path.Combine(Path.GetDirectoryName(arquivo)!, "openapi.snapshot.json");

    /// <summary>Chaves de objeto em ordem ordinal (a ordem dos paths depende da ordem de registro); arrays mantêm a ordem.</summary>
    private static string Canonico(string json) =>
        Ordenar(JsonNode.Parse(json))!.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

    private static JsonNode? Ordenar(JsonNode? no) => no switch
    {
        JsonObject objeto => new JsonObject(objeto
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => KeyValuePair.Create(p.Key, Ordenar(p.Value)))),
        JsonArray lista => new JsonArray(lista.Select(Ordenar).ToArray()),
        null => null,
        _ => no.DeepClone(),
    };
}
```

- [ ] **Step 2: Gerar o snapshot a partir do código atual**

Run (Docker rodando): `ATUALIZAR_SNAPSHOT_OPENAPI=1 dotnet test tests/Ocorrencias.Api.Tests --filter "FullyQualifiedName~ContratoTests.Openapi_igual_ao_snapshot"`
Expected: PASS, e `tests/Ocorrencias.Api.Tests/Esqueleto/openapi.snapshot.json` criado.

- [ ] **Step 3: Rodar a classe inteira sem a variável**

Run: `dotnet test tests/Ocorrencias.Api.Tests --filter "FullyQualifiedName~ContratoTests"`
Expected: 7 PASS. Se `Rotas_e_politicas_do_contrato` falhar **só por formato** do `RawText` (ex.: sem a barra inicial), ajuste as strings de `RotasEsperadas` para o formato impresso, mantendo os mesmos 21 itens e marcadores. Qualquer diferença de conjunto ou de marcador é bug a investigar, nunca motivo para editar a lista.

- [ ] **Step 4: Suíte completa**

Run: `dotnet test`
Expected: tudo verde (Domain 71, Simulator 42, API 156).

- [ ] **Step 5: Commit**

```bash
git add tests/Ocorrencias.Api.Tests/Esqueleto/ContratoTests.cs tests/Ocorrencias.Api.Tests/Esqueleto/openapi.snapshot.json
git commit -m "test(contrato): trava OpenAPI, rotas, Location, 401 e 404 antes da refatoração"
```

---

### Task 2: Tipos base `Resultado<T>`, `ErroAplicacao` e `IEndpointModule`

**Files:**
- Create: `src/Ocorrencias.Api/Common/Models/ErroAplicacao.cs`
- Create: `src/Ocorrencias.Api/Common/Models/Resultado.cs`
- Create: `src/Ocorrencias.Api/Common/Extensions/ErroAplicacaoExtensions.cs`
- Create: `src/Ocorrencias.Api/Common/Abstractions/IEndpointModule.cs`
- Test: `tests/Ocorrencias.Api.Tests/Common/ErroAplicacaoTests.cs`

**Interfaces:**
- Produces (namespace `Ocorrencias.Api.Common`):
  - `enum TipoErro { NaoEncontrado, NaoAutorizado, Campo, Regra }`
  - `sealed record ErroAplicacao(TipoErro Tipo, int Status, string? Type, string? Title, string? Campo = null)` com:
    - `static ErroAplicacao NaoEncontrado { get; }`
    - `static ErroAplicacao NaoAutorizado { get; }`
    - `static ErroAplicacao DeCampo(string campo, string mensagem)`
    - `static ErroAplicacao Regra(int status, string? type, string title)`
  - `readonly struct Resultado<T>` com `T? Valor`, `ErroAplicacao? Erro`, `bool Sucesso` e conversões implícitas de `T` e de `ErroAplicacao`
  - `readonly record struct Vazio`
  - extensões em `ErroAplicacao`:
    - `ProblemHttpResult ParaProblema()`
    - `ValidationProblem ParaValidacao()`
    - `NotFound ParaNaoEncontrado()`
    - `UnauthorizedHttpResult ParaNaoAutorizado()`
  - `interface IEndpointModule { static abstract void Map(IEndpointRouteBuilder api); }`

- [ ] **Step 1: Escrever os testes**

```csharp
using Microsoft.AspNetCore.Http;
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Tests.Common;

public sealed class ErroAplicacaoTests
{
    [Fact]
    public void Regra_vira_problem_details_com_status_type_e_title()
    {
        var resultado = ErroAplicacao.Regra(StatusCodes.Status422UnprocessableEntity, "problema-resolvido", "O problema já foi resolvido.").ParaProblema();

        resultado.StatusCode.Should().Be(422);
        resultado.ProblemDetails.Status.Should().Be(422);
        resultado.ProblemDetails.Type.Should().Be("problema-resolvido");
        resultado.ProblemDetails.Title.Should().Be("O problema já foi resolvido.");
    }

    [Fact]
    public void Regra_sem_type_equivale_ao_problem_de_hoje()
    {
        var novo = ErroAplicacao.Regra(StatusCodes.Status401Unauthorized, null, "E-mail ou senha inválidos.").ParaProblema();
        var antigo = TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "E-mail ou senha inválidos.");

        novo.StatusCode.Should().Be(antigo.StatusCode);
        novo.ProblemDetails.Should().BeEquivalentTo(antigo.ProblemDetails);
    }

    [Fact]
    public void Campo_vira_validation_problem_com_o_campo_em_errors()
    {
        var resultado = ErroAplicacao.DeCampo("categoriaId", "Categoria inexistente.").ParaValidacao();

        resultado.StatusCode.Should().Be(400);
        resultado.ProblemDetails.Errors.Should().ContainKey("categoriaId").WhoseValue.Should().Equal("Categoria inexistente.");
    }

    [Fact]
    public void Nao_encontrado_e_nao_autorizado_viram_status_sem_corpo()
    {
        ErroAplicacao.NaoEncontrado.ParaNaoEncontrado().StatusCode.Should().Be(404);
        ErroAplicacao.NaoAutorizado.ParaNaoAutorizado().StatusCode.Should().Be(401);
    }

    [Fact]
    public void Resultado_converte_de_valor_e_de_erro()
    {
        Resultado<int> sucesso = 7;
        Resultado<int> falha = ErroAplicacao.NaoEncontrado;

        sucesso.Sucesso.Should().BeTrue();
        sucesso.Valor.Should().Be(7);
        sucesso.Erro.Should().BeNull();
        falha.Sucesso.Should().BeFalse();
        falha.Erro.Should().Be(ErroAplicacao.NaoEncontrado);
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet build tests/Ocorrencias.Api.Tests`
Expected: FAIL com `CS0103`/`CS0246` para `ErroAplicacao`, `Resultado<>`.

- [ ] **Step 3: Implementar**

`src/Ocorrencias.Api/Common/Models/ErroAplicacao.cs`:
```csharp
namespace Ocorrencias.Api.Common;

public enum TipoErro
{
    NaoEncontrado,
    NaoAutorizado,
    Campo,
    Regra,
}

/// <summary>
/// Erro devolvido pelos services, sem dependência de HTTP. O module traduz com <see cref="ErroAplicacaoExtensions"/>;
/// <c>Type</c> e <c>Title</c> seguem a tabela de erros de docs/API.md.
/// </summary>
public sealed record ErroAplicacao(TipoErro Tipo, int Status, string? Type, string? Title, string? Campo = null)
{
    public static ErroAplicacao NaoEncontrado { get; } = new(TipoErro.NaoEncontrado, StatusCodes.Status404NotFound, null, null);

    /// <summary>Token válido de um usuário que não existe mais.</summary>
    public static ErroAplicacao NaoAutorizado { get; } = new(TipoErro.NaoAutorizado, StatusCodes.Status401Unauthorized, null, null);

    /// <summary>Erro de um campo do corpo detectado só no banco (ex.: categoria inexistente) → 400 ValidationProblem.</summary>
    public static ErroAplicacao DeCampo(string campo, string mensagem) =>
        new(TipoErro.Campo, StatusCodes.Status400BadRequest, null, mensagem, campo);

    public static ErroAplicacao Regra(int status, string? type, string title) => new(TipoErro.Regra, status, type, title);
}
```

`src/Ocorrencias.Api/Common/Models/Resultado.cs`:
```csharp
namespace Ocorrencias.Api.Common;

/// <summary>Valor ou erro de um service. Converte implicitamente de <typeparamref name="T"/> e de <see cref="ErroAplicacao"/>.</summary>
public readonly struct Resultado<T>
{
    private Resultado(T? valor, ErroAplicacao? erro)
    {
        Valor = valor;
        Erro = erro;
    }

    public T? Valor { get; }

    public ErroAplicacao? Erro { get; }

    public bool Sucesso => Erro is null;

    public static implicit operator Resultado<T>(T valor) => new(valor, null);

    public static implicit operator Resultado<T>(ErroAplicacao erro) => new(default, erro);
}

/// <summary>Valor de sucesso de operações sem retorno (ex.: aprovar uma verificação).</summary>
public readonly record struct Vazio;
```

`src/Ocorrencias.Api/Common/Extensions/ErroAplicacaoExtensions.cs`:
```csharp
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ocorrencias.Api.Common;

/// <summary>Tradução de <see cref="ErroAplicacao"/> para os mesmos resultados HTTP que os handlers devolviam antes.</summary>
public static class ErroAplicacaoExtensions
{
    public static ProblemHttpResult ParaProblema(this ErroAplicacao erro) =>
        TypedResults.Problem(statusCode: erro.Status, type: erro.Type, title: erro.Title);

    public static ValidationProblem ParaValidacao(this ErroAplicacao erro) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [erro.Campo!] = [erro.Title!] });

    public static NotFound ParaNaoEncontrado(this ErroAplicacao erro) => TypedResults.NotFound();

    public static UnauthorizedHttpResult ParaNaoAutorizado(this ErroAplicacao erro) => TypedResults.Unauthorized();
}
```

`src/Ocorrencias.Api/Common/Abstractions/IEndpointModule.cs`:
```csharp
namespace Ocorrencias.Api.Common;

/// <summary>
/// Endpoints de uma área. <c>MapEndpoints</c> encontra as implementações por reflexão e chama <see cref="Map"/>
/// com o grupo <c>/api/v1</c>, que já exige autenticação.
/// </summary>
public interface IEndpointModule
{
    static abstract void Map(IEndpointRouteBuilder api);
}
```

- [ ] **Step 4: Rodar os testes**

Run: `dotnet test tests/Ocorrencias.Api.Tests --filter "FullyQualifiedName~ErroAplicacaoTests"`
Expected: 5 PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Ocorrencias.Api/Common tests/Ocorrencias.Api.Tests/Common/ErroAplicacaoTests.cs
git commit -m "refactor(common): adiciona Resultado, ErroAplicacao e IEndpointModule"
```

---

### Task 3: Mover persistência, infra e common para a estrutura nova

Só move arquivos e troca namespaces. Nenhuma linha de lógica muda.

**Files:**
- Move: `src/Ocorrencias.Api/Infra/Data/**` → `src/Ocorrencias.Api/Infrastructure/Persistence/**` (inclui `Entidades/`, `Configurations/`, `Migrations/`, `AppDbContext.cs`, `SeedDesenvolvimento.cs`, `DadosConfig.cs`)
- Move: `Infra/Auth/JwtTokenService.cs` → `Feature/Auth/Services/`; `Infra/Auth/JwtOptions.cs` → `Feature/Auth/Models/`; `Infra/Auth/AuthConfig.cs` → `Infrastructure/Configurations/`
- Move: `Infra/Geocodificacao/{NominatimClient,ServicoGeocodificacao,LimitadorDeTaxa}.cs` → `Feature/Geocodificacao/Services/`; `{NominatimOptions,NominatimResposta,EnderecoGeocodificado}.cs` → `Feature/Geocodificacao/Models/`; `GeocodificacaoConfig.cs` → `Feature/Geocodificacao/`
- Move: `Infra/Confianca/{RecalculoProblema,ProcessadorCicloDeVida}.cs` → `Feature/Confianca/Services/`; `CicloDeVidaOptions.cs` → `Feature/Confianca/Models/`; `ConfiancaConfig.cs` → `Feature/Confianca/`; `Infra/Jobs/CicloDeVidaJob.cs` → `Feature/Confianca/Jobs/`
- Move: `Common/OpenApiConfig.cs` → `Infrastructure/Configurations/OpenApiConfig.cs`
- Move: `Common/{ClaimsPrincipalExtensions,Validacao,RegrasValidacao}.cs` → `Common/Extensions/`; `Common/Paginacao.cs` → `Common/Models/`; `Common/{Geo,RelogioPostgres,JsonConfig}.cs` → `Common/Helpers/`
- Modify: namespaces/usings em `src/Ocorrencias.Api/**` e `tests/Ocorrencias.Api.Tests/**`

**Interfaces:**
- Produces:

  | Namespace | Tipos |
  |---|---|
  | `Ocorrencias.Api.Persistence` | `AppDbContext`, entidades, configurations, `SeedDesenvolvimento`, `DadosConfig` |
  | `Ocorrencias.Api.Infrastructure.Persistence.Migrations` | migrations |
  | `Ocorrencias.Api.Auth` | `JwtTokenService`, `JwtOptions` |
  | `Ocorrencias.Api.Geocodificacao` | `INominatimClient`, `NominatimClient`, `IServicoGeocodificacao`, `ServicoGeocodificacao`, `LimitadorDeTaxa`, `NominatimOptions`, `NominatimResposta`, `EnderecoGeocodificado`, `GeocodificacaoConfig` |
  | `Ocorrencias.Api.Confianca` | `RecalculoProblema`, `ProcessadorCicloDeVida`, `CicloDeVidaOptions`, `CicloDeVidaJob`, `ConfiancaConfig` |
  | `Ocorrencias.Api.Infrastructure` | `AuthConfig`, `OpenApiConfig` |

- [ ] **Step 1: Mover com `git mv`**

```bash
cd src/Ocorrencias.Api
mkdir -p Infrastructure/Configurations Feature/Auth/Services Feature/Auth/Models \
  Feature/Geocodificacao/Services Feature/Geocodificacao/Models \
  Feature/Confianca/Services Feature/Confianca/Models Feature/Confianca/Jobs \
  Common/Extensions Common/Models Common/Helpers
git mv Infra/Data Infrastructure/Persistence
git mv Infra/Auth/JwtTokenService.cs Feature/Auth/Services/
git mv Infra/Auth/JwtOptions.cs Feature/Auth/Models/
git mv Infra/Auth/AuthConfig.cs Infrastructure/Configurations/
git mv Infra/Geocodificacao/NominatimClient.cs Infra/Geocodificacao/ServicoGeocodificacao.cs Infra/Geocodificacao/LimitadorDeTaxa.cs Feature/Geocodificacao/Services/
git mv Infra/Geocodificacao/NominatimOptions.cs Infra/Geocodificacao/NominatimResposta.cs Infra/Geocodificacao/EnderecoGeocodificado.cs Feature/Geocodificacao/Models/
git mv Infra/Geocodificacao/GeocodificacaoConfig.cs Feature/Geocodificacao/
git mv Infra/Confianca/RecalculoProblema.cs Infra/Confianca/ProcessadorCicloDeVida.cs Feature/Confianca/Services/
git mv Infra/Confianca/CicloDeVidaOptions.cs Feature/Confianca/Models/
git mv Infra/Confianca/ConfiancaConfig.cs Feature/Confianca/
git mv Infra/Jobs/CicloDeVidaJob.cs Feature/Confianca/Jobs/
git mv Common/OpenApiConfig.cs Infrastructure/Configurations/
git mv Common/ClaimsPrincipalExtensions.cs Common/Validacao.cs Common/RegrasValidacao.cs Common/Extensions/
git mv Common/Paginacao.cs Common/Models/
git mv Common/Geo.cs Common/RelogioPostgres.cs Common/JsonConfig.cs Common/Helpers/
ls Infra 2>/dev/null && find Infra -type f   # deve sair vazio
cd ../..
```

- [ ] **Step 2: Trocar namespaces com um script (ordem importa: do mais específico ao mais genérico)**

Salve em `$SCRATCH/ns.py`, onde `$SCRATCH` é o diretório de scratchpad da sessão, fora do repo:
```python
import pathlib, re, sys

raiz = pathlib.Path(sys.argv[1])
trocas = [
    ("Ocorrencias.Api.Infra.Data.Entidades", "Ocorrencias.Api.Persistence"),
    ("Ocorrencias.Api.Infra.Data.Configurations", "Ocorrencias.Api.Persistence"),
    ("Ocorrencias.Api.Infra.Data.Migrations", "Ocorrencias.Api.Infrastructure.Persistence.Migrations"),
    ("Ocorrencias.Api.Infra.Data", "Ocorrencias.Api.Persistence"),
    ("Ocorrencias.Api.Infra.Geocodificacao", "Ocorrencias.Api.Geocodificacao"),
    ("Ocorrencias.Api.Infra.Confianca", "Ocorrencias.Api.Confianca"),
    ("Ocorrencias.Api.Infra.Jobs", "Ocorrencias.Api.Confianca"),
]
# Infra.Auth depende do arquivo (JwtTokenService/JwtOptions → Auth; AuthConfig → Infrastructure).
usa_auth = {"Features/Auth/Cadastrar.cs", "Features/Auth/Entrar.cs", "Features/Auth/RespostaAuth.cs",
            "Infrastructure/Configurations/AuthConfig.cs"}
usa_infrastructure = {"Features/Endpoints.cs", "Features/Admin/Aprovar.cs", "Features/Admin/Recusar.cs",
                      "Features/Admin/ListarVerificacoes.cs", "Program.cs"}

for arquivo in raiz.rglob("*.cs"):
    if "/bin/" in str(arquivo) or "/obj/" in str(arquivo):
        continue
    texto = original = arquivo.read_text(encoding="utf-8-sig")
    for de, para in trocas:
        texto = texto.replace(de, para)
    relativo = arquivo.relative_to(raiz).as_posix()
    if relativo.endswith(("Feature/Auth/Services/JwtTokenService.cs", "Feature/Auth/Models/JwtOptions.cs")):
        texto = texto.replace("namespace Ocorrencias.Api.Infra.Auth;", "namespace Ocorrencias.Api.Auth;")
    if relativo == "Infrastructure/Configurations/AuthConfig.cs":
        texto = texto.replace("namespace Ocorrencias.Api.Infra.Auth;", "using Ocorrencias.Api.Auth;\n\nnamespace Ocorrencias.Api.Infrastructure;")
    if relativo == "Infrastructure/Configurations/OpenApiConfig.cs":
        texto = texto.replace("namespace Ocorrencias.Api.Common;", "namespace Ocorrencias.Api.Infrastructure;")
    if relativo in usa_auth:
        texto = texto.replace("using Ocorrencias.Api.Infra.Auth;", "using Ocorrencias.Api.Auth;")
    if relativo in usa_infrastructure:
        texto = texto.replace("using Ocorrencias.Api.Infra.Auth;", "using Ocorrencias.Api.Infrastructure;")
    # usings duplicados (ex.: Infra.Data + Infra.Data.Entidades → Persistence) e using do próprio namespace
    linhas, vistos = [], set()
    ns = re.search(r"^namespace ([\w.]+)", texto, re.M)
    for linha in texto.split("\n"):
        if linha.startswith("using ") and linha.rstrip().endswith(";"):
            if linha in vistos or (ns and linha.strip() == f"using {ns.group(1)};"):
                continue
            vistos.add(linha)
        linhas.append(linha)
    texto = "\n".join(linhas)
    if texto != original:
        arquivo.write_text(texto, encoding="utf-8")
        print("alterado:", relativo)
```

Run:
```bash
python3 $SCRATCH/ns.py src/Ocorrencias.Api
python3 $SCRATCH/ns.py tests/Ocorrencias.Api.Tests
grep -rn "Ocorrencias.Api.Infra\b\|Ocorrencias.Api.Infra\." src tests --include='*.cs' | grep -v "/bin/\|/obj/"   # deve sair vazio
```

- [ ] **Step 3: Ajustar os usings que o script não cobre**

- `src/Ocorrencias.Api/Program.cs`: adicione `using Ocorrencias.Api.Infrastructure;` se ainda não estiver lá (`AddOpenApiConfigurado` e `AddAutenticacao` agora moram nesse namespace).
- `tests/Ocorrencias.Api.Tests/Esqueleto/EsqueletoTests.cs`: adicione `using Ocorrencias.Api.Infrastructure;` (usa `OpenApiConfig.NomeSchema`).
- Os testes que usam `Ocorrencias.Api.Confianca`/`Geocodificacao`/`Persistence` já foram reescritos pelo script.

- [ ] **Step 4: Build e verificação do modelo EF**

Run: `dotnet build`
Expected: 0 erros. Os `Designer.cs` e o `AppDbContextModelSnapshot.cs` agora citam `"Ocorrencias.Api.Persistence.Categoria"` etc.

Run (Docker com o banco de dev, `docker compose up -d db`): `dotnet ef migrations has-pending-model-changes -p src/Ocorrencias.Api`
Expected: `No changes have been made to the model since the last migration.`

- [ ] **Step 5: Suíte completa**

Run: `dotnet test`
Expected: tudo verde, incluindo `ContratoTests.Openapi_igual_ao_snapshot` e `Modelo_do_ef_nao_tem_mudancas_pendentes`.

- [ ] **Step 6: Commit**

```bash
git add -A src/Ocorrencias.Api tests/Ocorrencias.Api.Tests
git commit -m "refactor(infra): move persistência, geocodificação, confiança e common para a estrutura nova"
```

---

### Task 4: Centralizar DI, configurações e mapeamento de endpoints

**Files:**
- Create: `src/Ocorrencias.Api/Common/Statics/PoliticasAutorizacao.cs`, `PoliticasCors.cs`, `PoliticasLimite.cs`
- Create: `src/Ocorrencias.Api/Infrastructure/Configurations/AppConfiguration.cs`
- Create: `src/Ocorrencias.Api/Infrastructure/DependencyInjections/DependencyInjection.cs`
- Create: `src/Ocorrencias.Api/Common/Extensions/EndpointRouteBuilderExtensions.cs`
- Delete: `Common/CorsConfig.cs`, `Common/LimitesDeRequisicao.cs`, `Infrastructure/Configurations/AuthConfig.cs`, `Infrastructure/Persistence/DadosConfig.cs`, `Feature/Geocodificacao/GeocodificacaoConfig.cs`, `Feature/Confianca/ConfiancaConfig.cs`
- Modify: `src/Ocorrencias.Api/Program.cs`, `Features/Endpoints.cs`, `Features/Admin/{Aprovar,Recusar,ListarVerificacoes}.cs`, `Features/Geocodificacao/Reversa.cs`, `Infrastructure/Configurations/OpenApiConfig.cs`

**Interfaces:**
- Consumes: `IEndpointModule` (Task 2).
- Produces:
  - `PoliticasAutorizacao.Autenticado` / `.Administrador`, com os valores `"Autenticado"` / `"Administrador"`
  - `PoliticasCors.Front`, com o valor `"Front"`
  - `PoliticasLimite.Geocodificacao`, com o valor `"geocodificacao"`
  - `DependencyInjection.AddAplicacao(this IServiceCollection, IConfiguration)`. É o método onde as Tasks 5–13 acrescentam `AddScoped`, dentro de `AddServicos`.
  - `AppConfiguration`: `AddCorsConfigurado(IConfiguration)`, `AddAutenticacao()` e `AddLimitesDeRequisicao(IConfiguration)`
  - `EndpointRouteBuilderExtensions.MapEndpoints(this IEndpointRouteBuilder)`, que registra os módulos e depois `MapFeaturesLegadas`
  - `Endpoints.MapFeaturesLegadas(this RouteGroupBuilder api)`, temporário até a Task 13

- [ ] **Step 1: Constantes em `Common/Statics`**

```csharp
// PoliticasAutorizacao.cs
namespace Ocorrencias.Api.Common;

public static class PoliticasAutorizacao
{
    public const string Autenticado = "Autenticado";
    public const string Administrador = "Administrador";
}
```
```csharp
// PoliticasCors.cs
namespace Ocorrencias.Api.Common;

public static class PoliticasCors
{
    public const string Front = "Front";
}
```
```csharp
// PoliticasLimite.cs
namespace Ocorrencias.Api.Common;

public static class PoliticasLimite
{
    public const string Geocodificacao = "geocodificacao";
}
```

- [ ] **Step 2: `AppConfiguration.cs`**

Os três métodos têm o corpo idêntico ao de `CorsConfig.AddCorsPorConfiguracao`, `AuthConfig.AddAutenticacao` e `LimitesDeRequisicao.AddLimitesDeRequisicao`, trocando só as constantes.

```csharp
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ocorrencias.Api.Auth;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Persistence;

namespace Ocorrencias.Api.Infrastructure;

/// <summary>Configurações de borda chamadas uma a uma no Program: CORS, autenticação e rate limit.</summary>
public static class AppConfiguration
{
    /// <summary>CORS restrito às origens de <c>Cors:Origens</c>.</summary>
    public static IServiceCollection AddCorsConfigurado(this IServiceCollection services, IConfiguration configuracao)
    {
        var origens = configuracao.GetSection("Cors:Origens").Get<string[]>() ?? [];

        return services.AddCors(o => o.AddPolicy(PoliticasCors.Front, p => p
            .WithOrigins(origens)
            .AllowAnyHeader()
            .AllowAnyMethod()));
    }

    public static IServiceCollection AddAutenticacao(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.Secao)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddIdentityCore<Usuario>(o =>
            {
                o.User.RequireUniqueEmail = true;
                // UserName = e-mail: aceita qualquer caractere que a validação de e-mail aceite (ex.: apóstrofo, acentos).
                o.User.AllowedUserNameCharacters = "";
                // Contrato: só "senha ≥ 8 caracteres".
                o.Password.RequiredLength = 8;
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequiredUniqueChars = 1;
            })
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddSingleton<JwtTokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, TimeProvider>((bearer, jwt, relogio) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Emissor,
                    ValidAudience = jwt.Value.Audiencia,
                    IssuerSigningKey = JwtTokenService.Chave(jwt.Value),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = JwtTokenService.ClaimNome,
                    RoleClaimType = JwtTokenService.ClaimPerfil,
                    // Validade conferida pelo mesmo TimeProvider que emite o token (os testes controlam o tempo).
                    LifetimeValidator = (inicio, fim, _, parametros) =>
                    {
                        var agora = relogio.GetUtcNow().UtcDateTime;
                        var folga = parametros.ClockSkew;
                        return (inicio is null || inicio.Value <= agora + folga)
                            && fim is not null && fim.Value > agora - folga;
                    },
                };
            });

        // O perfil do token serve só para autorizar rotas de admin; o peso do voto lê o perfil do banco.
        services.AddAuthorizationBuilder()
            .AddPolicy(PoliticasAutorizacao.Autenticado, p => p.RequireAuthenticatedUser())
            .AddPolicy(PoliticasAutorizacao.Administrador, p => p.RequireAuthenticatedUser().RequireClaim(JwtTokenService.ClaimPerfil, "administrador"));

        return services;
    }

    /// <summary>
    /// Limite por IP (docs/API.md: 30 req/min na geocodificação). Atrás de proxy reverso, habilite
    /// <c>ASPNETCORE_FORWARDEDHEADERS_ENABLED=true</c> para o IP real do cliente chegar aqui.
    /// </summary>
    public static IServiceCollection AddLimitesDeRequisicao(this IServiceCollection services, IConfiguration configuracao)
    {
        var porMinuto = configuracao.GetValue("LimitesDeRequisicao:GeocodificacaoPorMinuto", 30);

        return services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = async (contexto, ct) =>
            {
                var http = contexto.HttpContext;
                if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var aguardar))
                {
                    http.Response.Headers.RetryAfter = ((int)Math.Ceiling(aguardar.TotalSeconds)).ToString();
                }

                await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = http,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Type = "limite-requisicoes",
                        Title = "Muitas requisições. Tente novamente em instantes.",
                    },
                });
            };

            o.AddPolicy(PoliticasLimite.Geocodificacao, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = porMinuto,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });
    }
}
```

- [ ] **Step 3: `DependencyInjection.cs`**

`AddPersistencia`, `AddGeocodificacao` e `AddConfianca` têm o corpo idêntico ao de `DadosConfig`, `GeocodificacaoConfig` e `ConfiancaConfig`.

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Confianca;
using Ocorrencias.Api.Geocodificacao;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain.Confianca;

namespace Ocorrencias.Api.Infrastructure;

/// <summary>Tudo que é interno da aplicação; configurações de borda ficam em <see cref="AppConfiguration"/>.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddAplicacao(this IServiceCollection services, IConfiguration configuracao) =>
        services
            .AddTratamentoDeErros()
            .AddJson()
            .AddRelogio()
            .AddValidacao()
            .AddPersistencia(configuracao)
            .AddGeocodificacao()
            .AddConfianca(configuracao)
            .AddServicos();

    private static IServiceCollection AddTratamentoDeErros(this IServiceCollection services) =>
        services.AddProblemDetails();

    private static IServiceCollection AddJson(this IServiceCollection services) =>
        services.ConfigureHttpJsonOptions(o => JsonConfig.Configurar(o.SerializerOptions));

    private static IServiceCollection AddRelogio(this IServiceCollection services) =>
        services.AddSingleton<TimeProvider>(new RelogioPostgres());

    private static IServiceCollection AddValidacao(this IServiceCollection services)
    {
        Validacao.ConfigurarGlobal();
        return services.AddValidatorsFromAssemblyContaining<Program>();
    }

    private static IServiceCollection AddPersistencia(this IServiceCollection services, IConfiguration configuracao)
    {
        var conexao = configuracao.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' não configurada (ConnectionStrings__Default).");

        services.AddDbContext<AppDbContext>(o => o
            .UseNpgsql(conexao, npgsql => npgsql.UseNetTopologySuite(geographyAsDefault: true))
            .UseSnakeCaseNamingConvention());

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

        return services;
    }

    private static IServiceCollection AddGeocodificacao(this IServiceCollection services)
    {
        services.AddOptions<NominatimOptions>()
            .BindConfiguration(NominatimOptions.Secao)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient(NominatimClient.NomeHttpClient, (sp, http) =>
            {
                var opcoes = sp.GetRequiredService<IOptions<NominatimOptions>>().Value;
                http.BaseAddress = new Uri(opcoes.UrlBase);
                http.Timeout = opcoes.Timeout;
                http.DefaultRequestHeaders.UserAgent.ParseAdd(opcoes.UserAgent);
                http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pt-BR");
            })
            // O log padrão do HttpClient grava a URL com lat/lon: desligado (LGPD, RNF03).
            .RemoveAllLoggers();

        services.AddSingleton<INominatimClient, NominatimClient>();
        services.AddSingleton<IServicoGeocodificacao, ServicoGeocodificacao>();

        return services;
    }

    /// <summary>
    /// Configuração ativa do motor. Da configuração vem só o nome (<c>Confianca:Configuracao</c>, padrão "B");
    /// os valores vêm do Domain.
    /// </summary>
    private static IServiceCollection AddConfianca(this IServiceCollection services, IConfiguration configuracao)
    {
        var nome = configuracao.GetValue("Confianca:Configuracao", ConfiguracaoConfianca.B.Nome)!;
        services.AddSingleton(ConfiguracaoConfianca.PorNome(nome));

        services.AddOptions<CicloDeVidaOptions>().BindConfiguration(CicloDeVidaOptions.Secao);
        services.AddScoped<ProcessadorCicloDeVida>();
        services.AddHostedService<CicloDeVidaJob>();

        return services;
    }

    /// <summary>Services das features, um por área.</summary>
    private static IServiceCollection AddServicos(this IServiceCollection services)
    {
        return services;
    }
}
```

- [ ] **Step 4: `EndpointRouteBuilderExtensions.cs` e ponte para as features legadas**

```csharp
using System.Reflection;
using Ocorrencias.Api.Features;

namespace Ocorrencias.Api.Common;

public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Registra todos os <see cref="IEndpointModule"/> sob <c>/api/v1</c>. Por padrão exige autenticação; endpoints de
    /// visitante (🔓 em docs/API.md) chamam <c>AllowAnonymous()</c> no próprio módulo.
    /// </summary>
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization(PoliticasAutorizacao.Autenticado);

        foreach (var modulo in Modulos())
        {
            modulo.GetMethod(nameof(IEndpointModule.Map), BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [api]);
        }

        api.MapFeaturesLegadas();

        return app;
    }

    private static IEnumerable<Type> Modulos() =>
        typeof(EndpointRouteBuilderExtensions).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IEndpointModule).IsAssignableFrom(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal);
}
```

`Features/Endpoints.cs` fica assim (mesmas 21 chamadas, sem criar o grupo):
```csharp
using Ocorrencias.Api.Features.Admin;
using Ocorrencias.Api.Features.Auth;
using Ocorrencias.Api.Features.Categorias;
using Ocorrencias.Api.Features.Geocodificacao;
using Ocorrencias.Api.Features.Incidentes;
using Ocorrencias.Api.Features.Problemas;
using Ocorrencias.Api.Features.Usuarios;
using Ocorrencias.Api.Features.Verificacao;
using Ocorrencias.Api.Features.Votos;

namespace Ocorrencias.Api.Features;

/// <summary>Features ainda não migradas para <c>Modules/</c>. Removido quando a última área migrar.</summary>
public static class Endpoints
{
    public static RouteGroupBuilder MapFeaturesLegadas(this RouteGroupBuilder api)
    {
        api.MapCadastrar();
        api.MapEntrar();

        api.MapObterMe();
        api.MapAtualizarMe();
        api.MapObterHistorico();

        api.MapListarCategorias();

        api.MapReversa();

        api.MapListarProblemasNaArea();
        api.MapListarProblemasRecentes();
        api.MapBuscarDuplicatas();
        api.MapObterDetalheProblema();
        api.MapRegistrarProblema();

        api.MapVotarExistencia();
        api.MapVotarPersistencia();

        api.MapRegistrarIncidente();
        api.MapObterCalorIncidentes();
        api.MapObterAreaIncidentes();

        api.MapSolicitarVerificacao();

        api.MapListarVerificacoes();
        api.MapAprovarVerificacao();
        api.MapRecusarVerificacao();

        return api;
    }
}
```

- [ ] **Step 5: Trocar as referências às constantes antigas**

- `Features/Admin/Aprovar.cs`, `Recusar.cs`, `ListarVerificacoes.cs`: `AuthConfig.Administrador` → `PoliticasAutorizacao.Administrador`. Troque `using Ocorrencias.Api.Infrastructure;` por `using Ocorrencias.Api.Common;` se ele ainda não estiver no arquivo.
- `Features/Geocodificacao/Reversa.cs`: `LimitesDeRequisicao.Geocodificacao` → `PoliticasLimite.Geocodificacao`.

- [ ] **Step 6: Ordem fixa das tags no OpenAPI**

O array `tags` da raiz segue a ordem em que os endpoints são registrados. Com os módulos descobertos por nome, essa ordem mudaria, e com ela a barra lateral do Scalar. Em `OpenApiConfig.AddOpenApiConfigurado`, acrescente um document transformer depois dos que já existem e os dois membros privados abaixo:

```csharp
            // Ordem das seções no Scalar: a de docs/API.md, independente da ordem de registro dos módulos.
            o.AddDocumentTransformer((documento, _, _) =>
            {
                if (documento.Tags is { Count: > 0 } tags)
                {
                    documento.Tags = new SortedSet<OpenApiTag>(tags, Comparer<OpenApiTag>.Create((a, b) =>
                    {
                        var (pa, pb) = (PosicaoTag(a.Name), PosicaoTag(b.Name));
                        return pa != pb ? pa.CompareTo(pb) : string.CompareOrdinal(a.Name, b.Name);
                    }));
                }

                return Task.CompletedTask;
            });
```
```csharp
    private static readonly string[] OrdemTags =
        ["Auth", "Usuários", "Categorias", "Geocodificação", "Problemas", "Votos", "Incidentes", "Verificação", "Admin"];

    private static int PosicaoTag(string? nome)
    {
        var posicao = Array.IndexOf(OrdemTags, nome);
        return posicao < 0 ? int.MaxValue : posicao;
    }
```
(`OpenApiDocument.Tags` é `ISet<OpenApiTag>` no Microsoft.OpenApi 2.12; o `SortedSet` com comparador fixa a ordem de serialização.)

- [ ] **Step 7: `Program.cs` final**

```csharp
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Infrastructure;
using Ocorrencias.Api.Persistence;
using Scalar.AspNetCore;

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

- [ ] **Step 8: Apagar os arquivos de configuração antigos**

```bash
git rm src/Ocorrencias.Api/Common/CorsConfig.cs src/Ocorrencias.Api/Common/LimitesDeRequisicao.cs \
  src/Ocorrencias.Api/Infrastructure/Configurations/AuthConfig.cs src/Ocorrencias.Api/Infrastructure/Persistence/DadosConfig.cs \
  src/Ocorrencias.Api/Feature/Geocodificacao/GeocodificacaoConfig.cs src/Ocorrencias.Api/Feature/Confianca/ConfiancaConfig.cs
```

- [ ] **Step 9: Build e testes**

Run: `dotnet build && dotnet test`
Expected: tudo verde. O `ContratoTests.Openapi_igual_ao_snapshot` prova que a ordem das tags e os schemas não mudaram, e o `Rotas_e_politicas_do_contrato` prova que as 21 rotas e as políticas continuam iguais.

- [ ] **Step 10: Commit**

```bash
git add -A src/Ocorrencias.Api
git commit -m "refactor(infra): centraliza registro de dependências e mapeamento de endpoints"
```

---

### Task 5: Área Categorias

**Files:**
- Create: `src/Ocorrencias.Api/Feature/Categorias/Models/CategoriaDtos.cs`, `Abstractions/ICategoriaService.cs`, `Services/CategoriaService.cs`
- Create: `src/Ocorrencias.Api/Modules/CategoriaModule.cs`
- Delete: `src/Ocorrencias.Api/Features/Categorias/` (`Listar.cs`, `CategoriaResumo.cs`)
- Modify: `Features/Endpoints.cs` (remover `api.MapListarCategorias();` e o `using ...Features.Categorias;`), `DependencyInjection.AddServicos`
- Modify: usings `Ocorrencias.Api.Features.Categorias` → `Ocorrencias.Api.Categorias` em `Features/Problemas/{BuscarDuplicatas,ListarRecentes,ObterDetalhe}.cs`, `Features/Usuarios/ObterHistorico.cs`
- Test: `tests/Ocorrencias.Api.Tests/Problemas/ProblemasTests.cs`, `Categorias/CategoriasTests.cs` (só renomes)

**Interfaces:**
- Produces (`Ocorrencias.Api.Categorias`): `CategoriaResumo(short Id, string Nome, string Grupo, string Icone)`, `ListarCategoriasResponse`, `ICategoriaService.ListarAsync(CancellationToken)` → `Task<List<ListarCategoriasResponse>>`.

- [ ] **Step 1: Atualizar os testes para os nomes novos**

Em `Problemas/ProblemasTests.cs`: adicione `using Ocorrencias.Api.Categorias;` e troque `new Features.Categorias.CategoriaResumo(` por `new CategoriaResumo(`. Em `Categorias/CategoriasTests.cs`, se usar `ListarCategorias.Response`, troque por `ListarCategoriasResponse` com `using Ocorrencias.Api.Categorias;`.

- [ ] **Step 2: Build falha**

Run: `dotnet build tests/Ocorrencias.Api.Tests`
Expected: FAIL, `CS0246: CategoriaResumo` / namespace `Ocorrencias.Api.Categorias` não existe.

- [ ] **Step 3: Implementar**

`Feature/Categorias/Models/CategoriaDtos.cs`:
```csharp
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Categorias;

/// <summary>Categoria embutida em outras respostas (<c>{ id, nome, grupo, icone }</c>).</summary>
public sealed record CategoriaResumo(short Id, string Nome, string Grupo, string Icone);

public sealed record ListarCategoriasResponse(short Id, string Nome, string Grupo, TipoOcorrencia Tipo, string Icone);
```

`Feature/Categorias/Abstractions/ICategoriaService.cs`:
```csharp
namespace Ocorrencias.Api.Categorias;

public interface ICategoriaService
{
    Task<List<ListarCategoriasResponse>> ListarAsync(CancellationToken ct);
}
```

`Feature/Categorias/Services/CategoriaService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Persistence;

namespace Ocorrencias.Api.Categorias;

public sealed class CategoriaService(AppDbContext db) : ICategoriaService
{
    public Task<List<ListarCategoriasResponse>> ListarAsync(CancellationToken ct) =>
        db.Categorias
            .OrderBy(c => c.Id)
            .Select(c => new ListarCategoriasResponse(c.Id, c.Nome, c.Grupo, c.Tipo, c.Icone))
            .ToListAsync(ct);
}
```

`Modules/CategoriaModule.cs`:
```csharp
using Ocorrencias.Api.Categorias;
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Modules;

public sealed class CategoriaModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/categorias", Listar)
            .AllowAnonymous()
            .WithTags("Categorias");
    }

    private static Task<List<ListarCategoriasResponse>> Listar(ICategoriaService service, CancellationToken ct) =>
        service.ListarAsync(ct);
}
```

Em `DependencyInjection.AddServicos`, antes do `return`:
```csharp
        services.AddScoped<ICategoriaService, CategoriaService>();
```
(acrescente `using Ocorrencias.Api.Categorias;` no topo).

Apague `Features/Categorias/` (`git rm -r src/Ocorrencias.Api/Features/Categorias`), remova `api.MapListarCategorias();` e o using de `Features/Endpoints.cs`, e troque o using de `CategoriaResumo` nos 4 arquivos legados listados em **Files**.

- [ ] **Step 4: Testes**

Run: `dotnet build && dotnet test tests/Ocorrencias.Api.Tests`
Expected: tudo verde, inclusive `ContratoTests`.

- [ ] **Step 5: Commit**

```bash
git add -A src/Ocorrencias.Api tests/Ocorrencias.Api.Tests
git commit -m "refactor(categorias): extrai service e module"
```

---

### Task 6: Área Auth

**Files:**
- Create: `Feature/Auth/Models/AuthDtos.cs`, `Feature/Auth/Validators/AuthValidators.cs`, `Feature/Auth/Errors/ErrosAuth.cs`, `Feature/Auth/Abstractions/IAuthService.cs`, `Feature/Auth/Services/AuthService.cs`, `Modules/AuthModule.cs`
- Delete: `Features/Auth/` (`Cadastrar.cs`, `Entrar.cs`, `RespostaAuth.cs`)
- Modify: `Features/Endpoints.cs` (remover `MapCadastrar`, `MapEntrar` e o using), `Common/Erros.cs` (remover `EmailJaCadastrado` e `CredenciaisInvalidas`), `DependencyInjection.AddServicos`
- Test: `tests/Ocorrencias.Api.Tests/Infra/ClienteApi.cs`, `Infra/Cenarios.cs`, `Auth/AuthTests.cs` (renomes)

**Interfaces:**
- Consumes: `JwtTokenService.Gerar(Usuario)` → `(string Token, DateTimeOffset ExpiraEm)` (já existe em `Feature/Auth/Services`), `Resultado<T>`, `ErroAplicacao`.
- Produces (`Ocorrencias.Api.Auth`): `CadastrarRequest`, `EntrarRequest`, `RespostaAuth`, `IAuthService.CadastrarAsync(CadastrarRequest)` / `EntrarAsync(EntrarRequest)` → `Task<Resultado<RespostaAuth>>`.

- [ ] **Step 1: Atualizar os testes**

- `Infra/ClienteApi.cs`: `using Ocorrencias.Api.Features.Auth;` → `using Ocorrencias.Api.Auth;`
- `Infra/Cenarios.cs`: adicione `using Ocorrencias.Api.Auth;` e troque `Features.Auth.RespostaAuth` por `RespostaAuth`.
- `Auth/AuthTests.cs`: troque `using Ocorrencias.Api.Features.Auth;` por `using Ocorrencias.Api.Auth;` e os tipos aninhados pelo mapa de renomeações.

- [ ] **Step 2: Build falha**

Run: `dotnet build tests/Ocorrencias.Api.Tests`
Expected: FAIL, namespace `Ocorrencias.Api.Auth` sem `RespostaAuth`.

- [ ] **Step 3: Implementar**

`Feature/Auth/Models/AuthDtos.cs`:
```csharp
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Auth;

public sealed record CadastrarRequest(string Nome, string Email, string Senha, bool AceitouTermos);

public sealed record EntrarRequest(string Email, string Senha);

/// <summary>Corpo de resposta comum a cadastro e login.</summary>
public sealed record RespostaAuth(string Token, DateTimeOffset ExpiraEm, RespostaAuth.UsuarioResumo Usuario)
{
    public sealed record UsuarioResumo(Guid Id, string Nome, string Email, Perfil Perfil);
}
```

`Feature/Auth/Validators/AuthValidators.cs`:
```csharp
using FluentValidation;

namespace Ocorrencias.Api.Auth;

public sealed class CadastrarValidator : AbstractValidator<CadastrarRequest>
{
    public CadastrarValidator()
    {
        RuleFor(r => r.Nome).NotEmpty().MaximumLength(120);
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(r => r.Senha).NotEmpty().MinimumLength(8).WithMessage("A senha precisa ter pelo menos 8 caracteres.");
        RuleFor(r => r.AceitouTermos).Equal(true).WithMessage("É preciso aceitar os termos de uso.");
    }
}

public sealed class EntrarValidator : AbstractValidator<EntrarRequest>
{
    public EntrarValidator()
    {
        RuleFor(r => r.Email).NotEmpty();
        RuleFor(r => r.Senha).NotEmpty();
    }
}
```

`Feature/Auth/Errors/ErrosAuth.cs`:
```csharp
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Auth;

public static class ErrosAuth
{
    public static readonly ErroAplicacao EmailJaCadastrado =
        ErroAplicacao.Regra(StatusCodes.Status409Conflict, "email-ja-cadastrado", "Já existe uma conta com este e-mail.");

    public static readonly ErroAplicacao CredenciaisInvalidas =
        ErroAplicacao.Regra(StatusCodes.Status401Unauthorized, null, "E-mail ou senha inválidos.");
}
```

`Feature/Auth/Abstractions/IAuthService.cs`:
```csharp
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Auth;

public interface IAuthService
{
    Task<Resultado<RespostaAuth>> CadastrarAsync(CadastrarRequest request);

    Task<Resultado<RespostaAuth>> EntrarAsync(EntrarRequest request);
}
```

`Feature/Auth/Services/AuthService.cs`:
```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Persistence;

namespace Ocorrencias.Api.Auth;

public sealed class AuthService(UserManager<Usuario> usuarios, JwtTokenService jwt, TimeProvider relogio) : IAuthService
{
    public async Task<Resultado<RespostaAuth>> CadastrarAsync(CadastrarRequest request)
    {
        var email = request.Email.Trim();
        if (await usuarios.FindByEmailAsync(email) is not null)
        {
            return ErrosAuth.EmailJaCadastrado;
        }

        var usuario = new Usuario
        {
            Id = Guid.CreateVersion7(),
            Nome = request.Nome.Trim(),
            Email = email,
            UserName = email,
            CriadoEm = relogio.GetUtcNow(),
        };

        IdentityResult resultado;
        try
        {
            resultado = await usuarios.CreateAsync(usuario, request.Senha);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Corrida entre a checagem acima e o INSERT: o índice único de e-mail barra no banco.
            return ErrosAuth.EmailJaCadastrado;
        }

        if (!resultado.Succeeded)
        {
            if (resultado.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
            {
                return ErrosAuth.EmailJaCadastrado;
            }

            throw new InvalidOperationException(
                "Falha ao criar usuário: " + string.Join("; ", resultado.Errors.Select(e => e.Code)));
        }

        return CriarResposta(usuario);
    }

    public async Task<Resultado<RespostaAuth>> EntrarAsync(EntrarRequest request)
    {
        var usuario = await usuarios.FindByEmailAsync(request.Email.Trim());
        if (usuario is null || !await usuarios.CheckPasswordAsync(usuario, request.Senha))
        {
            return ErrosAuth.CredenciaisInvalidas;
        }

        return CriarResposta(usuario);
    }

    private RespostaAuth CriarResposta(Usuario usuario)
    {
        var (token, expiraEm) = jwt.Gerar(usuario);
        return new RespostaAuth(token, expiraEm, new RespostaAuth.UsuarioResumo(usuario.Id, usuario.Nome, usuario.Email!, usuario.Perfil));
    }
}
```

`Modules/AuthModule.cs`:
```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using Ocorrencias.Api.Auth;
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Modules;

public sealed class AuthModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapPost("/auth/cadastro", Cadastrar)
            .Validar<CadastrarRequest>()
            .AllowAnonymous()
            .WithTags("Auth");

        api.MapPost("/auth/login", Entrar)
            .Validar<EntrarRequest>()
            .AllowAnonymous()
            .WithTags("Auth");
    }

    private static async Task<Results<Created<RespostaAuth>, ProblemHttpResult>> Cadastrar(CadastrarRequest request, IAuthService service)
    {
        var r = await service.CadastrarAsync(request);
        return r.Erro switch
        {
            null => TypedResults.Created("/api/v1/me", r.Valor!),
            var e => e.ParaProblema(),
        };
    }

    private static async Task<Results<Ok<RespostaAuth>, ProblemHttpResult>> Entrar(EntrarRequest request, IAuthService service)
    {
        var r = await service.EntrarAsync(request);
        return r.Erro switch
        {
            null => TypedResults.Ok(r.Valor!),
            var e => e.ParaProblema(),
        };
    }
}
```

Em `AddServicos`: `services.AddScoped<IAuthService, AuthService>();` (com `using Ocorrencias.Api.Auth;`).

Apague `Features/Auth/`, remova as duas chamadas e o using de `Features/Endpoints.cs`, e remova `EmailJaCadastrado` e `CredenciaisInvalidas` de `Common/Erros.cs`.

- [ ] **Step 4: Testes**

Run: `dotnet build && dotnet test tests/Ocorrencias.Api.Tests`
Expected: tudo verde.

- [ ] **Step 5: Commit**

```bash
git add -A src/Ocorrencias.Api tests/Ocorrencias.Api.Tests
git commit -m "refactor(auth): extrai service, erros e module"
```

---

### Task 7: Área Usuários

**Files:**
- Create: `Feature/Usuarios/Models/UsuarioDtos.cs`, `Feature/Usuarios/Models/LinhaHistorico.cs`, `Feature/Usuarios/Validators/UsuarioValidators.cs`, `Feature/Usuarios/Abstractions/IUsuarioService.cs`, `Feature/Usuarios/Services/UsuarioService.cs`, `Modules/UsuarioModule.cs`
- Delete: `Features/Usuarios/`
- Modify: `Features/Endpoints.cs` (remover `MapObterMe`, `MapAtualizarMe`, `MapObterHistorico` e o using), `AddServicos`
- Test: `Usuarios/MeTests.cs`, `Usuarios/HistoricoTests.cs`, `Verificacao/VerificacaoTests.cs` (usa `ObterMe.Response`) e qualquer outro que use `ObterMe.*`

**Interfaces:**
- Consumes: `CategoriaResumo` (Task 5), `Paginado<T>`, `Paginacao`.
- Produces (`Ocorrencias.Api.Usuarios`):
  - `ObterMeResponse`, `ObterMeVerificacaoResumo`, `AtualizarMeRequest`, `ObterHistoricoRequest`, `ObterHistoricoItem`, `ObterHistoricoTipoItem`, `ObterHistoricoValorItem`
  - `IUsuarioService.ObterMeAsync(Guid usuarioId, CancellationToken)` → `Task<Resultado<ObterMeResponse>>`
  - `IUsuarioService.AtualizarMeAsync(Guid usuarioId, AtualizarMeRequest, CancellationToken)` → `Task<Resultado<ObterMeResponse>>`
  - `IUsuarioService.ObterHistoricoAsync(Guid usuarioId, ObterHistoricoRequest, CancellationToken)` → `Task<Paginado<ObterHistoricoItem>>`

- [ ] **Step 1: Atualizar os testes**

- `Usuarios/HistoricoTests.cs`: troque `using static Ocorrencias.Api.Features.Usuarios.ObterHistorico;` e `using Ocorrencias.Api.Features.Usuarios;` por `using Ocorrencias.Api.Usuarios;`. Depois troque `Item` → `ObterHistoricoItem`, `TipoItem` → `ObterHistoricoTipoItem` e `ValorItem` → `ObterHistoricoValorItem`.
- `MeTests.cs`, `VerificacaoTests.cs` e outros: `using Ocorrencias.Api.Features.Usuarios;` → `using Ocorrencias.Api.Usuarios;`; `ObterMe.Response` → `ObterMeResponse`; `ObterMe.VerificacaoResumo` → `ObterMeVerificacaoResumo`.

- [ ] **Step 2: Build falha**

Run: `dotnet build tests/Ocorrencias.Api.Tests` → FAIL (`ObterMeResponse` inexistente).

- [ ] **Step 3: Implementar**

`Feature/Usuarios/Models/UsuarioDtos.cs`:
```csharp
using Ocorrencias.Api.Categorias;
using Ocorrencias.Api.Common;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Usuarios;

public sealed record ObterMeResponse(
    Guid Id,
    string Nome,
    string Email,
    Perfil Perfil,
    DateTimeOffset CriadoEm,
    DateTimeOffset? VerificadoEm,
    int TotalRegistros,
    int TotalVotos,
    ObterMeVerificacaoResumo? Verificacao);

/// <summary>Solicitação de verificação mais recente.</summary>
public sealed record ObterMeVerificacaoResumo(
    StatusVerificacao Status, TipoVinculo TipoVinculo, string? MotivoRecusa, DateTimeOffset CriadaEm);

public sealed record AtualizarMeRequest(string Nome);

public enum ObterHistoricoTipoItem
{
    RegistroProblema = 1,
    RegistroIncidente = 2,
    VotoExistencia = 3,
    VotoPersistencia = 4,
}

public enum ObterHistoricoValorItem
{
    Confirma = 1,
    NaoVi = 2,
    Continua = 3,
    Resolvido = 4,
}

/// <param name="Filtro">tudo | registros | votos.</param>
public sealed record ObterHistoricoRequest(string Filtro = "tudo", int Pagina = 1, int Tamanho = Paginacao.TamanhoPadrao);

/// <summary>
/// Para <c>registroIncidente</c>, <c>endereco</c> é o rótulo da área (nulo se a geocodificação falhou) e
/// <c>ocorrenciaId</c> é nulo: o incidente não é navegável.
/// </summary>
public sealed record ObterHistoricoItem(
    ObterHistoricoTipoItem Tipo,
    Guid? OcorrenciaId,
    CategoriaResumo Categoria,
    string? Endereco,
    ObterHistoricoValorItem? Valor,
    StatusProblema? StatusProblema,
    DateTimeOffset Data);
```

`Feature/Usuarios/Models/LinhaHistorico.cs`:
```csharp
using System.ComponentModel.DataAnnotations.Schema;

namespace Ocorrencias.Api.Usuarios;

/// <summary>Linha crua da união (registros de problema, de incidente e votos).</summary>
public sealed class LinhaHistorico
{
    /// <summary>Id da linha de origem (problema, incidente ou voto): desempate único da paginação, não exposto.</summary>
    [Column("chave")] public Guid Chave { get; init; }
    [Column("tipo")] public int Tipo { get; init; }
    [Column("ocorrencia_id")] public Guid? OcorrenciaId { get; init; }
    [Column("categoria_id")] public short CategoriaId { get; init; }
    [Column("categoria_nome")] public string CategoriaNome { get; init; } = "";
    [Column("grupo")] public string Grupo { get; init; } = "";
    [Column("icone")] public string Icone { get; init; } = "";
    [Column("endereco")] public string? Endereco { get; init; }
    [Column("valor")] public short? Valor { get; init; }
    [Column("status_problema")] public short? StatusProblema { get; init; }
    [Column("data")] public DateTimeOffset Data { get; init; }
}
```

`Feature/Usuarios/Validators/UsuarioValidators.cs`:
```csharp
using FluentValidation;
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Usuarios;

public sealed class AtualizarMeValidator : AbstractValidator<AtualizarMeRequest>
{
    public AtualizarMeValidator()
    {
        RuleFor(r => r.Nome).NotEmpty().MaximumLength(120);
    }
}

public sealed class ObterHistoricoValidator : AbstractValidator<ObterHistoricoRequest>
{
    private static readonly string[] Filtros = ["tudo", "registros", "votos"];

    public ObterHistoricoValidator()
    {
        RuleFor(r => r.Filtro).Must(f => Filtros.Contains(f)).WithMessage("Filtro deve ser tudo, registros ou votos.");
        RuleFor(r => r.Pagina).InclusiveBetween(1, Paginacao.PaginaMaxima);
        RuleFor(r => r.Tamanho).InclusiveBetween(1, Paginacao.TamanhoMaximo);
    }
}
```

`Feature/Usuarios/Abstractions/IUsuarioService.cs`:
```csharp
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Usuarios;

public interface IUsuarioService
{
    Task<Resultado<ObterMeResponse>> ObterMeAsync(Guid usuarioId, CancellationToken ct);

    Task<Resultado<ObterMeResponse>> AtualizarMeAsync(Guid usuarioId, AtualizarMeRequest request, CancellationToken ct);

    Task<Paginado<ObterHistoricoItem>> ObterHistoricoAsync(Guid usuarioId, ObterHistoricoRequest request, CancellationToken ct);
}
```

`Feature/Usuarios/Services/UsuarioService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Categorias;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Usuarios;

public sealed class UsuarioService(AppDbContext db) : IUsuarioService
{
    /// <summary>Usuário do token que não existe mais → <see cref="ErroAplicacao.NaoAutorizado"/>.</summary>
    public async Task<Resultado<ObterMeResponse>> ObterMeAsync(Guid usuarioId, CancellationToken ct)
    {
        var resposta = await ConsultarMeAsync(usuarioId, ct);
        if (resposta is null)
        {
            return ErroAplicacao.NaoAutorizado;
        }

        return resposta;
    }

    /// <summary>Responde com o mesmo corpo de <c>GET /me</c>.</summary>
    public async Task<Resultado<ObterMeResponse>> AtualizarMeAsync(Guid usuarioId, AtualizarMeRequest request, CancellationToken ct)
    {
        var alterados = await db.Users
            .Where(u => u.Id == usuarioId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Nome, request.Nome.Trim()), ct);

        if (alterados == 0)
        {
            return ErroAplicacao.NaoAutorizado;
        }

        return (await ConsultarMeAsync(usuarioId, ct))!;
    }

    /// <summary>Itens em ordem decrescente de data.</summary>
    public async Task<Paginado<ObterHistoricoItem>> ObterHistoricoAsync(Guid usuarioId, ObterHistoricoRequest request, CancellationToken ct)
    {
        var registros = request.Filtro is "tudo" or "registros";
        var votos = request.Filtro is "tudo" or "votos";

        var uniao = db.Database.SqlQuery<LinhaHistorico>($"""
            SELECT p.id AS chave, 1 AS tipo, p.id AS ocorrencia_id, c.id AS categoria_id, c.nome AS categoria_nome, c.grupo, c.icone,
                   p.endereco_aproximado AS endereco, NULL::smallint AS valor, p.status AS status_problema, p.criado_em AS data
            FROM problemas p JOIN categorias c ON c.id = p.categoria_id
            WHERE p.autor_id = {usuarioId} AND {registros}
            UNION ALL
            SELECT i.id, 2, NULL::uuid, c.id, c.nome, c.grupo, c.icone,
                   i.rotulo_area, NULL::smallint, NULL::smallint, i.registrado_em
            FROM incidentes i JOIN categorias c ON c.id = i.categoria_id
            WHERE i.autor_id = {usuarioId} AND {registros}
            UNION ALL
            SELECT v.id, CASE v.tipo WHEN 1 THEN 3 ELSE 4 END, p.id, c.id, c.nome, c.grupo, c.icone,
                   p.endereco_aproximado, v.valor, p.status, v.registrado_em
            FROM votos v JOIN problemas p ON p.id = v.problema_id JOIN categorias c ON c.id = p.categoria_id
            WHERE v.usuario_id = {usuarioId} AND {votos}
            """);

        var total = await uniao.CountAsync(ct);
        var linhas = await uniao
            .OrderByDescending(l => l.Data)
            .ThenBy(l => l.Chave)
            .Skip(Paginacao.Pular(request.Pagina, request.Tamanho))
            .Take(request.Tamanho)
            .ToListAsync(ct);

        return new Paginado<ObterHistoricoItem>(linhas.Select(Mapear).ToList(), request.Pagina, request.Tamanho, total);
    }

    /// <summary>Nulo se o usuário do token não existe mais.</summary>
    private Task<ObterMeResponse?> ConsultarMeAsync(Guid usuarioId, CancellationToken ct) =>
        db.Users
            .Where(u => u.Id == usuarioId)
            .Select(u => new ObterMeResponse(
                u.Id,
                u.Nome,
                u.Email!,
                u.Perfil,
                u.CriadoEm,
                u.VerificadoEm,
                db.Problemas.Count(p => p.AutorId == u.Id) + db.Incidentes.Count(i => i.AutorId == u.Id),
                db.Votos.Count(v => v.UsuarioId == u.Id),
                db.SolicitacoesVerificacao
                    .Where(s => s.UsuarioId == u.Id)
                    .OrderByDescending(s => s.CriadaEm)
                    .Select(s => new ObterMeVerificacaoResumo(s.Status, s.TipoVinculo, s.MotivoRecusa, s.CriadaEm))
                    .FirstOrDefault()))
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

    private static ObterHistoricoItem Mapear(LinhaHistorico l)
    {
        var tipo = (ObterHistoricoTipoItem)l.Tipo;
        ObterHistoricoValorItem? valor = (tipo, (ValorVoto?)l.Valor) switch
        {
            (ObterHistoricoTipoItem.VotoExistencia, ValorVoto.Positivo) => ObterHistoricoValorItem.Confirma,
            (ObterHistoricoTipoItem.VotoExistencia, ValorVoto.Negativo) => ObterHistoricoValorItem.NaoVi,
            (ObterHistoricoTipoItem.VotoPersistencia, ValorVoto.Positivo) => ObterHistoricoValorItem.Continua,
            (ObterHistoricoTipoItem.VotoPersistencia, ValorVoto.Negativo) => ObterHistoricoValorItem.Resolvido,
            _ => null,
        };

        return new ObterHistoricoItem(
            tipo,
            l.OcorrenciaId,
            new CategoriaResumo(l.CategoriaId, l.CategoriaNome, l.Grupo, l.Icone),
            l.Endereco,
            valor,
            (StatusProblema?)l.StatusProblema,
            l.Data);
    }
}
```

`Modules/UsuarioModule.cs`:
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Usuarios;

namespace Ocorrencias.Api.Modules;

public sealed class UsuarioModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/me", ObterMe).WithTags("Usuários");

        api.MapPatch("/me", AtualizarMe)
            .Validar<AtualizarMeRequest>()
            .WithTags("Usuários");

        api.MapGet("/me/historico", ObterHistorico)
            .Validar<ObterHistoricoRequest>()
            .WithTags("Usuários");
    }

    private static async Task<Results<Ok<ObterMeResponse>, UnauthorizedHttpResult>> ObterMe(
        ClaimsPrincipal principal, IUsuarioService service, CancellationToken ct)
    {
        var r = await service.ObterMeAsync(principal.ObterUsuarioId(), ct);
        return r.Erro switch
        {
            null => TypedResults.Ok(r.Valor!),
            var e => e.ParaNaoAutorizado(),
        };
    }

    private static async Task<Results<Ok<ObterMeResponse>, UnauthorizedHttpResult>> AtualizarMe(
        AtualizarMeRequest request, ClaimsPrincipal principal, IUsuarioService service, CancellationToken ct)
    {
        var r = await service.AtualizarMeAsync(principal.ObterUsuarioId(), request, ct);
        return r.Erro switch
        {
            null => TypedResults.Ok(r.Valor!),
            var e => e.ParaNaoAutorizado(),
        };
    }

    private static Task<Paginado<ObterHistoricoItem>> ObterHistorico(
        [AsParameters] ObterHistoricoRequest request, ClaimsPrincipal principal, IUsuarioService service, CancellationToken ct) =>
        service.ObterHistoricoAsync(principal.ObterUsuarioId(), request, ct);
}
```

Em `AddServicos`: `services.AddScoped<IUsuarioService, UsuarioService>();` (com `using Ocorrencias.Api.Usuarios;`). Apague `Features/Usuarios/` e remova as 3 chamadas e o using de `Features/Endpoints.cs`.

- [ ] **Step 4: Testes**

Run: `dotnet build && dotnet test tests/Ocorrencias.Api.Tests` → tudo verde.

- [ ] **Step 5: Commit**

```bash
git add -A src/Ocorrencias.Api tests/Ocorrencias.Api.Tests
git commit -m "refactor(usuarios): extrai service e module"
```

---

### Task 8: Área Geocodificação (module)

**Files:**
- Create: `Feature/Geocodificacao/Models/GeocodificacaoDtos.cs`, `Feature/Geocodificacao/Validators/GeocodificacaoValidators.cs`, `Modules/GeocodificacaoModule.cs`
- Delete: `Features/Geocodificacao/Reversa.cs`
- Modify: `Features/Endpoints.cs` (remover `MapReversa` e o using)
- Test: `Geocodificacao/ReversaTests.cs` (`Reversa.Response` → `ReversaResponse`, `using Ocorrencias.Api.Geocodificacao;` no lugar de `using Ocorrencias.Api.Features.Geocodificacao;`)

**Interfaces:**
- Consumes: `IServicoGeocodificacao.ReversaAsync(double lat, double lng, CancellationToken)` → `Task<EnderecoGeocodificado>` (já existe), `PoliticasLimite.Geocodificacao`.
- Produces: `ReversaRequest(double Lat, double Lng)`, `ReversaResponse(...)`. Não há service novo.

- [ ] **Step 1: Atualizar `ReversaTests.cs`** conforme **Files**.
- [ ] **Step 2: Build falha** — Run: `dotnet build tests/Ocorrencias.Api.Tests` → FAIL (`ReversaResponse`).
- [ ] **Step 3: Implementar**

`Feature/Geocodificacao/Models/GeocodificacaoDtos.cs`:
```csharp
namespace Ocorrencias.Api.Geocodificacao;

public sealed record ReversaRequest(double Lat, double Lng);

public sealed record ReversaResponse(
    bool Disponivel,
    string Endereco,
    string? Logradouro,
    string? Numero,
    string? Bairro,
    string? Cidade,
    string? Uf,
    string? RotuloArea);
```

`Feature/Geocodificacao/Validators/GeocodificacaoValidators.cs`:
```csharp
using FluentValidation;

namespace Ocorrencias.Api.Geocodificacao;

public sealed class ReversaValidator : AbstractValidator<ReversaRequest>
{
    public ReversaValidator()
    {
        RuleFor(r => r.Lat).InclusiveBetween(-90, 90);
        RuleFor(r => r.Lng).InclusiveBetween(-180, 180);
    }
}
```

`Modules/GeocodificacaoModule.cs`:
```csharp
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Geocodificacao;

namespace Ocorrencias.Api.Modules;

public sealed class GeocodificacaoModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/geocodificacao/reversa", Reversa)
            .Validar<ReversaRequest>()
            .RequireRateLimiting(PoliticasLimite.Geocodificacao)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous()
            .WithTags("Geocodificação");
    }

    /// <summary>Sempre 200: com o Nominatim fora do ar e sem cache, vem <c>disponivel: false</c>.</summary>
    private static async Task<ReversaResponse> Reversa(
        [AsParameters] ReversaRequest request, IServicoGeocodificacao geocodificacao, CancellationToken ct)
    {
        var e = await geocodificacao.ReversaAsync(request.Lat, request.Lng, ct);
        return new ReversaResponse(e.Disponivel, e.Endereco, e.Logradouro, e.Numero, e.Bairro, e.Cidade, e.Uf, e.RotuloArea);
    }
}
```

Apague `Features/Geocodificacao/` e remova a chamada e o using de `Features/Endpoints.cs`.

- [ ] **Step 4: Testes** — Run: `dotnet build && dotnet test tests/Ocorrencias.Api.Tests` → tudo verde, incluindo o 429 de `ReversaTests`.
- [ ] **Step 5: Commit**

```bash
git add -A src/Ocorrencias.Api tests/Ocorrencias.Api.Tests
git commit -m "refactor(geocodificacao): move a reversa para module"
```

---

### Task 9: Área Problemas

**Files:**
- Create: `Feature/Problemas/Models/ProblemaDtos.cs`, `Feature/Problemas/Models/LimitesProblema.cs`, `Feature/Problemas/Validators/ProblemaValidators.cs`, `Feature/Problemas/Errors/ErrosProblema.cs`, `Feature/Problemas/Abstractions/IProblemaService.cs`, `Feature/Problemas/Services/ProblemaService.cs`, `Feature/Problemas/Services/ExibicaoConfianca.cs`, `Modules/ProblemaModule.cs`
- Delete: `Features/Problemas/` (6 arquivos)
- Modify: `Features/Votos/{Votacao,VotarExistencia,VotarPersistencia}.cs` (passam a usar `IProblemaService.ConsultarDetalheAsync`), `Features/Endpoints.cs` (remover as 5 chamadas e o using), `Common/Erros.cs` (remover `CategoriaNaoEProblema`), `AddServicos`
- Test: `Infra/Cenarios.cs`, `Problemas/ProblemasTests.cs`, `Votos/VotosTests.cs`, `Confianca/CicloDeVidaTests.cs`, `Admin/AdminTests.cs` e qualquer outro que use `ObterDetalheProblema.*`, `ListarProblemasNaArea.*`, `ListarProblemasRecentes.*` ou `BuscarDuplicatas.*`

**Interfaces:**
- Consumes: `CategoriaResumo` (Task 5), `ValorExistencia`/`ValorPersistencia` (ainda em `Ocorrencias.Api.Features.Votos`; a Task 10 move), `IServicoGeocodificacao`, `ConfiguracaoConfianca`, `Geo`, `Listas`, `Paginado<T>`, `Paginacao`, `RegrasValidacao`.
- Produces (`Ocorrencias.Api.Problemas`):
  - os DTOs do mapa de renomeações
  - `IProblemaService.RegistrarAsync(RegistrarProblemaRequest, Guid autorId, CancellationToken)` → `Task<Resultado<ObterDetalheProblemaResponse>>`
  - `IProblemaService.ObterDetalheAsync(Guid id, Guid? usuarioId, CancellationToken)` → `Task<Resultado<ObterDetalheProblemaResponse>>`
  - `IProblemaService.ConsultarDetalheAsync(Guid id, Guid? usuarioId, DateTimeOffset agora, CancellationToken)` → `Task<ObterDetalheProblemaResponse?>`
  - `IProblemaService.BuscarDuplicatasAsync(BuscarDuplicatasRequest, CancellationToken)` → `Task<List<BuscarDuplicatasResponse>>`
  - `IProblemaService.ListarNaAreaAsync(ListarProblemasNaAreaRequest, CancellationToken)` → `Task<List<ListarProblemasNaAreaResponse>>`
  - `IProblemaService.ListarRecentesAsync(ListarProblemasRecentesRequest, CancellationToken)` → `Task<Paginado<ListarProblemasRecentesItem>>`

- [ ] **Step 1: Atualizar os testes** pelo mapa de renomeações: `using Ocorrencias.Api.Features.Problemas;` → `using Ocorrencias.Api.Problemas;`; `ObterDetalheProblema.Response` → `ObterDetalheProblemaResponse` etc.
- [ ] **Step 2: Build falha** — Run: `dotnet build tests/Ocorrencias.Api.Tests` → FAIL (`ObterDetalheProblemaResponse`).
- [ ] **Step 3: Implementar**

`Feature/Problemas/Models/ProblemaDtos.cs`:
```csharp
using Ocorrencias.Api.Categorias;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Features.Votos;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Problemas;

public sealed record RegistrarProblemaRequest(short CategoriaId, double Lat, double Lng, string? Descricao);

public sealed record BuscarDuplicatasRequest(short CategoriaId, double Lat, double Lng);

public sealed record BuscarDuplicatasResponse(
    Guid Id,
    CategoriaResumo Categoria,
    string Endereco,
    DateTimeOffset CriadoEm,
    int DistanciaMetros,
    FaixaConfianca? Faixa,
    int Confirmacoes);

/// <param name="Grupos">Separados por vírgula (ex.: <c>viaPublica,eletrica</c>). Vazio = todos.</param>
/// <param name="Status">Separados por vírgula (ex.: <c>nova,ativa</c>). Vazio = todos os não resolvidos.</param>
public sealed record ListarProblemasNaAreaRequest(double MinLat, double MinLng, double MaxLat, double MaxLng, string? Grupos, string? Status);

public sealed record ListarProblemasNaAreaResponse(
    Guid Id, short CategoriaId, double Lat, double Lng, StatusProblema Status, double? Confianca, FaixaConfianca? Faixa);

public sealed record ListarProblemasRecentesRequest(
    double Lat,
    double Lng,
    double RaioKm = 10,
    string? Grupos = null,
    int Pagina = 1,
    int Tamanho = Paginacao.TamanhoPadrao);

public sealed record ListarProblemasRecentesItem(
    Guid Id,
    CategoriaResumo Categoria,
    string Endereco,
    DateTimeOffset CriadoEm,
    StatusProblema Status,
    FaixaConfianca? Faixa);

public sealed record ObterDetalheProblemaResponse(
    Guid Id,
    CategoriaResumo Categoria,
    string? Descricao,
    double Lat,
    double Lng,
    string Endereco,
    DateTimeOffset CriadoEm,
    StatusProblema Status,
    double? Confianca,
    FaixaConfianca? Faixa,
    ObterDetalheProblemaAvaliacao? Avaliacao,
    ObterDetalheProblemaContagemExistencia Existencia,
    ObterDetalheProblemaContagemPersistencia Persistencia,
    ObterDetalheProblemaMeusVotos? MeusVotos,
    bool SouAutor);

/// <summary>Só em <c>nova</c>: quantas pessoas já votaram e quando a avaliação encerra.</summary>
public sealed record ObterDetalheProblemaAvaliacao(int Votantes, int Necessarios, DateTimeOffset EncerraEm);

/// <summary>Contagens de pessoas (não pesos).</summary>
public sealed record ObterDetalheProblemaContagemExistencia(int Confirmacoes, int NaoVi);

public sealed record ObterDetalheProblemaContagemPersistencia(int Continua, int Resolvido, int ResolvidoUltimas24h);

public sealed record ObterDetalheProblemaMeusVotos(ValorExistencia? Existencia, ValorPersistencia? Persistencia);
```

`Feature/Problemas/Models/LimitesProblema.cs`:
```csharp
namespace Ocorrencias.Api.Problemas;

public static class LimitesProblema
{
    public const double RaioDuplicatasMetros = 50;
    public const int MaximoDuplicatas = 20;
    public const int MaximoNaArea = 500;
    public const double RaioMaximoRecentesKm = 50;
}
```

`Feature/Problemas/Validators/ProblemaValidators.cs`:
```csharp
using FluentValidation;
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Problemas;

public sealed class RegistrarProblemaValidator : AbstractValidator<RegistrarProblemaRequest>
{
    public RegistrarProblemaValidator()
    {
        RuleFor(r => r.CategoriaId).GreaterThan((short)0);
        RuleFor(r => r.Lat).Latitude();
        RuleFor(r => r.Lng).Longitude();
        RuleFor(r => r.Descricao).MaximumLength(280);
    }
}

public sealed class BuscarDuplicatasValidator : AbstractValidator<BuscarDuplicatasRequest>
{
    public BuscarDuplicatasValidator()
    {
        RuleFor(r => r.CategoriaId).GreaterThan((short)0);
        RuleFor(r => r.Lat).Latitude();
        RuleFor(r => r.Lng).Longitude();
    }
}

public sealed class ListarProblemasNaAreaValidator : AbstractValidator<ListarProblemasNaAreaRequest>
{
    public ListarProblemasNaAreaValidator()
    {
        RuleFor(r => r.MinLat).Latitude();
        RuleFor(r => r.MaxLat).Latitude().GreaterThan(r => r.MinLat);
        RuleFor(r => r.MinLng).Longitude();
        RuleFor(r => r.MaxLng).Longitude().GreaterThan(r => r.MinLng);
        RuleFor(r => r.Grupos).ListaDeGrupos();
        RuleFor(r => r.Status).ListaDeStatus();
    }
}

public sealed class ListarProblemasRecentesValidator : AbstractValidator<ListarProblemasRecentesRequest>
{
    public ListarProblemasRecentesValidator()
    {
        RuleFor(r => r.Lat).Latitude();
        RuleFor(r => r.Lng).Longitude();
        RuleFor(r => r.RaioKm).GreaterThan(0).LessThanOrEqualTo(LimitesProblema.RaioMaximoRecentesKm);
        RuleFor(r => r.Grupos).ListaDeGrupos();
        RuleFor(r => r.Pagina).InclusiveBetween(1, Paginacao.PaginaMaxima);
        RuleFor(r => r.Tamanho).InclusiveBetween(1, Paginacao.TamanhoMaximo);
    }
}
```

`Feature/Problemas/Errors/ErrosProblema.cs`:
```csharp
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Problemas;

public static class ErrosProblema
{
    public static readonly ErroAplicacao CategoriaInexistente = ErroAplicacao.DeCampo("categoriaId", "Categoria inexistente.");

    public static readonly ErroAplicacao CategoriaNaoEProblema = ErroAplicacao.Regra(
        StatusCodes.Status422UnprocessableEntity, "categoria-nao-e-problema", "A categoria escolhida é de incidente, não de problema.");
}
```

`Feature/Problemas/Services/ExibicaoConfianca.cs`: o arquivo `Features/Problemas/ExibicaoConfianca.cs` move-se inteiro, só com `namespace Ocorrencias.Api.Problemas;`:
```csharp
using Ocorrencias.Domain;
using Ocorrencias.Domain.Confianca;

namespace Ocorrencias.Api.Problemas;

public static class ExibicaoConfianca
{
    /// <summary>
    /// Confiança arredondada em 1 casa (só na exibição) e a faixa correspondente. Em <c>nova</c> não é exibida:
    /// ambos vêm nulos, embora o valor seja calculado e gravado.
    /// </summary>
    public static (double? Confianca, FaixaConfianca? Faixa) Calcular(StatusProblema status, decimal? confianca, ConfiguracaoConfianca cfg)
    {
        if (status == StatusProblema.Nova || confianca is not { } valor)
        {
            return (null, null);
        }

        var indice = (double)valor;
        return (Math.Round(indice, 1, MidpointRounding.AwayFromZero), MotorConfianca.ClassificarFaixa(indice, cfg));
    }
}
```

`Feature/Problemas/Abstractions/IProblemaService.cs`:
```csharp
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Problemas;

public interface IProblemaService
{
    Task<Resultado<ObterDetalheProblemaResponse>> RegistrarAsync(RegistrarProblemaRequest request, Guid autorId, CancellationToken ct);

    Task<Resultado<ObterDetalheProblemaResponse>> ObterDetalheAsync(Guid id, Guid? usuarioId, CancellationToken ct);

    /// <summary>Detalhe no instante <paramref name="agora"/> (o voto usa o mesmo instante do recálculo). Nulo se não existe.</summary>
    Task<ObterDetalheProblemaResponse?> ConsultarDetalheAsync(Guid id, Guid? usuarioId, DateTimeOffset agora, CancellationToken ct);

    Task<List<BuscarDuplicatasResponse>> BuscarDuplicatasAsync(BuscarDuplicatasRequest request, CancellationToken ct);

    Task<List<ListarProblemasNaAreaResponse>> ListarNaAreaAsync(ListarProblemasNaAreaRequest request, CancellationToken ct);

    Task<Paginado<ListarProblemasRecentesItem>> ListarRecentesAsync(ListarProblemasRecentesRequest request, CancellationToken ct);
}
```

`Feature/Problemas/Services/ProblemaService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Categorias;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Features.Votos;
using Ocorrencias.Api.Geocodificacao;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain;
using Ocorrencias.Domain.Confianca;

namespace Ocorrencias.Api.Problemas;

public sealed class ProblemaService(
    AppDbContext db, IServicoGeocodificacao geocodificacao, TimeProvider relogio, ConfiguracaoConfianca cfg) : IProblemaService
{
    /// <summary>O endereço vem da geocodificação reversa no servidor (nunca do cliente) e nunca impede o registro.</summary>
    public async Task<Resultado<ObterDetalheProblemaResponse>> RegistrarAsync(RegistrarProblemaRequest request, Guid autorId, CancellationToken ct)
    {
        var categoria = await db.Categorias.FindAsync([request.CategoriaId], ct);
        if (categoria is null)
        {
            return ErrosProblema.CategoriaInexistente;
        }

        if (categoria.Tipo != TipoOcorrencia.Problema)
        {
            return ErrosProblema.CategoriaNaoEProblema;
        }

        var endereco = await geocodificacao.ReversaAsync(request.Lat, request.Lng, ct);
        var agora = relogio.GetUtcNow();

        var problema = new Problema
        {
            Id = Guid.CreateVersion7(),
            CategoriaId = categoria.Id,
            AutorId = autorId,
            Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim(),
            Localizacao = Geo.Ponto(request.Lat, request.Lng),
            EnderecoAproximado = endereco.Endereco,
            Status = StatusProblema.Nova,
            CriadoEm = agora,
            StatusAlteradoEm = agora,
        };
        db.Problemas.Add(problema);
        await db.SaveChangesAsync(ct);

        return (await ConsultarDetalheAsync(problema.Id, autorId, agora, ct))!;
    }

    public async Task<Resultado<ObterDetalheProblemaResponse>> ObterDetalheAsync(Guid id, Guid? usuarioId, CancellationToken ct)
    {
        var detalhe = await ConsultarDetalheAsync(id, usuarioId, relogio.GetUtcNow(), ct);
        if (detalhe is null)
        {
            return ErroAplicacao.NaoEncontrado;
        }

        return detalhe;
    }

    /// <param name="usuarioId">Nulo para visitante: <c>meusVotos</c> = null e <c>souAutor</c> = false.</param>
    public async Task<ObterDetalheProblemaResponse?> ConsultarDetalheAsync(Guid id, Guid? usuarioId, DateTimeOffset agora, CancellationToken ct)
    {
        var limite24h = agora.AddHours(-24);

        var linha = await db.Problemas
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id,
                Categoria = new CategoriaResumo(p.Categoria.Id, p.Categoria.Nome, p.Categoria.Grupo, p.Categoria.Icone),
                p.Descricao,
                p.Localizacao,
                p.EnderecoAproximado,
                p.CriadoEm,
                p.Status,
                p.Confianca,
                p.AutorId,
                Votantes = p.Votos.Select(v => v.UsuarioId).Distinct().Count(),
                Confirmacoes = p.Votos.Count(v => v.Tipo == TipoVoto.Existencia && v.Valor == ValorVoto.Positivo),
                NaoVi = p.Votos.Count(v => v.Tipo == TipoVoto.Existencia && v.Valor == ValorVoto.Negativo),
                Continua = p.Votos.Count(v => v.Tipo == TipoVoto.Persistencia && v.Valor == ValorVoto.Positivo),
                Resolvido = p.Votos.Count(v => v.Tipo == TipoVoto.Persistencia && v.Valor == ValorVoto.Negativo),
                ResolvidoUltimas24h = p.Votos.Count(v =>
                    v.Tipo == TipoVoto.Persistencia && v.Valor == ValorVoto.Negativo && v.RegistradoEm >= limite24h),
                MeuExistencia = p.Votos
                    .Where(v => v.UsuarioId == usuarioId && v.Tipo == TipoVoto.Existencia)
                    .Select(v => (ValorVoto?)v.Valor)
                    .FirstOrDefault(),
                MeuPersistencia = p.Votos
                    .Where(v => v.UsuarioId == usuarioId && v.Tipo == TipoVoto.Persistencia)
                    .Select(v => (ValorVoto?)v.Valor)
                    .FirstOrDefault(),
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (linha is null)
        {
            return null;
        }

        var (confianca, faixa) = ExibicaoConfianca.Calcular(linha.Status, linha.Confianca, cfg);

        return new ObterDetalheProblemaResponse(
            linha.Id,
            linha.Categoria,
            linha.Descricao,
            linha.Localizacao.Y,
            linha.Localizacao.X,
            linha.EnderecoAproximado,
            linha.CriadoEm,
            linha.Status,
            confianca,
            faixa,
            linha.Status == StatusProblema.Nova
                ? new ObterDetalheProblemaAvaliacao(linha.Votantes, cfg.VotantesParaSairDeNova, linha.CriadoEm + cfg.TempoMaximoEmNova)
                : null,
            new ObterDetalheProblemaContagemExistencia(linha.Confirmacoes, linha.NaoVi),
            new ObterDetalheProblemaContagemPersistencia(linha.Continua, linha.Resolvido, linha.ResolvidoUltimas24h),
            usuarioId is null
                ? null
                : new ObterDetalheProblemaMeusVotos((ValorExistencia?)linha.MeuExistencia, (ValorPersistencia?)linha.MeuPersistencia),
            usuarioId is not null && linha.AutorId == usuarioId);
    }

    /// <summary>Mesma categoria, não resolvidos, a até 50 m (<c>ST_DWithin</c>), do mais próximo ao mais distante.</summary>
    public async Task<List<BuscarDuplicatasResponse>> BuscarDuplicatasAsync(BuscarDuplicatasRequest request, CancellationToken ct)
    {
        var ponto = Geo.Ponto(request.Lat, request.Lng);

        var linhas = await db.Problemas
            .Where(p => p.CategoriaId == request.CategoriaId
                && p.Status != StatusProblema.Resolvida
                && p.Localizacao.IsWithinDistance(ponto, LimitesProblema.RaioDuplicatasMetros))
            .Select(p => new
            {
                p.Id,
                Categoria = new CategoriaResumo(p.Categoria.Id, p.Categoria.Nome, p.Categoria.Grupo, p.Categoria.Icone),
                p.EnderecoAproximado,
                p.CriadoEm,
                Distancia = p.Localizacao.Distance(ponto),
                p.Status,
                p.Confianca,
                Confirmacoes = p.Votos.Count(v => v.Tipo == TipoVoto.Existencia && v.Valor == ValorVoto.Positivo),
            })
            .OrderBy(p => p.Distancia)
            .Take(LimitesProblema.MaximoDuplicatas)
            .AsNoTracking()
            .ToListAsync(ct);

        return linhas
            .Select(l => new BuscarDuplicatasResponse(
                l.Id,
                l.Categoria,
                l.EnderecoAproximado,
                l.CriadoEm,
                (int)Math.Round(l.Distancia, MidpointRounding.AwayFromZero),
                ExibicaoConfianca.Calcular(l.Status, l.Confianca, cfg).Faixa,
                l.Confirmacoes))
            .ToList();
    }

    /// <summary>Marcadores da área visível. Nunca retorna <c>resolvida</c>; até 500, os de maior confiança primeiro.</summary>
    public async Task<List<ListarProblemasNaAreaResponse>> ListarNaAreaAsync(ListarProblemasNaAreaRequest request, CancellationToken ct)
    {
        var grupos = Listas.Separar(request.Grupos);
        var pedidos = Listas.Separar(request.Status).Select(s => Enum.Parse<StatusProblema>(s, ignoreCase: true)).ToArray();
        StatusProblema[] status = pedidos.Length == 0
            ? [StatusProblema.Nova, StatusProblema.Ativa, StatusProblema.PossivelmenteResolvida]
            : pedidos.Where(s => s != StatusProblema.Resolvida).Distinct().ToArray();
        if (status.Length == 0)
        {
            return []; // só "resolvida" foi pedido, e o mapa nunca mostra resolvidos
        }

        // Recorte planar em lat/lng (geometry), não geodésico: em geography as arestas do retângulo seriam
        // geodésicas e, em viewport largo, incluiriam pontos de fora e excluiriam pontos de dentro.
        // Usa o índice GIST de expressão ix_problemas_localizacao_geometry.
        var consulta = db.Problemas
            .FromSql($"""
                SELECT * FROM problemas
                WHERE localizacao::geometry && ST_MakeEnvelope({request.MinLng}, {request.MinLat}, {request.MaxLng}, {request.MaxLat}, 4326)
                """)
            .Where(p => status.Contains(p.Status));
        if (grupos.Length > 0)
        {
            consulta = consulta.Where(p => grupos.Contains(p.Categoria.Grupo));
        }

        var linhas = await consulta
            .OrderByDescending(p => p.Confianca.HasValue && p.Status != StatusProblema.Nova)
            .ThenByDescending(p => p.Confianca)
            .ThenByDescending(p => p.CriadoEm)
            .Take(LimitesProblema.MaximoNaArea)
            .Select(p => new { p.Id, p.CategoriaId, p.Localizacao, p.Status, p.Confianca })
            .AsNoTracking()
            .ToListAsync(ct);

        return linhas
            .Select(l =>
            {
                var (confianca, faixa) = ExibicaoConfianca.Calcular(l.Status, l.Confianca, cfg);
                return new ListarProblemasNaAreaResponse(l.Id, l.CategoriaId, l.Localizacao.Y, l.Localizacao.X, l.Status, confianca, faixa);
            })
            .ToList();
    }

    /// <summary>Problemas não resolvidos no raio, mais recentes primeiro.</summary>
    public async Task<Paginado<ListarProblemasRecentesItem>> ListarRecentesAsync(ListarProblemasRecentesRequest request, CancellationToken ct)
    {
        var ponto = Geo.Ponto(request.Lat, request.Lng);
        var grupos = Listas.Separar(request.Grupos);

        var consulta = db.Problemas.Where(p =>
            p.Status != StatusProblema.Resolvida && p.Localizacao.IsWithinDistance(ponto, request.RaioKm * 1000));
        if (grupos.Length > 0)
        {
            consulta = consulta.Where(p => grupos.Contains(p.Categoria.Grupo));
        }

        var total = await consulta.CountAsync(ct);
        var linhas = await consulta
            .OrderByDescending(p => p.CriadoEm)
            .ThenBy(p => p.Id)
            .Skip(Paginacao.Pular(request.Pagina, request.Tamanho))
            .Take(request.Tamanho)
            .Select(p => new
            {
                p.Id,
                Categoria = new CategoriaResumo(p.Categoria.Id, p.Categoria.Nome, p.Categoria.Grupo, p.Categoria.Icone),
                p.EnderecoAproximado,
                p.CriadoEm,
                p.Status,
                p.Confianca,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var itens = linhas
            .Select(l => new ListarProblemasRecentesItem(
                l.Id, l.Categoria, l.EnderecoAproximado, l.CriadoEm, l.Status,
                ExibicaoConfianca.Calcular(l.Status, l.Confianca, cfg).Faixa))
            .ToList();

        return new Paginado<ListarProblemasRecentesItem>(itens, request.Pagina, request.Tamanho, total);
    }
}
```

`Modules/ProblemaModule.cs`, com a mesma ordem de registro de hoje:
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Problemas;

namespace Ocorrencias.Api.Modules;

public sealed class ProblemaModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/problemas", ListarNaArea)
            .Validar<ListarProblemasNaAreaRequest>()
            .AllowAnonymous()
            .WithTags("Problemas");

        api.MapGet("/problemas/recentes", ListarRecentes)
            .Validar<ListarProblemasRecentesRequest>()
            .AllowAnonymous()
            .WithTags("Problemas");

        api.MapGet("/problemas/duplicatas", BuscarDuplicatas)
            .Validar<BuscarDuplicatasRequest>()
            .WithTags("Problemas");

        api.MapGet("/problemas/{id:guid}", ObterDetalhe)
            .AllowAnonymous()
            .WithTags("Problemas");

        api.MapPost("/problemas", Registrar)
            .Validar<RegistrarProblemaRequest>()
            .WithTags("Problemas");
    }

    private static Task<List<ListarProblemasNaAreaResponse>> ListarNaArea(
        [AsParameters] ListarProblemasNaAreaRequest request, IProblemaService service, CancellationToken ct) =>
        service.ListarNaAreaAsync(request, ct);

    private static Task<Paginado<ListarProblemasRecentesItem>> ListarRecentes(
        [AsParameters] ListarProblemasRecentesRequest request, IProblemaService service, CancellationToken ct) =>
        service.ListarRecentesAsync(request, ct);

    private static Task<List<BuscarDuplicatasResponse>> BuscarDuplicatas(
        [AsParameters] BuscarDuplicatasRequest request, IProblemaService service, CancellationToken ct) =>
        service.BuscarDuplicatasAsync(request, ct);

    private static async Task<Results<Ok<ObterDetalheProblemaResponse>, NotFound>> ObterDetalhe(
        Guid id, ClaimsPrincipal principal, IProblemaService service, CancellationToken ct)
    {
        var r = await service.ObterDetalheAsync(id, principal.ObterUsuarioIdOuNulo(), ct);
        return r.Erro switch
        {
            null => TypedResults.Ok(r.Valor!),
            var e => e.ParaNaoEncontrado(),
        };
    }

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
}
```

Ajuste nos votos legados, até a Task 10:
- `Features/Votos/Votacao.cs`:
  - troque `using Ocorrencias.Api.Features.Problemas;` por `using Ocorrencias.Api.Problemas;`;
  - acrescente o parâmetro `IProblemaService problemas` ao `VotarAsync`, logo depois de `AppDbContext db`;
  - troque o tipo de retorno `ObterDetalheProblema.Response` por `ObterDetalheProblemaResponse`;
  - troque a última consulta por `var detalhe = await problemas.ConsultarDetalheAsync(problemaId, usuarioId, agora, ct);`.
- `Features/Votos/VotarExistencia.cs` e `VotarPersistencia.cs`:
  - troque o using e o tipo de retorno do mesmo jeito;
  - acrescente `IProblemaService problemas` aos parâmetros do `Handle`, logo depois de `AppDbContext db`;
  - passe `problemas` na chamada: `Votacao.VotarAsync(db, problemas, id, ...)`.

Em `AddServicos`: `services.AddScoped<IProblemaService, ProblemaService>();` (com `using Ocorrencias.Api.Problemas;`). Apague `Features/Problemas/`, remova as 5 chamadas e o using de `Features/Endpoints.cs`, e remova `CategoriaNaoEProblema` de `Common/Erros.cs`.

- [ ] **Step 4: Testes** — Run: `dotnet build && dotnet test tests/Ocorrencias.Api.Tests` → tudo verde.
- [ ] **Step 5: Commit**

```bash
git add -A src/Ocorrencias.Api tests/Ocorrencias.Api.Tests
git commit -m "refactor(problemas): extrai service, erros e module"
```

---

### Task 10: Área Votos

**Files:**
- Create: `Feature/Votos/Models/VotoDtos.cs`, `Feature/Votos/Validators/VotoValidators.cs`, `Feature/Votos/Errors/ErrosVoto.cs`, `Feature/Votos/Abstractions/IVotoService.cs`, `Feature/Votos/Services/VotoService.cs`, `Modules/VotoModule.cs`
- Delete: `Features/Votos/` (4 arquivos)
- Modify: `Feature/Problemas/Models/ProblemaDtos.cs` e `Feature/Problemas/Services/ProblemaService.cs` (`using Ocorrencias.Api.Features.Votos;` → `using Ocorrencias.Api.Votos;`), `Features/Endpoints.cs` (remover as 2 chamadas e o using), `Common/Erros.cs` (remover `AutorNaoVotaExistencia`, `PersistenciaIndisponivelEmNova`, `ProblemaResolvido`), `AddServicos`
- Test: `Votos/VotosTests.cs` e quem usar `ValorExistencia`/`ValorPersistencia`: `using Ocorrencias.Api.Features.Votos;` → `using Ocorrencias.Api.Votos;`

**Interfaces:**
- Consumes: `IProblemaService.ConsultarDetalheAsync` (Task 9), `RecalculoProblema.CarregarComBloqueioAsync(AppDbContext, Guid, CancellationToken)` e `RecalculoProblema.Aplicar(Problema, IEnumerable<Voto>, DateTimeOffset, ConfiguracaoConfianca)` (`Ocorrencias.Api.Confianca`).
- Produces (`Ocorrencias.Api.Votos`):
  - `ValorExistencia`, `ValorPersistencia`, `VotarExistenciaRequest`, `VotarPersistenciaRequest`
  - `IVotoService.VotarExistenciaAsync(Guid problemaId, Guid usuarioId, VotarExistenciaRequest, CancellationToken)` → `Task<Resultado<ObterDetalheProblemaResponse>>`
  - `IVotoService.VotarPersistenciaAsync(...)`, com a mesma forma

- [ ] **Step 1: Atualizar os testes** (usings).
- [ ] **Step 2: Build falha** — Run: `dotnet build tests/Ocorrencias.Api.Tests` → FAIL (namespace `Ocorrencias.Api.Votos`).
- [ ] **Step 3: Implementar**

`Feature/Votos/Models/VotoDtos.cs`:
```csharp
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Votos;

/// <summary>Voto de existência no contrato: <c>confirma</c> | <c>naoVi</c>. Mesmos números de <see cref="ValorVoto"/>.</summary>
public enum ValorExistencia
{
    Confirma = ValorVoto.Positivo,
    NaoVi = ValorVoto.Negativo,
}

/// <summary>Voto de persistência no contrato: <c>continua</c> | <c>resolvido</c>. Mesmos números de <see cref="ValorVoto"/>.</summary>
public enum ValorPersistencia
{
    Continua = ValorVoto.Positivo,
    Resolvido = ValorVoto.Negativo,
}

/// <summary><c>lat</c>/<c>lng</c> opcionais: sem localização, o peso de proximidade é 1.</summary>
public sealed record VotarExistenciaRequest(ValorExistencia Valor, double? Lat, double? Lng);

/// <summary><c>lat</c>/<c>lng</c> opcionais: sem localização, o peso de proximidade é 1.</summary>
public sealed record VotarPersistenciaRequest(ValorPersistencia Valor, double? Lat, double? Lng);
```

`Feature/Votos/Validators/VotoValidators.cs`:
```csharp
using FluentValidation;
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Votos;

public sealed class VotarExistenciaValidator : AbstractValidator<VotarExistenciaRequest>
{
    public VotarExistenciaValidator()
    {
        RuleFor(r => r.Valor).IsInEnum();
        RegrasPosicaoVoto.Aplicar(this, r => r.Lat, r => r.Lng);
    }
}

public sealed class VotarPersistenciaValidator : AbstractValidator<VotarPersistenciaRequest>
{
    public VotarPersistenciaValidator()
    {
        RuleFor(r => r.Valor).IsInEnum();
        RegrasPosicaoVoto.Aplicar(this, r => r.Lat, r => r.Lng);
    }
}

internal static class RegrasPosicaoVoto
{
    /// <summary>Posição opcional do votante: os dois campos juntos, ou nenhum.</summary>
    public static void Aplicar<T>(AbstractValidator<T> validador, Func<T, double?> lat, Func<T, double?> lng)
    {
        validador.RuleFor(r => lat(r)).Latitude().OverridePropertyName("lat");
        validador.RuleFor(r => lng(r)).Longitude().OverridePropertyName("lng");
        validador.RuleFor(r => r)
            .Must(r => lat(r).HasValue == lng(r).HasValue)
            .OverridePropertyName("lat")
            .WithMessage("Informe lat e lng juntos, ou nenhum dos dois.");
    }
}
```

`Feature/Votos/Errors/ErrosVoto.cs`:
```csharp
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Votos;

public static class ErrosVoto
{
    public static readonly ErroAplicacao AutorNaoVotaExistencia = ErroAplicacao.Regra(
        StatusCodes.Status422UnprocessableEntity, "autor-nao-vota-existencia", "Quem registrou o problema não vota na existência dele.");

    public static readonly ErroAplicacao PersistenciaIndisponivelEmNova = ErroAplicacao.Regra(
        StatusCodes.Status422UnprocessableEntity, "persistencia-indisponivel-em-nova", "\"Ainda está lá?\" só abre depois que o problema sai de Nova.");

    public static readonly ErroAplicacao ProblemaResolvido = ErroAplicacao.Regra(
        StatusCodes.Status422UnprocessableEntity, "problema-resolvido", "O problema já foi resolvido e não recebe mais votos.");
}
```

`Feature/Votos/Abstractions/IVotoService.cs`:
```csharp
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Problemas;

namespace Ocorrencias.Api.Votos;

public interface IVotoService
{
    Task<Resultado<ObterDetalheProblemaResponse>> VotarExistenciaAsync(
        Guid problemaId, Guid usuarioId, VotarExistenciaRequest request, CancellationToken ct);

    Task<Resultado<ObterDetalheProblemaResponse>> VotarPersistenciaAsync(
        Guid problemaId, Guid usuarioId, VotarPersistenciaRequest request, CancellationToken ct);
}
```

`Feature/Votos/Services/VotoService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Confianca;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Api.Problemas;
using Ocorrencias.Domain;
using Ocorrencias.Domain.Confianca;

namespace Ocorrencias.Api.Votos;

/// <summary>Votos de existência e persistência (docs/diagramas/05_seq_votar.puml).</summary>
public sealed class VotoService(
    AppDbContext db, IProblemaService problemas, TimeProvider relogio, ConfiguracaoConfianca cfg) : IVotoService
{
    public Task<Resultado<ObterDetalheProblemaResponse>> VotarExistenciaAsync(
        Guid problemaId, Guid usuarioId, VotarExistenciaRequest request, CancellationToken ct) =>
        VotarAsync(problemaId, usuarioId, TipoVoto.Existencia, (ValorVoto)request.Valor, request.Lat, request.Lng, ct);

    public Task<Resultado<ObterDetalheProblemaResponse>> VotarPersistenciaAsync(
        Guid problemaId, Guid usuarioId, VotarPersistenciaRequest request, CancellationToken ct) =>
        VotarAsync(problemaId, usuarioId, TipoVoto.Persistencia, (ValorVoto)request.Valor, request.Lat, request.Lng, ct);

    /// <summary>
    /// Numa única transação: bloqueia o problema, valida as regras, calcula a distância no PostGIS (a posição
    /// do votante é descartada), faz o UPSERT do voto com o perfil atual do banco, recalcula a confiança e
    /// avalia o ciclo de vida.
    /// </summary>
    private async Task<Resultado<ObterDetalheProblemaResponse>> VotarAsync(
        Guid problemaId, Guid usuarioId, TipoVoto tipo, ValorVoto valor, double? lat, double? lng, CancellationToken ct)
    {
        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        var problema = await RecalculoProblema.CarregarComBloqueioAsync(db, problemaId, ct);
        if (problema is null)
        {
            return ErroAplicacao.NaoEncontrado;
        }

        if (problema.Status == StatusProblema.Resolvida)
        {
            return ErrosVoto.ProblemaResolvido;
        }

        if (tipo == TipoVoto.Existencia && problema.AutorId == usuarioId)
        {
            return ErrosVoto.AutorNaoVotaExistencia;
        }

        if (tipo == TipoVoto.Persistencia && problema.Status == StatusProblema.Nova)
        {
            return ErrosVoto.PersistenciaIndisponivelEmNova;
        }

        // O perfil vem do banco, não do token (que vale 7 dias e pode estar desatualizado após uma aprovação).
        var perfil = await db.Users.Where(u => u.Id == usuarioId).Select(u => (Perfil?)u.Perfil).SingleOrDefaultAsync(ct);
        if (perfil is null)
        {
            return ErroAplicacao.NaoAutorizado;
        }

        var distancia = await CalcularDistanciaAsync(problemaId, lat, lng, ct);
        var agora = relogio.GetUtcNow();

        var votos = await db.Votos.Where(v => v.ProblemaId == problemaId).ToListAsync(ct);
        var voto = votos.SingleOrDefault(v => v.UsuarioId == usuarioId && v.Tipo == tipo);
        if (voto is null)
        {
            voto = new Voto { Id = Guid.CreateVersion7(), ProblemaId = problemaId, UsuarioId = usuarioId, Tipo = tipo };
            db.Votos.Add(voto);
            votos.Add(voto);
        }

        // UPSERT: votar de novo substitui valor, perfil, distância e instante.
        voto.Valor = valor;
        voto.PerfilNoMomento = perfil.Value;
        voto.DistanciaMetros = distancia;
        voto.RegistradoEm = agora;

        RecalculoProblema.Aplicar(problema, votos, agora, cfg);

        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);

        return (await problemas.ConsultarDetalheAsync(problemaId, usuarioId, agora, ct))!;
    }

    /// <summary><c>ST_Distance</c> em geography (metros). Nulo = votante sem localização. A posição não é persistida.</summary>
    private async Task<int?> CalcularDistanciaAsync(Guid problemaId, double? lat, double? lng, CancellationToken ct)
    {
        if (lat is not { } latitude || lng is not { } longitude)
        {
            return null;
        }

        var ponto = Geo.Ponto(latitude, longitude);
        var metros = await db.Problemas
            .Where(p => p.Id == problemaId)
            .Select(p => p.Localizacao.Distance(ponto))
            .SingleAsync(ct);

        return (int)Math.Round(metros, MidpointRounding.AwayFromZero);
    }
}
```

`Modules/VotoModule.cs`:
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Problemas;
using Ocorrencias.Api.Votos;

namespace Ocorrencias.Api.Modules;

public sealed class VotoModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapPut("/problemas/{id:guid}/votos/existencia", VotarExistencia)
            .Validar<VotarExistenciaRequest>()
            .WithTags("Votos");

        api.MapPut("/problemas/{id:guid}/votos/persistencia", VotarPersistencia)
            .Validar<VotarPersistenciaRequest>()
            .WithTags("Votos");
    }

    private static async Task<Results<Ok<ObterDetalheProblemaResponse>, NotFound, ProblemHttpResult, UnauthorizedHttpResult>> VotarExistencia(
        Guid id, VotarExistenciaRequest request, ClaimsPrincipal principal, IVotoService service, CancellationToken ct) =>
        ParaHttp(await service.VotarExistenciaAsync(id, principal.ObterUsuarioId(), request, ct));

    private static async Task<Results<Ok<ObterDetalheProblemaResponse>, NotFound, ProblemHttpResult, UnauthorizedHttpResult>> VotarPersistencia(
        Guid id, VotarPersistenciaRequest request, ClaimsPrincipal principal, IVotoService service, CancellationToken ct) =>
        ParaHttp(await service.VotarPersistenciaAsync(id, principal.ObterUsuarioId(), request, ct));

    private static Results<Ok<ObterDetalheProblemaResponse>, NotFound, ProblemHttpResult, UnauthorizedHttpResult> ParaHttp(
        Resultado<ObterDetalheProblemaResponse> r) =>
        r.Erro switch
        {
            null => TypedResults.Ok(r.Valor!),
            { Tipo: TipoErro.NaoEncontrado } e => e.ParaNaoEncontrado(),
            { Tipo: TipoErro.NaoAutorizado } e => e.ParaNaoAutorizado(),
            var e => e.ParaProblema(),
        };
}
```

Em `AddServicos`: `services.AddScoped<IVotoService, VotoService>();` (com `using Ocorrencias.Api.Votos;`). Apague `Features/Votos/`, troque o using de Votos nos 2 arquivos de Problemas, remova as 2 chamadas e o using de `Features/Endpoints.cs`, e remova os 3 erros de `Common/Erros.cs`.

- [ ] **Step 4: Testes** — Run: `dotnet build && dotnet test tests/Ocorrencias.Api.Tests` → tudo verde. Inclui o teste de 5 votos simultâneos de `VotosTests` (`FOR UPDATE` preservado) e o `Token_de_usuario_removido_recebe_401`.
- [ ] **Step 5: Commit**

```bash
git add -A src/Ocorrencias.Api tests/Ocorrencias.Api.Tests
git commit -m "refactor(votos): extrai service, erros e module"
```

---

### Task 11: Área Incidentes

**Files:**
- Create: `Feature/Incidentes/Models/IncidenteDtos.cs`, `Feature/Incidentes/Models/LimitesIncidente.cs`, `Feature/Incidentes/Validators/IncidenteValidators.cs`, `Feature/Incidentes/Errors/ErrosIncidente.cs`, `Feature/Incidentes/Abstractions/IIncidenteService.cs`, `Feature/Incidentes/Services/IncidenteService.cs`, `Modules/IncidenteModule.cs`
- Delete: `Features/Incidentes/` (3 arquivos)
- Modify: `Features/Endpoints.cs` (remover as 3 chamadas e o using), `Common/Erros.cs` (remover `CategoriaNaoEIncidente`, `HorarioForaDaJanela`, `LimiteDiarioIncidentes`), `AddServicos`
- Test: `Incidentes/IncidentesTests.cs` (renomes; `ObterAreaIncidentes.Nivel` → `IncidenteService.Nivel`)

**Interfaces:**
- Consumes: `IServicoGeocodificacao`, `Geo`, `Listas`.
- Produces (`Ocorrencias.Api.Incidentes`):
  - os DTOs do mapa de renomeações e `IncidenteRegistrado(double Lat, double Lng, ObterAreaIncidentesResponse Area)`
  - `IIncidenteService.RegistrarAsync(RegistrarIncidenteRequest, Guid autorId, CancellationToken)` → `Task<Resultado<IncidenteRegistrado>>`
  - `IIncidenteService.ObterCalorAsync(ObterCalorIncidentesRequest, CancellationToken)` → `Task<List<ObterCalorIncidentesResponse>>`
  - `IIncidenteService.ObterAreaAsync(ObterAreaIncidentesRequest, CancellationToken)` → `Task<ObterAreaIncidentesResponse>`
  - `static IncidenteService.Nivel(int)` e `static IncidenteService.Arredondar(double)`

- [ ] **Step 1: Atualizar `IncidentesTests.cs`** (`using Ocorrencias.Api.Incidentes;` + renomes).
- [ ] **Step 2: Build falha** — Run: `dotnet build tests/Ocorrencias.Api.Tests` → FAIL.
- [ ] **Step 3: Implementar**

`Feature/Incidentes/Models/IncidenteDtos.cs`:
```csharp
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Incidentes;

public sealed record RegistrarIncidenteRequest(short CategoriaId, double Lat, double Lng, DateTimeOffset OcorridoEm);

public sealed record ObterCalorIncidentesRequest(double MinLat, double MinLng, double MaxLat, double MaxLng, int Dias = 7, string? Grupos = null);

/// <summary>Célula do mapa de calor: o ponto já é arredondado em 3 casas na gravação.</summary>
public sealed record ObterCalorIncidentesResponse(double Lat, double Lng, int Quantidade);

public sealed record ObterAreaIncidentesRequest(double Lat, double Lng);

public sealed record ObterAreaIncidentesResponse(
    int RaioMetros, DateTimeOffset AtualizadoEm, NivelConcentracao Nivel, IReadOnlyList<ObterAreaIncidentesContagem> Contagens);

public sealed record ObterAreaIncidentesContagem(short CategoriaId, string Nome, int Dias7, int Dias30);

/// <summary>Resultado interno do registro: o ponto arredondado (para o <c>Location</c>) e a área. Não é DTO de contrato.</summary>
public sealed record IncidenteRegistrado(double Lat, double Lng, ObterAreaIncidentesResponse Area);
```

`Feature/Incidentes/Models/LimitesIncidente.cs`:
```csharp
namespace Ocorrencias.Api.Incidentes;

public static class LimitesIncidente
{
    public const int CasasDecimais = 3;
    public const short RaioMetros = 100;
    public const int MaximoPor24h = 10;
    public static readonly TimeSpan Janela = TimeSpan.FromHours(24);

    public const int DiasCalorMaximo = 30;
    public const int MaximoCelulasCalor = 5000;

    public const int RaioAreaMetros = 400;
    public const int LimiarMedia = 5;
    public const int LimiarAlta = 15;
}
```

`Feature/Incidentes/Validators/IncidenteValidators.cs`:
```csharp
using FluentValidation;
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Incidentes;

public sealed class RegistrarIncidenteValidator : AbstractValidator<RegistrarIncidenteRequest>
{
    public RegistrarIncidenteValidator()
    {
        RuleFor(r => r.CategoriaId).GreaterThan((short)0);
        RuleFor(r => r.Lat).Latitude();
        RuleFor(r => r.Lng).Longitude();
    }
}

public sealed class ObterCalorIncidentesValidator : AbstractValidator<ObterCalorIncidentesRequest>
{
    public ObterCalorIncidentesValidator()
    {
        RuleFor(r => r.MinLat).Latitude();
        RuleFor(r => r.MaxLat).Latitude().GreaterThan(r => r.MinLat);
        RuleFor(r => r.MinLng).Longitude();
        RuleFor(r => r.MaxLng).Longitude().GreaterThan(r => r.MinLng);
        RuleFor(r => r.Dias).InclusiveBetween(1, LimitesIncidente.DiasCalorMaximo);
        RuleFor(r => r.Grupos).ListaDeGrupos();
    }
}

public sealed class ObterAreaIncidentesValidator : AbstractValidator<ObterAreaIncidentesRequest>
{
    public ObterAreaIncidentesValidator()
    {
        RuleFor(r => r.Lat).Latitude();
        RuleFor(r => r.Lng).Longitude();
    }
}
```

`Feature/Incidentes/Errors/ErrosIncidente.cs`:
```csharp
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Incidentes;

public static class ErrosIncidente
{
    public static readonly ErroAplicacao CategoriaInexistente = ErroAplicacao.DeCampo("categoriaId", "Categoria inexistente.");

    public static readonly ErroAplicacao CategoriaNaoEIncidente = ErroAplicacao.Regra(
        StatusCodes.Status422UnprocessableEntity, "categoria-nao-e-incidente", "A categoria escolhida é de problema, não de incidente.");

    public static readonly ErroAplicacao HorarioForaDaJanela = ErroAplicacao.Regra(
        StatusCodes.Status422UnprocessableEntity, "horario-fora-da-janela", "O horário do incidente precisa estar nas últimas 24 horas.");

    public static readonly ErroAplicacao LimiteDiarioIncidentes = ErroAplicacao.Regra(
        StatusCodes.Status429TooManyRequests, "limite-diario-incidentes", "Limite de 10 incidentes em 24 horas atingido.");
}
```

`Feature/Incidentes/Abstractions/IIncidenteService.cs`:
```csharp
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Incidentes;

public interface IIncidenteService
{
    Task<Resultado<IncidenteRegistrado>> RegistrarAsync(RegistrarIncidenteRequest request, Guid autorId, CancellationToken ct);

    Task<List<ObterCalorIncidentesResponse>> ObterCalorAsync(ObterCalorIncidentesRequest request, CancellationToken ct);

    Task<ObterAreaIncidentesResponse> ObterAreaAsync(ObterAreaIncidentesRequest request, CancellationToken ct);
}
```

`Feature/Incidentes/Services/IncidenteService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Geocodificacao;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Incidentes;

public sealed class IncidenteService(AppDbContext db, IServicoGeocodificacao geocodificacao, TimeProvider relogio) : IIncidenteService
{
    /// <summary>
    /// Arredonda o ponto em 3 casas ANTES de gravar e guarda o rótulo da geocodificação do ponto arredondado.
    /// Responde com o resumo da área do ponto arredondado. O autor nunca é exposto.
    /// </summary>
    public async Task<Resultado<IncidenteRegistrado>> RegistrarAsync(RegistrarIncidenteRequest request, Guid autorId, CancellationToken ct)
    {
        var categoria = await db.Categorias.FindAsync([request.CategoriaId], ct);
        if (categoria is null)
        {
            return ErrosIncidente.CategoriaInexistente;
        }

        if (categoria.Tipo != TipoOcorrencia.Incidente)
        {
            return ErrosIncidente.CategoriaNaoEIncidente;
        }

        var agora = relogio.GetUtcNow();
        if (request.OcorridoEm < agora - LimitesIncidente.Janela || request.OcorridoEm > agora)
        {
            return ErrosIncidente.HorarioForaDaJanela;
        }

        var desde = agora - LimitesIncidente.Janela;

        // Pré-checagem sem bloqueio: evita gastar uma consulta ao Nominatim com quem já atingiu o limite.
        if (await ContarRecentesAsync(autorId, desde, ct) >= LimitesIncidente.MaximoPor24h)
        {
            return ErrosIncidente.LimiteDiarioIncidentes;
        }

        var lat = Arredondar(request.Lat);
        var lng = Arredondar(request.Lng);
        var endereco = await geocodificacao.ReversaAsync(lat, lng, ct);

        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        // Serializa os registros da mesma conta: sem isso, requisições simultâneas furariam o limite de 10.
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({ChaveBloqueio(autorId)})", ct);

        if (await ContarRecentesAsync(autorId, desde, ct) >= LimitesIncidente.MaximoPor24h)
        {
            return ErrosIncidente.LimiteDiarioIncidentes;
        }

        db.Incidentes.Add(new Incidente
        {
            Id = Guid.CreateVersion7(),
            CategoriaId = categoria.Id,
            AutorId = autorId,
            AreaAproximada = Geo.Ponto(lat, lng),
            RaioMetros = LimitesIncidente.RaioMetros,
            RotuloArea = endereco.RotuloArea,
            OcorridoEm = request.OcorridoEm.ToUniversalTime(),
            RegistradoEm = agora,
        });
        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);

        return new IncidenteRegistrado(lat, lng, await ConsultarAreaAsync(lat, lng, agora, ct));
    }

    /// <summary>
    /// Agrupa por ponto arredondado os incidentes da janela (<c>ocorrido_em</c>) dentro do retângulo. O recorte
    /// é planar (<c>::geometry &amp;&amp;</c>, índice ix_incidentes_area_aproximada_geometry): em geography as
    /// arestas seriam geodésicas e o retângulo, inexato em viewport largo.
    /// </summary>
    public async Task<List<ObterCalorIncidentesResponse>> ObterCalorAsync(ObterCalorIncidentesRequest request, CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();
        var desde = agora.AddDays(-request.Dias);
        var grupos = Listas.Separar(request.Grupos);

        return await db.Database
            .SqlQuery<ObterCalorIncidentesResponse>($"""
                SELECT ST_Y(i.area_aproximada::geometry) AS lat,
                       ST_X(i.area_aproximada::geometry) AS lng,
                       count(*)::int AS quantidade
                FROM incidentes i
                JOIN categorias c ON c.id = i.categoria_id
                WHERE i.ocorrido_em >= {desde} AND i.ocorrido_em <= {agora}
                  AND i.area_aproximada::geometry && ST_MakeEnvelope({request.MinLng}, {request.MinLat}, {request.MaxLng}, {request.MaxLat}, 4326)
                  AND (cardinality({grupos}) = 0 OR c.grupo = ANY({grupos}))
                GROUP BY 1, 2
                ORDER BY quantidade DESC
                LIMIT {LimitesIncidente.MaximoCelulasCalor}
                """)
            .ToListAsync(ct);
    }

    public Task<ObterAreaIncidentesResponse> ObterAreaAsync(ObterAreaIncidentesRequest request, CancellationToken ct) =>
        ConsultarAreaAsync(request.Lat, request.Lng, relogio.GetUtcNow(), ct);

    public static NivelConcentracao Nivel(int total7Dias) =>
        total7Dias >= LimitesIncidente.LimiarAlta ? NivelConcentracao.Alta
        : total7Dias >= LimitesIncidente.LimiarMedia ? NivelConcentracao.Media
        : NivelConcentracao.Baixa;

    public static double Arredondar(double coordenada) =>
        Math.Round(coordenada, LimitesIncidente.CasasDecimais, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Contagens de 7 e 30 dias (por <c>ocorrido_em</c>) num raio fixo de 400 m, para todas as categorias de
    /// incidente (inclusive as zeradas). O nível usa o total de 7 dias.
    /// </summary>
    private async Task<ObterAreaIncidentesResponse> ConsultarAreaAsync(double lat, double lng, DateTimeOffset agora, CancellationToken ct)
    {
        var ponto = Geo.Ponto(lat, lng);
        var desde7 = agora.AddDays(-7);
        var desde30 = agora.AddDays(-30);

        var porCategoria = await db.Incidentes
            .Where(i => i.OcorridoEm >= desde30 && i.OcorridoEm <= agora && i.AreaAproximada.IsWithinDistance(ponto, LimitesIncidente.RaioAreaMetros))
            .GroupBy(i => i.CategoriaId)
            .Select(g => new { CategoriaId = g.Key, Dias7 = g.Count(i => i.OcorridoEm >= desde7), Dias30 = g.Count() })
            .ToDictionaryAsync(g => g.CategoriaId, ct);

        var categorias = await db.Categorias
            .Where(c => c.Tipo == TipoOcorrencia.Incidente)
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.Nome })
            .ToListAsync(ct);

        var contagens = categorias
            .Select(c => porCategoria.TryGetValue(c.Id, out var n)
                ? new ObterAreaIncidentesContagem(c.Id, c.Nome, n.Dias7, n.Dias30)
                : new ObterAreaIncidentesContagem(c.Id, c.Nome, 0, 0))
            .ToList();

        return new ObterAreaIncidentesResponse(LimitesIncidente.RaioAreaMetros, agora, Nivel(contagens.Sum(c => c.Dias7)), contagens);
    }

    private Task<int> ContarRecentesAsync(Guid autorId, DateTimeOffset desde, CancellationToken ct) =>
        db.Incidentes.CountAsync(i => i.AutorId == autorId && i.RegistradoEm > desde, ct);

    /// <summary>Chave de 64 bits do advisory lock, derivada do id do usuário.</summary>
    private static long ChaveBloqueio(Guid usuarioId)
    {
        Span<byte> bytes = stackalloc byte[16];
        usuarioId.TryWriteBytes(bytes);
        return BitConverter.ToInt64(bytes[..8]) ^ BitConverter.ToInt64(bytes[8..]);
    }
}
```

`Modules/IncidenteModule.cs`:
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Incidentes;

namespace Ocorrencias.Api.Modules;

public sealed class IncidenteModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapPost("/incidentes", Registrar)
            .Validar<RegistrarIncidenteRequest>()
            .WithTags("Incidentes");

        api.MapGet("/incidentes/calor", ObterCalor)
            .Validar<ObterCalorIncidentesRequest>()
            .AllowAnonymous()
            .WithTags("Incidentes");

        api.MapGet("/incidentes/area", ObterArea)
            .Validar<ObterAreaIncidentesRequest>()
            .AllowAnonymous()
            .WithTags("Incidentes");
    }

    private static async Task<Results<Created<ObterAreaIncidentesResponse>, ValidationProblem, ProblemHttpResult>> Registrar(
        RegistrarIncidenteRequest request, ClaimsPrincipal principal, IIncidenteService service, CancellationToken ct)
    {
        var r = await service.RegistrarAsync(request, principal.ObterUsuarioId(), ct);
        return r.Erro switch
        {
            null => TypedResults.Created(
                FormattableString.Invariant($"/api/v1/incidentes/area?lat={r.Valor!.Lat}&lng={r.Valor.Lng}"), r.Valor.Area),
            { Tipo: TipoErro.Campo } e => e.ParaValidacao(),
            var e => e.ParaProblema(),
        };
    }

    private static Task<List<ObterCalorIncidentesResponse>> ObterCalor(
        [AsParameters] ObterCalorIncidentesRequest request, IIncidenteService service, CancellationToken ct) =>
        service.ObterCalorAsync(request, ct);

    private static Task<ObterAreaIncidentesResponse> ObterArea(
        [AsParameters] ObterAreaIncidentesRequest request, IIncidenteService service, CancellationToken ct) =>
        service.ObterAreaAsync(request, ct);
}
```

Em `AddServicos`: `services.AddScoped<IIncidenteService, IncidenteService>();` (com `using Ocorrencias.Api.Incidentes;`). Apague `Features/Incidentes/`, remova as 3 chamadas e o using de `Features/Endpoints.cs`, e remova os 3 erros de `Common/Erros.cs`.

- [ ] **Step 4: Testes** — Run: `dotnet build && dotnet test tests/Ocorrencias.Api.Tests` → tudo verde. Inclui `Location_do_incidente_usa_o_ponto_arredondado_em_cultura_invariante` e o teste dos 15 registros simultâneos (advisory lock).
- [ ] **Step 5: Commit**

```bash
git add -A src/Ocorrencias.Api tests/Ocorrencias.Api.Tests
git commit -m "refactor(incidentes): extrai service, erros e module"
```

---

### Task 12: Área Verificação

**Files:**
- Create: `Feature/Verificacao/Models/VerificacaoDtos.cs`, `Feature/Verificacao/Validators/VerificacaoValidators.cs`, `Feature/Verificacao/Errors/ErrosVerificacao.cs`, `Feature/Verificacao/Abstractions/IVerificacaoService.cs`, `Feature/Verificacao/Services/VerificacaoService.cs`, `Modules/VerificacaoModule.cs`
- Delete: `Features/Verificacao/Solicitar.cs`
- Modify: `Features/Endpoints.cs` (remover `MapSolicitarVerificacao` e o using), `Common/Erros.cs` (remover `VerificacaoPendenteExistente`, `JaVerificado`), `AddServicos`
- Test: `Verificacao/VerificacaoTests.cs`, `Admin/AdminTests.cs` (`SolicitarVerificacao.Response` → `SolicitarVerificacaoResponse`, `using Ocorrencias.Api.Verificacao;`)

**Interfaces:**
- Produces (`Ocorrencias.Api.Verificacao`): `SolicitarVerificacaoRequest`, `SolicitarVerificacaoResponse`, `IVerificacaoService.SolicitarAsync(Guid usuarioId, SolicitarVerificacaoRequest, CancellationToken)` → `Task<Resultado<SolicitarVerificacaoResponse>>`.

- [ ] **Step 1: Atualizar os testes** (usings e renomes).
- [ ] **Step 2: Build falha** — Run: `dotnet build tests/Ocorrencias.Api.Tests` → FAIL.
- [ ] **Step 3: Implementar**

`Feature/Verificacao/Models/VerificacaoDtos.cs`:
```csharp
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Verificacao;

public sealed record SolicitarVerificacaoRequest(TipoVinculo TipoVinculo, string Justificativa);

public sealed record SolicitarVerificacaoResponse(
    Guid Id, TipoVinculo TipoVinculo, string Justificativa, StatusVerificacao Status, DateTimeOffset CriadaEm);
```

`Feature/Verificacao/Validators/VerificacaoValidators.cs`:
```csharp
using FluentValidation;

namespace Ocorrencias.Api.Verificacao;

public sealed class SolicitarVerificacaoValidator : AbstractValidator<SolicitarVerificacaoRequest>
{
    public SolicitarVerificacaoValidator()
    {
        RuleFor(r => r.TipoVinculo).IsInEnum();
        RuleFor(r => r.Justificativa)
            .NotEmpty()
            .Must(j => j?.Trim().Length is >= 30 and <= 500)
            .WithMessage("A justificativa precisa ter entre 30 e 500 caracteres.");
    }
}
```

`Feature/Verificacao/Errors/ErrosVerificacao.cs`:
```csharp
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Verificacao;

public static class ErrosVerificacao
{
    public static readonly ErroAplicacao VerificacaoPendenteExistente = ErroAplicacao.Regra(
        StatusCodes.Status409Conflict, "verificacao-pendente-existente", "Você já tem uma solicitação de verificação em análise.");

    public static readonly ErroAplicacao JaVerificado = ErroAplicacao.Regra(
        StatusCodes.Status422UnprocessableEntity, "ja-verificado", "Sua conta já é verificada.");
}
```

`Feature/Verificacao/Abstractions/IVerificacaoService.cs`:
```csharp
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Verificacao;

public interface IVerificacaoService
{
    Task<Resultado<SolicitarVerificacaoResponse>> SolicitarAsync(Guid usuarioId, SolicitarVerificacaoRequest request, CancellationToken ct);
}
```

`Feature/Verificacao/Services/VerificacaoService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Verificacao;

public sealed class VerificacaoService(AppDbContext db, TimeProvider relogio) : IVerificacaoService
{
    public async Task<Resultado<SolicitarVerificacaoResponse>> SolicitarAsync(Guid usuarioId, SolicitarVerificacaoRequest request, CancellationToken ct)
    {
        var perfil = await db.Users.Where(u => u.Id == usuarioId).Select(u => (Perfil?)u.Perfil).SingleOrDefaultAsync(ct);
        if (perfil is null)
        {
            return ErroAplicacao.NaoAutorizado;
        }

        if (perfil != Perfil.Cidadao)
        {
            return ErrosVerificacao.JaVerificado;
        }

        if (await db.SolicitacoesVerificacao.AnyAsync(s => s.UsuarioId == usuarioId && s.Status == StatusVerificacao.Pendente, ct))
        {
            return ErrosVerificacao.VerificacaoPendenteExistente;
        }

        var solicitacao = new SolicitacaoVerificacao
        {
            Id = Guid.CreateVersion7(),
            UsuarioId = usuarioId,
            TipoVinculo = request.TipoVinculo,
            Justificativa = request.Justificativa.Trim(),
            Status = StatusVerificacao.Pendente,
            CriadaEm = relogio.GetUtcNow(),
        };
        db.SolicitacoesVerificacao.Add(solicitacao);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Corrida entre a checagem e o INSERT: o índice único parcial barra a segunda pendente.
            return ErrosVerificacao.VerificacaoPendenteExistente;
        }

        return new SolicitarVerificacaoResponse(
            solicitacao.Id, solicitacao.TipoVinculo, solicitacao.Justificativa, solicitacao.Status, solicitacao.CriadaEm);
    }
}
```

`Modules/VerificacaoModule.cs`:
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Verificacao;

namespace Ocorrencias.Api.Modules;

public sealed class VerificacaoModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapPost("/verificacoes", Solicitar)
            .Validar<SolicitarVerificacaoRequest>()
            .WithTags("Verificação");
    }

    private static async Task<Results<Created<SolicitarVerificacaoResponse>, ProblemHttpResult, UnauthorizedHttpResult>> Solicitar(
        SolicitarVerificacaoRequest request, ClaimsPrincipal principal, IVerificacaoService service, CancellationToken ct)
    {
        var r = await service.SolicitarAsync(principal.ObterUsuarioId(), request, ct);
        return r.Erro switch
        {
            null => TypedResults.Created("/api/v1/me", r.Valor!),
            { Tipo: TipoErro.NaoAutorizado } e => e.ParaNaoAutorizado(),
            var e => e.ParaProblema(),
        };
    }
}
```

Em `AddServicos`: `services.AddScoped<IVerificacaoService, VerificacaoService>();` (com `using Ocorrencias.Api.Verificacao;`). Apague `Features/Verificacao/`, remova a chamada e o using de `Features/Endpoints.cs`, e remova os 2 erros de `Common/Erros.cs`.

- [ ] **Step 4: Testes** — Run: `dotnet build && dotnet test tests/Ocorrencias.Api.Tests` → tudo verde.
- [ ] **Step 5: Commit**

```bash
git add -A src/Ocorrencias.Api tests/Ocorrencias.Api.Tests
git commit -m "refactor(verificacao): extrai service, erros e module"
```

---

### Task 13: Área Admin e remoção do legado

**Files:**
- Create: `Feature/Admin/Models/AdminDtos.cs`, `Feature/Admin/Validators/AdminValidators.cs`, `Feature/Admin/Errors/ErrosAdmin.cs`, `Feature/Admin/Abstractions/IAdminService.cs`, `Feature/Admin/Services/AdminService.cs`, `Modules/AdminModule.cs`
- Delete: `Features/Admin/` (4 arquivos), `Features/Endpoints.cs`, `Common/Erros.cs`, pasta `Features/`
- Modify: `Common/Extensions/EndpointRouteBuilderExtensions.cs` (remover `api.MapFeaturesLegadas();` e `using Ocorrencias.Api.Features;`), `AddServicos`
- Test: `Admin/AdminTests.cs` (`ListarVerificacoes.*` → nomes novos, `ListarVerificacoes.Iniciais` → `AdminService.Iniciais`, `using Ocorrencias.Api.Admin;`)

**Interfaces:**
- Produces (`Ocorrencias.Api.Admin`):
  - `ListarVerificacoesRequest`, `ListarVerificacoesItem`, `ListarVerificacoesUsuarioResumo`, `RecusarVerificacaoRequest`
  - `IAdminService.ListarVerificacoesAsync(ListarVerificacoesRequest, CancellationToken)` → `Task<Paginado<ListarVerificacoesItem>>`
  - `IAdminService.AprovarAsync(Guid id, Guid adminId, CancellationToken)` → `Task<Resultado<Vazio>>`
  - `IAdminService.RecusarAsync(Guid id, Guid adminId, RecusarVerificacaoRequest, CancellationToken)` → `Task<Resultado<Vazio>>`
  - `static AdminService.Iniciais(string)`

- [ ] **Step 1: Atualizar `AdminTests.cs`** conforme **Files**.
- [ ] **Step 2: Build falha** — Run: `dotnet build tests/Ocorrencias.Api.Tests` → FAIL.
- [ ] **Step 3: Implementar**

`Feature/Admin/Models/AdminDtos.cs`:
```csharp
using Ocorrencias.Api.Common;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Admin;

/// <param name="Status">pendente | aprovada | recusada. Vazio = todas.</param>
public sealed record ListarVerificacoesRequest(string? Status = null, int Pagina = 1, int Tamanho = Paginacao.TamanhoPadrao);

public sealed record ListarVerificacoesItem(
    Guid Id,
    ListarVerificacoesUsuarioResumo Usuario,
    TipoVinculo TipoVinculo,
    string Justificativa,
    StatusVerificacao Status,
    DateTimeOffset CriadaEm,
    string? MotivoRecusa);

public sealed record ListarVerificacoesUsuarioResumo(Guid Id, string Nome, string Iniciais);

public sealed record RecusarVerificacaoRequest(string Motivo);
```

`Feature/Admin/Validators/AdminValidators.cs`:
```csharp
using FluentValidation;
using Ocorrencias.Api.Common;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Admin;

public sealed class ListarVerificacoesValidator : AbstractValidator<ListarVerificacoesRequest>
{
    private static readonly HashSet<string> NomesStatus = new(Enum.GetNames<StatusVerificacao>(), StringComparer.OrdinalIgnoreCase);

    public ListarVerificacoesValidator()
    {
        RuleFor(r => r.Status)
            .Must(s => s is null || NomesStatus.Contains(s))
            .WithMessage("Status desconhecido. Valores aceitos: pendente, aprovada, recusada.");
        RuleFor(r => r.Pagina).InclusiveBetween(1, Paginacao.PaginaMaxima);
        RuleFor(r => r.Tamanho).InclusiveBetween(1, Paginacao.TamanhoMaximo);
    }
}

public sealed class RecusarVerificacaoValidator : AbstractValidator<RecusarVerificacaoRequest>
{
    public RecusarVerificacaoValidator()
    {
        RuleFor(r => r.Motivo).NotEmpty().MaximumLength(500);
    }
}
```

`Feature/Admin/Errors/ErrosAdmin.cs`:
```csharp
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Admin;

public static class ErrosAdmin
{
    public static readonly ErroAplicacao VerificacaoJaAnalisada = ErroAplicacao.Regra(
        StatusCodes.Status409Conflict, "verificacao-ja-analisada", "Esta solicitação já foi analisada.");
}
```

`Feature/Admin/Abstractions/IAdminService.cs`:
```csharp
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Admin;

public interface IAdminService
{
    Task<Paginado<ListarVerificacoesItem>> ListarVerificacoesAsync(ListarVerificacoesRequest request, CancellationToken ct);

    Task<Resultado<Vazio>> AprovarAsync(Guid id, Guid adminId, CancellationToken ct);

    Task<Resultado<Vazio>> RecusarAsync(Guid id, Guid adminId, RecusarVerificacaoRequest request, CancellationToken ct);
}
```

`Feature/Admin/Services/AdminService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Admin;

public sealed class AdminService(AppDbContext db, TimeProvider relogio) : IAdminService
{
    /// <summary>Pendentes da mais antiga para a mais nova (fila de análise); demais, da mais nova para a mais antiga.</summary>
    public async Task<Paginado<ListarVerificacoesItem>> ListarVerificacoesAsync(ListarVerificacoesRequest request, CancellationToken ct)
    {
        var consulta = db.SolicitacoesVerificacao.AsQueryable();
        StatusVerificacao? status = request.Status is null ? null : Enum.Parse<StatusVerificacao>(request.Status, ignoreCase: true);
        if (status is not null)
        {
            consulta = consulta.Where(s => s.Status == status);
        }

        var total = await consulta.CountAsync(ct);
        var ordenada = status == StatusVerificacao.Pendente
            ? consulta.OrderBy(s => s.CriadaEm)
            : consulta.OrderByDescending(s => s.CriadaEm);

        var linhas = await ordenada
            .ThenBy(s => s.Id)
            .Skip(Paginacao.Pular(request.Pagina, request.Tamanho))
            .Take(request.Tamanho)
            .Select(s => new
            {
                s.Id,
                s.UsuarioId,
                s.Usuario.Nome,
                s.TipoVinculo,
                s.Justificativa,
                s.Status,
                s.CriadaEm,
                s.MotivoRecusa,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var itens = linhas
            .Select(l => new ListarVerificacoesItem(
                l.Id, new ListarVerificacoesUsuarioResumo(l.UsuarioId, l.Nome, Iniciais(l.Nome)),
                l.TipoVinculo, l.Justificativa, l.Status, l.CriadaEm, l.MotivoRecusa))
            .ToList();

        return new Paginado<ListarVerificacoesItem>(itens, request.Pagina, request.Tamanho, total);
    }

    /// <summary>
    /// Perfil do solicitante → <c>cidadaoVerificado</c>. Vale para os votos SEGUINTES: os anteriores mantêm o
    /// <c>perfil_no_momento</c> gravado.
    /// </summary>
    public async Task<Resultado<Vazio>> AprovarAsync(Guid id, Guid adminId, CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();
        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        var conclusao = await ConcluirAsync(id, adminId, StatusVerificacao.Aprovada, null, agora, ct);
        if (conclusao.Erro is { } erro)
        {
            return erro;
        }

        // Administrador não é rebaixado a verificado.
        await db.Users
            .Where(u => u.Id == conclusao.Valor && u.Perfil == Perfil.Cidadao)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Perfil, Perfil.CidadaoVerificado)
                .SetProperty(u => u.VerificadoEm, agora), ct);

        await transacao.CommitAsync(ct);
        return new Vazio();
    }

    public async Task<Resultado<Vazio>> RecusarAsync(Guid id, Guid adminId, RecusarVerificacaoRequest request, CancellationToken ct)
    {
        var conclusao = await ConcluirAsync(id, adminId, StatusVerificacao.Recusada, request.Motivo.Trim(), relogio.GetUtcNow(), ct);
        if (conclusao.Erro is { } erro)
        {
            return erro;
        }

        return new Vazio();
    }

    /// <summary>"Carlos Menezes" → "CM"; "Ana" → "A"; "maria da silva" → "MS".</summary>
    public static string Iniciais(string nome)
    {
        var partes = nome.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return partes.Length switch
        {
            0 => "",
            1 => char.ToUpperInvariant(partes[0][0]).ToString(),
            _ => string.Concat(char.ToUpperInvariant(partes[0][0]), char.ToUpperInvariant(partes[^1][0])),
        };
    }

    /// <summary>
    /// Transição única Pendente → Aprovada/Recusada. UPDATE condicional (<c>WHERE status = pendente</c>): duas análises
    /// simultâneas da mesma solicitação não passam ambas. Retorna o id do solicitante, ou o erro adequado.
    /// </summary>
    private async Task<Resultado<Guid>> ConcluirAsync(
        Guid id, Guid adminId, StatusVerificacao resultado, string? motivo, DateTimeOffset agora, CancellationToken ct)
    {
        var alteradas = await db.SolicitacoesVerificacao
            .Where(s => s.Id == id && s.Status == StatusVerificacao.Pendente)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, resultado)
                .SetProperty(x => x.MotivoRecusa, motivo)
                .SetProperty(x => x.AnalisadaPorId, adminId)
                .SetProperty(x => x.AnalisadaEm, agora), ct);

        if (alteradas == 1)
        {
            return await db.SolicitacoesVerificacao.Where(s => s.Id == id).Select(s => s.UsuarioId).SingleAsync(ct);
        }

        if (await db.SolicitacoesVerificacao.AnyAsync(s => s.Id == id, ct))
        {
            return ErrosAdmin.VerificacaoJaAnalisada;
        }

        return ErroAplicacao.NaoEncontrado;
    }
}
```

`Modules/AdminModule.cs`:
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Ocorrencias.Api.Admin;
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Modules;

public sealed class AdminModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/admin/verificacoes", ListarVerificacoes)
            .Validar<ListarVerificacoesRequest>()
            .RequireAuthorization(PoliticasAutorizacao.Administrador)
            .WithTags("Admin");

        api.MapPost("/admin/verificacoes/{id:guid}/aprovar", Aprovar)
            .RequireAuthorization(PoliticasAutorizacao.Administrador)
            .WithTags("Admin");

        api.MapPost("/admin/verificacoes/{id:guid}/recusar", Recusar)
            .Validar<RecusarVerificacaoRequest>()
            .RequireAuthorization(PoliticasAutorizacao.Administrador)
            .WithTags("Admin");
    }

    private static Task<Paginado<ListarVerificacoesItem>> ListarVerificacoes(
        [AsParameters] ListarVerificacoesRequest request, IAdminService service, CancellationToken ct) =>
        service.ListarVerificacoesAsync(request, ct);

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Aprovar(
        Guid id, ClaimsPrincipal principal, IAdminService service, CancellationToken ct) =>
        ParaHttp(await service.AprovarAsync(id, principal.ObterUsuarioId(), ct));

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Recusar(
        Guid id, RecusarVerificacaoRequest request, ClaimsPrincipal principal, IAdminService service, CancellationToken ct) =>
        ParaHttp(await service.RecusarAsync(id, principal.ObterUsuarioId(), request, ct));

    private static Results<NoContent, NotFound, ProblemHttpResult> ParaHttp(Resultado<Vazio> r) =>
        r.Erro switch
        {
            null => TypedResults.NoContent(),
            { Tipo: TipoErro.NaoEncontrado } e => e.ParaNaoEncontrado(),
            var e => e.ParaProblema(),
        };
}
```

Em `AddServicos`: `services.AddScoped<IAdminService, AdminService>();` (com `using Ocorrencias.Api.Admin;`).

Remoção do legado:
```bash
git rm -r src/Ocorrencias.Api/Features
git rm src/Ocorrencias.Api/Common/Erros.cs
```
Em `Common/Extensions/EndpointRouteBuilderExtensions.cs`, apague a linha `api.MapFeaturesLegadas();` e o `using Ocorrencias.Api.Features;`.

Run: `grep -rn "Ocorrencias.Api.Features\|Erros\.\(Campo\|Problema\)\b" src tests --include='*.cs' | grep -v "/bin/\|/obj/"`
Expected: vazio.

- [ ] **Step 4: Suíte completa**

Run: `dotnet build && dotnet test`
Expected: tudo verde. Domain 71, Simulator 42 e API com os testes de antes mais os das Tasks 1 e 2, incluindo `Rotas_e_politicas_do_contrato` (21 rotas só de módulos) e `Openapi_igual_ao_snapshot`.

- [ ] **Step 5: Commit**

```bash
git add -A src/Ocorrencias.Api tests/Ocorrencias.Api.Tests
git commit -m "refactor(admin): extrai service e module e remove as features legadas"
```

---

### Task 14: Documentação e verificação final

**Files:**
- Modify: `CLAUDE.md` (seções "Arquitetura" e "Convenções", e o comando `migrations add`)
- Modify: `docs/PLAN.md` (caminhos)

- [ ] **Step 1: `CLAUDE.md` — substituir a seção "Arquitetura: Vertical Slice"**

Troque o título por `## Arquitetura: vertical slice por feature (padrão MedComite.Core)` e a árvore por:
````markdown
```
Ocorrencias.slnx
src/
  Ocorrencias.Domain/          # motor de confiança + enums + tipos de valor. ZERO dependências (nem EF, nem ASP.NET)
    Confianca/MotorConfianca.cs, ConfiguracaoConfianca.cs, VotoFato.cs, ...
  Ocorrencias.Api/
    Program.cs                 # AddAplicacao() + configurações de borda + pipeline + MapEndpoints()
    Modules/                   # um IEndpointModule por área (descobertos por reflexão): só HTTP
      AuthModule.cs, UsuarioModule.cs, CategoriaModule.cs, GeocodificacaoModule.cs, ProblemaModule.cs,
      VotoModule.cs, IncidenteModule.cs, VerificacaoModule.cs, AdminModule.cs
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
````

Também apague o parágrafo "Cada arquivo de feature contém: `record Request`, ... o handler usa o `AppDbContext` direto." logo abaixo da árvore.

- [ ] **Step 2: `CLAUDE.md` — Convenções e Comandos**

Na lista "Convenções", acrescente:
```markdown
- Um service por área (`I<Area>Service`, scoped, registrado em `DependencyInjection.AddServicos`), um module por área. Endpoint novo nasce exigindo autenticação (grupo `/api/v1`); visitante só com `AllowAnonymous()` explícito.
- DTOs de contrato são `record`s de topo com o nome do schema no OpenAPI (`ObterDetalheProblemaResponse`). Mudou o OpenAPI? `ContratoTests.Openapi_igual_ao_snapshot` falha; regrave com `ATUALIZAR_SNAPSHOT_OPENAPI=1 dotnet test --filter Openapi_igual_ao_snapshot` **só** se a mudança de contrato foi aprovada (e replique em `docs/API.md` e no front).
```
Em "Comandos", troque a linha de `migrations add` por:
```bash
dotnet ef migrations add <Nome> -p src/Ocorrencias.Api -o Infrastructure/Persistence/Migrations
```

- [ ] **Step 3: `docs/PLAN.md` — caminhos**

- Linha 36: `` `Infra/Geocodificacao`: `` → `` `Feature/Geocodificacao`: ``
- Linha 51: `` `Infra/Jobs/CicloDeVidaJob` `` → `` `Feature/Confianca/Jobs/CicloDeVidaJob` ``

Run: `grep -rn "Infra/\|Features/" CLAUDE.md docs/PLAN.md`
Expected: vazio.

- [ ] **Step 4: Conferir o snapshot contra o OpenAPI do front**

Salve em `$SCRATCH/compara_openapi.py`:
```python
import json, sys

a = json.load(open(sys.argv[1], encoding="utf-8"))
b = json.load(open(sys.argv[2], encoding="utf-8"))
for chave in ("paths", "components", "tags"):
    igual = json.dumps(a.get(chave), sort_keys=True) == json.dumps(b.get(chave), sort_keys=True)
    print(chave, "igual" if igual else "DIFERENTE")
```
Run: `python3 $SCRATCH/compara_openapi.py tests/Ocorrencias.Api.Tests/Esqueleto/openapi.snapshot.json ../TCC.front/docs/openapi.json`
Expected: as três linhas `igual`. Se `paths`/`components` derem `DIFERENTE`, o snapshot do front é anterior à etapa-7 e não é causa desta refatoração (o snapshot da Task 1 foi gerado antes de qualquer mudança). Registre isso no PR e não altere o front sem perguntar.

- [ ] **Step 5: Verificação final**

Run:
```bash
dotnet build -c Release
dotnet test
find src/Ocorrencias.Api -type d -name Features -o -type d -name Infra   # vazio
git diff --stat etapa-7 -- src/Ocorrencias.Domain src/Ocorrencias.Simulator docs/API.md docs/MOTOR_CONFIANCA.md   # vazio
```
Expected: build sem erros, todos os testes verdes e os dois últimos comandos sem saída.

- [ ] **Step 6: Commit e PR**

```bash
git add CLAUDE.md docs/PLAN.md
git commit -m "docs(arquitetura): descreve a estrutura por feature, modules e infrastructure"
git push -u origin refactor-arquitetura
GH_TOKEN=$(gh auth token --user VictorHenriqueTerraCheru) gh pr create --base etapa-7 --head refactor-arquitetura \
  --title "refactor: arquitetura no padrão MedComite.Core" \
  --body-file $SCRATCH/pr-body.md
```
`$SCRATCH/pr-body.md` deve conter:
- resumo da estrutura nova;
- garantias: snapshot do OpenAPI, 21 rotas/políticas, contagem de testes;
- link para o spec e para este plano;
- a nota da Task 14 Step 4, se houver.
