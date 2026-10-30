using Microsoft.Extensions.Time.Testing;
using UrbanReports.Api.Geocoding;

namespace UrbanReports.Api.Tests.Geocoding;

public sealed class RequestThrottleTests
{
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);

    private RequestThrottle Create(TimeSpan? maxWait = null) =>
        new(_clock, TimeSpan.FromSeconds(1), maxWait ?? TimeSpan.FromSeconds(30));

    [Fact]
    public async Task Second_call_waits_the_min_interval()
    {
        var limiter = Create();
        await limiter.RunAsync(_ => Task.FromResult(1), CancellationToken.None);

        var second = limiter.RunAsync(_ => Task.FromResult(2), CancellationToken.None);
        await Task.Delay(50);
        second.IsCompleted.Should().BeFalse("ainda não passou 1 s desde a primeira");

        _clock.Advance(TimeSpan.FromMilliseconds(999));
        await Task.Delay(50);
        second.IsCompleted.Should().BeFalse();

        _clock.Advance(TimeSpan.FromMilliseconds(1));
        (await second.WaitAsync(TimeSpan.FromSeconds(5))).Should().Be(2);
    }
}
