using UrbanReports.Api.Common;

namespace UrbanReports.Api.Admin;

public static class AdminErrors
{
    public static readonly ApplicationError VerificationAlreadyReviewed = ApplicationError.Rule(
        StatusCodes.Status409Conflict, "verification-already-reviewed", "Esta solicitação já foi analisada.");

    public static readonly ApplicationError OwnRole = ApplicationError.Rule(
        StatusCodes.Status422UnprocessableEntity, "own-role", "Você não pode alterar o seu próprio perfil.");
}
