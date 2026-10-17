using System.ComponentModel.DataAnnotations;

namespace Ocorrencias.Api.Infra.Auth;

public sealed class JwtOptions
{
    public const string Secao = "Jwt";

    /// <summary>Chave HS256. Em produção vem de <c>Jwt__Chave</c>.</summary>
    [Required, MinLength(32)]
    public string Chave { get; set; } = "";

    public string Emissor { get; set; } = "ocorrencias-api";
    public string Audiencia { get; set; } = "ocorrencias-front";

    [Range(1, 30)]
    public int ValidadeDias { get; set; } = 7;
}
