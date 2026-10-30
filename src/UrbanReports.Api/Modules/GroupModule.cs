using Microsoft.AspNetCore.Http.HttpResults;
using UrbanReports.Api.Common;
using UrbanReports.Api.Groups;

namespace UrbanReports.Api.Modules;

public sealed class GroupModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/groups", List)
            .AllowAnonymous()
            .WithTags("Categories");

        api.MapPost("/admin/groups", Create)
            .Validate<CreateGroupRequest>()
            .RequireAuthorization(AuthorizationPolicies.Administrator)
            .WithTags("Admin");

        api.MapPut("/admin/groups/{key}", Edit)
            .Validate<EditGroupRequest>()
            .RequireAuthorization(AuthorizationPolicies.Administrator)
            .WithTags("Admin");

        api.MapDelete("/admin/groups/{key}", Delete)
            .RequireAuthorization(AuthorizationPolicies.Administrator)
            .WithTags("Admin");
    }

    private static Task<List<GroupResponse>> List(IGroupService service, CancellationToken ct) =>
        service.ListAsync(ct);

    private static async Task<Results<Created<GroupResponse>, ProblemHttpResult>> Create(
        CreateGroupRequest request, IGroupService service, CancellationToken ct)
    {
        var r = await service.CreateAsync(request, ct);
        return r.Error switch
        {
            null => TypedResults.Created("/api/v1/groups", r.Value),
            var e => e.ToProblem(),
        };
    }

    private static async Task<Results<Ok<GroupResponse>, NotFound, ProblemHttpResult>> Edit(
        string key, EditGroupRequest request, IGroupService service, CancellationToken ct)
    {
        var r = await service.EditAsync(key, request, ct);
        return r.Error switch
        {
            null => TypedResults.Ok(r.Value),
            { Kind: ErrorType.NotFound } e => e.ToNotFound(),
            var e => e.ToProblem(),
        };
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Delete(
        string key, IGroupService service, CancellationToken ct)
    {
        var r = await service.DeleteAsync(key, ct);
        return r.Error switch
        {
            null => TypedResults.NoContent(),
            { Kind: ErrorType.NotFound } e => e.ToNotFound(),
            var e => e.ToProblem(),
        };
    }
}
