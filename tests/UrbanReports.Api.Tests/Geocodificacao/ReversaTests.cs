using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using UrbanReports.Api.Geocodificacao;
using UrbanReports.Api.Tests.Infra;

namespace UrbanReports.Api.Tests.Geocodificacao;

[Collection(ApiCollection.Nome)]
public sealed class ReversaTests(ApiFactory fabrica)
{
    private static string Url(double lat, double lng) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/geocodificacao/reversa?lat={lat}&lng={lng}");

    [Fact]
    public async Task Visitante_obtem_o_endereco_montado()
    {
        var (lat, lng) = ApiFactory.PontoUnico();

        var resposta = await fabrica.CreateClient().GetAsync(Url(lat, lng));
        var corpo = await resposta.LerAsync<ReversaResponse>();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        corpo.Should().Be(new ReversaResponse(
            true,
            "Rua Voluntários da Pátria, 170 — Botafogo",
            "Rua Voluntários da Pátria",
            "170",
            "Botafogo",
            "Rio de Janeiro",
            "RJ",
            "Rua Voluntários da Pátria, Botafogo"));
    }

    [Fact]
    public async Task Duas_chamadas_seguidas_geram_uma_requisicao_ao_nominatim()
    {
        var (lat, lng) = ApiFactory.PontoUnico();
        var cliente = fabrica.CreateClient();
        var antes = fabrica.Nominatim.Chamadas;

        await cliente.GetAsync(Url(lat, lng));
        await cliente.GetAsync(Url(lat, lng));

        (fabrica.Nominatim.Chamadas - antes).Should().Be(1);
    }

    [Fact]
    public async Task Nominatim_fora_do_ar_responde_200_com_disponivel_false()
    {
        var (lat, lng) = ApiFactory.PontoUnico();
        fabrica.Nominatim.ForaDoAr(lat, lng);

        var resposta = await fabrica.CreateClient().GetAsync(Url(lat, lng));
        var corpo = await resposta.LerJsonAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("disponivel").GetBoolean().Should().BeFalse();
        corpo.GetProperty("endereco").GetString().Should().StartWith("Próximo a ");
        corpo.GetProperty("rotuloArea").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [Theory]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    public async Task Coordenada_invalida_devolve_400(double lat, double lng)
    {
        var resposta = await fabrica.CreateClient().GetAsync(Url(lat, lng));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Excesso_de_requisicoes_por_ip_devolve_429_limite_requisicoes()
    {
        await using var limitada = fabrica.WithWebHostBuilder(b => b.UseSetting("LimitesDeRequisicao:GeocodificacaoPorMinuto", "2"));
        var cliente = limitada.CreateClient();
        var (lat, lng) = ApiFactory.PontoUnico();

        (await cliente.GetAsync(Url(lat, lng))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await cliente.GetAsync(Url(lat, lng))).StatusCode.Should().Be(HttpStatusCode.OK);
        var terceira = await cliente.GetAsync(Url(lat, lng));

        terceira.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await terceira.LerJsonAsync()).GetProperty("type").GetString().Should().Be("limite-requisicoes");
    }
}
