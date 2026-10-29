using UrbanReports.Api.Common;

namespace UrbanReports.Api.Tests.Common;

public sealed class PostgresClockTests
{
    [Fact]
    public void Truncates_ticks_below_one_microsecond()
    {
        var instant = new DateTimeOffset(2026, 10, 16, 3, 36, 51, TimeSpan.Zero).AddTicks(4_987_082);

        PostgresClock.Truncate(instant).Should().Be(instant.AddTicks(-2));
    }

    [Fact]
    public void System_clock_comes_aligned_to_microseconds() =>
        (new PostgresClock().GetUtcNow().Ticks % 10).Should().Be(0);
}
