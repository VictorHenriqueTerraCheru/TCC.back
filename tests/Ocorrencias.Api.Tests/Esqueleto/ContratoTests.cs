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
        "GET /api/v1/saude [anonimo]",
        "GET /api/v1/saude/banco [anonimo]",
        "GET /api/v1/geocodificacao/reversa [anonimo]",
        "GET /api/v1/problemas [anonimo]",
        "GET /api/v1/problemas/recentes [anonimo]",
        "GET /api/v1/problemas/duplicatas",
        "GET /api/v1/problemas/{id:guid} [anonimo]",
        "POST /api/v1/problemas",
        "PUT /api/v1/problemas/{id:guid}/voto",
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
        (await removido.VotarAsync(problema.Id, "sim")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Recursos_inexistentes_devolvem_404()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var id = Guid.NewGuid();

        (await fabrica.CreateClient().GetAsync($"/api/v1/problemas/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await pessoa.VotarAsync(id, "sim")).StatusCode.Should().Be(HttpStatusCode.NotFound);
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
