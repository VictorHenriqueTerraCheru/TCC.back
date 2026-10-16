using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Infrastructure;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Features.Admin;

public static class AprovarVerificacao
{
    public static IEndpointRouteBuilder MapAprovarVerificacao(this IEndpointRouteBuilder app)
    {
        app.MapPost("/admin/verificacoes/{id:guid}/aprovar", Handle)
            .RequireAuthorization(AuthConfig.Administrador)
            .WithTags("Admin");
        return app;
    }

    /// <summary>
    /// Perfil do solicitante → <c>cidadaoVerificado</c>. Vale para os votos SEGUINTES: os anteriores mantêm o
    /// <c>perfil_no_momento</c> gravado.
    /// </summary>
    public static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Handle(
        Guid id, ClaimsPrincipal principal, AppDbContext db, TimeProvider relogio, CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();
        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        var (usuarioId, erro) = await Analise.ConcluirAsync(db, id, principal, StatusVerificacao.Aprovada, null, agora, ct);
        if (erro is not null)
        {
            return erro;
        }

        // Administrador não é rebaixado a verificado.
        await db.Users
            .Where(u => u.Id == usuarioId && u.Perfil == Perfil.Cidadao)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Perfil, Perfil.CidadaoVerificado)
                .SetProperty(u => u.VerificadoEm, agora), ct);

        await transacao.CommitAsync(ct);
        return TypedResults.NoContent();
    }
}
