using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Common;
using UrbanReports.Api.Persistence;

namespace UrbanReports.Api.Admin;

public sealed class AdminService(AppDbContext db, TimeProvider clock) : IAdminService
{
    public async Task<Paged<ListVerificationsItem>> ListVerificationsAsync(ListVerificationsRequest request, CancellationToken ct)
    {
        var query = db.VerificationRequests.AsQueryable();
        VerificationStatus? status = request.Status is null ? null : Enum.Parse<VerificationStatus>(request.Status, ignoreCase: true);
        if (status is not null)
        {
            query = query.Where(s => s.Status == status);
        }

        var total = await query.CountAsync(ct);
        var sorted = status == VerificationStatus.Pending
            ? query.OrderBy(s => s.CreatedAt)
            : query.OrderByDescending(s => s.CreatedAt);

        var rows = await sorted
            .ThenBy(s => s.Id)
            .Skip(Pagination.Skip(request.Page, request.PageSize))
            .Take(request.PageSize)
            .Select(s => new
            {
                s.Id,
                s.UserId,
                s.User.Name,
                s.AffiliationType,
                s.Justification,
                s.Status,
                s.CreatedAt,
                s.RejectionReason,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var items = rows
            .Select(row => new ListVerificationsItem(
                row.Id, new ListVerificationsUserSummary(row.UserId, row.Name, Initials(row.Name)),
                row.AffiliationType, row.Justification, row.Status, row.CreatedAt, row.RejectionReason))
            .ToList();

        return new Paged<ListVerificationsItem>(items, request.Page, request.PageSize, total);
    }

    public async Task<Result<Empty>> ApproveAsync(Guid id, Guid adminId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var completion = await FinishAsync(id, adminId, VerificationStatus.Approved, null, now, ct);
        if (completion.Error is { } error)
        {
            return error;
        }

        await db.Users
            .Where(u => u.Id == completion.Value && u.Role == Role.Citizen)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Role, Role.VerifiedCitizen)
                .SetProperty(u => u.VerifiedAt, now), ct);

        await transaction.CommitAsync(ct);
        return new Empty();
    }

    public async Task<Result<Empty>> RejectAsync(Guid id, Guid adminId, RejectVerificationRequest request, CancellationToken ct)
    {
        var completion = await FinishAsync(id, adminId, VerificationStatus.Rejected, request.Reason.Trim(), clock.GetUtcNow(), ct);
        if (completion.Error is { } error)
        {
            return error;
        }

        return new Empty();
    }

    public async Task<Paged<ListUsersItem>> ListUsersAsync(ListUsersRequest request, CancellationToken ct)
    {
        var query = db.Users.AsQueryable();
        if (request.Role is not null)
        {
            var role = Enum.Parse<Role>(request.Role, ignoreCase: true);
            query = query.Where(u => u.Role == role);
        }

        if (request.Search?.Trim() is { Length: > 0 } search)
        {
            var pattern = $"%{search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            query = query.Where(u => EF.Functions.ILike(u.Name, pattern) || EF.Functions.ILike(u.Email!, pattern));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .ThenBy(u => u.Id)
            .Skip(Pagination.Skip(request.Page, request.PageSize))
            .Take(request.PageSize)
            .Select(u => new ListUsersItem(u.Id, u.Name, u.Email!, u.Role, u.CreatedAt, u.VerifiedAt))
            .AsNoTracking()
            .ToListAsync(ct);

        return new Paged<ListUsersItem>(items, request.Page, request.PageSize, total);
    }

    public async Task<Result<Empty>> ChangeRoleAsync(Guid id, Guid adminId, ChangeRoleRequest request, CancellationToken ct)
    {
        if (id == adminId)
        {
            return AdminErrors.OwnRole;
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return ApplicationError.NotFound;
        }

        user.Role = request.Role;
        switch (request.Role)
        {
            case Role.VerifiedCitizen:
                user.VerifiedAt ??= clock.GetUtcNow();
                break;
            case Role.Citizen:
                user.VerifiedAt = null;
                break;
        }

        await db.SaveChangesAsync(ct);
        return new Empty();
    }

    public static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => "",
            1 => char.ToUpperInvariant(parts[0][0]).ToString(),
            _ => string.Concat(char.ToUpperInvariant(parts[0][0]), char.ToUpperInvariant(parts[^1][0])),
        };
    }

    private async Task<Result<Guid>> FinishAsync(
        Guid id, Guid adminId, VerificationStatus result, string? reason, DateTimeOffset now, CancellationToken ct)
    {
        var changed = await db.VerificationRequests
            .Where(s => s.Id == id && s.Status == VerificationStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, result)
                .SetProperty(x => x.RejectionReason, reason)
                .SetProperty(x => x.ReviewedById, adminId)
                .SetProperty(x => x.ReviewedAt, now), ct);

        if (changed == 1)
        {
            return await db.VerificationRequests.Where(s => s.Id == id).Select(s => s.UserId).SingleAsync(ct);
        }

        if (await db.VerificationRequests.AnyAsync(s => s.Id == id, ct))
        {
            return AdminErrors.VerificationAlreadyReviewed;
        }

        return ApplicationError.NotFound;
    }
}
