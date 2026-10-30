using UrbanReports.Api.Common;

namespace UrbanReports.Api.Health;

public static class HealthErrors
{
    public static readonly ApplicationError DatabaseUnavailable = ApplicationError.Rule(
        StatusCodes.Status503ServiceUnavailable, "database-unavailable", "Banco de dados indisponível.");
}
