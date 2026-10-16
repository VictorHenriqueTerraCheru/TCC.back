using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Features.Incidentes;

public static class ObterAreaIncidentes
{
    public const int RaioMetros = 400;
    public const int LimiarMedia = 5;
    public const int LimiarAlta = 15;

    public sealed record Request(double Lat, double Lng);

    public sealed record Response(int RaioMetros, DateTimeOffset AtualizadoEm, NivelConcentracao Nivel, IReadOnlyList<Contagem> Contagens);

    public sealed record Contagem(short CategoriaId, string Nome, int Dias7, int Dias30);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.Lat).Latitude();
            RuleFor(r => r.Lng).Longitude();
        }
    }

    public static IEndpointRouteBuilder MapObterAreaIncidentes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/incidentes/area", Handle)
            .Validar<Request>()
            .AllowAnonymous()
            .WithTags("Incidentes");
        return app;
    }

    public static async Task<Response> Handle([AsParameters] Request request, AppDbContext db, TimeProvider relogio, CancellationToken ct) =>
        await ConsultarAsync(db, request.Lat, request.Lng, relogio.GetUtcNow(), ct);

    /// <summary>
    /// Contagens de 7 e 30 dias (por <c>ocorrido_em</c>) num raio fixo de 400 m, para todas as categorias de
    /// incidente (inclusive as zeradas). O nível usa o total de 7 dias.
    /// </summary>
    public static async Task<Response> ConsultarAsync(AppDbContext db, double lat, double lng, DateTimeOffset agora, CancellationToken ct)
    {
        var ponto = Geo.Ponto(lat, lng);
        var desde7 = agora.AddDays(-7);
        var desde30 = agora.AddDays(-30);

        var porCategoria = await db.Incidentes
            .Where(i => i.OcorridoEm >= desde30 && i.OcorridoEm <= agora && i.AreaAproximada.IsWithinDistance(ponto, RaioMetros))
            .GroupBy(i => i.CategoriaId)
            .Select(g => new { CategoriaId = g.Key, Dias7 = g.Count(i => i.OcorridoEm >= desde7), Dias30 = g.Count() })
            .ToDictionaryAsync(g => g.CategoriaId, ct);

        var categorias = await db.Categorias
            .Where(c => c.Tipo == TipoOcorrencia.Incidente)
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.Nome })
            .ToListAsync(ct);

        var contagens = categorias
            .Select(c => porCategoria.TryGetValue(c.Id, out var n)
                ? new Contagem(c.Id, c.Nome, n.Dias7, n.Dias30)
                : new Contagem(c.Id, c.Nome, 0, 0))
            .ToList();

        return new Response(RaioMetros, agora, Nivel(contagens.Sum(c => c.Dias7)), contagens);
    }

    public static NivelConcentracao Nivel(int total7Dias) =>
        total7Dias >= LimiarAlta ? NivelConcentracao.Alta
        : total7Dias >= LimiarMedia ? NivelConcentracao.Media
        : NivelConcentracao.Baixa;
}
