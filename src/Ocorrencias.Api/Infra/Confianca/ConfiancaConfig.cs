using Ocorrencias.Api.Infra.Jobs;
using Ocorrencias.Domain.Confianca;

namespace Ocorrencias.Api.Infra.Confianca;

public static class ConfiancaConfig
{
    /// <summary>
    /// Configuração ativa do motor. Da configuração vem só o nome (<c>Confianca:Configuracao</c>, padrão "B");
    /// os valores vêm do Domain.
    /// </summary>
    public static IServiceCollection AddConfianca(this IServiceCollection services, IConfiguration configuracao)
    {
        var nome = configuracao.GetValue("Confianca:Configuracao", ConfiguracaoConfianca.B.Nome)!;
        services.AddSingleton(ConfiguracaoConfianca.PorNome(nome));

        services.AddOptions<CicloDeVidaOptions>().BindConfiguration(CicloDeVidaOptions.Secao);
        services.AddScoped<ProcessadorCicloDeVida>();
        services.AddHostedService<CicloDeVidaJob>();

        return services;
    }
}
