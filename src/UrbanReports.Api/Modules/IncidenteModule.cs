using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using UrbanReports.Api.Common;
using UrbanReports.Api.Incidents;

namespace UrbanReports.Api.Modules;

public sealed class IncidentModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapPost("/incidents", Create)
            .Validate<CreateIncidentRequest>()
            .WithTags("Incidents");

        api.MapGet("/incidents/heatmap", GetHeatmap)
            .Validate<GetIncidentHeatmapRequest>()
            .AllowAnonymous()
            .WithTags("Incidents");

        api.MapGet("/incidents/area", GetArea)
            .Validate<GetIncidentAreaRequest>()
            .AllowAnonymous()
            .WithTags("Incidents");
    }

    private static async Task<Results<Created<GetIncidentAreaResponse>, ValidationProblem, ProblemHttpResult>> Create(
        CreateIncidentRequest request, ClaimsPrincipal principal, IIncidentService service, CancellationToken ct)
    {
        var r = await service.CreateAsync(request, principal.GetUserId(), ct);
        return r.Error switch
        {
            null => TypedResults.Created(
                FormattableString.Invariant($"/api/v1/incidents/area?lat={r.Value!.Lat}&lng={r.Value.Lng}"), r.Value.Area),
            { Kind: ErrorType.Field } e => e.ToValidation(),
            var e => e.ToProblem(),
        };
    }

    private static Task<List<GetIncidentHeatmapResponse>> GetHeatmap(
        [AsParameters] GetIncidentHeatmapRequest request, IIncidentService service, CancellationToken ct) =>
        service.GetHeatmapAsync(request, ct);

    private static Task<GetIncidentAreaResponse> GetArea(
        [AsParameters] GetIncidentAreaRequest request, IIncidentService service, CancellationToken ct) =>
        service.GetAreaAsync(request, ct);
}
