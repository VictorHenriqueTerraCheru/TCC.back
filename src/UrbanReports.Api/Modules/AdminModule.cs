using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using UrbanReports.Api.Admin;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Modules;

public sealed class AdminModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/admin/verificacoes", ListarVerificacoes)
            .Validar<ListarVerificacoesRequest>()
            .RequireAuthorization(PoliticasAutorizacao.Administrador)
            .WithTags("Admin");

        api.MapPost("/admin/verificacoes/{id:guid}/aprovar", Aprovar)
            .RequireAuthorization(PoliticasAutorizacao.Administrador)
            .WithTags("Admin");

        api.MapPost("/admin/verificacoes/{id:guid}/recusar", Recusar)
            .Validar<RecusarVerificacaoRequest>()
            .RequireAuthorization(PoliticasAutorizacao.Administrador)
            .WithTags("Admin");

        api.MapGet("/admin/usuarios", ListarUsuarios)
            .Validar<ListarUsuariosRequest>()
            .RequireAuthorization(PoliticasAutorizacao.Administrador)
            .WithTags("Admin");

        api.MapPatch("/admin/usuarios/{id:guid}/perfil", AlterarPerfil)
            .Validar<AlterarPerfilRequest>()
            .RequireAuthorization(PoliticasAutorizacao.Administrador)
            .WithTags("Admin");
    }

    private static Task<Paginado<ListarVerificacoesItem>> ListarVerificacoes(
        [AsParameters] ListarVerificacoesRequest request, IAdminService service, CancellationToken ct) =>
        service.ListarVerificacoesAsync(request, ct);

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Aprovar(
        Guid id, ClaimsPrincipal principal, IAdminService service, CancellationToken ct) =>
        ParaHttp(await service.AprovarAsync(id, principal.ObterUsuarioId(), ct));

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Recusar(
        Guid id, RecusarVerificacaoRequest request, ClaimsPrincipal principal, IAdminService service, CancellationToken ct) =>
        ParaHttp(await service.RecusarAsync(id, principal.ObterUsuarioId(), request, ct));

    private static Task<Paginado<ListarUsuariosItem>> ListarUsuarios(
        [AsParameters] ListarUsuariosRequest request, IAdminService service, CancellationToken ct) =>
        service.ListarUsuariosAsync(request, ct);

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> AlterarPerfil(
        Guid id, AlterarPerfilRequest request, ClaimsPrincipal principal, IAdminService service, CancellationToken ct) =>
        ParaHttp(await service.AlterarPerfilAsync(id, principal.ObterUsuarioId(), request, ct));

    private static Results<NoContent, NotFound, ProblemHttpResult> ParaHttp(Resultado<Vazio> r) =>
        r.Erro switch
        {
            null => TypedResults.NoContent(),
            { Tipo: TipoErro.NaoEncontrado } e => e.ParaNaoEncontrado(),
            var e => e.ParaProblema(),
        };
}
