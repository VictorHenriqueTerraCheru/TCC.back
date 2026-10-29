using UrbanReports.Api.Common;
using UrbanReports.Api.Persistence;

namespace UrbanReports.Api.Saude;

public sealed class SaudeService(AppDbContext db, ILogger<SaudeService> logger) : ISaudeService
{
    public async Task<Resultado<StatusSaudeResponse>> VerificarBancoAsync(CancellationToken ct)
    {
        try
        {
            if (await db.Database.CanConnectAsync(ct))
            {
                return StatusSaudeResponse.Saudavel;
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Health check do banco falhou.");
            return ErrosSaude.BancoIndisponivel;
        }

        logger.LogWarning("Health check do banco falhou: sem conexão.");
        return ErrosSaude.BancoIndisponivel;
    }
}
