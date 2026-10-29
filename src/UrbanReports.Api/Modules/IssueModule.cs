using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using UrbanReports.Api.Common;
using UrbanReports.Api.Issues;

namespace UrbanReports.Api.Modules;

public sealed class IssueModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/issues", ListInArea)
            .Validate<ListIssuesInAreaRequest>()
            .AllowAnonymous()
            .WithTags("Issues");

        api.MapGet("/issues/recent", ListRecent)
            .Validate<ListRecentIssuesRequest>()
            .AllowAnonymous()
            .WithTags("Issues");

        api.MapGet("/issues/duplicates", SearchDuplicates)
            .Validate<SearchDuplicatesRequest>()
            .WithTags("Issues");

        api.MapGet("/issues/{id:guid}", GetDetail)
            .AllowAnonymous()
            .WithTags("Issues");

        api.MapPost("/issues", Create)
            .Validate<CreateIssueRequest>()
            .WithTags("Issues");
    }

    private static Task<List<ListIssuesInAreaResponse>> ListInArea(
        [AsParameters] ListIssuesInAreaRequest request, IIssueService service, CancellationToken ct) =>
        service.ListInAreaAsync(request, ct);

    private static Task<Paged<ListRecentIssuesItem>> ListRecent(
        [AsParameters] ListRecentIssuesRequest request, IIssueService service, CancellationToken ct) =>
        service.ListRecentAsync(request, ct);

    private static Task<List<SearchDuplicatesResponse>> SearchDuplicates(
        [AsParameters] SearchDuplicatesRequest request, IIssueService service, CancellationToken ct) =>
        service.SearchDuplicatesAsync(request, ct);

    private static async Task<Results<Ok<GetIssueDetailResponse>, NotFound>> GetDetail(
        Guid id, ClaimsPrincipal principal, IIssueService service, CancellationToken ct)
    {
        var r = await service.GetDetailAsync(id, principal.GetUserIdOrNull(), ct);
        return r.Error switch
        {
            null => TypedResults.Ok(r.Value!),
            var e => e.ToNotFound(),
        };
    }

    private static async Task<Results<Created<GetIssueDetailResponse>, ValidationProblem, ProblemHttpResult>> Create(
        CreateIssueRequest request, ClaimsPrincipal principal, IIssueService service, CancellationToken ct)
    {
        var r = await service.CreateAsync(request, principal.GetUserId(), ct);
        return r.Error switch
        {
            null => TypedResults.Created($"/api/v1/issues/{r.Value!.Id}", r.Value),
            { Kind: ErrorType.Field } e => e.ToValidation(),
            var e => e.ToProblem(),
        };
    }
}
