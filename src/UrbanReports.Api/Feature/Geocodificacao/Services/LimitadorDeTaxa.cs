namespace UrbanReports.Api.Geocodificacao;

/// <summary>
/// Fila de execução única com intervalo mínimo entre o fim de uma chamada e o início da próxima.
/// Garante a política de no máximo 1 requisição por segundo ao Nominatim.
/// </summary>
public sealed class LimitadorDeTaxa(TimeProvider relogio, TimeSpan intervaloMinimo, TimeSpan esperaMaxima)
{
    private readonly SemaphoreSlim _vez = new(1, 1);
    private DateTimeOffset? _ultimaChamada;

    /// <exception cref="TimeoutException">A vez na fila não chegou dentro da espera máxima.</exception>
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
