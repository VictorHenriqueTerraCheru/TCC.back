using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Auth;
using Ocorrencias.Api.Categorias;
using Ocorrencias.Api.Confianca;
using Ocorrencias.Api.Geocodificacao;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain.Confianca;

namespace Ocorrencias.Api.Infrastructure;

/// <summary>Tudo que é interno da aplicação; configurações de borda ficam em <see cref="AppConfiguration"/>.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddAplicacao(this IServiceCollection services, IConfiguration configuracao) =>
        services
            .AddTratamentoDeErros()
            .AddJson()
            .AddRelogio()
            .AddValidacao()
            .AddPersistencia(configuracao)
            .AddGeocodificacao()
            .AddConfianca(configuracao)
            .AddServicos();

    private static IServiceCollection AddTratamentoDeErros(this IServiceCollection services) =>
        services.AddProblemDetails();

    private static IServiceCollection AddJson(this IServiceCollection services) =>
        services.ConfigureHttpJsonOptions(o => JsonConfig.Configurar(o.SerializerOptions));

    private static IServiceCollection AddRelogio(this IServiceCollection services) =>
        services.AddSingleton<TimeProvider>(new RelogioPostgres());

    private static IServiceCollection AddValidacao(this IServiceCollection services)
    {
        Validacao.ConfigurarGlobal();
        return services.AddValidatorsFromAssemblyContaining<Program>();
    }

    private static IServiceCollection AddPersistencia(this IServiceCollection services, IConfiguration configuracao)
    {
        var conexao = configuracao.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' não configurada (ConnectionStrings__Default).");

        services.AddDbContext<AppDbContext>(o => o
            .UseNpgsql(conexao, npgsql => npgsql.UseNetTopologySuite(geographyAsDefault: true))
            .UseSnakeCaseNamingConvention());

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

        return services;
    }

    /// <summary>Interno (não privado): os testes do NominatimClient montam só esta parte do container.</summary>
    internal static IServiceCollection AddGeocodificacao(this IServiceCollection services)
    {
        services.AddOptions<NominatimOptions>()
            .BindConfiguration(NominatimOptions.Secao)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient(NominatimClient.NomeHttpClient, (sp, http) =>
            {
                var opcoes = sp.GetRequiredService<IOptions<NominatimOptions>>().Value;
                http.BaseAddress = new Uri(opcoes.UrlBase);
                http.Timeout = opcoes.Timeout;
                http.DefaultRequestHeaders.UserAgent.ParseAdd(opcoes.UserAgent);
                http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pt-BR");
            })
            // O log padrão do HttpClient grava a URL com lat/lon: desligado (LGPD, RNF03).
            .RemoveAllLoggers();

        services.AddSingleton<INominatimClient, NominatimClient>();
        services.AddSingleton<IServicoGeocodificacao, ServicoGeocodificacao>();

        return services;
    }

    /// <summary>
    /// Configuração ativa do motor. Da configuração vem só o nome (<c>Confianca:Configuracao</c>, padrão "B");
    /// os valores vêm do Domain.
    /// </summary>
    private static IServiceCollection AddConfianca(this IServiceCollection services, IConfiguration configuracao)
    {
        var nome = configuracao.GetValue("Confianca:Configuracao", ConfiguracaoConfianca.B.Nome)!;
        services.AddSingleton(ConfiguracaoConfianca.PorNome(nome));

        services.AddOptions<CicloDeVidaOptions>().BindConfiguration(CicloDeVidaOptions.Secao);
        services.AddScoped<ProcessadorCicloDeVida>();
        services.AddHostedService<CicloDeVidaJob>();

        return services;
    }

    /// <summary>Services das features, um por área.</summary>
    private static IServiceCollection AddServicos(this IServiceCollection services)
    {
        services.AddScoped<ICategoriaService, CategoriaService>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
