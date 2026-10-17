using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Incidentes;

namespace Ocorrencias.Api.Modules;

public sealed class IncidenteModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapPost("/incidentes", Registrar)
            .Validar<RegistrarIncidenteRequest>()
            .WithTags("Incidentes");

        api.MapGet("/incidentes/calor", ObterCalor)
            .Validar<ObterCalorIncidentesRequest>()
            .AllowAnonymous()
            .WithTags("Incidentes");

        api.MapGet("/incidentes/area", ObterArea)
            .Validar<ObterAreaIncidentesRequest>()
            .AllowAnonymous()
            .WithTags("Incidentes");
    }

    private static async Task<Results<Created<ObterAreaIncidentesResponse>, ValidationProblem, ProblemHttpResult>> Registrar(
        RegistrarIncidenteRequest request, ClaimsPrincipal principal, IIncidenteService service, CancellationToken ct)
    {
        var r = await service.RegistrarAsync(request, principal.ObterUsuarioId(), ct);
        return r.Erro switch
        {
            null => TypedResults.Created(
                FormattableString.Invariant($"/api/v1/incidentes/area?lat={r.Valor!.Lat}&lng={r.Valor.Lng}"), r.Valor.Area),
            { Tipo: TipoErro.Campo } e => e.ParaValidacao(),
            var e => e.ParaProblema(),
        };
    }

    private static Task<List<ObterCalorIncidentesResponse>> ObterCalor(
        [AsParameters] ObterCalorIncidentesRequest request, IIncidenteService service, CancellationToken ct) =>
        service.ObterCalorAsync(request, ct);

    private static Task<ObterAreaIncidentesResponse> ObterArea(
        [AsParameters] ObterAreaIncidentesRequest request, IIncidenteService service, CancellationToken ct) =>
        service.ObterAreaAsync(request, ct);
}
