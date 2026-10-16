using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ocorrencias.Api.Persistence;

using Ocorrencias.Api.Auth;

namespace Ocorrencias.Api.Infrastructure;

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
                // UserName = e-mail: aceita qualquer caractere que a validação de e-mail aceite (ex.: apóstrofo, acentos).
                o.User.AllowedUserNameCharacters = "";
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
            .Configure<IOptions<JwtOptions>, TimeProvider>((bearer, jwt, relogio) =>
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
                    // Validade conferida pelo mesmo TimeProvider que emite o token (os testes controlam o tempo).
                    LifetimeValidator = (inicio, fim, _, parametros) =>
                    {
                        var agora = relogio.GetUtcNow().UtcDateTime;
                        var folga = parametros.ClockSkew;
                        return (inicio is null || inicio.Value <= agora + folga)
                            && fim is not null && fim.Value > agora - folga;
                    },
                };
            });

        // O perfil do token serve só para autorizar rotas de admin; o peso do voto lê o perfil do banco.
        services.AddAuthorizationBuilder()
            .AddPolicy(Autenticado, p => p.RequireAuthenticatedUser())
            .AddPolicy(Administrador, p => p.RequireAuthenticatedUser().RequireClaim(JwtTokenService.ClaimPerfil, "administrador"));

        return services;
    }
}
