using FluentValidation;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Infra.Geocodificacao;

namespace Ocorrencias.Api.Features.Geocodificacao;

public static class Reversa
{
    public sealed record Request(double Lat, double Lng);

    public sealed record Response(
        bool Disponivel,
        string Endereco,
        string? Logradouro,
        string? Numero,
        string? Bairro,
        string? Cidade,
        string? Uf,
        string? RotuloArea);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.Lat).InclusiveBetween(-90, 90);
            RuleFor(r => r.Lng).InclusiveBetween(-180, 180);
        }
    }

    public static IEndpointRouteBuilder MapReversa(this IEndpointRouteBuilder app)
    {
        app.MapGet("/geocodificacao/reversa", Handle)
            .Validar<Request>()
            .RequireRateLimiting(LimitesDeRequisicao.Geocodificacao)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous()
            .WithTags("Geocodificação");
        return app;
    }

    /// <summary>Sempre 200: com o Nominatim fora do ar e sem cache, vem <c>disponivel: false</c>.</summary>
    public static async Task<Response> Handle([AsParameters] Request request, IServicoGeocodificacao geocodificacao, CancellationToken ct)
    {
        var e = await geocodificacao.ReversaAsync(request.Lat, request.Lng, ct);
        return new Response(e.Disponivel, e.Endereco, e.Logradouro, e.Numero, e.Bairro, e.Cidade, e.Uf, e.RotuloArea);
    }
}
