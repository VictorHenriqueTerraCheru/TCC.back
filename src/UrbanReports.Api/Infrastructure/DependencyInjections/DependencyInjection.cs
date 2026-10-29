using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UrbanReports.Api.Common;
using UrbanReports.Api.Saude;
using UrbanReports.Api.Admin;
using UrbanReports.Api.Verificacao;
using UrbanReports.Api.Incidentes;
using UrbanReports.Api.Votos;
using UrbanReports.Api.Problemas;
using UrbanReports.Api.Usuarios;
using UrbanReports.Api.Auth;
using UrbanReports.Api.Categorias;
using UrbanReports.Api.Grupos;
using UrbanReports.Api.Confianca;
using UrbanReports.Api.Geocodificacao;
using UrbanReports.Api.Persistence;
using UrbanReports.Api.Confianca.Motor;

namespace UrbanReports.Api.Infrastructure;

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

        return services;
    }

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
            .RemoveAllLoggers();

        services.AddSingleton<INominatimClient, NominatimClient>();
        services.AddSingleton<IServicoGeocodificacao, ServicoGeocodificacao>();

        return services;
    }

    private static IServiceCollection AddConfianca(this IServiceCollection services, IConfiguration configuracao)
    {
        var nome = configuracao.GetValue("Confianca:Configuracao", ConfiguracaoConfianca.B.Nome)!;
        services.AddSingleton(ConfiguracaoConfianca.PorNome(nome));

        services.AddOptions<CicloDeVidaOptions>().BindConfiguration(CicloDeVidaOptions.Secao);
        services.AddScoped<ProcessadorCicloDeVida>();
        services.AddHostedService<CicloDeVidaJob>();

        return services;
    }

    private static IServiceCollection AddServicos(this IServiceCollection services)
    {
        services.AddScoped<ICategoriaService, CategoriaService>();
        services.AddScoped<IGrupoService, GrupoService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUsuarioService, UsuarioService>();
        services.AddScoped<IProblemaService, ProblemaService>();
        services.AddScoped<IVotoService, VotoService>();
        services.AddScoped<IIncidenteService, IncidenteService>();
        services.AddScoped<IVerificacaoService, VerificacaoService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<ISaudeService, SaudeService>();
        return services;
    }
}
