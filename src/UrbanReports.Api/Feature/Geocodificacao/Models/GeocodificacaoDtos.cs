namespace UrbanReports.Api.Geocoding;

public sealed record ReverseRequest(double Lat, double Lng);

public sealed record ReverseResponse(
    bool Available,
    string Address,
    string? Street,
    string? Number,
    string? Neighborhood,
    string? City,
    string? State,
    string? AreaLabel);
