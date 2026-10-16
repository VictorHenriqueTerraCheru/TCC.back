using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Features.Categorias;
using Ocorrencias.Api.Features.Votos;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain;
using Ocorrencias.Domain.Confianca;

namespace Ocorrencias.Api.Features.Problemas;

public static class ObterDetalheProblema
{
    public sealed record Response(
        Guid Id,
        CategoriaResumo Categoria,
        string? Descricao,
        double Lat,
        double Lng,
        string Endereco,
        DateTimeOffset CriadoEm,
        StatusProblema Status,
        double? Confianca,
        FaixaConfianca? Faixa,
        Avaliacao? Avaliacao,
        ContagemExistencia Existencia,
        ContagemPersistencia Persistencia,
        MeusVotos? MeusVotos,
        bool SouAutor);

    /// <summary>Só em <c>nova</c>: quantas pessoas já votaram e quando a avaliação encerra.</summary>
    public sealed record Avaliacao(int Votantes, int Necessarios, DateTimeOffset EncerraEm);

    /// <summary>Contagens de pessoas (não pesos).</summary>
    public sealed record ContagemExistencia(int Confirmacoes, int NaoVi);

    public sealed record ContagemPersistencia(int Continua, int Resolvido, int ResolvidoUltimas24h);

    public sealed record MeusVotos(ValorExistencia? Existencia, ValorPersistencia? Persistencia);

    public static IEndpointRouteBuilder MapObterDetalheProblema(this IEndpointRouteBuilder app)
    {
        app.MapGet("/problemas/{id:guid}", Handle)
            .AllowAnonymous()
            .WithTags("Problemas");
        return app;
    }

    public static async Task<Results<Ok<Response>, NotFound>> Handle(
        Guid id, ClaimsPrincipal principal, AppDbContext db, TimeProvider relogio, ConfiguracaoConfianca cfg, CancellationToken ct)
    {
        var detalhe = await ConsultarAsync(db, id, principal.ObterUsuarioIdOuNulo(), relogio.GetUtcNow(), cfg, ct);
        return detalhe is null ? TypedResults.NotFound() : TypedResults.Ok(detalhe);
    }

    /// <param name="usuarioId">Nulo para visitante: <c>meusVotos</c> = null e <c>souAutor</c> = false.</param>
    public static async Task<Response?> ConsultarAsync(
        AppDbContext db, Guid id, Guid? usuarioId, DateTimeOffset agora, ConfiguracaoConfianca cfg, CancellationToken ct)
    {
        var limite24h = agora.AddHours(-24);

        var linha = await db.Problemas
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id,
                Categoria = new CategoriaResumo(p.Categoria.Id, p.Categoria.Nome, p.Categoria.Grupo, p.Categoria.Icone),
                p.Descricao,
                p.Localizacao,
                p.EnderecoAproximado,
                p.CriadoEm,
                p.Status,
                p.Confianca,
                p.AutorId,
                Votantes = p.Votos.Select(v => v.UsuarioId).Distinct().Count(),
                Confirmacoes = p.Votos.Count(v => v.Tipo == TipoVoto.Existencia && v.Valor == ValorVoto.Positivo),
                NaoVi = p.Votos.Count(v => v.Tipo == TipoVoto.Existencia && v.Valor == ValorVoto.Negativo),
                Continua = p.Votos.Count(v => v.Tipo == TipoVoto.Persistencia && v.Valor == ValorVoto.Positivo),
                Resolvido = p.Votos.Count(v => v.Tipo == TipoVoto.Persistencia && v.Valor == ValorVoto.Negativo),
                ResolvidoUltimas24h = p.Votos.Count(v =>
                    v.Tipo == TipoVoto.Persistencia && v.Valor == ValorVoto.Negativo && v.RegistradoEm >= limite24h),
                MeuExistencia = p.Votos
                    .Where(v => v.UsuarioId == usuarioId && v.Tipo == TipoVoto.Existencia)
                    .Select(v => (ValorVoto?)v.Valor)
                    .FirstOrDefault(),
                MeuPersistencia = p.Votos
                    .Where(v => v.UsuarioId == usuarioId && v.Tipo == TipoVoto.Persistencia)
                    .Select(v => (ValorVoto?)v.Valor)
                    .FirstOrDefault(),
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (linha is null)
        {
            return null;
        }

        var (confianca, faixa) = ExibicaoConfianca.Calcular(linha.Status, linha.Confianca, cfg);

        return new Response(
            linha.Id,
            linha.Categoria,
            linha.Descricao,
            linha.Localizacao.Y,
            linha.Localizacao.X,
            linha.EnderecoAproximado,
            linha.CriadoEm,
            linha.Status,
            confianca,
            faixa,
            linha.Status == StatusProblema.Nova
                ? new Avaliacao(linha.Votantes, cfg.VotantesParaSairDeNova, linha.CriadoEm + cfg.TempoMaximoEmNova)
                : null,
            new ContagemExistencia(linha.Confirmacoes, linha.NaoVi),
            new ContagemPersistencia(linha.Continua, linha.Resolvido, linha.ResolvidoUltimas24h),
            usuarioId is null
                ? null
                : new MeusVotos((ValorExistencia?)linha.MeuExistencia, (ValorPersistencia?)linha.MeuPersistencia),
            usuarioId is not null && linha.AutorId == usuarioId);
    }
}
