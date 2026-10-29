using UrbanReports.Api.Common;
using UrbanReports.Api.Problemas;

namespace UrbanReports.Api.Votos;

public interface IVotoService
{
    Task<Resultado<ObterDetalheProblemaResponse>> VotarAsync(
        Guid problemaId, Guid usuarioId, VotarRequest request, CancellationToken ct);
}
