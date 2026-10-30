using System.Net;
using Microsoft.AspNetCore.Hosting;
using UrbanReports.Api.Tests.Infra;

namespace UrbanReports.Api.Tests.Health;

[Collection(ApiCollection.Name)]
public sealed class HealthTests(ApiFactory factory)
{
    [Fact]
    public async Task Application_returns_200_without_authentication()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("status").GetString().Should().Be("healthy");
    }

    [Fact]
    public async Task Reachable_database_returns_200_without_authentication()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/health/database");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("status").GetString().Should().Be("healthy");
    }

    [Fact]
    public async Task Unreachable_database_returns_503_database_unavailable()
    {
        await using var withoutDatabase = factory.WithWebHostBuilder(b => b.UseSetting(
            "ConnectionStrings:Default", "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2"));

        var response = await withoutDatabase.CreateClient().GetAsync("/api/v1/health/database");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.ReadJsonAsync()).GetProperty("type").GetString().Should().Be("database-unavailable");
    }

    [Fact]
    public async Task Old_health_route_is_gone()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
