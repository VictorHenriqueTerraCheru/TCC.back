using UrbanReports.Api.Common;
using UrbanReports.Api.Trust.Engine;

namespace UrbanReports.Api.Tests.Trust.Engine;

internal static class Scenario
{
    public static readonly DateTimeOffset T0 = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    public static VoteFact Vote(
        VoteValue value,
        Role role = Role.Citizen,
        double? distance = 100,
        DateTimeOffset? at = null,
        Guid? user = null) =>
        new(user ?? Guid.NewGuid(), value, role, distance, at ?? T0);

    public static VoteFact Yes(Role role = Role.Citizen, double? distance = 100, DateTimeOffset? at = null, Guid? user = null) =>
        Vote(VoteValue.Positive, role, distance, at, user);

    public static VoteFact No(Role role = Role.Citizen, double? distance = 100, DateTimeOffset? at = null, Guid? user = null) =>
        Vote(VoteValue.Negative, role, distance, at, user);

    public static List<VoteFact> Repeat(int times, Func<VoteFact> create) =>
        Enumerable.Range(0, times).Select(_ => create()).ToList();

    public static IssueState State(
        IssueStatus status,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? statusChangedAt = null,
        DateTimeOffset? resolutionConditionSince = null) =>
        new(status, createdAt ?? T0, statusChangedAt ?? createdAt ?? T0, resolutionConditionSince);
}
