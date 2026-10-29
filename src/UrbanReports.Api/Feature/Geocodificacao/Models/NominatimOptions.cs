using System.ComponentModel.DataAnnotations;

namespace UrbanReports.Api.Geocoding;

public sealed class NominatimOptions
{
    public const string Section = "Nominatim";

    public string UrlBase { get; set; } = "https://nominatim.openstreetmap.org/";

    [Required, Url]
    public string ProjectUrl { get; set; } = "https://github.com/VictorHenriqueTerraCheru/TCC.back";

    [EmailAddress]
    public string? Email { get; set; }

    public TimeSpan MinInterval { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan MaxQueueWait { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromDays(7);

    [Range(1, 1_000_000)]
    public int CacheEntryLimit { get; set; } = 50_000;

    public string UserAgent => string.IsNullOrWhiteSpace(Email)
        ? $"OcorrenciasUrbanasTCC/1.0 (+{ProjectUrl})"
        : $"OcorrenciasUrbanasTCC/1.0 (+{ProjectUrl}; {Email})";
}
