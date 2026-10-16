using Ocorrencias.Api.Common;
using Ocorrencias.Api.Problemas;

namespace Ocorrencias.Api.Votos;

public interface IVotoService
{
    Task<Resultado<ObterDetalheProblemaResponse>> VotarExistenciaAsync(
        Guid problemaId, Guid usuarioId, VotarExistenciaRequest request, CancellationToken ct);

    Task<Resultado<ObterDetalheProblemaResponse>> VotarPersistenciaAsync(
        Guid problemaId, Guid usuarioId, VotarPersistenciaRequest request, CancellationToken ct);
}
