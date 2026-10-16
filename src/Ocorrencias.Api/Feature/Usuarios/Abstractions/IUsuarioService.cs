using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Usuarios;

public interface IUsuarioService
{
    Task<Resultado<ObterMeResponse>> ObterMeAsync(Guid usuarioId, CancellationToken ct);

    Task<Resultado<ObterMeResponse>> AtualizarMeAsync(Guid usuarioId, AtualizarMeRequest request, CancellationToken ct);

    Task<Paginado<ObterHistoricoItem>> ObterHistoricoAsync(Guid usuarioId, ObterHistoricoRequest request, CancellationToken ct);
}
