using UrbanReports.Api.Common;

namespace UrbanReports.Api.Incidents;

public interface IIncidentService
{
    Task<Result<CreatedIncident>> CreateAsync(CreateIncidentRequest request, Guid authorId, CancellationToken ct);

    Task<List<GetIncidentHeatmapResponse>> GetHeatmapAsync(GetIncidentHeatmapRequest request, CancellationToken ct);

    Task<GetIncidentAreaResponse> GetAreaAsync(GetIncidentAreaRequest request, CancellationToken ct);
}
