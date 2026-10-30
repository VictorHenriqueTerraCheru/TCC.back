using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Categories;
using UrbanReports.Api.Common;
using UrbanReports.Api.Votes;
using UrbanReports.Api.Geocoding;
using UrbanReports.Api.Persistence;
using UrbanReports.Api.Trust.Engine;

namespace UrbanReports.Api.Issues;

public sealed class IssueService(
    AppDbContext db, IGeocodingService geocoding, TimeProvider clock, TrustConfiguration cfg) : IIssueService
{
    public async Task<Result<GetIssueDetailResponse>> CreateAsync(CreateIssueRequest request, Guid authorId, CancellationToken ct)
    {
        var category = await db.Categories.FindAsync([request.CategoryId], ct);
        if (category is not { Active: true })
        {
            return IssueErrors.MissingCategory;
        }

        if (category.Type != ReportType.Issue)
        {
            return IssueErrors.CategoryNotIssue;
        }

        var address = await geocoding.ReverseAsync(request.Lat, request.Lng, ct);
        var now = clock.GetUtcNow();

        var issue = new Issue
        {
            Id = Guid.CreateVersion7(),
            CategoryId = category.Id,
            AuthorId = authorId,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Location = Geo.ToPoint(request.Lat, request.Lng),
            ApproximateAddress = address.Address,
            Status = IssueStatus.New,
            CreatedAt = now,
            StatusChangedAt = now,
        };
        db.Issues.Add(issue);
        await db.SaveChangesAsync(ct);

        return (await QueryDetailAsync(issue.Id, authorId, now, ct))!;
    }

    public async Task<Result<GetIssueDetailResponse>> GetDetailAsync(Guid id, Guid? userId, CancellationToken ct)
    {
        var detail = await QueryDetailAsync(id, userId, clock.GetUtcNow(), ct);
        if (detail is null)
        {
            return ApplicationError.NotFound;
        }

        return detail;
    }

    public async Task<GetIssueDetailResponse?> QueryDetailAsync(Guid id, Guid? userId, DateTimeOffset now, CancellationToken ct)
    {
        var since24h = now.AddHours(-24);

        var row = await db.Issues
            .Where(issue => issue.Id == id)
            .Select(issue => new
            {
                issue.Id,
                Category = new CategorySummary(issue.Category.Id, issue.Category.Name, issue.Category.Group, issue.Category.Icon),
                issue.Description,
                issue.Location,
                issue.ApproximateAddress,
                issue.CreatedAt,
                issue.Status,
                issue.TrustScore,
                issue.AuthorId,
                Voters = issue.Votes.Select(v => v.UserId).Distinct().Count(),
                Yes = issue.Votes.Count(v => v.Value == VoteValue.Positive),
                No = issue.Votes.Count(v => v.Value == VoteValue.Negative),
                NoLast24h = issue.Votes.Count(v => v.Value == VoteValue.Negative && v.RegisteredAt >= since24h),
                MyVote = issue.Votes
                    .Where(v => v.UserId == userId)
                    .Select(v => (VoteValue?)v.Value)
                    .FirstOrDefault(),
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        var (trust, band) = TrustDisplay.Calculate(row.Status, row.TrustScore, cfg);

        return new GetIssueDetailResponse(
            row.Id,
            row.Category,
            row.Description,
            row.Location.Y,
            row.Location.X,
            row.ApproximateAddress,
            row.CreatedAt,
            row.Status,
            trust,
            band,
            row.Status == IssueStatus.New
                ? new GetIssueDetailEvaluation(row.Voters, cfg.VotersToLeaveNew, row.CreatedAt + cfg.MaxTimeInNew)
                : null,
            new GetIssueDetailVotes(row.Yes, row.No, row.NoLast24h),
            userId is null ? null : (VoteAnswer?)row.MyVote,
            userId is not null && row.AuthorId == userId);
    }

    public async Task<List<SearchDuplicatesResponse>> SearchDuplicatesAsync(SearchDuplicatesRequest request, CancellationToken ct)
    {
        var point = Geo.ToPoint(request.Lat, request.Lng);

        var rows = await db.Issues
            .Where(issue => issue.CategoryId == request.CategoryId
                && issue.Status != IssueStatus.Resolved
                && issue.Location.IsWithinDistance(point, IssueLimits.DuplicatesRadiusMeters))
            .Select(issue => new
            {
                issue.Id,
                Category = new CategorySummary(issue.Category.Id, issue.Category.Name, issue.Category.Group, issue.Category.Icon),
                issue.ApproximateAddress,
                issue.CreatedAt,
                Distance = issue.Location.Distance(point),
                issue.Status,
                issue.TrustScore,
                Confirmations = issue.Votes.Count(v => v.Value == VoteValue.Positive),
            })
            .OrderBy(issue => issue.Distance)
            .Take(IssueLimits.MaxDuplicates)
            .AsNoTracking()
            .ToListAsync(ct);

        return rows
            .Select(issue => new SearchDuplicatesResponse(
                issue.Id,
                issue.Category,
                issue.ApproximateAddress,
                issue.CreatedAt,
                (int)Math.Round(issue.Distance, MidpointRounding.AwayFromZero),
                TrustDisplay.Calculate(issue.Status, issue.TrustScore, cfg).Band,
                issue.Confirmations))
            .ToList();
    }

    public async Task<List<ListIssuesInAreaResponse>> ListInAreaAsync(ListIssuesInAreaRequest request, CancellationToken ct)
    {
        var groups = Lists.Split(request.Groups);
        var requestedStatuses = Lists.Split(request.Status).Select(s => Enum.Parse<IssueStatus>(s, ignoreCase: true)).ToArray();
        IssueStatus[] status = requestedStatuses.Length == 0
            ? [IssueStatus.New, IssueStatus.Active, IssueStatus.PossiblyResolved]
            : requestedStatuses.Where(s => s != IssueStatus.Resolved).Distinct().ToArray();
        if (status.Length == 0)
        {
            return [];
        }

        var query = db.Issues
            .FromSql($"""
                SELECT * FROM issues
                WHERE location::geometry && ST_MakeEnvelope({request.MinLng}, {request.MinLat}, {request.MaxLng}, {request.MaxLat}, 4326)
                """)
            .Where(issue => status.Contains(issue.Status));
        if (groups.Length > 0)
        {
            query = query.Where(issue => groups.Contains(issue.Category.Group));
        }

        var rows = await query
            .OrderByDescending(issue => issue.TrustScore.HasValue && issue.Status != IssueStatus.New)
            .ThenByDescending(issue => issue.TrustScore)
            .ThenByDescending(issue => issue.CreatedAt)
            .Take(IssueLimits.MaxInArea)
            .Select(issue => new { issue.Id, issue.CategoryId, issue.Location, issue.Status, issue.TrustScore })
            .AsNoTracking()
            .ToListAsync(ct);

        return rows
            .Select(issue =>
            {
                var (trust, band) = TrustDisplay.Calculate(issue.Status, issue.TrustScore, cfg);
                return new ListIssuesInAreaResponse(issue.Id, issue.CategoryId, issue.Location.Y, issue.Location.X, issue.Status, trust, band);
            })
            .ToList();
    }

    public async Task<Paged<ListRecentIssuesItem>> ListRecentAsync(ListRecentIssuesRequest request, CancellationToken ct)
    {
        var point = Geo.ToPoint(request.Lat, request.Lng);
        var groups = Lists.Split(request.Groups);

        var query = db.Issues.Where(issue =>
            issue.Status != IssueStatus.Resolved && issue.Location.IsWithinDistance(point, request.RadiusKm * 1000));
        if (groups.Length > 0)
        {
            query = query.Where(issue => groups.Contains(issue.Category.Group));
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(issue => issue.CreatedAt)
            .ThenBy(issue => issue.Id)
            .Skip(Pagination.Skip(request.Page, request.PageSize))
            .Take(request.PageSize)
            .Select(issue => new
            {
                issue.Id,
                Category = new CategorySummary(issue.Category.Id, issue.Category.Name, issue.Category.Group, issue.Category.Icon),
                issue.ApproximateAddress,
                issue.CreatedAt,
                issue.Status,
                issue.TrustScore,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var items = rows
            .Select(issue => new ListRecentIssuesItem(
                issue.Id, issue.Category, issue.ApproximateAddress, issue.CreatedAt, issue.Status,
                TrustDisplay.Calculate(issue.Status, issue.TrustScore, cfg).Band))
            .ToList();

        return new Paged<ListRecentIssuesItem>(items, request.Page, request.PageSize, total);
    }
}
