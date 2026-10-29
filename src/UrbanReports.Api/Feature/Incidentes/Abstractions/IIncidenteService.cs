using UrbanReports.Api.Common;

namespace UrbanReports.Api.Incidentes;

public interface IIncidenteService
{
    Task<Resultado<IncidenteRegistrado>> RegistrarAsync(RegistrarIncidenteRequest request, Guid autorId, CancellationToken ct);

    Task<List<ObterCalorIncidentesResponse>> ObterCalorAsync(ObterCalorIncidentesRequest request, CancellationToken ct);

    Task<ObterAreaIncidentesResponse> ObterAreaAsync(ObterAreaIncidentesRequest request, CancellationToken ct);
}
