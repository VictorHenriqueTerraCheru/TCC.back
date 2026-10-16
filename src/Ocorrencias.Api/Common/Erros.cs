using Microsoft.AspNetCore.Http.HttpResults;

namespace Ocorrencias.Api.Common;

/// <summary>Erros de regra de negócio com <c>type</c> estável (tabela de docs/API.md).</summary>
public static class Erros
{
    public static ProblemHttpResult CategoriaNaoEProblema =>
        Problema(StatusCodes.Status422UnprocessableEntity, "categoria-nao-e-problema", "A categoria escolhida é de incidente, não de problema.");

    public static ProblemHttpResult AutorNaoVotaExistencia =>
        Problema(StatusCodes.Status422UnprocessableEntity, "autor-nao-vota-existencia", "Quem registrou o problema não vota na existência dele.");

    public static ProblemHttpResult PersistenciaIndisponivelEmNova =>
        Problema(StatusCodes.Status422UnprocessableEntity, "persistencia-indisponivel-em-nova", "\"Ainda está lá?\" só abre depois que o problema sai de Nova.");

    public static ProblemHttpResult ProblemaResolvido =>
        Problema(StatusCodes.Status422UnprocessableEntity, "problema-resolvido", "O problema já foi resolvido e não recebe mais votos.");

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
