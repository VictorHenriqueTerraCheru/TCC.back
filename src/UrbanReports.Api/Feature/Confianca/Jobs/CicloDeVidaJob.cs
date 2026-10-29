using Microsoft.Extensions.Options;

namespace UrbanReports.Api.Confianca;

public sealed class CicloDeVidaJob(
    IServiceScopeFactory escopos, IOptions<CicloDeVidaOptions> opcoes, TimeProvider relogio, ILogger<CicloDeVidaJob> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!opcoes.Value.Habilitado)
        {
            return;
        }

        using var timer = new PeriodicTimer(opcoes.Value.Intervalo, relogio);
        do
        {
            try
            {
                await using var escopo = escopos.CreateAsyncScope();
                await escopo.ServiceProvider.GetRequiredService<ProcessadorCicloDeVida>().ProcessarAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Falha no job de ciclo de vida; nova tentativa no próximo intervalo.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
