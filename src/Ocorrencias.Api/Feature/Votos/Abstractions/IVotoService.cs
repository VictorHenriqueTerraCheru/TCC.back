using Ocorrencias.Api.Common;
using Ocorrencias.Api.Problemas;

namespace Ocorrencias.Api.Votos;

public interface IVotoService
{
    Task<Resultado<ObterDetalheProblemaResponse>> VotarAsync(
        Guid problemaId, Guid usuarioId, VotarRequest request, CancellationToken ct);
}
