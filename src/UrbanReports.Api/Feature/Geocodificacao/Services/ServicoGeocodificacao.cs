using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace UrbanReports.Api.Geocodificacao;

public interface IServicoGeocodificacao
{
    Task<EnderecoGeocodificado> ReversaAsync(double lat, double lng, CancellationToken ct);
}

public sealed class ServicoGeocodificacao(
    INominatimClient nominatim,
    IOptions<NominatimOptions> opcoes,
    ILogger<ServicoGeocodificacao> logger) : IServicoGeocodificacao, IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = opcoes.Value.LimiteEntradasCache });
    private readonly ConcurrentDictionary<string, Lazy<Task<EnderecoGeocodificado?>>> _emAndamento = new();

    internal int EntradasEmCache => _cache.Count;

    public void Dispose() => _cache.Dispose();

    public async Task<EnderecoGeocodificado> ReversaAsync(double lat, double lng, CancellationToken ct)
    {
        var latChave = Math.Round(lat, 4, MidpointRounding.AwayFromZero);
        var lngChave = Math.Round(lng, 4, MidpointRounding.AwayFromZero);
        var chave = string.Create(CultureInfo.InvariantCulture, $"geo:{latChave:F4}:{lngChave:F4}");

        if (_cache.TryGetValue(chave, out EnderecoGeocodificado? emCache))
        {
            return emCache!;
        }

        var chamada = _emAndamento.GetOrAdd(chave, _ => new Lazy<Task<EnderecoGeocodificado?>>(
            () => ConsultarECachearAsync(chave, latChave, lngChave)));

        var resultado = await chamada.Value.WaitAsync(ct);
        return resultado ?? EnderecoGeocodificado.Indisponivel(lat, lng);
    }

    private async Task<EnderecoGeocodificado?> ConsultarECachearAsync(string chave, double lat, double lng)
    {
        try
        {
            if (_cache.TryGetValue(chave, out EnderecoGeocodificado? emCache))
            {
                return emCache;
            }

            var resposta = await nominatim.ReversaAsync(lat, lng, CancellationToken.None);
            var endereco = EnderecoGeocodificado.Montar(resposta, lat, lng);
            _cache.Set(chave, endereco, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = opcoes.Value.DuracaoCache,
                Size = 1,
            });
            return endereco;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or TimeoutException or JsonException)
        {
            logger.LogWarning(e, "Nominatim indisponível; respondendo sem endereço.");
            return null;
        }
        finally
        {
            _emAndamento.TryRemove(chave, out _);
        }
    }
}
