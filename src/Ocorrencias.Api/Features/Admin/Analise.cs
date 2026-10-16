using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Features.Admin;

/// <summary>Transição única Pendente → Aprovada/Recusada, comum a aprovar e recusar.</summary>
internal static class Analise
{
    /// <summary>
    /// UPDATE condicional (<c>WHERE status = pendente</c>): duas análises simultâneas da mesma solicitação
    /// não passam ambas. Retorna o id do solicitante, ou o erro adequado.
    /// </summary>
    public static async Task<(Guid? UsuarioId, Results<NoContent, NotFound, ProblemHttpResult>? Erro)> ConcluirAsync(
        AppDbContext db, Guid id, ClaimsPrincipal admin, StatusVerificacao resultado, string? motivo, DateTimeOffset agora, CancellationToken ct)
    {
        var adminId = admin.ObterUsuarioId();
        var alteradas = await db.SolicitacoesVerificacao
            .Where(s => s.Id == id && s.Status == StatusVerificacao.Pendente)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, resultado)
                .SetProperty(x => x.MotivoRecusa, motivo)
                .SetProperty(x => x.AnalisadaPorId, adminId)
                .SetProperty(x => x.AnalisadaEm, agora), ct);

        if (alteradas == 1)
        {
            var usuarioId = await db.SolicitacoesVerificacao.Where(s => s.Id == id).Select(s => s.UsuarioId).SingleAsync(ct);
            return (usuarioId, null);
        }

        if (await db.SolicitacoesVerificacao.AnyAsync(s => s.Id == id, ct))
        {
            return (null, Erros.VerificacaoJaAnalisada);
        }

        return (null, TypedResults.NotFound());
    }
}
