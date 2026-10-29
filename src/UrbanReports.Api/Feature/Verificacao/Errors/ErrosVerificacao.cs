using UrbanReports.Api.Common;

namespace UrbanReports.Api.Verificacao;

public static class ErrosVerificacao
{
    public static readonly ErroAplicacao VerificacaoPendenteExistente = ErroAplicacao.Regra(
        StatusCodes.Status409Conflict, "verificacao-pendente-existente", "Você já tem uma solicitação de verificação em análise.");

    public static readonly ErroAplicacao JaVerificado = ErroAplicacao.Regra(
        StatusCodes.Status422UnprocessableEntity, "ja-verificado", "Sua conta já é verificada.");
}
