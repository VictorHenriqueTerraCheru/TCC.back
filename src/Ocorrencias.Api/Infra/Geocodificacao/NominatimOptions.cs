using System.ComponentModel.DataAnnotations;

namespace Ocorrencias.Api.Infra.Geocodificacao;

public sealed class NominatimOptions
{
    public const string Secao = "Nominatim";

    public string UrlBase { get; set; } = "https://nominatim.openstreetmap.org/";

    /// <summary>
    /// Identificação exigida pela política de uso do Nominatim ("a valid HTTP Referer or User-Agent identifying
    /// the application"; o User-Agent padrão de biblioteca não vale, e sem User-Agent a resposta é 403).
    /// </summary>
    [Required, Url]
    public string UrlProjeto { get; set; } = "https://github.com/VictorHenriqueTerraCheru/TCC.back";

    /// <summary>
    /// Opcional (<c>Nominatim__Email</c>): a API pede e-mail só para grandes volumes. Se informado, vai no
    /// User-Agent e no parâmetro <c>email</c>. Não use e-mail de exemplo: o Nominatim recusa (403).
    /// </summary>
    [EmailAddress]
    public string? Email { get; set; }

    /// <summary>Intervalo mínimo entre requisições ao Nominatim (política: no máximo 1 por segundo).</summary>
    public TimeSpan IntervaloMinimo { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Tempo máximo esperando a vez na fila antes de desistir (a resposta sai com <c>disponivel: false</c>).</summary>
    public TimeSpan EsperaMaximaNaFila { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan DuracaoCache { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Máximo de endereços em cache (cada um ocupa poucas centenas de bytes).</summary>
    [Range(1, 1_000_000)]
    public int LimiteEntradasCache { get; set; } = 50_000;

    public string UserAgent => string.IsNullOrWhiteSpace(Email)
        ? $"OcorrenciasUrbanasTCC/1.0 (+{UrlProjeto})"
        : $"OcorrenciasUrbanasTCC/1.0 (+{UrlProjeto}; {Email})";
}
