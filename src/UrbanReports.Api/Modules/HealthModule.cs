using Microsoft.AspNetCore.Http.HttpResults;
using UrbanReports.Api.Common;
using UrbanReports.Api.Health;

namespace UrbanReports.Api.Modules;

public sealed class HealthModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/health", Application)
            .AllowAnonymous()
            .WithTags("Health");

        api.MapGet("/health/database", Database)
            .AllowAnonymous()
            .WithTags("Health")
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static Ok<HealthStatusResponse> Application() => TypedResults.Ok(HealthStatusResponse.Healthy);

    private static async Task<Results<Ok<HealthStatusResponse>, ProblemHttpResult>> Database(IHealthService service, CancellationToken ct)
    {
        var r = await service.VerifyDatabaseAsync(ct);
        return r.Error switch
        {
            null => TypedResults.Ok(r.Value!),
            var e => e.ToProblem(),
        };
    }
}
