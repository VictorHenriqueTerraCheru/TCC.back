using Microsoft.Extensions.Options;

namespace Ocorrencias.Api.Infra.Geocodificacao;

public static class GeocodificacaoConfig
{
    public static IServiceCollection AddGeocodificacao(this IServiceCollection services)
    {
        services.AddOptions<NominatimOptions>()
            .BindConfiguration(NominatimOptions.Secao)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddMemoryCache();

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
}
