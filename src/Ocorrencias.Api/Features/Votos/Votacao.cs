using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Problemas;
using Ocorrencias.Api.Confianca;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain;
using Ocorrencias.Domain.Confianca;

namespace Ocorrencias.Api.Features.Votos;

/// <summary>Núcleo comum aos votos de existência e persistência (docs/diagramas/05_seq_votar.puml).</summary>
public static class Votacao
{
    /// <summary>Posição opcional do votante: os dois campos juntos, ou nenhum.</summary>
    public static void RegrasPosicao<T>(AbstractValidator<T> validador, Func<T, double?> lat, Func<T, double?> lng)
    {
        validador.RuleFor(r => lat(r)).Latitude().OverridePropertyName("lat");
        validador.RuleFor(r => lng(r)).Longitude().OverridePropertyName("lng");
        validador.RuleFor(r => r)
            .Must(r => lat(r).HasValue == lng(r).HasValue)
            .OverridePropertyName("lat")
            .WithMessage("Informe lat e lng juntos, ou nenhum dos dois.");
    }

    /// <summary>
    /// Numa única transação: bloqueia o problema, valida as regras, calcula a distância no PostGIS (a posição
    /// do votante é descartada), faz o UPSERT do voto com o perfil atual do banco, recalcula a confiança e
    /// avalia o ciclo de vida.
    /// </summary>
    public static async Task<Results<Ok<ObterDetalheProblemaResponse>, NotFound, ProblemHttpResult, UnauthorizedHttpResult>> VotarAsync(
        AppDbContext db,
        IProblemaService problemas,
        Guid problemaId,
        Guid usuarioId,
        TipoVoto tipo,
        ValorVoto valor,
        double? lat,
        double? lng,
        TimeProvider relogio,
        ConfiguracaoConfianca cfg,
        CancellationToken ct)
    {
        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        var problema = await RecalculoProblema.CarregarComBloqueioAsync(db, problemaId, ct);
        if (problema is null)
        {
            return TypedResults.NotFound();
        }

        if (problema.Status == StatusProblema.Resolvida)
        {
            return Erros.ProblemaResolvido;
        }

        if (tipo == TipoVoto.Existencia && problema.AutorId == usuarioId)
        {
            return Erros.AutorNaoVotaExistencia;
        }

        if (tipo == TipoVoto.Persistencia && problema.Status == StatusProblema.Nova)
        {
            return Erros.PersistenciaIndisponivelEmNova;
        }

        // O perfil vem do banco, não do token (que vale 7 dias e pode estar desatualizado após uma aprovação).
        var perfil = await db.Users.Where(u => u.Id == usuarioId).Select(u => (Perfil?)u.Perfil).SingleOrDefaultAsync(ct);
        if (perfil is null)
        {
            return TypedResults.Unauthorized();
        }

        var distancia = await CalcularDistanciaAsync(db, problemaId, lat, lng, ct);
        var agora = relogio.GetUtcNow();

        var votos = await db.Votos.Where(v => v.ProblemaId == problemaId).ToListAsync(ct);
        var voto = votos.SingleOrDefault(v => v.UsuarioId == usuarioId && v.Tipo == tipo);
        if (voto is null)
        {
            voto = new Voto { Id = Guid.CreateVersion7(), ProblemaId = problemaId, UsuarioId = usuarioId, Tipo = tipo };
            db.Votos.Add(voto);
            votos.Add(voto);
        }

        // UPSERT: votar de novo substitui valor, perfil, distância e instante.
        voto.Valor = valor;
        voto.PerfilNoMomento = perfil.Value;
        voto.DistanciaMetros = distancia;
        voto.RegistradoEm = agora;

        RecalculoProblema.Aplicar(problema, votos, agora, cfg);

        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);

        var detalhe = await problemas.ConsultarDetalheAsync(problemaId, usuarioId, agora, ct);
        return TypedResults.Ok(detalhe!);
    }

    /// <summary><c>ST_Distance</c> em geography (metros). Nulo = votante sem localização. A posição não é persistida.</summary>
    private static async Task<int?> CalcularDistanciaAsync(AppDbContext db, Guid problemaId, double? lat, double? lng, CancellationToken ct)
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
