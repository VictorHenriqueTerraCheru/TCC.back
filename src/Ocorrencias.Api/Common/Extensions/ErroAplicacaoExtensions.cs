using Microsoft.AspNetCore.Http.HttpResults;

namespace Ocorrencias.Api.Common;

/// <summary>Tradução de <see cref="ErroAplicacao"/> para os mesmos resultados HTTP que os handlers devolviam antes.</summary>
public static class ErroAplicacaoExtensions
{
    public static ProblemHttpResult ParaProblema(this ErroAplicacao erro) =>
        TypedResults.Problem(statusCode: erro.Status, type: erro.Type, title: erro.Title);

    public static ValidationProblem ParaValidacao(this ErroAplicacao erro) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [erro.Campo!] = [erro.Title!] });

    public static NotFound ParaNaoEncontrado(this ErroAplicacao erro) => TypedResults.NotFound();

    public static UnauthorizedHttpResult ParaNaoAutorizado(this ErroAplicacao erro) => TypedResults.Unauthorized();
}
