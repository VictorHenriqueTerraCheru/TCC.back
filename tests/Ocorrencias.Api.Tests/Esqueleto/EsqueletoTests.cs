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
