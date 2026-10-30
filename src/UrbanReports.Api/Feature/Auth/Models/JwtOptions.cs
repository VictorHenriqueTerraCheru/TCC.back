using System.ComponentModel.DataAnnotations;

namespace UrbanReports.Api.Auth;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    [Required, MinLength(32)]
    public string Key { get; set; } = "";

    public string Issuer { get; set; } = "ocorrencias-api";
    public string Audience { get; set; } = "ocorrencias-front";

    [Range(1, 30)]
    public int ValidityDays { get; set; } = 7;
}
