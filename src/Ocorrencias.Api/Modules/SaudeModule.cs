using Microsoft.AspNetCore.Http.HttpResults;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Saude;

namespace Ocorrencias.Api.Modules;

/// <summary>Health checks para o host e o monitoramento.</summary>
public sealed class SaudeModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/saude", Aplicacao)
            .AllowAnonymous()
            .WithTags("Saude");

        api.MapGet("/saude/banco", Banco)
            .AllowAnonymous()
            .WithTags("Saude")
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static Ok<StatusSaudeResponse> Aplicacao() => TypedResults.Ok(StatusSaudeResponse.Saudavel);

    private static async Task<Results<Ok<StatusSaudeResponse>, ProblemHttpResult>> Banco(ISaudeService service, CancellationToken ct)
    {
        var r = await service.VerificarBancoAsync(ct);
        return r.Erro switch
        {
            null => TypedResults.Ok(r.Valor!),
            var e => e.ParaProblema(),
        };
    }
}
