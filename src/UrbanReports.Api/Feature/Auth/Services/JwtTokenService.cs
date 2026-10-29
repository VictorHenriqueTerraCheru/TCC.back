using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using UrbanReports.Api.Persistence;

namespace UrbanReports.Api.Auth;

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    public const string NameClaim = "name";
    public const string RoleClaim = "role";

    private readonly JwtOptions _options = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public static SymmetricSecurityKey Key(JwtOptions options) => new(Encoding.UTF8.GetBytes(options.Key));

    public (string Token, DateTimeOffset ExpiresAt) Generate(User user)
    {
        var now = clock.GetUtcNow();
        var expiresAt = now.AddDays(_options.ValidityDays);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(Key(_options), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email!,
                [NameClaim] = user.Name,
                [RoleClaim] = JsonNamingPolicy.CamelCase.ConvertName(user.Role.ToString()),
            },
        });

        return (token, expiresAt);
    }
}
