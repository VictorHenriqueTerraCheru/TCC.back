using UrbanReports.Api.Common;
using UrbanReports.Api.Geocodificacao;

namespace UrbanReports.Api.Modules;

public sealed class GeocodificacaoModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/geocodificacao/reversa", Reversa)
            .Validar<ReversaRequest>()
            .RequireRateLimiting(PoliticasLimite.Geocodificacao)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous()
            .WithTags("Geocodificação");
    }

    private static async Task<ReversaResponse> Reversa(
        [AsParameters] ReversaRequest request, IServicoGeocodificacao geocodificacao, CancellationToken ct)
    {
        var e = await geocodificacao.ReversaAsync(request.Lat, request.Lng, ct);
        return new ReversaResponse(e.Disponivel, e.Endereco, e.Logradouro, e.Numero, e.Bairro, e.Cidade, e.Uf, e.RotuloArea);
    }
}
