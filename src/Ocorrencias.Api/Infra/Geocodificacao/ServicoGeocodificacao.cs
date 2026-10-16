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
/// O cache é próprio e limitado em <see cref="NominatimOptions.LimiteEntradasCache"/>: o endpoint é anônimo
/// e aceita qualquer coordenada, então sem limite um cliente poderia encher a memória.
/// </summary>
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

        // A consulta compartilhada não é cancelada por um único chamador.
        var resultado = await chamada.Value.WaitAsync(ct);
        return resultado ?? EnderecoGeocodificado.Indisponivel(lat, lng);
    }

    /// <summary>Nulo em falha (não vai para o cache, para tentar de novo na próxima chamada).</summary>
    private async Task<EnderecoGeocodificado?> ConsultarECachearAsync(string chave, double lat, double lng)
    {
        try
        {
            // Um chamador pode ter visto o cache vazio logo antes de a consulta anterior gravar e sair de
            // _emAndamento; rechecar aqui evita uma segunda requisição para a mesma chave.
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
