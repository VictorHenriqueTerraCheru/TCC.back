using Microsoft.AspNetCore.Http.HttpResults;

namespace Ocorrencias.Api.Common;

/// <summary>Erros de regra de negócio com <c>type</c> estável (tabela de docs/API.md).</summary>
public static class Erros
{
    public static ProblemHttpResult CategoriaNaoEIncidente =>
        Problema(StatusCodes.Status422UnprocessableEntity, "categoria-nao-e-incidente", "A categoria escolhida é de problema, não de incidente.");

    public static ProblemHttpResult HorarioForaDaJanela =>
        Problema(StatusCodes.Status422UnprocessableEntity, "horario-fora-da-janela", "O horário do incidente precisa estar nas últimas 24 horas.");

    public static ProblemHttpResult LimiteDiarioIncidentes =>
        Problema(StatusCodes.Status429TooManyRequests, "limite-diario-incidentes", "Limite de 10 incidentes em 24 horas atingido.");

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
