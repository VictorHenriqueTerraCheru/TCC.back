using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Features.Categorias;

public static class ListarCategorias
{
    public sealed record Response(short Id, string Nome, string Grupo, TipoOcorrencia Tipo, string Icone);

    public static IEndpointRouteBuilder MapListarCategorias(this IEndpointRouteBuilder app)
    {
        app.MapGet("/categorias", Handle)
            .AllowAnonymous()
            .WithTags("Categorias");
        return app;
    }

    public static async Task<List<Response>> Handle(AppDbContext db, CancellationToken ct) =>
        await db.Categorias
            .OrderBy(c => c.Id)
            .Select(c => new Response(c.Id, c.Nome, c.Grupo, c.Tipo, c.Icone))
            .ToListAsync(ct);
}
