using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Ocorrencias.Api.Common;

public static class LimitesDeRequisicao
{
    public const string Geocodificacao = "geocodificacao";

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

            o.AddPolicy(Geocodificacao, http => RateLimitPartition.GetFixedWindowLimiter(
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
