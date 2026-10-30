using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Persistence;

public sealed class VerificationRequestConfiguration : IEntityTypeConfiguration<VerificationRequest>
{
    public void Configure(EntityTypeBuilder<VerificationRequest> b)
    {
        b.ToTable("verification_requests");
        b.Property(s => s.Justification).HasMaxLength(500);
        b.Property(s => s.RejectionReason).HasMaxLength(500);

        b.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(s => s.ReviewedBy).WithMany().HasForeignKey(s => s.ReviewedById).OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(s => new { s.UserId, s.CreatedAt });
        b.HasIndex(s => new { s.Status, s.CreatedAt });

        b.HasIndex(s => s.UserId)
            .IsUnique()
            .HasFilter($"status = {(short)VerificationStatus.Pending}")
            .HasDatabaseName("ix_verification_requests_user_pending");
    }
}
