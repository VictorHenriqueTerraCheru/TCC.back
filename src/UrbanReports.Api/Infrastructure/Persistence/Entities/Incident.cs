using NetTopologySuite.Geometries;

namespace UrbanReports.Api.Persistence;

public sealed class Incident
{
    public Guid Id { get; set; }
    public short CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;

    public required Point ApproximateArea { get; set; }

    public short RadiusMeters { get; set; }
    public string? AreaLabel { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
}
