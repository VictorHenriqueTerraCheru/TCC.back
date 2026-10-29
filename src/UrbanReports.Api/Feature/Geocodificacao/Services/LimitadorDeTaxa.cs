namespace UrbanReports.Api.Geocodificacao;

public sealed class LimitadorDeTaxa(TimeProvider relogio, TimeSpan intervaloMinimo, TimeSpan esperaMaxima)
{
    private readonly SemaphoreSlim _vez = new(1, 1);
    private DateTimeOffset? _ultimaChamada;

    public async Task<T> ExecutarAsync<T>(Func<CancellationToken, Task<T>> acao, CancellationToken ct)
    {
        if (!await _vez.WaitAsync(esperaMaxima, ct))
        {
            throw new TimeoutException("Fila do Nominatim cheia.");
        }

        try
        {
            if (_ultimaChamada is { } ultima)
            {
                var espera = ultima + intervaloMinimo - relogio.GetUtcNow();
                if (espera > TimeSpan.Zero)
                {
                    await Task.Delay(espera, relogio, ct);
                }
            }

            try
            {
                return await acao(ct);
            }
            finally
            {
                _ultimaChamada = relogio.GetUtcNow();
            }
        }
        finally
        {
            _vez.Release();
        }
    }
}
