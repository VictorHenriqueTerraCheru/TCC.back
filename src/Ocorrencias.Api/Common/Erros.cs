using Microsoft.AspNetCore.Http.HttpResults;

namespace Ocorrencias.Api.Common;

/// <summary>Erros de regra de negócio com <c>type</c> estável (tabela de docs/API.md).</summary>
public static class Erros
{
    public static ProblemHttpResult VerificacaoPendenteExistente =>
        Problema(StatusCodes.Status409Conflict, "verificacao-pendente-existente", "Você já tem uma solicitação de verificação em análise.");

    public static ProblemHttpResult JaVerificado =>
        Problema(StatusCodes.Status422UnprocessableEntity, "ja-verificado", "Sua conta já é verificada.");

    public static ProblemHttpResult VerificacaoJaAnalisada =>
        Problema(StatusCodes.Status409Conflict, "verificacao-ja-analisada", "Esta solicitação já foi analisada.");

    public static ValidationProblem Campo(string campo, string mensagem) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [campo] = [mensagem] });

    public static ProblemHttpResult Problema(int status, string type, string title, string? detail = null) =>
        TypedResults.Problem(statusCode: status, type: type, title: title, detail: detail);
}
