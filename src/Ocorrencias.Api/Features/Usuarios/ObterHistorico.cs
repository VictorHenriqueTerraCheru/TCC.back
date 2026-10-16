using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Claims;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Categorias;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Features.Usuarios;

public static class ObterHistorico
{
    public enum TipoItem
    {
        RegistroProblema = 1,
        RegistroIncidente = 2,
        VotoExistencia = 3,
        VotoPersistencia = 4,
    }

    public enum ValorItem
    {
        Confirma = 1,
        NaoVi = 2,
        Continua = 3,
        Resolvido = 4,
    }

    /// <param name="Filtro">tudo | registros | votos.</param>
    public sealed record Request(string Filtro = "tudo", int Pagina = 1, int Tamanho = Paginacao.TamanhoPadrao);

    /// <summary>
    /// Para <c>registroIncidente</c>, <c>endereco</c> é o rótulo da área (nulo se a geocodificação falhou) e
    /// <c>ocorrenciaId</c> é nulo: o incidente não é navegável.
    /// </summary>
    public sealed record Item(
        TipoItem Tipo,
        Guid? OcorrenciaId,
        CategoriaResumo Categoria,
        string? Endereco,
        ValorItem? Valor,
        StatusProblema? StatusProblema,
        DateTimeOffset Data);

    private static readonly string[] Filtros = ["tudo", "registros", "votos"];

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.Filtro).Must(f => Filtros.Contains(f)).WithMessage("Filtro deve ser tudo, registros ou votos.");
            RuleFor(r => r.Pagina).InclusiveBetween(1, Paginacao.PaginaMaxima);
            RuleFor(r => r.Tamanho).InclusiveBetween(1, Paginacao.TamanhoMaximo);
        }
    }

    public static IEndpointRouteBuilder MapObterHistorico(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me/historico", Handle)
            .Validar<Request>()
            .WithTags("Usuários");
        return app;
    }

    /// <summary>Linha crua da união (registros de problema, de incidente e votos).</summary>
    public sealed class Linha
    {
        /// <summary>Id da linha de origem (problema, incidente ou voto): desempate único da paginação, não exposto.</summary>
        [Column("chave")] public Guid Chave { get; init; }
        [Column("tipo")] public int Tipo { get; init; }
        [Column("ocorrencia_id")] public Guid? OcorrenciaId { get; init; }
        [Column("categoria_id")] public short CategoriaId { get; init; }
        [Column("categoria_nome")] public string CategoriaNome { get; init; } = "";
        [Column("grupo")] public string Grupo { get; init; } = "";
        [Column("icone")] public string Icone { get; init; } = "";
        [Column("endereco")] public string? Endereco { get; init; }
        [Column("valor")] public short? Valor { get; init; }
        [Column("status_problema")] public short? StatusProblema { get; init; }
        [Column("data")] public DateTimeOffset Data { get; init; }
    }

    /// <summary>Itens em ordem decrescente de data.</summary>
    public static async Task<Paginado<Item>> Handle(
        [AsParameters] Request request, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var usuarioId = principal.ObterUsuarioId();
        var registros = request.Filtro is "tudo" or "registros";
        var votos = request.Filtro is "tudo" or "votos";

        var uniao = db.Database.SqlQuery<Linha>($"""
            SELECT p.id AS chave, 1 AS tipo, p.id AS ocorrencia_id, c.id AS categoria_id, c.nome AS categoria_nome, c.grupo, c.icone,
                   p.endereco_aproximado AS endereco, NULL::smallint AS valor, p.status AS status_problema, p.criado_em AS data
            FROM problemas p JOIN categorias c ON c.id = p.categoria_id
            WHERE p.autor_id = {usuarioId} AND {registros}
            UNION ALL
            SELECT i.id, 2, NULL::uuid, c.id, c.nome, c.grupo, c.icone,
                   i.rotulo_area, NULL::smallint, NULL::smallint, i.registrado_em
            FROM incidentes i JOIN categorias c ON c.id = i.categoria_id
            WHERE i.autor_id = {usuarioId} AND {registros}
            UNION ALL
            SELECT v.id, CASE v.tipo WHEN 1 THEN 3 ELSE 4 END, p.id, c.id, c.nome, c.grupo, c.icone,
                   p.endereco_aproximado, v.valor, p.status, v.registrado_em
            FROM votos v JOIN problemas p ON p.id = v.problema_id JOIN categorias c ON c.id = p.categoria_id
            WHERE v.usuario_id = {usuarioId} AND {votos}
            """);

        var total = await uniao.CountAsync(ct);
        var linhas = await uniao
            .OrderByDescending(l => l.Data)
            .ThenBy(l => l.Chave)
            .Skip(Paginacao.Pular(request.Pagina, request.Tamanho))
            .Take(request.Tamanho)
            .ToListAsync(ct);

        return new Paginado<Item>(linhas.Select(Mapear).ToList(), request.Pagina, request.Tamanho, total);
    }

    private static Item Mapear(Linha l)
    {
        var tipo = (TipoItem)l.Tipo;
        ValorItem? valor = (tipo, (ValorVoto?)l.Valor) switch
        {
            (TipoItem.VotoExistencia, ValorVoto.Positivo) => ValorItem.Confirma,
            (TipoItem.VotoExistencia, ValorVoto.Negativo) => ValorItem.NaoVi,
            (TipoItem.VotoPersistencia, ValorVoto.Positivo) => ValorItem.Continua,
            (TipoItem.VotoPersistencia, ValorVoto.Negativo) => ValorItem.Resolvido,
            _ => null,
        };

        return new Item(
            tipo,
            l.OcorrenciaId,
            new CategoriaResumo(l.CategoriaId, l.CategoriaNome, l.Grupo, l.Icone),
            l.Endereco,
            valor,
            (StatusProblema?)l.StatusProblema,
            l.Data);
    }
}
