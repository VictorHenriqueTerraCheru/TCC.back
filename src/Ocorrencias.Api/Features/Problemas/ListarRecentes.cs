using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Features.Categorias;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain;
using Ocorrencias.Domain.Confianca;

namespace Ocorrencias.Api.Features.Problemas;

public static class ListarProblemasRecentes
{
    public const double RaioMaximoKm = 50;

    public sealed record Request(
        double Lat,
        double Lng,
        double RaioKm = 10,
        string? Grupos = null,
        int Pagina = 1,
        int Tamanho = Paginacao.TamanhoPadrao);

    public sealed record Item(
        Guid Id,
        CategoriaResumo Categoria,
        string Endereco,
        DateTimeOffset CriadoEm,
        StatusProblema Status,
        FaixaConfianca? Faixa);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.Lat).Latitude();
            RuleFor(r => r.Lng).Longitude();
            RuleFor(r => r.RaioKm).GreaterThan(0).LessThanOrEqualTo(RaioMaximoKm);
            RuleFor(r => r.Grupos).ListaDeGrupos();
            RuleFor(r => r.Pagina).InclusiveBetween(1, Paginacao.PaginaMaxima);
            RuleFor(r => r.Tamanho).InclusiveBetween(1, Paginacao.TamanhoMaximo);
        }
    }

    public static IEndpointRouteBuilder MapListarProblemasRecentes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/problemas/recentes", Handle)
            .Validar<Request>()
            .AllowAnonymous()
            .WithTags("Problemas");
        return app;
    }

    /// <summary>Problemas não resolvidos no raio, mais recentes primeiro.</summary>
    public static async Task<Paginado<Item>> Handle(
        [AsParameters] Request request, AppDbContext db, ConfiguracaoConfianca cfg, CancellationToken ct)
    {
        var ponto = Geo.Ponto(request.Lat, request.Lng);
        var grupos = Listas.Separar(request.Grupos);

        var consulta = db.Problemas.Where(p =>
            p.Status != StatusProblema.Resolvida && p.Localizacao.IsWithinDistance(ponto, request.RaioKm * 1000));
        if (grupos.Length > 0)
        {
            consulta = consulta.Where(p => grupos.Contains(p.Categoria.Grupo));
        }

        var total = await consulta.CountAsync(ct);
        var linhas = await consulta
            .OrderByDescending(p => p.CriadoEm)
            .ThenBy(p => p.Id)
            .Skip(Paginacao.Pular(request.Pagina, request.Tamanho))
            .Take(request.Tamanho)
            .Select(p => new
            {
                p.Id,
                Categoria = new CategoriaResumo(p.Categoria.Id, p.Categoria.Nome, p.Categoria.Grupo, p.Categoria.Icone),
                p.EnderecoAproximado,
                p.CriadoEm,
                p.Status,
                p.Confianca,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var itens = linhas
            .Select(l => new Item(
                l.Id, l.Categoria, l.EnderecoAproximado, l.CriadoEm, l.Status,
                ExibicaoConfianca.Calcular(l.Status, l.Confianca, cfg).Faixa))
            .ToList();

        return new Paginado<Item>(itens, request.Pagina, request.Tamanho, total);
    }
}
