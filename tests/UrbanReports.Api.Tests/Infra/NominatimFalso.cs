using System.Collections.Concurrent;
using UrbanReports.Api.Geocodificacao;

namespace UrbanReports.Api.Tests.Infra;

/// <summary>Substitui o Nominatim real nos testes (nunca chamamos o serviço público em teste).</summary>
public sealed class NominatimFalso : INominatimClient
{
    private readonly ConcurrentDictionary<(double, double), NominatimResposta?> _respostas = new();
    private readonly ConcurrentDictionary<(double, double), bool> _foraDoAr = new();
    private int _chamadas;

    public int Chamadas => _chamadas;

    /// <summary>Atraso artificial, para testar chamadas simultâneas.</summary>
    public TimeSpan Atraso { get; set; } = TimeSpan.Zero;

    public static NominatimResposta Endereco(
        string? rua = "Rua Voluntários da Pátria", string? numero = "170", string? bairro = "Botafogo",
        string? cidade = "Rio de Janeiro", string? iso = "BR-RJ") =>
        new(new NominatimResposta.Endereco { Road = rua, HouseNumber = numero, Suburb = bairro, City = cidade, Iso3166Nivel4 = iso }, null);

    public void Responder(double lat, double lng, NominatimResposta? resposta) => _respostas[Chave(lat, lng)] = resposta;

    public void ForaDoAr(double lat, double lng, bool fora = true) => _foraDoAr[Chave(lat, lng)] = fora;

    public async Task<NominatimResposta?> ReversaAsync(double lat, double lng, CancellationToken ct)
    {
        Interlocked.Increment(ref _chamadas);
        if (Atraso > TimeSpan.Zero)
        {
            await Task.Delay(Atraso, ct);
        }

        if (_foraDoAr.GetValueOrDefault(Chave(lat, lng)))
        {
            throw new HttpRequestException("Nominatim fora do ar (simulado).");
        }

        return _respostas.TryGetValue(Chave(lat, lng), out var resposta) ? resposta : Endereco();
    }

    private static (double, double) Chave(double lat, double lng) =>
        (Math.Round(lat, 4, MidpointRounding.AwayFromZero), Math.Round(lng, 4, MidpointRounding.AwayFromZero));
}
