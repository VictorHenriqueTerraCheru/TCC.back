namespace UrbanReports.Api.Common;

public sealed class RelogioPostgres : TimeProvider
{
    public static DateTimeOffset Truncar(DateTimeOffset instante) =>
        new(instante.Ticks - instante.Ticks % TicksPorMicrossegundo, instante.Offset);

    private const long TicksPorMicrossegundo = TimeSpan.TicksPerMillisecond / 1000;

    public override DateTimeOffset GetUtcNow() => Truncar(System.GetUtcNow());
}
