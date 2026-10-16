using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Admin;

public static class ErrosAdmin
{
    public static readonly ErroAplicacao VerificacaoJaAnalisada = ErroAplicacao.Regra(
        StatusCodes.Status409Conflict, "verificacao-ja-analisada", "Esta solicitação já foi analisada.");
}
