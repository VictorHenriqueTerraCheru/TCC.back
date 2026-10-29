namespace UrbanReports.Api.Common;

public sealed class PostgresClock : TimeProvider
{
    public static DateTimeOffset Truncate(DateTimeOffset instant) =>
        new(instant.Ticks - instant.Ticks % TicksPerMicrosecond, instant.Offset);

    private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

    public override DateTimeOffset GetUtcNow() => Truncate(System.GetUtcNow());
}
