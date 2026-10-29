using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Persistence;
using UrbanReports.Api.Common;
using UrbanReports.Api.Trust.Engine;

namespace UrbanReports.Api.Trust;

public sealed class LifecycleProcessor(
    AppDbContext db, TimeProvider clock, TrustConfiguration cfg, ILogger<LifecycleProcessor> logger)
{
    public async Task<int> ProcessAsync(CancellationToken ct)
    {
        var ids = await db.Issues
            .Where(p => p.Status != IssueStatus.Resolved)
            .Select(p => p.Id)
            .ToListAsync(ct);

        var transitions = 0;
        foreach (var id in ids)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var issue = await IssueRecalculation.LoadWithLockAsync(db, id, ct);
            if (issue is null || issue.Status == IssueStatus.Resolved)
            {
                continue;
            }

            var votes = await db.Votes.Where(v => v.IssueId == id).AsNoTracking().ToListAsync(ct);
            var previousStatus = issue.Status;
            IssueRecalculation.Apply(issue, votes, clock.GetUtcNow(), cfg);

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            db.ChangeTracker.Clear();

            if (issue.Status != previousStatus)
            {
                transitions++;
                logger.LogInformation("Problema {ProblemaId}: {De} → {Para}.", id, previousStatus, issue.Status);
            }
        }

        return transitions;
    }
}
