using System.ComponentModel.DataAnnotations;

namespace UrbanReports.Api.Geocodificacao;

public sealed class NominatimOptions
{
    public const string Secao = "Nominatim";

    public string UrlBase { get; set; } = "https://nominatim.openstreetmap.org/";

    [Required, Url]
    public string UrlProjeto { get; set; } = "https://github.com/VictorHenriqueTerraCheru/TCC.back";

    [EmailAddress]
    public string? Email { get; set; }

    public TimeSpan IntervaloMinimo { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan EsperaMaximaNaFila { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan DuracaoCache { get; set; } = TimeSpan.FromDays(7);

    [Range(1, 1_000_000)]
    public int LimiteEntradasCache { get; set; } = 50_000;

    public string UserAgent => string.IsNullOrWhiteSpace(Email)
        ? $"OcorrenciasUrbanasTCC/1.0 (+{UrlProjeto})"
        : $"OcorrenciasUrbanasTCC/1.0 (+{UrlProjeto}; {Email})";
}
