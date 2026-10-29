using UrbanReports.Api.Common;
using UrbanReports.Api.Persistence;

namespace UrbanReports.Api.Health;

public sealed class HealthService(AppDbContext db, ILogger<HealthService> logger) : IHealthService
{
    public async Task<Result<HealthStatusResponse>> VerifyDatabaseAsync(CancellationToken ct)
    {
        try
        {
            if (await db.Database.CanConnectAsync(ct))
            {
                return HealthStatusResponse.Healthy;
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Health check do banco falhou.");
            return HealthErrors.DatabaseUnavailable;
        }

        logger.LogWarning("Health check do banco falhou: sem conexão.");
        return HealthErrors.DatabaseUnavailable;
    }
}
