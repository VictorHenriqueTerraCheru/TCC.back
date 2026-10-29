using UrbanReports.Api.Common;

namespace UrbanReports.Api.Admin;

public static class ErrosAdmin
{
    public static readonly ErroAplicacao VerificacaoJaAnalisada = ErroAplicacao.Regra(
        StatusCodes.Status409Conflict, "verificacao-ja-analisada", "Esta solicitação já foi analisada.");

    public static readonly ErroAplicacao ProprioPerfil = ErroAplicacao.Regra(
        StatusCodes.Status422UnprocessableEntity, "proprio-perfil", "Você não pode alterar o seu próprio perfil.");
}
