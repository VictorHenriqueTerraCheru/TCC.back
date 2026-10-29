using UrbanReports.Api.Common;

namespace UrbanReports.Api.Trust.Engine;

public sealed record VoteFact(
    Guid UserId,
    VoteValue Value,
    Role RoleAtVote,
    double? DistanceMeters,
    DateTimeOffset RegisteredAt);
