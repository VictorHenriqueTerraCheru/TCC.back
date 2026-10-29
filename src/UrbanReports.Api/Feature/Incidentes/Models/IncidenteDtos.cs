using UrbanReports.Api.Common;

namespace UrbanReports.Api.Incidents;

public sealed record CreateIncidentRequest(short CategoryId, double Lat, double Lng, DateTimeOffset OccurredAt);

public sealed record GetIncidentHeatmapRequest(double MinLat, double MinLng, double MaxLat, double MaxLng, int Days = 7, string? Groups = null);

public sealed record GetIncidentHeatmapResponse(double Lat, double Lng, int Count);

public sealed record GetIncidentAreaRequest(double Lat, double Lng);

public sealed record GetIncidentAreaResponse(
    int RadiusMeters, DateTimeOffset UpdatedAt, ConcentrationLevel Level, IReadOnlyList<GetIncidentAreaCount> Counts);

public sealed record GetIncidentAreaCount(short CategoryId, string Name, int Days7, int Days30);

public sealed record CreatedIncident(double Lat, double Lng, GetIncidentAreaResponse Area);
