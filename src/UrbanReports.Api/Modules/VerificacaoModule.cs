using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using UrbanReports.Api.Common;
using UrbanReports.Api.Verification;

namespace UrbanReports.Api.Modules;

public sealed class VerificationModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapPost("/verifications", Request)
            .Validate<RequestVerificationRequest>()
            .WithTags("Verification");
    }

    private static async Task<Results<Created<RequestVerificationResponse>, ProblemHttpResult, UnauthorizedHttpResult>> Request(
        RequestVerificationRequest request, ClaimsPrincipal principal, IVerificationService service, CancellationToken ct)
    {
        var r = await service.RequestAsync(principal.GetUserId(), request, ct);
        return r.Error switch
        {
            null => TypedResults.Created("/api/v1/me", r.Value!),
            { Kind: ErrorType.Unauthorized } e => e.ToUnauthorized(),
            var e => e.ToProblem(),
        };
    }
}
