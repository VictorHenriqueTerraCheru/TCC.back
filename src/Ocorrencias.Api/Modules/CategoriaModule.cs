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
    }

    private static Task<List<ListarCategoriasResponse>> Listar(ICategoriaService service, CancellationToken ct) =>
        service.ListarAsync(ct);
}
