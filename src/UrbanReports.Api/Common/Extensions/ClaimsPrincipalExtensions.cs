using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace UrbanReports.Api.Common;

public static class ClaimsPrincipalExtensions
{
    public static Guid ObterUsuarioId(this ClaimsPrincipal usuario) =>
        Guid.Parse(usuario.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Token sem a claim 'sub'."));

    public static Guid? ObterUsuarioIdOuNulo(this ClaimsPrincipal usuario) =>
        Guid.TryParse(usuario.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;
}
