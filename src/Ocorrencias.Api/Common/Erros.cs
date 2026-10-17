using Microsoft.AspNetCore.Http.HttpResults;

namespace Ocorrencias.Api.Common;

/// <summary>Erros de regra de negócio com <c>type</c> estável (tabela de docs/API.md).</summary>
public static class Erros
{
    public static ProblemHttpResult EmailJaCadastrado =>
        Problema(StatusCodes.Status409Conflict, "email-ja-cadastrado", "Já existe uma conta com este e-mail.");

    public static ProblemHttpResult CategoriaNaoEProblema =>
        Problema(StatusCodes.Status422UnprocessableEntity, "categoria-nao-e-problema", "A categoria escolhida é de incidente, não de problema.");

    public static ProblemHttpResult AutorNaoVotaExistencia =>
        Problema(StatusCodes.Status422UnprocessableEntity, "autor-nao-vota-existencia", "Quem registrou o problema não vota na existência dele.");

    public static ProblemHttpResult PersistenciaIndisponivelEmNova =>
        Problema(StatusCodes.Status422UnprocessableEntity, "persistencia-indisponivel-em-nova", "\"Ainda está lá?\" só abre depois que o problema sai de Nova.");

    public static ProblemHttpResult ProblemaResolvido =>
        Problema(StatusCodes.Status422UnprocessableEntity, "problema-resolvido", "O problema já foi resolvido e não recebe mais votos.");

    public static ValidationProblem Campo(string campo, string mensagem) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [campo] = [mensagem] });

    public static ProblemHttpResult CredenciaisInvalidas =>
        TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "E-mail ou senha inválidos.");

    public static ProblemHttpResult Problema(int status, string type, string title, string? detail = null) =>
        TypedResults.Problem(statusCode: status, type: type, title: title, detail: detail);
}
