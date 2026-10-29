using UrbanReports.Api.Common;

namespace UrbanReports.Api.Trust.Engine;

public sealed record IssueState(
    IssueStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset StatusChangedAt,
    DateTimeOffset? ResolutionConditionSince);
