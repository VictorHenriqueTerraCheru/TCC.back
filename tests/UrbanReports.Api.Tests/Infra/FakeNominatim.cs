using System.Collections.Concurrent;
using UrbanReports.Api.Geocoding;

namespace UrbanReports.Api.Tests.Infra;

public sealed class FakeNominatim : INominatimClient
{
    private readonly ConcurrentDictionary<(double, double), NominatimResponse?> _responses = new();
    private readonly ConcurrentDictionary<(double, double), bool> _offline = new();
    private int _calls;

    public int Calls => _calls;

    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public static NominatimResponse Address(
        string? street = "Rua Voluntários da Pátria", string? number = "170", string? neighborhood = "Botafogo",
        string? city = "Rio de Janeiro", string? iso = "BR-RJ") =>
        new(new NominatimResponse.AddressParts { Road = street, HouseNumber = number, Suburb = neighborhood, City = city, Iso3166Level4 = iso }, null);

    public void Respond(double lat, double lng, NominatimResponse? response) => _responses[Key(lat, lng)] = response;

    public void SetOffline(double lat, double lng, bool offline = true) => _offline[Key(lat, lng)] = offline;

    public async Task<NominatimResponse?> ReverseAsync(double lat, double lng, CancellationToken ct)
    {
        Interlocked.Increment(ref _calls);
        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, ct);
        }

        if (_offline.GetValueOrDefault(Key(lat, lng)))
        {
            throw new HttpRequestException("Nominatim fora do ar (simulado).");
        }

        return _responses.TryGetValue(Key(lat, lng), out var response) ? response : Address();
    }

    private static (double, double) Key(double lat, double lng) =>
        (Math.Round(lat, 4, MidpointRounding.AwayFromZero), Math.Round(lng, 4, MidpointRounding.AwayFromZero));
}
