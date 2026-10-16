using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Api.Geocodificacao;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Features.Incidentes;

public static class RegistrarIncidente
{
    public const int CasasDecimais = 3;
    public const short RaioMetros = 100;
    public const int LimitePor24h = 10;
    public static readonly TimeSpan Janela = TimeSpan.FromHours(24);

    public sealed record Request(short CategoriaId, double Lat, double Lng, DateTimeOffset OcorridoEm);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.CategoriaId).GreaterThan((short)0);
            RuleFor(r => r.Lat).Latitude();
            RuleFor(r => r.Lng).Longitude();
        }
    }

    public static IEndpointRouteBuilder MapRegistrarIncidente(this IEndpointRouteBuilder app)
    {
        app.MapPost("/incidentes", Handle)
            .Validar<Request>()
            .WithTags("Incidentes");
        return app;
    }

    /// <summary>
    /// Arredonda o ponto em 3 casas ANTES de gravar e guarda o rótulo da geocodificação do ponto arredondado.
    /// Responde com o resumo da área do ponto arredondado. O autor nunca é exposto.
    /// </summary>
    public static async Task<Results<Created<ObterAreaIncidentes.Response>, ValidationProblem, ProblemHttpResult>> Handle(
        Request request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IServicoGeocodificacao geocodificacao,
        TimeProvider relogio,
        CancellationToken ct)
    {
        var categoria = await db.Categorias.FindAsync([request.CategoriaId], ct);
        if (categoria is null)
        {
            return Erros.Campo("categoriaId", "Categoria inexistente.");
        }

        if (categoria.Tipo != TipoOcorrencia.Incidente)
        {
            return Erros.CategoriaNaoEIncidente;
        }

        var agora = relogio.GetUtcNow();
        if (request.OcorridoEm < agora - Janela || request.OcorridoEm > agora)
        {
            return Erros.HorarioForaDaJanela;
        }

        var autorId = principal.ObterUsuarioId();
        var desde = agora - Janela;

        // Pré-checagem sem bloqueio: evita gastar uma consulta ao Nominatim com quem já atingiu o limite.
        if (await ContarRecentesAsync(db, autorId, desde, ct) >= LimitePor24h)
        {
            return Erros.LimiteDiarioIncidentes;
        }

        var lat = Arredondar(request.Lat);
        var lng = Arredondar(request.Lng);
        var endereco = await geocodificacao.ReversaAsync(lat, lng, ct);

        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        // Serializa os registros da mesma conta: sem isso, requisições simultâneas furariam o limite de 10.
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({ChaveBloqueio(autorId)})", ct);

        if (await ContarRecentesAsync(db, autorId, desde, ct) >= LimitePor24h)
        {
            return Erros.LimiteDiarioIncidentes;
        }

        db.Incidentes.Add(new Incidente
        {
            Id = Guid.CreateVersion7(),
            CategoriaId = categoria.Id,
            AutorId = autorId,
            AreaAproximada = Geo.Ponto(lat, lng),
            RaioMetros = RaioMetros,
            RotuloArea = endereco.RotuloArea,
            OcorridoEm = request.OcorridoEm.ToUniversalTime(),
            RegistradoEm = agora,
        });
        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);

        var area = await ObterAreaIncidentes.ConsultarAsync(db, lat, lng, agora, ct);
        return TypedResults.Created(
            FormattableString.Invariant($"/api/v1/incidentes/area?lat={lat}&lng={lng}"), area);
    }

    private static Task<int> ContarRecentesAsync(AppDbContext db, Guid autorId, DateTimeOffset desde, CancellationToken ct) =>
        db.Incidentes.CountAsync(i => i.AutorId == autorId && i.RegistradoEm > desde, ct);

    public static double Arredondar(double coordenada) =>
        Math.Round(coordenada, CasasDecimais, MidpointRounding.AwayFromZero);

    /// <summary>Chave de 64 bits do advisory lock, derivada do id do usuário.</summary>
    private static long ChaveBloqueio(Guid usuarioId)
    {
        Span<byte> bytes = stackalloc byte[16];
        usuarioId.TryWriteBytes(bytes);
        return BitConverter.ToInt64(bytes[..8]) ^ BitConverter.ToInt64(bytes[8..]);
    }
}
