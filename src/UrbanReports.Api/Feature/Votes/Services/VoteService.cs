using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Common;
using UrbanReports.Api.Trust;
using UrbanReports.Api.Persistence;
using UrbanReports.Api.Issues;
using UrbanReports.Api.Trust.Engine;

namespace UrbanReports.Api.Votes;

public sealed class VoteService(
    AppDbContext db, IIssueService issues, TimeProvider clock, TrustConfiguration cfg) : IVoteService
{
    public async Task<Result<GetIssueDetailResponse>> VoteAsync(
        Guid issueId, Guid userId, VoteRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var issue = await IssueRecalculation.LoadWithLockAsync(db, issueId, ct);
        if (issue is null)
        {
            return ApplicationError.NotFound;
        }

        if (issue.Status == IssueStatus.Resolved)
        {
            return VoteErrors.IssueResolved;
        }

        if (issue.AuthorId == userId)
        {
            return VoteErrors.AuthorCannotVote;
        }

        var role = await db.Users.Where(u => u.Id == userId).Select(u => (Role?)u.Role).SingleOrDefaultAsync(ct);
        if (role is null)
        {
            return ApplicationError.Unauthorized;
        }

        var distance = await CalculateDistanceAsync(issueId, request.Lat, request.Lng, ct);
        var now = clock.GetUtcNow();

        var votes = await db.Votes.Where(v => v.IssueId == issueId).ToListAsync(ct);
        var vote = votes.SingleOrDefault(v => v.UserId == userId);
        if (vote is null)
        {
            vote = new Vote { Id = Guid.CreateVersion7(), IssueId = issueId, UserId = userId };
            db.Votes.Add(vote);
            votes.Add(vote);
        }

        vote.Value = (VoteValue)request.Value;
        vote.RoleAtVote = role.Value;
        vote.DistanceMeters = distance;
        vote.RegisteredAt = now;

        IssueRecalculation.Apply(issue, votes, now, cfg);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return (await issues.QueryDetailAsync(issueId, userId, now, ct))!;
    }

    private async Task<int?> CalculateDistanceAsync(Guid issueId, double? lat, double? lng, CancellationToken ct)
    {
        if (lat is not { } latitude || lng is not { } longitude)
        {
            return null;
        }

        var point = Geo.ToPoint(latitude, longitude);
        var meters = await db.Issues
            .Where(issue => issue.Id == issueId)
            .Select(issue => issue.Location.Distance(point))
            .SingleAsync(ct);

        return (int)Math.Round(meters, MidpointRounding.AwayFromZero);
    }
}
