using System.Net;
using System.Text.Json;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Tests.Infra;

namespace Ocorrencias.Api.Tests.Esqueleto;

[Collection(ApiCollection.Nome)]
public sealed class EsqueletoTests(ApiFactory fabrica)
{
    private readonly HttpClient _cliente = fabrica.CreateClient();

    [Fact]
    public async Task Health_responde_200()
    {
        var resposta = await _cliente.GetAsync("/health");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Documento_openapi_descreve_enums_como_string_e_nao_colide_nomes()
    {
        var resposta = await _cliente.GetAsync("/openapi/v1.json");
        var documento = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        documento.Should().Contain("\"cidadaoVerificado\"");
        documento.Should().Contain("\"CadastrarRequest\"").And.Contain("\"EntrarRequest\"");
    }

    [Fact]
    public async Task Openapi_gera_tipos_precisos_para_o_front()
    {
        var documento = await (await _cliente.GetAsync("/openapi/v1.json")).LerJsonAsync();

        documento.GetRawText().Should().NotContain("\"pattern\"", "números não devem aceitar string (integer|string)");
        foreach (var schema in documento.GetProperty("components").GetProperty("schemas").EnumerateObject())
        {
            if (schema.Value.TryGetProperty("enum", out var valores))
            {
                valores.EnumerateArray().Should().NotContain(v => v.ValueKind == JsonValueKind.Null, $"enum {schema.Name} não deve incluir null");
            }
        }

        foreach (var caminho in documento.GetProperty("paths").EnumerateObject())
        {
            foreach (var operacao in caminho.Value.EnumerateObject())
            {
                if (!operacao.Value.TryGetProperty("parameters", out var parametros))
                {
                    continue;
                }

                foreach (var parametro in parametros.EnumerateArray().Where(p => p.GetProperty("in").GetString() == "query"))
                {
                    var nome = parametro.GetProperty("name").GetString()!;
                    char.IsLower(nome[0]).Should().BeTrue($"parâmetro {nome} em {caminho.Name} deve ser camelCase");
                }
            }
        }
    }

    private sealed record Envelope<T>(T Valor);

    private static class Feature
    {
        public sealed record Item(int Id);
    }

    [Fact]
    public void Nome_de_schema_generico_inclui_o_argumento_qualificado() =>
        OpenApiConfig.NomeSchema(typeof(Envelope<Feature.Item>)).Should().Be("EsqueletoTestsEnvelopeFeatureItem");

    [Fact]
    public void Nullable_usa_o_mesmo_nome_do_tipo() =>
        OpenApiConfig.NomeSchema(typeof(Domain.Perfil?)).Should().Be("Perfil");

    [Fact]
    public async Task Nenhum_schema_e_variacao_nullable()
    {
        var documento = await (await _cliente.GetAsync("/openapi/v1.json")).LerJsonAsync();

        documento.GetProperty("components").GetProperty("schemas").EnumerateObject()
            .Select(p => p.Name).Should().NotContain(n => n.StartsWith("Nullable", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/api/v1/auth/login", "post", false)]
    [InlineData("/api/v1/auth/cadastro", "post", false)]
    [InlineData("/api/v1/categorias", "get", false)]
    [InlineData("/api/v1/me", "get", true)]
    [InlineData("/api/v1/me", "patch", true)]
    public async Task Openapi_exige_bearer_so_nas_operacoes_protegidas(string caminho, string metodo, bool exigeBearer)
    {
        var documento = await (await _cliente.GetAsync("/openapi/v1.json")).LerJsonAsync();
        var operacao = documento.GetProperty("paths").GetProperty(caminho).GetProperty(metodo);

        operacao.TryGetProperty("security", out _).Should().Be(exigeBearer);
    }

    [Theory]
    [InlineData(ApiFactory.OrigemFront, true)]
    [InlineData("https://malicioso.example", false)]
    public async Task Cors_libera_somente_origens_configuradas(string origem, bool liberada)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Options, "/health");
        requisicao.Headers.Add("Origin", origem);
        requisicao.Headers.Add("Access-Control-Request-Method", "GET");

        var resposta = await _cliente.SendAsync(requisicao);

        resposta.Headers.Contains("Access-Control-Allow-Origin").Should().Be(liberada);
    }

    private enum StatusExemplo { PossivelmenteResolvida }

    [Fact]
    public void Enums_trafegam_como_string_camel_case()
    {
        var opcoes = new JsonSerializerOptions();
        JsonConfig.Configurar(opcoes);

        JsonSerializer.Serialize(StatusExemplo.PossivelmenteResolvida, opcoes)
            .Should().Be("\"possivelmenteResolvida\"");
    }

    [Fact]
    public void Datas_trafegam_em_utc_com_z()
    {
        var opcoes = new JsonSerializerOptions();
        JsonConfig.Configurar(opcoes);
        var data = new DateTimeOffset(2026, 10, 6, 19, 19, 0, TimeSpan.FromHours(-3));

        JsonSerializer.Serialize(data, opcoes).Should().Be("\"2026-10-06T22:19:00Z\"");
        JsonSerializer.Serialize<DateTimeOffset?>(null, opcoes).Should().Be("null");
    }
}
