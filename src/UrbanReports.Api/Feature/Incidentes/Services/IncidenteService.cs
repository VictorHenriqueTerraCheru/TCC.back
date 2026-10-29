using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Common;
using UrbanReports.Api.Geocodificacao;
using UrbanReports.Api.Persistence;

namespace UrbanReports.Api.Incidentes;

public sealed class IncidenteService(AppDbContext db, IServicoGeocodificacao geocodificacao, TimeProvider relogio) : IIncidenteService
{
    public async Task<Resultado<IncidenteRegistrado>> RegistrarAsync(RegistrarIncidenteRequest request, Guid autorId, CancellationToken ct)
    {
        var categoria = await db.Categorias.FindAsync([request.CategoriaId], ct);
        if (categoria is not { Ativa: true })
        {
            return ErrosIncidente.CategoriaInexistente;
        }

        if (categoria.Tipo != TipoOcorrencia.Incidente)
        {
            return ErrosIncidente.CategoriaNaoEIncidente;
        }

        var agora = relogio.GetUtcNow();
        if (request.OcorridoEm < agora - LimitesIncidente.Janela || request.OcorridoEm > agora)
        {
            return ErrosIncidente.HorarioForaDaJanela;
        }

        var desde = agora - LimitesIncidente.Janela;

        if (await ContarRecentesAsync(autorId, desde, ct) >= LimitesIncidente.MaximoPor24h)
        {
            return ErrosIncidente.LimiteDiarioIncidentes;
        }

        var lat = Arredondar(request.Lat);
        var lng = Arredondar(request.Lng);
        var endereco = await geocodificacao.ReversaAsync(lat, lng, ct);

        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({ChaveBloqueio(autorId)})", ct);

        if (await ContarRecentesAsync(autorId, desde, ct) >= LimitesIncidente.MaximoPor24h)
        {
            return ErrosIncidente.LimiteDiarioIncidentes;
        }

        db.Incidentes.Add(new Incidente
        {
            Id = Guid.CreateVersion7(),
            CategoriaId = categoria.Id,
            AutorId = autorId,
            AreaAproximada = Geo.Ponto(lat, lng),
            RaioMetros = LimitesIncidente.RaioMetros,
            RotuloArea = endereco.RotuloArea,
            OcorridoEm = request.OcorridoEm.ToUniversalTime(),
            RegistradoEm = agora,
        });
        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);

        return new IncidenteRegistrado(lat, lng, await ConsultarAreaAsync(lat, lng, agora, ct));
    }

    public async Task<List<ObterCalorIncidentesResponse>> ObterCalorAsync(ObterCalorIncidentesRequest request, CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();
        var desde = agora.AddDays(-request.Dias);
        var grupos = Listas.Separar(request.Grupos);

        return await db.Database
            .SqlQuery<ObterCalorIncidentesResponse>($"""
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
                LIMIT {LimitesIncidente.MaximoCelulasCalor}
                """)
            .ToListAsync(ct);
    }

    public Task<ObterAreaIncidentesResponse> ObterAreaAsync(ObterAreaIncidentesRequest request, CancellationToken ct) =>
        ConsultarAreaAsync(request.Lat, request.Lng, relogio.GetUtcNow(), ct);

    public static NivelConcentracao Nivel(int total7Dias) =>
        total7Dias >= LimitesIncidente.LimiarAlta ? NivelConcentracao.Alta
        : total7Dias >= LimitesIncidente.LimiarMedia ? NivelConcentracao.Media
        : NivelConcentracao.Baixa;

    public static double Arredondar(double coordenada) =>
        Math.Round(coordenada, LimitesIncidente.CasasDecimais, MidpointRounding.AwayFromZero);

    private async Task<ObterAreaIncidentesResponse> ConsultarAreaAsync(double lat, double lng, DateTimeOffset agora, CancellationToken ct)
    {
        var ponto = Geo.Ponto(lat, lng);
        var desde7 = agora.AddDays(-7);
        var desde30 = agora.AddDays(-30);

        var porCategoria = await db.Incidentes
            .Where(i => i.OcorridoEm >= desde30 && i.OcorridoEm <= agora && i.AreaAproximada.IsWithinDistance(ponto, LimitesIncidente.RaioAreaMetros))
            .GroupBy(i => i.CategoriaId)
            .Select(g => new { CategoriaId = g.Key, Dias7 = g.Count(i => i.OcorridoEm >= desde7), Dias30 = g.Count() })
            .ToDictionaryAsync(g => g.CategoriaId, ct);

        var categorias = await db.Categorias
            .Where(c => c.Tipo == TipoOcorrencia.Incidente && c.Ativa)
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.Nome })
            .ToListAsync(ct);

        var contagens = categorias
            .Select(c => porCategoria.TryGetValue(c.Id, out var n)
                ? new ObterAreaIncidentesContagem(c.Id, c.Nome, n.Dias7, n.Dias30)
                : new ObterAreaIncidentesContagem(c.Id, c.Nome, 0, 0))
            .ToList();

        return new ObterAreaIncidentesResponse(LimitesIncidente.RaioAreaMetros, agora, Nivel(contagens.Sum(c => c.Dias7)), contagens);
    }

    private Task<int> ContarRecentesAsync(Guid autorId, DateTimeOffset desde, CancellationToken ct) =>
        db.Incidentes.CountAsync(i => i.AutorId == autorId && i.RegistradoEm > desde, ct);

    private static long ChaveBloqueio(Guid usuarioId)
    {
        Span<byte> bytes = stackalloc byte[16];
        usuarioId.TryWriteBytes(bytes);
        return BitConverter.ToInt64(bytes[..8]) ^ BitConverter.ToInt64(bytes[8..]);
    }
}
