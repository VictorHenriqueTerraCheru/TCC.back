using Microsoft.EntityFrameworkCore;
using Npgsql;
using UrbanReports.Api.Common;
using UrbanReports.Api.Persistence;

namespace UrbanReports.Api.Verification;

public sealed class VerificationService(AppDbContext db, TimeProvider clock) : IVerificationService
{
    public async Task<Result<RequestVerificationResponse>> RequestAsync(Guid userId, RequestVerificationRequest request, CancellationToken ct)
    {
        var role = await db.Users.Where(u => u.Id == userId).Select(u => (Role?)u.Role).SingleOrDefaultAsync(ct);
        if (role is null)
        {
            return ApplicationError.Unauthorized;
        }

        if (role != Role.Citizen)
        {
            return VerificationErrors.AlreadyVerified;
        }

        if (await db.VerificationRequests.AnyAsync(s => s.UserId == userId && s.Status == VerificationStatus.Pending, ct))
        {
            return VerificationErrors.VerificationAlreadyPending;
        }

        var verificationRequest = new VerificationRequest
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            AffiliationType = request.AffiliationType,
            Justification = request.Justification.Trim(),
            Status = VerificationStatus.Pending,
            CreatedAt = clock.GetUtcNow(),
        };
        db.VerificationRequests.Add(verificationRequest);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return VerificationErrors.VerificationAlreadyPending;
        }

        return new RequestVerificationResponse(
            verificationRequest.Id, verificationRequest.AffiliationType, verificationRequest.Justification, verificationRequest.Status, verificationRequest.CreatedAt);
    }
}
