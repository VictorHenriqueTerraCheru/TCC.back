using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Persistence;
using UrbanReports.Api.Common;
using UrbanReports.Api.Confianca.Motor;

namespace UrbanReports.Api.Confianca;

/// <summary>
/// Recalcula confiança e ciclo de vida de todos os problemas não resolvidos. Necessário porque o
/// decaimento temporal (e o prazo de 72 h / 48 h) muda o resultado mesmo sem votos novos.
/// </summary>
public sealed class ProcessadorCicloDeVida(
    AppDbContext db, TimeProvider relogio, ConfiguracaoConfianca cfg, ILogger<ProcessadorCicloDeVida> logger)
{
    /// <returns>Quantidade de problemas cujo status mudou.</returns>
    public async Task<int> ProcessarAsync(CancellationToken ct)
    {
        var ids = await db.Problemas
            .Where(p => p.Status != StatusProblema.Resolvida)
            .Select(p => p.Id)
            .ToListAsync(ct);

        var transicoes = 0;
        foreach (var id in ids)
        {
            await using var transacao = await db.Database.BeginTransactionAsync(ct);

            var problema = await RecalculoProblema.CarregarComBloqueioAsync(db, id, ct);
            if (problema is null || problema.Status == StatusProblema.Resolvida)
            {
                continue;
            }

            var votos = await db.Votos.Where(v => v.ProblemaId == id).AsNoTracking().ToListAsync(ct);
            var statusAnterior = problema.Status;
            RecalculoProblema.Aplicar(problema, votos, relogio.GetUtcNow(), cfg);

            await db.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
            db.ChangeTracker.Clear();

            if (problema.Status != statusAnterior)
            {
                transicoes++;
                logger.LogInformation("Problema {ProblemaId}: {De} → {Para}.", id, statusAnterior, problema.Status);
            }
        }

        return transicoes;
    }
}
