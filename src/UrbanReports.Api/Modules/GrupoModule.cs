using Microsoft.AspNetCore.Http.HttpResults;
using UrbanReports.Api.Common;
using UrbanReports.Api.Grupos;

namespace UrbanReports.Api.Modules;

public sealed class GrupoModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/grupos", Listar)
            .AllowAnonymous()
            .WithTags("Categorias");

        api.MapPost("/admin/grupos", Criar)
            .Validar<CriarGrupoRequest>()
            .RequireAuthorization(PoliticasAutorizacao.Administrador)
            .WithTags("Admin");

        api.MapPut("/admin/grupos/{chave}", Editar)
            .Validar<EditarGrupoRequest>()
            .RequireAuthorization(PoliticasAutorizacao.Administrador)
            .WithTags("Admin");

        api.MapDelete("/admin/grupos/{chave}", Apagar)
            .RequireAuthorization(PoliticasAutorizacao.Administrador)
            .WithTags("Admin");
    }

    private static Task<List<GrupoResponse>> Listar(IGrupoService service, CancellationToken ct) =>
        service.ListarAsync(ct);

    private static async Task<Results<Created<GrupoResponse>, ProblemHttpResult>> Criar(
        CriarGrupoRequest request, IGrupoService service, CancellationToken ct)
    {
        var r = await service.CriarAsync(request, ct);
        return r.Erro switch
        {
            null => TypedResults.Created("/api/v1/grupos", r.Valor),
            var e => e.ParaProblema(),
        };
    }

    private static async Task<Results<Ok<GrupoResponse>, NotFound, ProblemHttpResult>> Editar(
        string chave, EditarGrupoRequest request, IGrupoService service, CancellationToken ct)
    {
        var r = await service.EditarAsync(chave, request, ct);
        return r.Erro switch
        {
            null => TypedResults.Ok(r.Valor),
            { Tipo: TipoErro.NaoEncontrado } e => e.ParaNaoEncontrado(),
            var e => e.ParaProblema(),
        };
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Apagar(
        string chave, IGrupoService service, CancellationToken ct)
    {
        var r = await service.ApagarAsync(chave, ct);
        return r.Erro switch
        {
            null => TypedResults.NoContent(),
            { Tipo: TipoErro.NaoEncontrado } e => e.ParaNaoEncontrado(),
            var e => e.ParaProblema(),
        };
    }
}
