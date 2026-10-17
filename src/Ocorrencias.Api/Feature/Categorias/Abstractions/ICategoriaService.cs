namespace Ocorrencias.Api.Categorias;

public interface ICategoriaService
{
    Task<List<ListarCategoriasResponse>> ListarAsync(CancellationToken ct);
}
