using System.Text.Json.Serialization;

namespace Ocorrencias.Api.Geocodificacao;

/// <summary>Subconjunto da resposta de <c>/reverse?format=jsonv2&amp;addressdetails=1</c>.</summary>
public sealed record NominatimResposta(
    [property: JsonPropertyName("address")] NominatimResposta.Endereco? Address,
    [property: JsonPropertyName("error")] string? Error)
{
    public sealed record Endereco
    {
        [JsonPropertyName("road")] public string? Road { get; init; }
        [JsonPropertyName("pedestrian")] public string? Pedestrian { get; init; }
        [JsonPropertyName("house_number")] public string? HouseNumber { get; init; }
        [JsonPropertyName("suburb")] public string? Suburb { get; init; }
        [JsonPropertyName("neighbourhood")] public string? Neighbourhood { get; init; }
        [JsonPropertyName("quarter")] public string? Quarter { get; init; }
        [JsonPropertyName("city")] public string? City { get; init; }
        [JsonPropertyName("town")] public string? Town { get; init; }
        [JsonPropertyName("village")] public string? Village { get; init; }
        [JsonPropertyName("municipality")] public string? Municipality { get; init; }
        [JsonPropertyName("ISO3166-2-lvl4")] public string? Iso3166Nivel4 { get; init; }
    }
}
