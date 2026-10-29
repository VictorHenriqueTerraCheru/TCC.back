using UrbanReports.Api.Categories;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Users;

public sealed record GetMeResponse(
    Guid Id,
    string Name,
    string Email,
    Role Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset? VerifiedAt,
    int TotalReports,
    int TotalVotes,
    GetMeVerificationSummary? Verification);

public sealed record GetMeVerificationSummary(
    VerificationStatus Status, AffiliationType AffiliationType, string? RejectionReason, DateTimeOffset CreatedAt);

public sealed record UpdateMeRequest(string Name);

public enum GetHistoryItemType
{
    IssueReport = 1,
    IncidentReport = 2,
    Vote = 3,
}

public enum GetHistoryItemValue
{
    Yes = 1,
    No = 2,
}

public sealed record GetHistoryRequest(string Filter = "all", int Page = 1, int PageSize = Pagination.DefaultPageSize);

public sealed record GetHistoryItem(
    GetHistoryItemType Type,
    Guid? ReportId,
    CategorySummary Category,
    string? Address,
    GetHistoryItemValue? Value,
    IssueStatus? IssueStatus,
    DateTimeOffset Date);
