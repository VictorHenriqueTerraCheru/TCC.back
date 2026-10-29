using UrbanReports.Api.Common;

namespace UrbanReports.Api.Verification;

public sealed record RequestVerificationRequest(AffiliationType AffiliationType, string Justification);

public sealed record RequestVerificationResponse(
    Guid Id, AffiliationType AffiliationType, string Justification, VerificationStatus Status, DateTimeOffset CreatedAt);
