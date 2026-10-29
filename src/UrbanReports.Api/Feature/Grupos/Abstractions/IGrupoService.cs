using UrbanReports.Api.Common;

namespace UrbanReports.Api.Grupos;

public interface IGrupoService
{
    Task<List<GrupoResponse>> ListarAsync(CancellationToken ct);

    Task<Resultado<GrupoResponse>> CriarAsync(CriarGrupoRequest request, CancellationToken ct);

    Task<Resultado<GrupoResponse>> EditarAsync(string chave, EditarGrupoRequest request, CancellationToken ct);

    Task<Resultado<Vazio>> ApagarAsync(string chave, CancellationToken ct);
}
