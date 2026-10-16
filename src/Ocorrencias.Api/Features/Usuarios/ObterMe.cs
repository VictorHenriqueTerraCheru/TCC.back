using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Features.Usuarios;

public static class ObterMe
{
    public sealed record Response(
        Guid Id,
        string Nome,
        string Email,
        Perfil Perfil,
        DateTimeOffset CriadoEm,
        DateTimeOffset? VerificadoEm,
        int TotalRegistros,
        int TotalVotos,
        VerificacaoResumo? Verificacao);

    /// <summary>Solicitação de verificação mais recente.</summary>
    public sealed record VerificacaoResumo(
        StatusVerificacao Status, TipoVinculo TipoVinculo, string? MotivoRecusa, DateTimeOffset CriadaEm);

    public static IEndpointRouteBuilder MapObterMe(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", Handle).WithTags("Usuários");
        return app;
    }

    public static async Task<Results<Ok<Response>, UnauthorizedHttpResult>> Handle(
        ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var resposta = await Consultar(db, principal.ObterUsuarioId(), ct);
        return resposta is null ? TypedResults.Unauthorized() : TypedResults.Ok(resposta);
    }

    /// <summary>Nulo se o usuário do token não existe mais.</summary>
    public static Task<Response?> Consultar(AppDbContext db, Guid usuarioId, CancellationToken ct) =>
        db.Users
            .Where(u => u.Id == usuarioId)
            .Select(u => new Response(
                u.Id,
                u.Nome,
                u.Email!,
                u.Perfil,
                u.CriadoEm,
                u.VerificadoEm,
                db.Problemas.Count(p => p.AutorId == u.Id) + db.Incidentes.Count(i => i.AutorId == u.Id),
                db.Votos.Count(v => v.UsuarioId == u.Id),
                db.SolicitacoesVerificacao
                    .Where(s => s.UsuarioId == u.Id)
                    .OrderByDescending(s => s.CriadaEm)
                    .Select(s => new VerificacaoResumo(s.Status, s.TipoVinculo, s.MotivoRecusa, s.CriadaEm))
                    .FirstOrDefault()))
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
}
