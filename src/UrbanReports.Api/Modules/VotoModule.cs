using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using UrbanReports.Api.Common;
using UrbanReports.Api.Problemas;
using UrbanReports.Api.Votos;

namespace UrbanReports.Api.Modules;

public sealed class VotoModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapPut("/problemas/{id:guid}/voto", Votar)
            .Validar<VotarRequest>()
            .WithTags("Votos");
    }

    private static async Task<Results<Ok<ObterDetalheProblemaResponse>, NotFound, ProblemHttpResult, UnauthorizedHttpResult>> Votar(
        Guid id, VotarRequest request, ClaimsPrincipal principal, IVotoService service, CancellationToken ct) =>
        ParaHttp(await service.VotarAsync(id, principal.ObterUsuarioId(), request, ct));

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
