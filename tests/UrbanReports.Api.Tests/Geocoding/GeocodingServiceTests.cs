using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UrbanReports.Api.Geocoding;
using UrbanReports.Api.Tests.Infra;

namespace UrbanReports.Api.Tests.Geocoding;

public sealed class GeocodingServiceTests : IDisposable
{
    private readonly FakeNominatim _nominatim = new();
    private readonly GeocodingService _service;

    public GeocodingServiceTests() => _service = Create(_nominatim);

    public void Dispose() => _service.Dispose();

    private static GeocodingService Create(FakeNominatim nominatim, int cacheLimit = 50_000) =>
        new(
            nominatim,
            Options.Create(new NominatimOptions { Email = "contato@example.com", CacheEntryLimit = cacheLimit }),
            NullLogger<GeocodingService>.Instance);

    [Fact]
    public async Task Two_calls_to_the_same_point_make_a_single_request()
    {
        var first = await _service.ReverseAsync(-22.9521, -43.1868, CancellationToken.None);
        var second = await _service.ReverseAsync(-22.9521, -43.1868, CancellationToken.None);

        _nominatim.Calls.Should().Be(1);
        second.Should().Be(first);
    }
}
