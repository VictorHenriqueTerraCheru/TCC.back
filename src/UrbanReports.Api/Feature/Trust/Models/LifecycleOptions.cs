namespace UrbanReports.Api.Trust;

public sealed class LifecycleOptions
{
    public const string Section = "Lifecycle";

    public bool Enabled { get; set; } = true;

    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(15);
}
