using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Confianca;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Api.Problemas;
using Ocorrencias.Api.Confianca.Motor;

namespace Ocorrencias.Api.Votos;

/// <summary>Resposta a "Ainda está lá?" (docs/diagramas/05_seq_votar.puml).</summary>
public sealed class VotoService(
    AppDbContext db, IProblemaService problemas, TimeProvider relogio, ConfiguracaoConfianca cfg) : IVotoService
{
    /// <summary>
    /// Numa única transação: bloqueia o problema, valida as regras, calcula a distância no PostGIS (a posição
    /// do votante é descartada), faz o UPSERT do voto com o perfil atual do banco, recalcula a confiança e
    /// avalia o ciclo de vida.
    /// </summary>
    public async Task<Resultado<ObterDetalheProblemaResponse>> VotarAsync(
        Guid problemaId, Guid usuarioId, VotarRequest request, CancellationToken ct)
    {
        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        var problema = await RecalculoProblema.CarregarComBloqueioAsync(db, problemaId, ct);
        if (problema is null)
        {
            return ErroAplicacao.NaoEncontrado;
        }

        if (problema.Status == StatusProblema.Resolvida)
        {
            return ErrosVoto.ProblemaResolvido;
        }

        if (problema.AutorId == usuarioId)
        {
            return ErrosVoto.AutorNaoVota;
        }

        // O perfil vem do banco, não do token (que vale 7 dias e pode estar desatualizado após uma aprovação).
        var perfil = await db.Users.Where(u => u.Id == usuarioId).Select(u => (Perfil?)u.Perfil).SingleOrDefaultAsync(ct);
        if (perfil is null)
        {
            return ErroAplicacao.NaoAutorizado;
        }

        var distancia = await CalcularDistanciaAsync(problemaId, request.Lat, request.Lng, ct);
        var agora = relogio.GetUtcNow();

        var votos = await db.Votos.Where(v => v.ProblemaId == problemaId).ToListAsync(ct);
        var voto = votos.SingleOrDefault(v => v.UsuarioId == usuarioId);
        if (voto is null)
        {
            voto = new Voto { Id = Guid.CreateVersion7(), ProblemaId = problemaId, UsuarioId = usuarioId };
            db.Votos.Add(voto);
            votos.Add(voto);
        }

        // UPSERT: votar de novo substitui valor, perfil, distância e instante.
        voto.Valor = (ValorVoto)request.Valor;
        voto.PerfilNoMomento = perfil.Value;
        voto.DistanciaMetros = distancia;
        voto.RegistradoEm = agora;

        RecalculoProblema.Aplicar(problema, votos, agora, cfg);

        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);

        return (await problemas.ConsultarDetalheAsync(problemaId, usuarioId, agora, ct))!;
    }

    /// <summary><c>ST_Distance</c> em geography (metros). Nulo = votante sem localização. A posição não é persistida.</summary>
    private async Task<int?> CalcularDistanciaAsync(Guid problemaId, double? lat, double? lng, CancellationToken ct)
    {
        if (lat is not { } latitude || lng is not { } longitude)
        {
            return null;
        }

        var ponto = Geo.Ponto(latitude, longitude);
        var metros = await db.Problemas
            .Where(p => p.Id == problemaId)
            .Select(p => p.Localizacao.Distance(ponto))
            .SingleAsync(ct);

        return (int)Math.Round(metros, MidpointRounding.AwayFromZero);
    }
}
