using UrbanReports.Api.Categories;
using UrbanReports.Api.Common;
using UrbanReports.Api.Votes;

namespace UrbanReports.Api.Issues;

public sealed record CreateIssueRequest(short CategoryId, double Lat, double Lng, string? Description);

public sealed record SearchDuplicatesRequest(short CategoryId, double Lat, double Lng);

public sealed record SearchDuplicatesResponse(
    Guid Id,
    CategorySummary Category,
    string Address,
    DateTimeOffset CreatedAt,
    int DistanceMeters,
    TrustBand? Band,
    int Confirmations);

public sealed record ListIssuesInAreaRequest(double MinLat, double MinLng, double MaxLat, double MaxLng, string? Groups, string? Status);

public sealed record ListIssuesInAreaResponse(
    Guid Id, short CategoryId, double Lat, double Lng, IssueStatus Status, double? TrustScore, TrustBand? Band);

public sealed record ListRecentIssuesRequest(
    double Lat,
    double Lng,
    double RadiusKm = 10,
    string? Groups = null,
    int Page = 1,
    int PageSize = Pagination.DefaultPageSize);

public sealed record ListRecentIssuesItem(
    Guid Id,
    CategorySummary Category,
    string Address,
    DateTimeOffset CreatedAt,
    IssueStatus Status,
    TrustBand? Band);

public sealed record GetIssueDetailResponse(
    Guid Id,
    CategorySummary Category,
    string? Description,
    double Lat,
    double Lng,
    string Address,
    DateTimeOffset CreatedAt,
    IssueStatus Status,
    double? TrustScore,
    TrustBand? Band,
    GetIssueDetailEvaluation? Evaluation,
    GetIssueDetailVotes Votes,
    VoteAnswer? MyVote,
    bool IsAuthor);

public sealed record GetIssueDetailEvaluation(int Voters, int Required, DateTimeOffset ClosesAt);

public sealed record GetIssueDetailVotes(int Yes, int No, int NoLast24h);
