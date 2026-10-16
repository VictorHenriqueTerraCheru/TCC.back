using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ocorrencias.Api.Infra.Data;
using Ocorrencias.Api.Infra.Data.Entidades;

namespace Ocorrencias.Api.Infra.Auth;

public static class AuthConfig
{
    public const string Autenticado = "Autenticado";
    public const string Administrador = "Administrador";

    public static IServiceCollection AddAutenticacao(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.Secao)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddIdentityCore<Usuario>(o =>
            {
                o.User.RequireUniqueEmail = true;
                // Contrato: só "senha ≥ 8 caracteres".
                o.Password.RequiredLength = 8;
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequiredUniqueChars = 1;
            })
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddSingleton<JwtTokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Emissor,
                    ValidAudience = jwt.Value.Audiencia,
                    IssuerSigningKey = JwtTokenService.Chave(jwt.Value),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = JwtTokenService.ClaimNome,
                    RoleClaimType = JwtTokenService.ClaimPerfil,
                };
            });

        // O perfil do token serve só para autorizar rotas de admin; o peso do voto lê o perfil do banco.
        services.AddAuthorizationBuilder()
            .AddPolicy(Autenticado, p => p.RequireAuthenticatedUser())
            .AddPolicy(Administrador, p => p.RequireAuthenticatedUser().RequireClaim(JwtTokenService.ClaimPerfil, "administrador"));

        return services;
    }
}
