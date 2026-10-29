using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using UrbanReports.Api.Common;
using UrbanReports.Api.Verificacao;

namespace UrbanReports.Api.Modules;

public sealed class VerificacaoModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapPost("/verificacoes", Solicitar)
            .Validar<SolicitarVerificacaoRequest>()
            .WithTags("Verificação");
    }

    private static async Task<Results<Created<SolicitarVerificacaoResponse>, ProblemHttpResult, UnauthorizedHttpResult>> Solicitar(
        SolicitarVerificacaoRequest request, ClaimsPrincipal principal, IVerificacaoService service, CancellationToken ct)
    {
        var r = await service.SolicitarAsync(principal.ObterUsuarioId(), request, ct);
        return r.Erro switch
        {
            null => TypedResults.Created("/api/v1/me", r.Valor!),
            { Tipo: TipoErro.NaoAutorizado } e => e.ParaNaoAutorizado(),
            var e => e.ParaProblema(),
        };
    }
}
