using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Infrastructure;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Features.Admin;

public static class RecusarVerificacao
{
    public sealed record Request(string Motivo);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.Motivo).NotEmpty().MaximumLength(500);
        }
    }

    public static IEndpointRouteBuilder MapRecusarVerificacao(this IEndpointRouteBuilder app)
    {
        app.MapPost("/admin/verificacoes/{id:guid}/recusar", Handle)
            .Validar<Request>()
            .RequireAuthorization(AuthConfig.Administrador)
            .WithTags("Admin");
        return app;
    }

    public static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Handle(
        Guid id, Request request, ClaimsPrincipal principal, AppDbContext db, TimeProvider relogio, CancellationToken ct)
    {
        var (_, erro) = await Analise.ConcluirAsync(
            db, id, principal, StatusVerificacao.Recusada, request.Motivo.Trim(), relogio.GetUtcNow(), ct);

        return erro ?? TypedResults.NoContent();
    }
}
