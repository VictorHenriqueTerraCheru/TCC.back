using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Tests.Esqueleto;

public sealed class EsqueletoTests(WebApplicationFactory<Program> fabrica)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _cliente = fabrica.WithWebHostBuilder(b => b.UseEnvironment("Development")).CreateClient();

    [Fact]
    public async Task Health_responde_200()
    {
        var resposta = await _cliente.GetAsync("/health");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Documento_openapi_esta_disponivel()
    {
        var resposta = await _cliente.GetAsync("/openapi/v1.json");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("http://localhost:5173", true)]
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
}
