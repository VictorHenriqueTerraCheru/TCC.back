using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Persistence;

namespace Ocorrencias.Api.Features.Incidentes;

public static class ObterCalorIncidentes
{
    public const int DiasMaximo = 30;
    private const int LimiteCelulas = 5000;

    public sealed record Request(double MinLat, double MinLng, double MaxLat, double MaxLng, int Dias = 7, string? Grupos = null);

    /// <summary>Célula do mapa de calor: o ponto já é arredondado em 3 casas na gravação.</summary>
    public sealed record Response(double Lat, double Lng, int Quantidade);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.MinLat).Latitude();
            RuleFor(r => r.MaxLat).Latitude().GreaterThan(r => r.MinLat);
            RuleFor(r => r.MinLng).Longitude();
            RuleFor(r => r.MaxLng).Longitude().GreaterThan(r => r.MinLng);
            RuleFor(r => r.Dias).InclusiveBetween(1, DiasMaximo);
            RuleFor(r => r.Grupos).ListaDeGrupos();
        }
    }

    public static IEndpointRouteBuilder MapObterCalorIncidentes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/incidentes/calor", Handle)
            .Validar<Request>()
            .AllowAnonymous()
            .WithTags("Incidentes");
        return app;
    }

    /// <summary>
    /// Agrupa por ponto arredondado os incidentes da janela (<c>ocorrido_em</c>) dentro do retângulo. O recorte
    /// é planar (<c>::geometry &amp;&amp;</c>, índice ix_incidentes_area_aproximada_geometry): em geography as
    /// arestas seriam geodésicas e o retângulo, inexato em viewport largo.
    /// </summary>
    public static async Task<List<Response>> Handle(
        [AsParameters] Request request, AppDbContext db, TimeProvider relogio, CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();
        var desde = agora.AddDays(-request.Dias);
        var grupos = Listas.Separar(request.Grupos);

        return await db.Database
            .SqlQuery<Response>($"""
                SELECT ST_Y(i.area_aproximada::geometry) AS lat,
                       ST_X(i.area_aproximada::geometry) AS lng,
                       count(*)::int AS quantidade
                FROM incidentes i
                JOIN categorias c ON c.id = i.categoria_id
                WHERE i.ocorrido_em >= {desde} AND i.ocorrido_em <= {agora}
                  AND i.area_aproximada::geometry && ST_MakeEnvelope({request.MinLng}, {request.MinLat}, {request.MaxLng}, {request.MaxLat}, 4326)
                  AND (cardinality({grupos}) = 0 OR c.grupo = ANY({grupos}))
                GROUP BY 1, 2
                ORDER BY quantidade DESC
                LIMIT {LimiteCelulas}
                """)
            .ToListAsync(ct);
    }
}
