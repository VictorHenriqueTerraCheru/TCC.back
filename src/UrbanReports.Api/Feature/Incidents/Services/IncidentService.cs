using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Common;
using UrbanReports.Api.Geocoding;
using UrbanReports.Api.Persistence;

namespace UrbanReports.Api.Incidents;

public sealed class IncidentService(AppDbContext db, IGeocodingService geocoding, TimeProvider clock) : IIncidentService
{
    public async Task<Result<CreatedIncident>> CreateAsync(CreateIncidentRequest request, Guid authorId, CancellationToken ct)
    {
        var category = await db.Categories.FindAsync([request.CategoryId], ct);
        if (category is not { Active: true })
        {
            return IncidentErrors.MissingCategory;
        }

        if (category.Type != ReportType.Incident)
        {
            return IncidentErrors.CategoryNotIncident;
        }

        var now = clock.GetUtcNow();
        if (request.OccurredAt < now - IncidentLimits.Window || request.OccurredAt > now)
        {
            return IncidentErrors.TimeOutsideWindow;
        }

        var since = now - IncidentLimits.Window;

        if (await CountRecentAsync(authorId, since, ct) >= IncidentLimits.MaxPer24h)
        {
            return IncidentErrors.DailyIncidentLimit;
        }

        var lat = Round(request.Lat);
        var lng = Round(request.Lng);
        var address = await geocoding.ReverseAsync(lat, lng, ct);

        db.Incidents.Add(new Incident
        {
            Id = Guid.CreateVersion7(),
            CategoryId = category.Id,
            AuthorId = authorId,
            ApproximateArea = Geo.ToPoint(lat, lng),
            RadiusMeters = IncidentLimits.RadiusMeters,
            AreaLabel = address.AreaLabel,
            OccurredAt = request.OccurredAt.ToUniversalTime(),
            RegisteredAt = now,
        });
        await db.SaveChangesAsync(ct);

        return new CreatedIncident(lat, lng, await QueryAreaAsync(lat, lng, now, ct));
    }

    public async Task<List<GetIncidentHeatmapResponse>> GetHeatmapAsync(GetIncidentHeatmapRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var since = now.AddDays(-request.Days);
        var groups = Lists.Split(request.Groups);

        return await db.Database
            .SqlQuery<GetIncidentHeatmapResponse>($"""
                SELECT ST_Y(i.approximate_area::geometry) AS lat,
                       ST_X(i.approximate_area::geometry) AS lng,
                       count(*)::int AS count
                FROM incidents i
                JOIN categories c ON c.id = i.category_id
                WHERE i.occurred_at >= {since} AND i.occurred_at <= {now}
                  AND i.approximate_area::geometry && ST_MakeEnvelope({request.MinLng}, {request.MinLat}, {request.MaxLng}, {request.MaxLat}, 4326)
                  AND (cardinality({groups}) = 0 OR c."group" = ANY({groups}))
                GROUP BY 1, 2
                ORDER BY count DESC
                LIMIT {IncidentLimits.MaxHeatmapCells}
                """)
            .ToListAsync(ct);
    }

    public Task<GetIncidentAreaResponse> GetAreaAsync(GetIncidentAreaRequest request, CancellationToken ct) =>
        QueryAreaAsync(request.Lat, request.Lng, clock.GetUtcNow(), ct);

    public static ConcentrationLevel Level(int total7Days) =>
        total7Days >= IncidentLimits.HighThreshold ? ConcentrationLevel.High
        : total7Days >= IncidentLimits.MediumThreshold ? ConcentrationLevel.Medium
        : ConcentrationLevel.Low;

    public static double Round(double coordinate) =>
        Math.Round(coordinate, IncidentLimits.DecimalPlaces, MidpointRounding.AwayFromZero);

    private async Task<GetIncidentAreaResponse> QueryAreaAsync(double lat, double lng, DateTimeOffset now, CancellationToken ct)
    {
        var point = Geo.ToPoint(lat, lng);
        var since7 = now.AddDays(-7);
        var since30 = now.AddDays(-30);

        var byCategory = await db.Incidents
            .Where(i => i.OccurredAt >= since30 && i.OccurredAt <= now && i.ApproximateArea.IsWithinDistance(point, IncidentLimits.AreaRadiusMeters))
            .GroupBy(i => i.CategoryId)
            .Select(g => new { CategoryId = g.Key, Days7 = g.Count(i => i.OccurredAt >= since7), Days30 = g.Count() })
            .ToDictionaryAsync(g => g.CategoryId, ct);

        var categories = await db.Categories
            .Where(c => c.Type == ReportType.Incident && c.Active)
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(ct);

        var counts = categories
            .Select(c => byCategory.TryGetValue(c.Id, out var n)
                ? new GetIncidentAreaCount(c.Id, c.Name, n.Days7, n.Days30)
                : new GetIncidentAreaCount(c.Id, c.Name, 0, 0))
            .ToList();

        return new GetIncidentAreaResponse(IncidentLimits.AreaRadiusMeters, now, Level(counts.Sum(c => c.Days7)), counts);
    }

    private Task<int> CountRecentAsync(Guid authorId, DateTimeOffset since, CancellationToken ct) =>
        db.Incidents.CountAsync(i => i.AuthorId == authorId && i.RegisteredAt > since, ct);
}
