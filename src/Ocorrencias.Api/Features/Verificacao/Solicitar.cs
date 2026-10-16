using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Infra.Data;
using Ocorrencias.Api.Infra.Data.Entidades;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Features.Verificacao;

public static class SolicitarVerificacao
{
    public sealed record Request(TipoVinculo TipoVinculo, string Justificativa);

    public sealed record Response(Guid Id, TipoVinculo TipoVinculo, string Justificativa, StatusVerificacao Status, DateTimeOffset CriadaEm);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.TipoVinculo).IsInEnum();
            RuleFor(r => r.Justificativa)
                .NotEmpty()
                .Must(j => j.Trim().Length is >= 30 and <= 500)
                .WithMessage("A justificativa precisa ter entre 30 e 500 caracteres.");
        }
    }

    public static IEndpointRouteBuilder MapSolicitarVerificacao(this IEndpointRouteBuilder app)
    {
        app.MapPost("/verificacoes", Handle)
            .Validar<Request>()
            .WithTags("Verificação");
        return app;
    }

    public static async Task<Results<Created<Response>, ProblemHttpResult, UnauthorizedHttpResult>> Handle(
        Request request, ClaimsPrincipal principal, AppDbContext db, TimeProvider relogio, CancellationToken ct)
    {
        var usuarioId = principal.ObterUsuarioId();
        var perfil = await db.Users.Where(u => u.Id == usuarioId).Select(u => (Perfil?)u.Perfil).SingleOrDefaultAsync(ct);
        if (perfil is null)
        {
            return TypedResults.Unauthorized();
        }

        if (perfil != Perfil.Cidadao)
        {
            return Erros.JaVerificado;
        }

        if (await db.SolicitacoesVerificacao.AnyAsync(s => s.UsuarioId == usuarioId && s.Status == StatusVerificacao.Pendente, ct))
        {
            return Erros.VerificacaoPendenteExistente;
        }

        var solicitacao = new SolicitacaoVerificacao
        {
            Id = Guid.CreateVersion7(),
            UsuarioId = usuarioId,
            TipoVinculo = request.TipoVinculo,
            Justificativa = request.Justificativa.Trim(),
            Status = StatusVerificacao.Pendente,
            CriadaEm = relogio.GetUtcNow(),
        };
        db.SolicitacoesVerificacao.Add(solicitacao);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Corrida entre a checagem e o INSERT: o índice único parcial barra a segunda pendente.
            return Erros.VerificacaoPendenteExistente;
        }

        return TypedResults.Created(
            "/api/v1/me",
            new Response(solicitacao.Id, solicitacao.TipoVinculo, solicitacao.Justificativa, solicitacao.Status, solicitacao.CriadaEm));
    }
}
