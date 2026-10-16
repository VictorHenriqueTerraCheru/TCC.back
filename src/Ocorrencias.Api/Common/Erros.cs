using Microsoft.AspNetCore.Http.HttpResults;

namespace Ocorrencias.Api.Common;

/// <summary>Erros de regra de negócio com <c>type</c> estável (tabela de docs/API.md).</summary>
public static class Erros
{
    public static ProblemHttpResult EmailJaCadastrado =>
        Problema(StatusCodes.Status409Conflict, "email-ja-cadastrado", "Já existe uma conta com este e-mail.");

    public static ProblemHttpResult CredenciaisInvalidas =>
        TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "E-mail ou senha inválidos.");

    public static ProblemHttpResult Problema(int status, string type, string title, string? detail = null) =>
        TypedResults.Problem(statusCode: status, type: type, title: title, detail: detail);
}
