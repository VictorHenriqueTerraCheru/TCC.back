using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using UrbanReports.Api.Common;
using UrbanReports.Api.Issues;
using UrbanReports.Api.Votes;

namespace UrbanReports.Api.Modules;

public sealed class VoteModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapPut("/issues/{id:guid}/vote", Vote)
            .Validate<VoteRequest>()
            .WithTags("Votes");
    }

    private static async Task<Results<Ok<GetIssueDetailResponse>, NotFound, ProblemHttpResult, UnauthorizedHttpResult>> Vote(
        Guid id, VoteRequest request, ClaimsPrincipal principal, IVoteService service, CancellationToken ct) =>
        ToHttp(await service.VoteAsync(id, principal.GetUserId(), request, ct));

    private static Results<Ok<GetIssueDetailResponse>, NotFound, ProblemHttpResult, UnauthorizedHttpResult> ToHttp(
        Result<GetIssueDetailResponse> r) =>
        r.Error switch
        {
            null => TypedResults.Ok(r.Value!),
            { Kind: ErrorType.NotFound } e => e.ToNotFound(),
            { Kind: ErrorType.Unauthorized } e => e.ToUnauthorized(),
            var e => e.ToProblem(),
        };
}
