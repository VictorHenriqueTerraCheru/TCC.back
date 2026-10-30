using UrbanReports.Api.Common;

namespace UrbanReports.Api.Admin;

public sealed record ListVerificationsRequest(string? Status = null, int Page = 1, int PageSize = Pagination.DefaultPageSize);

public sealed record ListVerificationsItem(
    Guid Id,
    ListVerificationsUserSummary User,
    AffiliationType AffiliationType,
    string Justification,
    VerificationStatus Status,
    DateTimeOffset CreatedAt,
    string? RejectionReason);

public sealed record ListVerificationsUserSummary(Guid Id, string Name, string Initials);

public sealed record RejectVerificationRequest(string Reason);

public sealed record ListUsersRequest(string? Search = null, string? Role = null, int Page = 1, int PageSize = Pagination.DefaultPageSize);

public sealed record ListUsersItem(
    Guid Id,
    string Name,
    string Email,
    Role Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset? VerifiedAt);

public sealed record ChangeRoleRequest(Role Role);
