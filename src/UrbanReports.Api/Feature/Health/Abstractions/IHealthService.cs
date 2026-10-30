using UrbanReports.Api.Common;

namespace UrbanReports.Api.Health;

public interface IHealthService
{
    Task<Result<HealthStatusResponse>> VerifyDatabaseAsync(CancellationToken ct);
}
