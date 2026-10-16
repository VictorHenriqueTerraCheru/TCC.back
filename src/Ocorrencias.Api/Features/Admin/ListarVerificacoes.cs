using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Infra.Auth;
using Ocorrencias.Api.Infra.Data;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Features.Admin;

public static class ListarVerificacoes
{
    /// <param name="Status">pendente | aprovada | recusada. Vazio = todas.</param>
    public sealed record Request(string? Status = null, int Pagina = 1, int Tamanho = Paginacao.TamanhoPadrao);

    public sealed record Item(
        Guid Id,
        UsuarioResumo Usuario,
        TipoVinculo TipoVinculo,
        string Justificativa,
        StatusVerificacao Status,
        DateTimeOffset CriadaEm,
        string? MotivoRecusa);

    public sealed record UsuarioResumo(Guid Id, string Nome, string Iniciais);

    private static readonly HashSet<string> NomesStatus = new(Enum.GetNames<StatusVerificacao>(), StringComparer.OrdinalIgnoreCase);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.Status)
                .Must(s => s is null || NomesStatus.Contains(s))
                .WithMessage("Status desconhecido. Valores aceitos: pendente, aprovada, recusada.");
            RuleFor(r => r.Pagina).InclusiveBetween(1, Paginacao.PaginaMaxima);
            RuleFor(r => r.Tamanho).InclusiveBetween(1, Paginacao.TamanhoMaximo);
        }
    }

    public static IEndpointRouteBuilder MapListarVerificacoes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/verificacoes", Handle)
            .Validar<Request>()
            .RequireAuthorization(AuthConfig.Administrador)
            .WithTags("Admin");
        return app;
    }

    /// <summary>Pendentes da mais antiga para a mais nova (fila de análise); demais, da mais nova para a mais antiga.</summary>
    public static async Task<Paginado<Item>> Handle([AsParameters] Request request, AppDbContext db, CancellationToken ct)
    {
        var consulta = db.SolicitacoesVerificacao.AsQueryable();
        StatusVerificacao? status = request.Status is null ? null : Enum.Parse<StatusVerificacao>(request.Status, ignoreCase: true);
        if (status is not null)
        {
            consulta = consulta.Where(s => s.Status == status);
        }

        var total = await consulta.CountAsync(ct);
        var ordenada = status == StatusVerificacao.Pendente
            ? consulta.OrderBy(s => s.CriadaEm)
            : consulta.OrderByDescending(s => s.CriadaEm);

        var linhas = await ordenada
            .ThenBy(s => s.Id)
            .Skip(Paginacao.Pular(request.Pagina, request.Tamanho))
            .Take(request.Tamanho)
            .Select(s => new
            {
                s.Id,
                s.UsuarioId,
                s.Usuario.Nome,
                s.TipoVinculo,
                s.Justificativa,
                s.Status,
                s.CriadaEm,
                s.MotivoRecusa,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var itens = linhas
            .Select(l => new Item(
                l.Id, new UsuarioResumo(l.UsuarioId, l.Nome, Iniciais(l.Nome)),
                l.TipoVinculo, l.Justificativa, l.Status, l.CriadaEm, l.MotivoRecusa))
            .ToList();

        return new Paginado<Item>(itens, request.Pagina, request.Tamanho, total);
    }

    /// <summary>"Carlos Menezes" → "CM"; "Ana" → "A"; "maria da silva" → "MS".</summary>
    public static string Iniciais(string nome)
    {
        var partes = nome.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return partes.Length switch
        {
            0 => "",
            1 => char.ToUpperInvariant(partes[0][0]).ToString(),
            _ => string.Concat(char.ToUpperInvariant(partes[0][0]), char.ToUpperInvariant(partes[^1][0])),
        };
    }
}
