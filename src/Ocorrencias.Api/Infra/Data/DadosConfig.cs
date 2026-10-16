using Microsoft.EntityFrameworkCore;

namespace Ocorrencias.Api.Infra.Data;

public static class DadosConfig
{
    public static IServiceCollection AddDados(this IServiceCollection services, IConfiguration configuracao)
    {
        var conexao = configuracao.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' não configurada (ConnectionStrings__Default).");

        services.AddDbContext<AppDbContext>(o => o
            .UseNpgsql(conexao, npgsql => npgsql.UseNetTopologySuite(geographyAsDefault: true))
            .UseSnakeCaseNamingConvention());

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

        return services;
    }
}
