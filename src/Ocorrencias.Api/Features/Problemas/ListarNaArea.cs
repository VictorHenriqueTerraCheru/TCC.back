using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Infra.Data;
using Ocorrencias.Domain;
using Ocorrencias.Domain.Confianca;

namespace Ocorrencias.Api.Features.Problemas;

public static class ListarProblemasNaArea
{
    public const int Limite = 500;

    /// <param name="Grupos">Separados por vírgula (ex.: <c>viaPublica,eletrica</c>). Vazio = todos.</param>
    /// <param name="Status">Separados por vírgula (ex.: <c>nova,ativa</c>). Vazio = todos os não resolvidos.</param>
    public sealed record Request(double MinLat, double MinLng, double MaxLat, double MaxLng, string? Grupos, string? Status);

    public sealed record Response(
        Guid Id, short CategoriaId, double Lat, double Lng, StatusProblema Status, double? Confianca, FaixaConfianca? Faixa);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.MinLat).Latitude();
            RuleFor(r => r.MaxLat).Latitude().GreaterThan(r => r.MinLat);
            RuleFor(r => r.MinLng).Longitude();
            RuleFor(r => r.MaxLng).Longitude().GreaterThan(r => r.MinLng);
            RuleFor(r => r.Grupos).ListaDeGrupos();
            RuleFor(r => r.Status).ListaDeStatus();
        }
    }

    public static IEndpointRouteBuilder MapListarProblemasNaArea(this IEndpointRouteBuilder app)
    {
        app.MapGet("/problemas", Handle)
            .Validar<Request>()
            .AllowAnonymous()
            .WithTags("Problemas");
        return app;
    }

    /// <summary>Marcadores da área visível. Nunca retorna <c>resolvida</c>; até 500, os de maior confiança primeiro.</summary>
    public static async Task<List<Response>> Handle(
        [AsParameters] Request request, AppDbContext db, ConfiguracaoConfianca cfg, CancellationToken ct)
    {
        var area = Geo.Retangulo(request.MinLat, request.MinLng, request.MaxLat, request.MaxLng);
        var grupos = Listas.Separar(request.Grupos);
        var status = Listas.Separar(request.Status)
            .Select(s => Enum.Parse<StatusProblema>(s, ignoreCase: true))
            .Where(s => s != StatusProblema.Resolvida)
            .ToArray();
        if (status.Length == 0)
        {
            status = [StatusProblema.Nova, StatusProblema.Ativa, StatusProblema.PossivelmenteResolvida];
        }

        var consulta = db.Problemas.Where(p => status.Contains(p.Status) && p.Localizacao.Intersects(area));
        if (grupos.Length > 0)
        {
            consulta = consulta.Where(p => grupos.Contains(p.Categoria.Grupo));
        }

        var linhas = await consulta
            .OrderByDescending(p => p.Confianca.HasValue && p.Status != StatusProblema.Nova)
            .ThenByDescending(p => p.Confianca)
            .ThenByDescending(p => p.CriadoEm)
            .Take(Limite)
            .Select(p => new { p.Id, p.CategoriaId, p.Localizacao, p.Status, p.Confianca })
            .AsNoTracking()
            .ToListAsync(ct);

        return linhas
            .Select(l =>
            {
                var (confianca, faixa) = ExibicaoConfianca.Calcular(l.Status, l.Confianca, cfg);
                return new Response(l.Id, l.CategoriaId, l.Localizacao.Y, l.Localizacao.X, l.Status, confianca, faixa);
            })
            .ToList();
    }
}
