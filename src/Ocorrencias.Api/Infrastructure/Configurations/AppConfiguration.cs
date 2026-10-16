using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ocorrencias.Api.Auth;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Persistence;

namespace Ocorrencias.Api.Infrastructure;

/// <summary>Configurações de borda chamadas uma a uma no Program: CORS, autenticação e rate limit.</summary>
public static class AppConfiguration
{
    /// <summary>CORS restrito às origens de <c>Cors:Origens</c>.</summary>
    public static IServiceCollection AddCorsConfigurado(this IServiceCollection services, IConfiguration configuracao)
    {
        var origens = configuracao.GetSection("Cors:Origens").Get<string[]>() ?? [];

        return services.AddCors(o => o.AddPolicy(PoliticasCors.Front, p => p
            .WithOrigins(origens)
            .AllowAnyHeader()
            .AllowAnyMethod()));
    }

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
            .AddPolicy(PoliticasAutorizacao.Autenticado, p => p.RequireAuthenticatedUser())
            .AddPolicy(PoliticasAutorizacao.Administrador, p => p.RequireAuthenticatedUser().RequireClaim(JwtTokenService.ClaimPerfil, "administrador"));

        return services;
    }

    /// <summary>
    /// Limite por IP (docs/API.md: 30 req/min na geocodificação). Atrás de proxy reverso, habilite
    /// <c>ASPNETCORE_FORWARDEDHEADERS_ENABLED=true</c> para o IP real do cliente chegar aqui.
    /// </summary>
    public static IServiceCollection AddLimitesDeRequisicao(this IServiceCollection services, IConfiguration configuracao)
    {
        var porMinuto = configuracao.GetValue("LimitesDeRequisicao:GeocodificacaoPorMinuto", 30);

        return services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = async (contexto, ct) =>
            {
                var http = contexto.HttpContext;
                if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var aguardar))
                {
                    http.Response.Headers.RetryAfter = ((int)Math.Ceiling(aguardar.TotalSeconds)).ToString();
                }

                await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = http,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Type = "limite-requisicoes",
                        Title = "Muitas requisições. Tente novamente em instantes.",
                    },
                });
            };

            o.AddPolicy(PoliticasLimite.Geocodificacao, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = porMinuto,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });
    }
}
