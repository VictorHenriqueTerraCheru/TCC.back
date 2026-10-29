using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UrbanReports.Api.Geocoding;
using UrbanReports.Api.Infrastructure;

namespace UrbanReports.Api.Tests.Geocoding;

public sealed class NominatimClientTests
{
    private sealed class RecordingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private static INominatimClient Create(RecordingHandler handler, string? email = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Nominatim:Email"] = email })
            .Build();

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(TimeProvider.System)
            .AddLogging()
            .AddGeocoding();
        services.AddHttpClient(NominatimClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider().GetRequiredService<INominatimClient>();
    }

    [Fact]
    public async Task Sends_user_agent_language_and_parameters_with_decimal_point()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """{"address":{"road":"Rua A","ISO3166-2-lvl4":"BR-RJ"}}""");
        var previousCulture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("pt-BR");

        try
        {
            var response = await Create(handler).ReverseAsync(-22.9521, -43.1868, CancellationToken.None);

            response!.Address!.Road.Should().Be("Rua A");
            response.Address.Iso3166Level4.Should().Be("BR-RJ");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previousCulture;
        }

        var req = handler.Request!;
        req.RequestUri!.ToString().Should().Be(
            "https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat=-22.9521&lon=-43.1868&zoom=18&addressdetails=1");
        req.Headers.UserAgent.ToString().Should().Be("OcorrenciasUrbanasTCC/1.0 (+https://github.com/VictorHenriqueTerraCheru/TCC.back)");
        req.Headers.AcceptLanguage.ToString().Should().Be("pt-BR");
    }

    [Fact]
    public async Task With_configured_email_it_goes_in_user_agent_and_parameter()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "{}");

        await Create(handler, "contato@tcc.org").ReverseAsync(-22.9521, -43.1868, CancellationToken.None);

        handler.Request!.RequestUri!.Query.Should().EndWith("&email=contato%40tcc.org");
        handler.Request.Headers.UserAgent.ToString().Should()
            .Be("OcorrenciasUrbanasTCC/1.0 (+https://github.com/VictorHenriqueTerraCheru/TCC.back; contato@tcc.org)");
    }

    [Fact]
    public async Task Http_error_becomes_exception()
    {
        var client = Create(new RecordingHandler(HttpStatusCode.ServiceUnavailable, "{}"));

        var action = () => client.ReverseAsync(-22.95, -43.18, CancellationToken.None);

        await action.Should().ThrowAsync<HttpRequestException>();
    }
}
