using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using UrbanReports.Api.Admin;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Modules;

public sealed class AdminModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/admin/verifications", ListVerifications)
            .Validate<ListVerificationsRequest>()
            .RequireAuthorization(AuthorizationPolicies.Administrator)
            .WithTags("Admin");

        api.MapPost("/admin/verifications/{id:guid}/approve", Approve)
            .RequireAuthorization(AuthorizationPolicies.Administrator)
            .WithTags("Admin");

        api.MapPost("/admin/verifications/{id:guid}/reject", Reject)
            .Validate<RejectVerificationRequest>()
            .RequireAuthorization(AuthorizationPolicies.Administrator)
            .WithTags("Admin");

        api.MapGet("/admin/users", ListUsers)
            .Validate<ListUsersRequest>()
            .RequireAuthorization(AuthorizationPolicies.Administrator)
            .WithTags("Admin");

        api.MapPatch("/admin/users/{id:guid}/role", ChangeRole)
            .Validate<ChangeRoleRequest>()
            .RequireAuthorization(AuthorizationPolicies.Administrator)
            .WithTags("Admin");
    }

    private static Task<Paged<ListVerificationsItem>> ListVerifications(
        [AsParameters] ListVerificationsRequest request, IAdminService service, CancellationToken ct) =>
        service.ListVerificationsAsync(request, ct);

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Approve(
        Guid id, ClaimsPrincipal principal, IAdminService service, CancellationToken ct) =>
        ToHttp(await service.ApproveAsync(id, principal.GetUserId(), ct));

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Reject(
        Guid id, RejectVerificationRequest request, ClaimsPrincipal principal, IAdminService service, CancellationToken ct) =>
        ToHttp(await service.RejectAsync(id, principal.GetUserId(), request, ct));

    private static Task<Paged<ListUsersItem>> ListUsers(
        [AsParameters] ListUsersRequest request, IAdminService service, CancellationToken ct) =>
        service.ListUsersAsync(request, ct);

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> ChangeRole(
        Guid id, ChangeRoleRequest request, ClaimsPrincipal principal, IAdminService service, CancellationToken ct) =>
        ToHttp(await service.ChangeRoleAsync(id, principal.GetUserId(), request, ct));

    private static Results<NoContent, NotFound, ProblemHttpResult> ToHttp(Result<Empty> r) =>
        r.Error switch
        {
            null => TypedResults.NoContent(),
            { Kind: ErrorType.NotFound } e => e.ToNotFound(),
            var e => e.ToProblem(),
        };
}
