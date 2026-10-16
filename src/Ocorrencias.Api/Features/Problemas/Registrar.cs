using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Api.Geocodificacao;
using Ocorrencias.Domain;
using Ocorrencias.Domain.Confianca;

namespace Ocorrencias.Api.Features.Problemas;

public static class RegistrarProblema
{
    public sealed record Request(short CategoriaId, double Lat, double Lng, string? Descricao);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.CategoriaId).GreaterThan((short)0);
            RuleFor(r => r.Lat).Latitude();
            RuleFor(r => r.Lng).Longitude();
            RuleFor(r => r.Descricao).MaximumLength(280);
        }
    }

    public static IEndpointRouteBuilder MapRegistrarProblema(this IEndpointRouteBuilder app)
    {
        app.MapPost("/problemas", Handle)
            .Validar<Request>()
            .WithTags("Problemas");
        return app;
    }

    /// <summary>O endereço vem da geocodificação reversa no servidor (nunca do cliente) e nunca impede o registro.</summary>
    public static async Task<Results<Created<ObterDetalheProblema.Response>, ValidationProblem, ProblemHttpResult>> Handle(
        Request request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IServicoGeocodificacao geocodificacao,
        TimeProvider relogio,
        ConfiguracaoConfianca cfg,
        CancellationToken ct)
    {
        var categoria = await db.Categorias.FindAsync([request.CategoriaId], ct);
        if (categoria is null)
        {
            return Erros.Campo("categoriaId", "Categoria inexistente.");
        }

        if (categoria.Tipo != TipoOcorrencia.Problema)
        {
            return Erros.CategoriaNaoEProblema;
        }

        var endereco = await geocodificacao.ReversaAsync(request.Lat, request.Lng, ct);
        var agora = relogio.GetUtcNow();
        var autorId = principal.ObterUsuarioId();

        var problema = new Problema
        {
            Id = Guid.CreateVersion7(),
            CategoriaId = categoria.Id,
            AutorId = autorId,
            Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim(),
            Localizacao = Geo.Ponto(request.Lat, request.Lng),
            EnderecoAproximado = endereco.Endereco,
            Status = StatusProblema.Nova,
            CriadoEm = agora,
            StatusAlteradoEm = agora,
        };
        db.Problemas.Add(problema);
        await db.SaveChangesAsync(ct);

        var detalhe = await ObterDetalheProblema.ConsultarAsync(db, problema.Id, autorId, agora, cfg, ct);
        return TypedResults.Created($"/api/v1/problemas/{problema.Id}", detalhe);
    }
}
