using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using UrbanReports.Api.Geocoding;
using UrbanReports.Api.Tests.Infra;

namespace UrbanReports.Api.Tests.Geocoding;

[Collection(ApiCollection.Name)]
public sealed class ReverseTests(ApiFactory factory)
{
    private static string Url(double lat, double lng) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/geocoding/reverse?lat={lat}&lng={lng}");

    [Fact]
    public async Task Visitor_gets_the_built_address()
    {
        var (lat, lng) = ApiFactory.UniquePoint();

        var response = await factory.CreateClient().GetAsync(Url(lat, lng));
        var body = await response.ReadAsync<ReverseResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Be(new ReverseResponse(
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
    public async Task Too_many_requests_per_ip_returns_429_rate_limited()
    {
        await using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimits:GeocodingPerMinute", "2"));
        var client = limited.CreateClient();
        var (lat, lng) = ApiFactory.UniquePoint();

        (await client.GetAsync(Url(lat, lng))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync(Url(lat, lng))).StatusCode.Should().Be(HttpStatusCode.OK);
        var third = await client.GetAsync(Url(lat, lng));

        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await third.ReadJsonAsync()).GetProperty("type").GetString().Should().Be("rate-limited");
    }

    [Fact]
    public async Task Nominatim_offline_returns_200_with_available_false()
    {
        var (lat, lng) = ApiFactory.UniquePoint();
        factory.Nominatim.SetOffline(lat, lng);

        var response = await factory.CreateClient().GetAsync(Url(lat, lng));
        var body = await response.ReadJsonAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("available").GetBoolean().Should().BeFalse();
        body.GetProperty("address").GetString().Should().StartWith("Próximo a ");
        body.GetProperty("areaLabel").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }
}
