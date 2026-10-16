using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Ocorrencias.Api.Infra.Geocodificacao;

public interface IServicoGeocodificacao
{
    /// <summary>Nunca lança por falha do Nominatim: devolve <c>Disponivel = false</c> com o texto de coordenadas.</summary>
    Task<EnderecoGeocodificado> ReversaAsync(double lat, double lng, CancellationToken ct);
}

/// <summary>
/// Geocodificação reversa com cache em memória por coordenada arredondada em 4 casas (~11 m).
/// Chamadas simultâneas para a mesma chave compartilham uma única requisição ao Nominatim.
/// </summary>
public sealed class ServicoGeocodificacao(
    INominatimClient nominatim,
    IMemoryCache cache,
    IOptions<NominatimOptions> opcoes,
    ILogger<ServicoGeocodificacao> logger) : IServicoGeocodificacao
{
    private readonly ConcurrentDictionary<string, Lazy<Task<EnderecoGeocodificado?>>> _emAndamento = new();

    public async Task<EnderecoGeocodificado> ReversaAsync(double lat, double lng, CancellationToken ct)
    {
        var latChave = Math.Round(lat, 4, MidpointRounding.AwayFromZero);
        var lngChave = Math.Round(lng, 4, MidpointRounding.AwayFromZero);
        var chave = string.Create(CultureInfo.InvariantCulture, $"geo:{latChave:F4}:{lngChave:F4}");

        if (cache.TryGetValue(chave, out EnderecoGeocodificado? emCache))
        {
            return emCache!;
        }

        var chamada = _emAndamento.GetOrAdd(chave, _ => new Lazy<Task<EnderecoGeocodificado?>>(
            () => ConsultarECachearAsync(chave, latChave, lngChave)));

        // A consulta compartilhada não é cancelada por um único chamador.
        var resultado = await chamada.Value.WaitAsync(ct);
        return resultado ?? EnderecoGeocodificado.Indisponivel(lat, lng);
    }

    /// <summary>Nulo em falha (não vai para o cache, para tentar de novo na próxima chamada).</summary>
    private async Task<EnderecoGeocodificado?> ConsultarECachearAsync(string chave, double lat, double lng)
    {
        try
        {
            var resposta = await nominatim.ReversaAsync(lat, lng, CancellationToken.None);
            var endereco = EnderecoGeocodificado.Montar(resposta, lat, lng);
            cache.Set(chave, endereco, opcoes.Value.DuracaoCache);
            return endereco;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or TimeoutException or JsonException)
        {
            // Sem coordenadas no log (LGPD): o ponto pode ser a posição atual do usuário.
            logger.LogWarning(e, "Nominatim indisponível; respondendo sem endereço.");
            return null;
        }
        finally
        {
            _emAndamento.TryRemove(chave, out _);
        }
    }
}
