using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Persistence;
using UrbanReports.Api.Trust.Engine;

namespace UrbanReports.Api.Trust;

public static class IssueRecalculation
{
    public static async Task<Issue?> LoadWithLockAsync(AppDbContext db, Guid issueId, CancellationToken ct)
    {
        var issues = await db.Issues
            .FromSql($"SELECT * FROM issues WHERE id = {issueId} FOR UPDATE")
            .ToListAsync(ct);
        return issues.SingleOrDefault();
    }

    public static void Apply(Issue issue, IEnumerable<Vote> votes, DateTimeOffset now, TrustConfiguration cfg)
    {
        var facts = votes
            .Select(v => new VoteFact(v.UserId, v.Value, v.RoleAtVote, v.DistanceMeters, v.RegisteredAt))
            .ToList();

        var result = TrustEngine.CalculateTrust(facts, now, cfg);
        var state = TrustEngine.EvaluateLifecycle(
            new IssueState(issue.Status, issue.CreatedAt, issue.StatusChangedAt, issue.ResolutionConditionSince),
            facts,
            now,
            cfg);

        issue.TrustScore = Math.Round((decimal)result.Score, 2, MidpointRounding.AwayFromZero);
        issue.Status = state.Status;
        issue.StatusChangedAt = state.StatusChangedAt;
        issue.ResolutionConditionSince = state.ResolutionConditionSince;
    }
}
