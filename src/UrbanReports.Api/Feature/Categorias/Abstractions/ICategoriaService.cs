using UrbanReports.Api.Common;

namespace UrbanReports.Api.Categorias;

public interface ICategoriaService
{
    /// <summary>Só as ativas (visão pública).</summary>
    Task<List<ListarCategoriasResponse>> ListarAsync(CancellationToken ct);

    Task<List<CategoriaAdminResponse>> ListarTodasAsync(CancellationToken ct);

    Task<Resultado<CategoriaAdminResponse>> CriarAsync(CriarCategoriaRequest request, CancellationToken ct);

    Task<Resultado<CategoriaAdminResponse>> EditarAsync(short id, EditarCategoriaRequest request, CancellationToken ct);

    Task<Resultado<Vazio>> AlterarAtivaAsync(short id, bool ativa, CancellationToken ct);
}
