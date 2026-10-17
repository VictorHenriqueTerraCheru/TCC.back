using System.Net;
using Microsoft.AspNetCore.Hosting;
using Ocorrencias.Api.Tests.Infra;

namespace Ocorrencias.Api.Tests.Saude;

[Collection(ApiCollection.Nome)]
public sealed class SaudeTests(ApiFactory fabrica)
{
    [Fact]
    public async Task Aplicacao_responde_200_sem_autenticacao()
    {
        var resposta = await fabrica.CreateClient().GetAsync("/api/v1/saude");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resposta.LerJsonAsync()).GetProperty("status").GetString().Should().Be("saudavel");
    }

    [Fact]
    public async Task Banco_acessivel_responde_200_sem_autenticacao()
    {
        var resposta = await fabrica.CreateClient().GetAsync("/api/v1/saude/banco");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resposta.LerJsonAsync()).GetProperty("status").GetString().Should().Be("saudavel");
    }

    [Fact]
    public async Task Banco_inacessivel_responde_503_banco_indisponivel()
    {
        await using var semBanco = fabrica.WithWebHostBuilder(b => b.UseSetting(
            "ConnectionStrings:Default", "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2"));

        var resposta = await semBanco.CreateClient().GetAsync("/api/v1/saude/banco");

        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await resposta.LerJsonAsync()).GetProperty("type").GetString().Should().Be("banco-indisponivel");
    }

    [Fact]
    public async Task Rota_antiga_health_nao_existe_mais()
    {
        var resposta = await fabrica.CreateClient().GetAsync("/health");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
