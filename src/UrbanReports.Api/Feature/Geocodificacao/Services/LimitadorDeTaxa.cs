namespace UrbanReports.Api.Geocoding;

public sealed class RequestThrottle(TimeProvider clock, TimeSpan minInterval, TimeSpan maxWait)
{
    private readonly SemaphoreSlim _turn = new(1, 1);
    private DateTimeOffset? _lastCall;

    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (!await _turn.WaitAsync(maxWait, ct))
        {
            throw new TimeoutException("Fila do Nominatim cheia.");
        }

        try
        {
            if (_lastCall is { } last)
            {
                var wait = last + minInterval - clock.GetUtcNow();
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, clock, ct);
                }
            }

            try
            {
                return await action(ct);
            }
            finally
            {
                _lastCall = clock.GetUtcNow();
            }
        }
        finally
        {
            _turn.Release();
        }
    }
}
