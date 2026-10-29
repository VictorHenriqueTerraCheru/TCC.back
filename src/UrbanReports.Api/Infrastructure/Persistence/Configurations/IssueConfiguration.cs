using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UrbanReports.Api.Persistence;

public sealed class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> b)
    {
        b.ToTable("issues");
        b.Property(p => p.Description).HasMaxLength(280);
        b.Property(p => p.Location).HasColumnType("geography(Point,4326)");
        b.Property(p => p.ApproximateAddress).HasMaxLength(200);
        b.Property(p => p.TrustScore).HasPrecision(5, 2);

        b.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Author).WithMany().HasForeignKey(p => p.AuthorId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(p => p.Location).HasMethod("gist");
        b.HasIndex(p => new { p.AuthorId, p.CreatedAt });
        b.HasIndex(p => p.Status);
    }
}
