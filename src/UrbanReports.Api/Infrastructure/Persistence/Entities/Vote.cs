using UrbanReports.Api.Common;

namespace UrbanReports.Api.Persistence;

public sealed class Vote
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Issue Issue { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public VoteValue Value { get; set; }
    public Role RoleAtVote { get; set; }
    public int? DistanceMeters { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
}
