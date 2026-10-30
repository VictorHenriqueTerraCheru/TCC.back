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
    public async Task Cache_respects_the_entry_limit()
    {
        using var service = Create(new FakeNominatim(), cacheLimit: 2);

        for (var i = 0; i < 5; i++)
        {
            await service.ReverseAsync(-22.95 - i * 0.001, -43.18, CancellationToken.None);
        }

        service.CacheEntries.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task Two_calls_to_the_same_point_make_a_single_request()
    {
        var first = await _service.ReverseAsync(-22.9521, -43.1868, CancellationToken.None);
        var second = await _service.ReverseAsync(-22.9521, -43.1868, CancellationToken.None);

        _nominatim.Calls.Should().Be(1);
        second.Should().Be(first);
    }

    [Fact]
    public async Task Points_equal_at_4_places_share_the_cache()
    {
        await _service.ReverseAsync(-22.95211, -43.18681, CancellationToken.None);
        await _service.ReverseAsync(-22.95209, -43.18679, CancellationToken.None);

        _nominatim.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Points_different_at_4_places_query_again()
    {
        await _service.ReverseAsync(-22.9521, -43.1868, CancellationToken.None);
        await _service.ReverseAsync(-22.9522, -43.1868, CancellationToken.None);

        _nominatim.Calls.Should().Be(2);
    }

    [Fact]
    public async Task Nominatim_offline_returns_unavailable_and_does_not_cache()
    {
        _nominatim.SetOffline(-22.9521, -43.1868);

        var failure = await _service.ReverseAsync(-22.9521, -43.1868, CancellationToken.None);

        failure.Available.Should().BeFalse();
        failure.Address.Should().Be("Próximo a -22.95210, -43.18680");

        _nominatim.SetOffline(-22.9521, -43.1868, offline: false);
        var after = await _service.ReverseAsync(-22.9521, -43.1868, CancellationToken.None);

        after.Available.Should().BeTrue();
        _nominatim.Calls.Should().Be(2);
    }

    [Fact]
    public async Task Unavailable_uses_the_original_coordinate_in_text()
    {
        _nominatim.SetOffline(-22.95213, -43.18677);

        var e = await _service.ReverseAsync(-22.95213, -43.18677, CancellationToken.None);

        e.Address.Should().Be("Próximo a -22.95213, -43.18677");
    }

    [Fact]
    public async Task Concurrent_calls_to_the_same_point_share_the_request()
    {
        _nominatim.Delay = TimeSpan.FromMilliseconds(200);

        var results = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => _service.ReverseAsync(-22.9521, -43.1868, CancellationToken.None)));

        _nominatim.Calls.Should().Be(1);
        results.Should().AllSatisfy(r => r.Available.Should().BeTrue());
    }
}
