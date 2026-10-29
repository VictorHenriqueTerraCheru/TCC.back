using Microsoft.AspNetCore.Http.HttpResults;

namespace UrbanReports.Api.Common;

public static class ErroAplicacaoExtensions
{
    public static ProblemHttpResult ParaProblema(this ErroAplicacao erro) =>
        TypedResults.Problem(statusCode: erro.Status, type: erro.Type, title: erro.Title);

    public static ValidationProblem ParaValidacao(this ErroAplicacao erro) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [erro.Campo!] = [erro.Title!] });

    public static NotFound ParaNaoEncontrado(this ErroAplicacao erro) => TypedResults.NotFound();

    public static UnauthorizedHttpResult ParaNaoAutorizado(this ErroAplicacao erro) => TypedResults.Unauthorized();
}
