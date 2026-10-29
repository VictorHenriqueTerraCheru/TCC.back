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
        var limit24h = now.AddHours(-24);

        var row = await db.Issues
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id,
                Category = new CategorySummary(p.Category.Id, p.Category.Name, p.Category.Group, p.Category.Icon),
                p.Description,
                p.Location,
                p.ApproximateAddress,
                p.CreatedAt,
                p.Status,
                p.TrustScore,
                p.AuthorId,
                Voters = p.Votes.Select(v => v.UserId).Distinct().Count(),
                Yes = p.Votes.Count(v => v.Value == VoteValue.Positive),
                No = p.Votes.Count(v => v.Value == VoteValue.Negative),
                NoLast24h = p.Votes.Count(v => v.Value == VoteValue.Negative && v.RegisteredAt >= limit24h),
                MyVote = p.Votes
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
            .Where(p => p.CategoryId == request.CategoryId
                && p.Status != IssueStatus.Resolved
                && p.Location.IsWithinDistance(point, IssueLimits.DuplicatesRadiusMeters))
            .Select(p => new
            {
                p.Id,
                Category = new CategorySummary(p.Category.Id, p.Category.Name, p.Category.Group, p.Category.Icon),
                p.ApproximateAddress,
                p.CreatedAt,
                Distance = p.Location.Distance(point),
                p.Status,
                p.TrustScore,
                Confirmations = p.Votes.Count(v => v.Value == VoteValue.Positive),
            })
            .OrderBy(p => p.Distance)
            .Take(IssueLimits.MaxDuplicates)
            .AsNoTracking()
            .ToListAsync(ct);

        return rows
            .Select(l => new SearchDuplicatesResponse(
                l.Id,
                l.Category,
                l.ApproximateAddress,
                l.CreatedAt,
                (int)Math.Round(l.Distance, MidpointRounding.AwayFromZero),
                TrustDisplay.Calculate(l.Status, l.TrustScore, cfg).Band,
                l.Confirmations))
            .ToList();
    }

    public async Task<List<ListIssuesInAreaResponse>> ListInAreaAsync(ListIssuesInAreaRequest request, CancellationToken ct)
    {
        var groups = Lists.Split(request.Groups);
        var requests = Lists.Split(request.Status).Select(s => Enum.Parse<IssueStatus>(s, ignoreCase: true)).ToArray();
        IssueStatus[] status = requests.Length == 0
            ? [IssueStatus.New, IssueStatus.Active, IssueStatus.PossiblyResolved]
            : requests.Where(s => s != IssueStatus.Resolved).Distinct().ToArray();
        if (status.Length == 0)
        {
            return [];
        }

        var query = db.Issues
            .FromSql($"""
                SELECT * FROM issues
                WHERE location::geometry && ST_MakeEnvelope({request.MinLng}, {request.MinLat}, {request.MaxLng}, {request.MaxLat}, 4326)
                """)
            .Where(p => status.Contains(p.Status));
        if (groups.Length > 0)
        {
            query = query.Where(p => groups.Contains(p.Category.Group));
        }

        var rows = await query
            .OrderByDescending(p => p.TrustScore.HasValue && p.Status != IssueStatus.New)
            .ThenByDescending(p => p.TrustScore)
            .ThenByDescending(p => p.CreatedAt)
            .Take(IssueLimits.MaxInArea)
            .Select(p => new { p.Id, p.CategoryId, p.Location, p.Status, p.TrustScore })
            .AsNoTracking()
            .ToListAsync(ct);

        return rows
            .Select(l =>
            {
                var (trust, band) = TrustDisplay.Calculate(l.Status, l.TrustScore, cfg);
                return new ListIssuesInAreaResponse(l.Id, l.CategoryId, l.Location.Y, l.Location.X, l.Status, trust, band);
            })
            .ToList();
    }

    public async Task<Paged<ListRecentIssuesItem>> ListRecentAsync(ListRecentIssuesRequest request, CancellationToken ct)
    {
        var point = Geo.ToPoint(request.Lat, request.Lng);
        var groups = Lists.Split(request.Groups);

        var query = db.Issues.Where(p =>
            p.Status != IssueStatus.Resolved && p.Location.IsWithinDistance(point, request.RadiusKm * 1000));
        if (groups.Length > 0)
        {
            query = query.Where(p => groups.Contains(p.Category.Group));
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Skip(Pagination.Skip(request.Page, request.PageSize))
            .Take(request.PageSize)
            .Select(p => new
            {
                p.Id,
                Category = new CategorySummary(p.Category.Id, p.Category.Name, p.Category.Group, p.Category.Icon),
                p.ApproximateAddress,
                p.CreatedAt,
                p.Status,
                p.TrustScore,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var items = rows
            .Select(l => new ListRecentIssuesItem(
                l.Id, l.Category, l.ApproximateAddress, l.CreatedAt, l.Status,
                TrustDisplay.Calculate(l.Status, l.TrustScore, cfg).Band))
            .ToList();

        return new Paged<ListRecentIssuesItem>(items, request.Page, request.PageSize, total);
    }
}
