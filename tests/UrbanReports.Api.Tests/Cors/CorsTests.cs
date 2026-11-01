using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using UrbanReports.Api.Infrastructure;
using UrbanReports.Api.Tests.Infra;

namespace UrbanReports.Api.Tests.Cors;

[Collection(ApiCollection.Name)]
public sealed class CorsTests(ApiFactory factory)
{
    [Theory]
    [InlineData(ApiFactory.FrontOrigin, true)]
    [InlineData("https://malicioso.example", false)]
    public async Task Cors_allows_only_configured_origins(string origin, bool released)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/health");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await factory.CreateClient().SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().Be(released);
    }

    [Fact]
    public void Production_without_cors_origins_fails_to_start()
    {
        var configuration = new ConfigurationBuilder().Build();
        var environment = new HostingEnvironment { EnvironmentName = Environments.Production };

        var register = () => new ServiceCollection().AddConfiguredCors(configuration, environment);

        register.Should().Throw<InvalidOperationException>().WithMessage("*Cors:Origins*");
    }
}
