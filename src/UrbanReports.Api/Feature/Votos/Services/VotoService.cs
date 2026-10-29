using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Common;
using UrbanReports.Api.Confianca;
using UrbanReports.Api.Persistence;
using UrbanReports.Api.Problemas;
using UrbanReports.Api.Confianca.Motor;

namespace UrbanReports.Api.Votos;

public sealed class VotoService(
    AppDbContext db, IProblemaService problemas, TimeProvider relogio, ConfiguracaoConfianca cfg) : IVotoService
{
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

        voto.Valor = (ValorVoto)request.Valor;
        voto.PerfilNoMomento = perfil.Value;
        voto.DistanciaMetros = distancia;
        voto.RegistradoEm = agora;

        RecalculoProblema.Aplicar(problema, votos, agora, cfg);

        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);

        return (await problemas.ConsultarDetalheAsync(problemaId, usuarioId, agora, ct))!;
    }

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
