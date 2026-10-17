using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Incidentes;

public interface IIncidenteService
{
    Task<Resultado<IncidenteRegistrado>> RegistrarAsync(RegistrarIncidenteRequest request, Guid autorId, CancellationToken ct);

    Task<List<ObterCalorIncidentesResponse>> ObterCalorAsync(ObterCalorIncidentesRequest request, CancellationToken ct);

    Task<ObterAreaIncidentesResponse> ObterAreaAsync(ObterAreaIncidentesRequest request, CancellationToken ct);
}
