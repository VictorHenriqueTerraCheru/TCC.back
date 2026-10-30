namespace UrbanReports.Api.Incidents;

public static class IncidentLimits
{
    public const int DecimalPlaces = 3;
    public const short RadiusMeters = 100;
    public const int MaxPer24h = 10;
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);

    public const int MaxHeatmapDays = 30;
    public const int MaxHeatmapCells = 5000;

    public const int AreaRadiusMeters = 400;
    public const int MediumThreshold = 5;
    public const int HighThreshold = 15;
}
