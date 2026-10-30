using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace UrbanReports.Api.Geocoding;

public interface IGeocodingService
{
    Task<GeocodedAddress> ReverseAsync(double lat, double lng, CancellationToken ct);
}

public sealed class GeocodingService(
    INominatimClient nominatim,
    IOptions<NominatimOptions> options,
    ILogger<GeocodingService> logger) : IGeocodingService, IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = options.Value.CacheEntryLimit });
    private readonly ConcurrentDictionary<string, Lazy<Task<GeocodedAddress?>>> _inFlight = new();

    internal int CacheEntries => _cache.Count;

    public void Dispose() => _cache.Dispose();

    public async Task<GeocodedAddress> ReverseAsync(double lat, double lng, CancellationToken ct)
    {
        var latKey = Math.Round(lat, 4, MidpointRounding.AwayFromZero);
        var lngKey = Math.Round(lng, 4, MidpointRounding.AwayFromZero);
        var key = string.Create(CultureInfo.InvariantCulture, $"geo:{latKey:F4}:{lngKey:F4}");

        if (_cache.TryGetValue(key, out GeocodedAddress? cached))
        {
            return cached!;
        }

        var call = _inFlight.GetOrAdd(key, _ => new Lazy<Task<GeocodedAddress?>>(
            () => QueryAndCacheAsync(key, latKey, lngKey)));

        var result = await call.Value.WaitAsync(ct);
        return result ?? GeocodedAddress.Unavailable(lat, lng);
    }

    private async Task<GeocodedAddress?> QueryAndCacheAsync(string key, double lat, double lng)
    {
        try
        {
            if (_cache.TryGetValue(key, out GeocodedAddress? cached))
            {
                return cached;
            }

            var response = await nominatim.ReverseAsync(lat, lng, CancellationToken.None);
            var address = GeocodedAddress.Build(response, lat, lng);
            _cache.Set(key, address, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = options.Value.CacheDuration,
                Size = 1,
            });
            return address;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or TimeoutException or JsonException)
        {
            logger.LogWarning(e, "Nominatim indisponível; respondendo sem endereço.");
            return null;
        }
        finally
        {
            _inFlight.TryRemove(key, out _);
        }
    }
}
