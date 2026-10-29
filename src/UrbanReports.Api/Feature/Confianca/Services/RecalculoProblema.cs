using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Persistence;
using UrbanReports.Api.Confianca.Motor;

namespace UrbanReports.Api.Confianca;

public static class RecalculoProblema
{
    public static async Task<Problema?> CarregarComBloqueioAsync(AppDbContext db, Guid problemaId, CancellationToken ct)
    {
        var problemas = await db.Problemas
            .FromSql($"SELECT * FROM problemas WHERE id = {problemaId} FOR UPDATE")
            .ToListAsync(ct);
        return problemas.SingleOrDefault();
    }

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
