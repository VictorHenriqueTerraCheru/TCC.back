using System.ComponentModel.DataAnnotations;

namespace UrbanReports.Api.Auth;

public sealed class JwtOptions
{
    public const string Secao = "Jwt";

    [Required, MinLength(32)]
    public string Chave { get; set; } = "";

    public string Emissor { get; set; } = "ocorrencias-api";
    public string Audiencia { get; set; } = "ocorrencias-front";

    [Range(1, 30)]
    public int ValidadeDias { get; set; } = 7;
}
