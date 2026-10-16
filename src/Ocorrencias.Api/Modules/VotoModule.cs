using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Problemas;
using Ocorrencias.Api.Votos;

namespace Ocorrencias.Api.Modules;

public sealed class VotoModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapPut("/problemas/{id:guid}/votos/existencia", VotarExistencia)
            .Validar<VotarExistenciaRequest>()
            .WithTags("Votos");

        api.MapPut("/problemas/{id:guid}/votos/persistencia", VotarPersistencia)
            .Validar<VotarPersistenciaRequest>()
            .WithTags("Votos");
    }

    private static async Task<Results<Ok<ObterDetalheProblemaResponse>, NotFound, ProblemHttpResult, UnauthorizedHttpResult>> VotarExistencia(
        Guid id, VotarExistenciaRequest request, ClaimsPrincipal principal, IVotoService service, CancellationToken ct) =>
        ParaHttp(await service.VotarExistenciaAsync(id, principal.ObterUsuarioId(), request, ct));

    private static async Task<Results<Ok<ObterDetalheProblemaResponse>, NotFound, ProblemHttpResult, UnauthorizedHttpResult>> VotarPersistencia(
        Guid id, VotarPersistenciaRequest request, ClaimsPrincipal principal, IVotoService service, CancellationToken ct) =>
        ParaHttp(await service.VotarPersistenciaAsync(id, principal.ObterUsuarioId(), request, ct));

    private static Results<Ok<ObterDetalheProblemaResponse>, NotFound, ProblemHttpResult, UnauthorizedHttpResult> ParaHttp(
        Resultado<ObterDetalheProblemaResponse> r) =>
        r.Erro switch
        {
            null => TypedResults.Ok(r.Valor!),
            { Tipo: TipoErro.NaoEncontrado } e => e.ParaNaoEncontrado(),
            { Tipo: TipoErro.NaoAutorizado } e => e.ParaNaoAutorizado(),
            var e => e.ParaProblema(),
        };
}
