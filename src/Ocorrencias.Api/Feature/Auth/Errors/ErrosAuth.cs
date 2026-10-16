using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Auth;

public static class ErrosAuth
{
    public static readonly ErroAplicacao EmailJaCadastrado =
        ErroAplicacao.Regra(StatusCodes.Status409Conflict, "email-ja-cadastrado", "Já existe uma conta com este e-mail.");

    public static readonly ErroAplicacao CredenciaisInvalidas =
        ErroAplicacao.Regra(StatusCodes.Status401Unauthorized, null, "E-mail ou senha inválidos.");
}
