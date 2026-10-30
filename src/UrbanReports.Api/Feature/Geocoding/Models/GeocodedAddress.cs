using System.Globalization;

namespace UrbanReports.Api.Geocoding;

public sealed record GeocodedAddress(
    bool Available,
    string Address,
    string? Street,
    string? Number,
    string? Neighborhood,
    string? City,
    string? State,
    string? AreaLabel)
{
    public const int MaxAddressLength = 200;
    public const int MaxLabelLength = 120;

    public static GeocodedAddress Unavailable(double lat, double lng) =>
        new(false, CoordinatesText(lat, lng), null, null, null, null, null, null);

    public static GeocodedAddress Build(NominatimResponse? response, double lat, double lng)
    {
        var a = response?.Address;
        if (a is null)
        {
            return Unavailable(lat, lng);
        }

        var street = First(a.Road, a.Pedestrian);
        var number = First(a.HouseNumber);
        var neighborhood = First(a.Suburb, a.Neighbourhood, a.Quarter);
        var city = First(a.City, a.Town, a.Village, a.Municipality);
        var state = ExtractState(a.Iso3166Level4);

        var streetLine = Join(", ", street, street is null ? null : number);
        var address = Join(" — ", streetLine, neighborhood) ?? city;
        if (address is null)
        {
            return Unavailable(lat, lng);
        }

        var label = street is not null
            ? Join(", ", street, neighborhood)
            : Join(", ", neighborhood, city);

        return new GeocodedAddress(
            true,
            Truncate(address, MaxAddressLength),
            street,
            number,
            neighborhood,
            city,
            state,
            label is null ? null : Truncate(label, MaxLabelLength));
    }

    private static string CoordinatesText(double lat, double lng) =>
        string.Create(CultureInfo.InvariantCulture, $"Próximo a {lat:F5}, {lng:F5}");

    private static string? First(params string?[] values) =>
        values.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrEmpty(v));

    private static string? Join(string separator, params string?[] parts)
    {
        var present = parts.Where(p => !string.IsNullOrEmpty(p)).ToArray();
        return present.Length == 0 ? null : string.Join(separator, present);
    }

    private static string? ExtractState(string? iso) =>
        iso is { Length: > 3 } && iso.StartsWith("BR-", StringComparison.OrdinalIgnoreCase) ? iso[3..].ToUpperInvariant() : null;

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
