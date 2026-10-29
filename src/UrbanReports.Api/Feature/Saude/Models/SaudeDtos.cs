namespace UrbanReports.Api.Health;

public sealed record HealthStatusResponse(string Status)
{
    public static HealthStatusResponse Healthy { get; } = new("healthy");
}
