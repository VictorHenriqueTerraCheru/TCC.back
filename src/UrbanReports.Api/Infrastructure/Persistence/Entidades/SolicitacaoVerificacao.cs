using UrbanReports.Api.Common;

namespace UrbanReports.Api.Persistence;

public sealed class VerificationRequest
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public AffiliationType AffiliationType { get; set; }
    public required string Justification { get; set; }
    public VerificationStatus Status { get; set; } = VerificationStatus.Pending;
    public string? RejectionReason { get; set; }
    public Guid? ReviewedById { get; set; }
    public User? ReviewedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}
