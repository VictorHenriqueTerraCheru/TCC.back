using Microsoft.Extensions.Options;

namespace UrbanReports.Api.Trust;

public sealed class LifecycleJob(
    IServiceScopeFactory scopes, IOptions<LifecycleOptions> options, TimeProvider clock, ILogger<LifecycleJob> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.Value.Interval, clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<LifecycleProcessor>().ProcessAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Falha no job de ciclo de vida; nova tentativa no próximo intervalo.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
