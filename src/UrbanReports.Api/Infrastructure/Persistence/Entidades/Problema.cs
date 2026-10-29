using NetTopologySuite.Geometries;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Persistence;

public sealed class Issue
{
    public Guid Id { get; set; }
    public short CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public string? Description { get; set; }

    public required Point Location { get; set; }

    public required string ApproximateAddress { get; set; }
    public IssueStatus Status { get; set; } = IssueStatus.New;

    public decimal? TrustScore { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset StatusChangedAt { get; set; }
    public DateTimeOffset? ResolutionConditionSince { get; set; }
    public List<Vote> Votes { get; set; } = [];
}
