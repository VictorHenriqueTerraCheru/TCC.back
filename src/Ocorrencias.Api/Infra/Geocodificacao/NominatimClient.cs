using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Ocorrencias.Api.Infra.Geocodificacao;

public interface INominatimClient
{
    /// <summary>Geocodificação reversa. Lança exceção em falha de rede/HTTP/timeout.</summary>
    Task<NominatimResposta?> ReversaAsync(double lat, double lng, CancellationToken ct);
}

/// <summary>
/// Cliente do Nominatim (OpenStreetMap). Todas as chamadas passam por uma fila de 1 req/s.
/// Singleton: usa <see cref="IHttpClientFactory"/> a cada chamada.
/// </summary>
public sealed class NominatimClient(IHttpClientFactory fabrica, IOptions<NominatimOptions> opcoes, TimeProvider relogio)
    : INominatimClient
{
    public const string NomeHttpClient = "Nominatim";

    private readonly LimitadorDeTaxa _limitador = new(relogio, opcoes.Value.IntervaloMinimo, opcoes.Value.EsperaMaximaNaFila);

    public Task<NominatimResposta?> ReversaAsync(double lat, double lng, CancellationToken ct) =>
        _limitador.ExecutarAsync(async token =>
        {
            var url = string.Create(CultureInfo.InvariantCulture,
                $"reverse?format=jsonv2&lat={lat:0.#######}&lon={lng:0.#######}&zoom=18&addressdetails=1");

            var http = fabrica.CreateClient(NomeHttpClient);
            using var resposta = await http.GetAsync(url, token);
            resposta.EnsureSuccessStatusCode();
            return await resposta.Content.ReadFromJsonAsync<NominatimResposta>(token);
        }, ct);
}
