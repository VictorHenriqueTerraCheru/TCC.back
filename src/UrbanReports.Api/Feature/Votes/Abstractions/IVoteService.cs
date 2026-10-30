using UrbanReports.Api.Common;
using UrbanReports.Api.Issues;

namespace UrbanReports.Api.Votes;

public interface IVoteService
{
    Task<Result<GetIssueDetailResponse>> VoteAsync(
        Guid issueId, Guid userId, VoteRequest request, CancellationToken ct);
}
