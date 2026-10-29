using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using UrbanReports.Api.Persistence;

namespace UrbanReports.Api.Auth;

/// <summary>JWT HS256 com as claims <c>sub</c>, <c>email</c>, <c>nome</c> e <c>perfil</c>. Sem refresh token.</summary>
public sealed class JwtTokenService(IOptions<JwtOptions> opcoes, TimeProvider relogio)
{
    public const string ClaimNome = "nome";
    public const string ClaimPerfil = "perfil";

    private readonly JwtOptions _opcoes = opcoes.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public static SymmetricSecurityKey Chave(JwtOptions opcoes) => new(Encoding.UTF8.GetBytes(opcoes.Chave));

    public (string Token, DateTimeOffset ExpiraEm) Gerar(Usuario usuario)
    {
        var agora = relogio.GetUtcNow();
        var expiraEm = agora.AddDays(_opcoes.ValidadeDias);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _opcoes.Emissor,
            Audience = _opcoes.Audiencia,
            IssuedAt = agora.UtcDateTime,
            NotBefore = agora.UtcDateTime,
            Expires = expiraEm.UtcDateTime,
            SigningCredentials = new SigningCredentials(Chave(_opcoes), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = usuario.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = usuario.Email!,
                [ClaimNome] = usuario.Nome,
                [ClaimPerfil] = JsonNamingPolicy.CamelCase.ConvertName(usuario.Perfil.ToString()),
            },
        });

        return (token, expiraEm);
    }
}
