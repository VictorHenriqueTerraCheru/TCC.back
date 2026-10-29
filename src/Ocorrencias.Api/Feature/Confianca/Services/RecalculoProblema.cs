using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Api.Confianca.Motor;

namespace Ocorrencias.Api.Confianca;

/// <summary>Ponte entre as entidades e o motor do Domain. A API nunca reimplementa fórmula.</summary>
public static class RecalculoProblema
{
    /// <summary>
    /// Lê o problema com <c>SELECT ... FOR UPDATE</c>: votos simultâneos no mesmo problema (e o job) são
    /// serializados, e cada recálculo enxerga todos os votos já gravados. Exige transação aberta.
    /// </summary>
    public static async Task<Problema?> CarregarComBloqueioAsync(AppDbContext db, Guid problemaId, CancellationToken ct)
    {
        var problemas = await db.Problemas
            .FromSql($"SELECT * FROM problemas WHERE id = {problemaId} FOR UPDATE")
            .ToListAsync(ct);
        return problemas.SingleOrDefault();
    }

    /// <summary>Recalcula a confiança e avalia o ciclo de vida (uma transição por chamada).</summary>
    public static void Aplicar(Problema problema, IEnumerable<Voto> votos, DateTimeOffset agora, ConfiguracaoConfianca cfg)
    {
        var fatos = votos
            .Select(v => new VotoFato(v.UsuarioId, v.Valor, v.PerfilNoMomento, v.DistanciaMetros, v.RegistradoEm))
            .ToList();

        var resultado = MotorConfianca.CalcularConfianca(fatos, agora, cfg);
        var estado = MotorConfianca.AvaliarCicloDeVida(
            new EstadoProblema(problema.Status, problema.CriadoEm, problema.StatusAlteradoEm, problema.CondicaoResolucaoDesde),
            fatos,
            agora,
            cfg);

        problema.Confianca = Math.Round((decimal)resultado.Indice, 2, MidpointRounding.AwayFromZero);
        problema.Status = estado.Status;
        problema.StatusAlteradoEm = estado.StatusAlteradoEm;
        problema.CondicaoResolucaoDesde = estado.CondicaoResolucaoDesde;
    }
}
