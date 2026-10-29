using UrbanReports.Api.Common;

namespace UrbanReports.Api.Issues;

public interface IIssueService
{
    Task<Result<GetIssueDetailResponse>> CreateAsync(CreateIssueRequest request, Guid authorId, CancellationToken ct);

    Task<Result<GetIssueDetailResponse>> GetDetailAsync(Guid id, Guid? userId, CancellationToken ct);

    Task<GetIssueDetailResponse?> QueryDetailAsync(Guid id, Guid? userId, DateTimeOffset now, CancellationToken ct);

    Task<List<SearchDuplicatesResponse>> SearchDuplicatesAsync(SearchDuplicatesRequest request, CancellationToken ct);

    Task<List<ListIssuesInAreaResponse>> ListInAreaAsync(ListIssuesInAreaRequest request, CancellationToken ct);

    Task<Paged<ListRecentIssuesItem>> ListRecentAsync(ListRecentIssuesRequest request, CancellationToken ct);
}
