using UrbanReports.Api.Common;

namespace UrbanReports.Api.Verification;

public interface IVerificationService
{
    Task<Result<RequestVerificationResponse>> RequestAsync(Guid userId, RequestVerificationRequest request, CancellationToken ct);
}
