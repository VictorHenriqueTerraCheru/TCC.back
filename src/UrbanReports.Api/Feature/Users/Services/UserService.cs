using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Categories;
using UrbanReports.Api.Common;
using UrbanReports.Api.Persistence;

namespace UrbanReports.Api.Users;

public sealed class UserService(AppDbContext db) : IUserService
{
    public async Task<Result<GetMeResponse>> GetMeAsync(Guid userId, CancellationToken ct)
    {
        var response = await QueryMeAsync(userId, ct);
        if (response is null)
        {
            return ApplicationError.Unauthorized;
        }

        return response;
    }

    public async Task<Result<GetMeResponse>> UpdateMeAsync(Guid userId, UpdateMeRequest request, CancellationToken ct)
    {
        var changed = await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Name, request.Name.Trim()), ct);

        if (changed == 0)
        {
            return ApplicationError.Unauthorized;
        }

        return (await QueryMeAsync(userId, ct))!;
    }

    public async Task<Paged<GetHistoryItem>> GetHistoryAsync(Guid userId, GetHistoryRequest request, CancellationToken ct)
    {
        var reports = request.Filter is "all" or "reports";
        var votes = request.Filter is "all" or "votes";

        var union = db.Database.SqlQuery<HistoryRow>($"""
            SELECT p.id AS key, 1 AS type, p.id AS report_id, c.id AS category_id, c.name AS category_name, c."group", c.icon,
                   p.approximate_address AS address, NULL::smallint AS value, p.status AS issue_status, p.created_at AS date
            FROM issues p JOIN categories c ON c.id = p.category_id
            WHERE p.author_id = {userId} AND {reports}
            UNION ALL
            SELECT i.id, 2, NULL::uuid, c.id, c.name, c."group", c.icon,
                   i.area_label, NULL::smallint, NULL::smallint, i.registered_at
            FROM incidents i JOIN categories c ON c.id = i.category_id
            WHERE i.author_id = {userId} AND {reports}
            UNION ALL
            SELECT v.id, 3, p.id, c.id, c.name, c."group", c.icon,
                   p.approximate_address, v.value, p.status, v.registered_at
            FROM votes v JOIN issues p ON p.id = v.issue_id JOIN categories c ON c.id = p.category_id
            WHERE v.user_id = {userId} AND {votes}
            """);

        var total = await union.CountAsync(ct);
        var rows = await union
            .OrderByDescending(row => row.Date)
            .ThenBy(row => row.Key)
            .Skip(Pagination.Skip(request.Page, request.PageSize))
            .Take(request.PageSize)
            .ToListAsync(ct);

        return new Paged<GetHistoryItem>(rows.Select(Map).ToList(), request.Page, request.PageSize, total);
    }

    private Task<GetMeResponse?> QueryMeAsync(Guid userId, CancellationToken ct) =>
        db.Users
            .Where(u => u.Id == userId)
            .Select(u => new GetMeResponse(
                u.Id,
                u.Name,
                u.Email!,
                u.Role,
                u.CreatedAt,
                u.VerifiedAt,
                db.Issues.Count(issue => issue.AuthorId == u.Id) + db.Incidents.Count(i => i.AuthorId == u.Id),
                db.Votes.Count(v => v.UserId == u.Id),
                db.VerificationRequests
                    .Where(s => s.UserId == u.Id)
                    .OrderByDescending(s => s.CreatedAt)
                    .Select(s => new GetMeVerificationSummary(s.Status, s.AffiliationType, s.RejectionReason, s.CreatedAt))
                    .FirstOrDefault()))
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

    private static GetHistoryItem Map(HistoryRow row)
    {
        var type = (GetHistoryItemType)row.Type;
        GetHistoryItemValue? value = (VoteValue?)row.Value switch
        {
            VoteValue.Positive => GetHistoryItemValue.Yes,
            VoteValue.Negative => GetHistoryItemValue.No,
            _ => null,
        };

        return new GetHistoryItem(
            type,
            row.ReportId,
            new CategorySummary(row.CategoryId, row.CategoryName, row.Group, row.Icon),
            row.Address,
            value,
            (IssueStatus?)row.IssueStatus,
            row.Date);
    }
}
