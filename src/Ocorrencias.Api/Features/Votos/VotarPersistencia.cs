using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Features.Problemas;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain;
using Ocorrencias.Domain.Confianca;

namespace Ocorrencias.Api.Features.Votos;

public static class VotarPersistencia
{
    /// <summary><c>lat</c>/<c>lng</c> opcionais: sem localização, o peso de proximidade é 1.</summary>
    public sealed record Request(ValorPersistencia Valor, double? Lat, double? Lng);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.Valor).IsInEnum();
            Votacao.RegrasPosicao(this, r => r.Lat, r => r.Lng);
        }
    }

    public static IEndpointRouteBuilder MapVotarPersistencia(this IEndpointRouteBuilder app)
    {
        app.MapPut("/problemas/{id:guid}/votos/persistencia", Handle)
            .Validar<Request>()
            .WithTags("Votos");
        return app;
    }

    public static Task<Results<Ok<ObterDetalheProblema.Response>, NotFound, ProblemHttpResult, UnauthorizedHttpResult>> Handle(
        Guid id, Request request, ClaimsPrincipal principal, AppDbContext db, TimeProvider relogio, ConfiguracaoConfianca cfg, CancellationToken ct) =>
        Votacao.VotarAsync(db, id, principal.ObterUsuarioId(), TipoVoto.Persistencia, (ValorVoto)request.Valor, request.Lat, request.Lng, relogio, cfg, ct);
}
