using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Usuarios;

namespace Ocorrencias.Api.Modules;

public sealed class UsuarioModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/me", ObterMe).WithTags("Usuários");

        api.MapPatch("/me", AtualizarMe)
            .Validar<AtualizarMeRequest>()
            .WithTags("Usuários");

        api.MapGet("/me/historico", ObterHistorico)
            .Validar<ObterHistoricoRequest>()
            .WithTags("Usuários");
    }

    private static async Task<Results<Ok<ObterMeResponse>, UnauthorizedHttpResult>> ObterMe(
        ClaimsPrincipal principal, IUsuarioService service, CancellationToken ct)
    {
        var r = await service.ObterMeAsync(principal.ObterUsuarioId(), ct);
        return r.Erro switch
        {
            null => TypedResults.Ok(r.Valor!),
            var e => e.ParaNaoAutorizado(),
        };
    }

    private static async Task<Results<Ok<ObterMeResponse>, UnauthorizedHttpResult>> AtualizarMe(
        AtualizarMeRequest request, ClaimsPrincipal principal, IUsuarioService service, CancellationToken ct)
    {
        var r = await service.AtualizarMeAsync(principal.ObterUsuarioId(), request, ct);
        return r.Erro switch
        {
            null => TypedResults.Ok(r.Valor!),
            var e => e.ParaNaoAutorizado(),
        };
    }

    private static Task<Paginado<ObterHistoricoItem>> ObterHistorico(
        [AsParameters] ObterHistoricoRequest request, ClaimsPrincipal principal, IUsuarioService service, CancellationToken ct) =>
        service.ObterHistoricoAsync(principal.ObterUsuarioId(), request, ct);
}
