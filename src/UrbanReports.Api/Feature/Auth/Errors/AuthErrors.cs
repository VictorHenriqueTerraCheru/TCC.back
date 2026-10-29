using UrbanReports.Api.Common;

namespace UrbanReports.Api.Auth;

public static class AuthErrors
{
    public static readonly ApplicationError EmailAlreadyRegistered =
        ApplicationError.Rule(StatusCodes.Status409Conflict, "email-already-registered", "Já existe uma conta com este e-mail.");

    public static readonly ApplicationError InvalidCredentials =
        ApplicationError.Rule(StatusCodes.Status401Unauthorized, null, "E-mail ou senha inválidos.");
}
