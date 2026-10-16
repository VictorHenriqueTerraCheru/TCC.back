namespace Ocorrencias.Api.Common;

public static class CorsConfig
{
    public const string Politica = "Front";

    /// <summary>CORS restrito às origens de <c>Cors:Origens</c>.</summary>
    public static IServiceCollection AddCorsPorConfiguracao(this IServiceCollection services, IConfiguration configuracao)
    {
        var origens = configuracao.GetSection("Cors:Origens").Get<string[]>() ?? [];

        return services.AddCors(o => o.AddPolicy(Politica, p => p
            .WithOrigins(origens)
            .AllowAnyHeader()
            .AllowAnyMethod()));
    }
}
