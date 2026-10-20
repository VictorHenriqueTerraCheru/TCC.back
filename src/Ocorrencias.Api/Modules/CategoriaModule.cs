using Microsoft.AspNetCore.Http.HttpResults;
using Ocorrencias.Api.Categorias;
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Modules;

public sealed class CategoriaModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/categorias", Listar)
            .AllowAnonymous()
            .WithTags("Categorias");

        api.MapGet("/admin/categorias", ListarTodas)
            .RequireAuthorization(PoliticasAutorizacao.Administrador)
            .WithTags("Admin");

        api.MapPost("/admin/categorias", Criar)
            .Validar<CriarCategoriaRequest>()
            .RequireAuthorization(PoliticasAutorizacao.Administrador)
            .WithTags("Admin");

        api.MapPut("/admin/categorias/{id:int}", Editar)
            .Validar<EditarCategoriaRequest>()
            .RequireAuthorization(PoliticasAutorizacao.Administrador)
            .WithTags("Admin");

        api.MapPatch("/admin/categorias/{id:int}/ativa", AlterarAtiva)
            .RequireAuthorization(PoliticasAutorizacao.Administrador)
            .WithTags("Admin");
    }

    private static Task<List<ListarCategoriasResponse>> Listar(ICategoriaService service, CancellationToken ct) =>
        service.ListarAsync(ct);

    private static Task<List<CategoriaAdminResponse>> ListarTodas(ICategoriaService service, CancellationToken ct) =>
        service.ListarTodasAsync(ct);

    private static async Task<Results<Created<CategoriaAdminResponse>, ProblemHttpResult>> Criar(
        CriarCategoriaRequest request, ICategoriaService service, CancellationToken ct)
    {
        var r = await service.CriarAsync(request, ct);
        return r.Erro switch
        {
            null => TypedResults.Created("/api/v1/admin/categorias", r.Valor),
            var e => e.ParaProblema(),
        };
    }

    private static async Task<Results<Ok<CategoriaAdminResponse>, NotFound, ProblemHttpResult>> Editar(
        int id, EditarCategoriaRequest request, ICategoriaService service, CancellationToken ct)
    {
        if (id is < 1 or > short.MaxValue)
        {
            return TypedResults.NotFound();
        }

        var r = await service.EditarAsync((short)id, request, ct);
        return r.Erro switch
        {
            null => TypedResults.Ok(r.Valor),
            { Tipo: TipoErro.NaoEncontrado } e => e.ParaNaoEncontrado(),
            var e => e.ParaProblema(),
        };
    }

    private static async Task<Results<NoContent, NotFound>> AlterarAtiva(
        int id, AlterarAtivaCategoriaRequest request, ICategoriaService service, CancellationToken ct)
    {
        if (id is < 1 or > short.MaxValue)
        {
            return TypedResults.NotFound();
        }

        var r = await service.AlterarAtivaAsync((short)id, request.Ativa, ct);
        return r.Erro is null ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
