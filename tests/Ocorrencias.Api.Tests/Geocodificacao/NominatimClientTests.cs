using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ocorrencias.Api.Geocodificacao;

namespace Ocorrencias.Api.Tests.Geocodificacao;

public sealed class NominatimClientTests
{
    private sealed class HandlerGravador(HttpStatusCode status, string corpo) : HttpMessageHandler
    {
        public HttpRequestMessage? Requisicao { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requisicao = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") });
        }
    }

    private static INominatimClient Criar(HandlerGravador handler, string? email = null)
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Nominatim:Email"] = email })
            .Build();

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuracao)
            .AddSingleton(TimeProvider.System)
            .AddLogging()
            .AddGeocodificacao();
        services.AddHttpClient(NominatimClient.NomeHttpClient).ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider().GetRequiredService<INominatimClient>();
    }

    [Fact]
    public async Task Envia_user_agent_idioma_e_parametros_com_ponto_decimal()
    {
        var handler = new HandlerGravador(HttpStatusCode.OK, """{"address":{"road":"Rua A","ISO3166-2-lvl4":"BR-RJ"}}""");
        var culturaAnterior = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("pt-BR");

        try
        {
            var resposta = await Criar(handler).ReversaAsync(-22.9521, -43.1868, CancellationToken.None);

            resposta!.Address!.Road.Should().Be("Rua A");
            resposta.Address.Iso3166Nivel4.Should().Be("BR-RJ");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = culturaAnterior;
        }

        var req = handler.Requisicao!;
        req.RequestUri!.ToString().Should().Be(
            "https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat=-22.9521&lon=-43.1868&zoom=18&addressdetails=1");
        req.Headers.UserAgent.ToString().Should().Be("OcorrenciasUrbanasTCC/1.0 (+https://github.com/VictorHenriqueTerraCheru/TCC.back)");
        req.Headers.AcceptLanguage.ToString().Should().Be("pt-BR");
    }

    [Fact]
    public async Task Com_email_configurado_ele_vai_no_user_agent_e_no_parametro()
    {
        var handler = new HandlerGravador(HttpStatusCode.OK, "{}");

        await Criar(handler, "contato@tcc.org").ReversaAsync(-22.9521, -43.1868, CancellationToken.None);

        handler.Requisicao!.RequestUri!.Query.Should().EndWith("&email=contato%40tcc.org");
        handler.Requisicao.Headers.UserAgent.ToString().Should()
            .Be("OcorrenciasUrbanasTCC/1.0 (+https://github.com/VictorHenriqueTerraCheru/TCC.back; contato@tcc.org)");
    }

    [Fact]
    public async Task Erro_http_vira_excecao()
    {
        var cliente = Criar(new HandlerGravador(HttpStatusCode.ServiceUnavailable, "{}"));

        var acao = () => cliente.ReversaAsync(-22.95, -43.18, CancellationToken.None);

        await acao.Should().ThrowAsync<HttpRequestException>();
    }
}
