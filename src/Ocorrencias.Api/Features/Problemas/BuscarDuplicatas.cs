using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Features.Categorias;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain;
using Ocorrencias.Domain.Confianca;

namespace Ocorrencias.Api.Features.Problemas;

public static class BuscarDuplicatas
{
    public const double RaioMetros = 50;
    private const int Limite = 20;

    public sealed record Request(short CategoriaId, double Lat, double Lng);

    public sealed record Response(
        Guid Id,
        CategoriaResumo Categoria,
        string Endereco,
        DateTimeOffset CriadoEm,
        int DistanciaMetros,
        FaixaConfianca? Faixa,
        int Confirmacoes);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.CategoriaId).GreaterThan((short)0);
            RuleFor(r => r.Lat).Latitude();
            RuleFor(r => r.Lng).Longitude();
        }
    }

    public static IEndpointRouteBuilder MapBuscarDuplicatas(this IEndpointRouteBuilder app)
    {
        app.MapGet("/problemas/duplicatas", Handle)
            .Validar<Request>()
            .WithTags("Problemas");
        return app;
    }

    /// <summary>Mesma categoria, não resolvidos, a até 50 m (<c>ST_DWithin</c>), do mais próximo ao mais distante.</summary>
    public static async Task<List<Response>> Handle(
        [AsParameters] Request request, AppDbContext db, ConfiguracaoConfianca cfg, CancellationToken ct)
    {
        var ponto = Geo.Ponto(request.Lat, request.Lng);

        var linhas = await db.Problemas
            .Where(p => p.CategoriaId == request.CategoriaId
                && p.Status != StatusProblema.Resolvida
                && p.Localizacao.IsWithinDistance(ponto, RaioMetros))
            .Select(p => new
            {
                p.Id,
                Categoria = new CategoriaResumo(p.Categoria.Id, p.Categoria.Nome, p.Categoria.Grupo, p.Categoria.Icone),
                p.EnderecoAproximado,
                p.CriadoEm,
                Distancia = p.Localizacao.Distance(ponto),
                p.Status,
                p.Confianca,
                Confirmacoes = p.Votos.Count(v => v.Tipo == TipoVoto.Existencia && v.Valor == ValorVoto.Positivo),
            })
            .OrderBy(p => p.Distancia)
            .Take(Limite)
            .AsNoTracking()
            .ToListAsync(ct);

        return linhas
            .Select(l => new Response(
                l.Id,
                l.Categoria,
                l.EnderecoAproximado,
                l.CriadoEm,
                (int)Math.Round(l.Distancia, MidpointRounding.AwayFromZero),
                ExibicaoConfianca.Calcular(l.Status, l.Confianca, cfg).Faixa,
                l.Confirmacoes))
            .ToList();
    }
}
