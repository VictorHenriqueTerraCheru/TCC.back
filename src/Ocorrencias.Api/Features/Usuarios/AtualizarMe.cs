using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Persistence;

namespace Ocorrencias.Api.Features.Usuarios;

public static class AtualizarMe
{
    public sealed record Request(string Nome);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.Nome).NotEmpty().MaximumLength(120);
        }
    }

    public static IEndpointRouteBuilder MapAtualizarMe(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/me", Handle)
            .Validar<Request>()
            .WithTags("Usuários");
        return app;
    }

    /// <summary>Responde com o mesmo corpo de <c>GET /me</c>.</summary>
    public static async Task<Results<Ok<ObterMe.Response>, UnauthorizedHttpResult>> Handle(
        Request request, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var usuarioId = principal.ObterUsuarioId();
        var alterados = await db.Users
            .Where(u => u.Id == usuarioId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Nome, request.Nome.Trim()), ct);

        if (alterados == 0)
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok((await ObterMe.Consultar(db, usuarioId, ct))!);
    }
}
