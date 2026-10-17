using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Persistence;

namespace Ocorrencias.Api.Categorias;

public sealed class CategoriaService(AppDbContext db) : ICategoriaService
{
    public Task<List<ListarCategoriasResponse>> ListarAsync(CancellationToken ct) =>
        db.Categorias
            .OrderBy(c => c.Id)
            .Select(c => new ListarCategoriasResponse(c.Id, c.Nome, c.Grupo, c.Tipo, c.Icone))
            .ToListAsync(ct);
}
