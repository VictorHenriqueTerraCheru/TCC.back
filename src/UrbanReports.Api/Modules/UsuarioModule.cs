using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using UrbanReports.Api.Common;
using UrbanReports.Api.Users;

namespace UrbanReports.Api.Modules;

public sealed class UserModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/me", GetMe).WithTags("Users");

        api.MapPatch("/me", UpdateMe)
            .Validate<UpdateMeRequest>()
            .WithTags("Users");

        api.MapGet("/me/history", GetHistory)
            .Validate<GetHistoryRequest>()
            .WithTags("Users");
    }

    private static async Task<Results<Ok<GetMeResponse>, UnauthorizedHttpResult>> GetMe(
        ClaimsPrincipal principal, IUserService service, CancellationToken ct)
    {
        var r = await service.GetMeAsync(principal.GetUserId(), ct);
        return r.Error switch
        {
            null => TypedResults.Ok(r.Value!),
            var e => e.ToUnauthorized(),
        };
    }

    private static async Task<Results<Ok<GetMeResponse>, UnauthorizedHttpResult>> UpdateMe(
        UpdateMeRequest request, ClaimsPrincipal principal, IUserService service, CancellationToken ct)
    {
        var r = await service.UpdateMeAsync(principal.GetUserId(), request, ct);
        return r.Error switch
        {
            null => TypedResults.Ok(r.Value!),
            var e => e.ToUnauthorized(),
        };
    }

    private static Task<Paged<GetHistoryItem>> GetHistory(
        [AsParameters] GetHistoryRequest request, ClaimsPrincipal principal, IUserService service, CancellationToken ct) =>
        service.GetHistoryAsync(principal.GetUserId(), request, ct);
}
