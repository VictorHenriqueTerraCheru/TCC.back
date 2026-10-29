using UrbanReports.Api.Common;

namespace UrbanReports.Api.Verification;

public static class VerificationErrors
{
    public static readonly ApplicationError VerificationAlreadyPending = ApplicationError.Rule(
        StatusCodes.Status409Conflict, "verification-already-pending", "Você já tem uma solicitação de verificação em análise.");

    public static readonly ApplicationError AlreadyVerified = ApplicationError.Rule(
        StatusCodes.Status422UnprocessableEntity, "already-verified", "Sua conta já é verificada.");
}
