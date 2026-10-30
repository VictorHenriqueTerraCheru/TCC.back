using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UrbanReports.Api.Persistence;

public sealed class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> b)
    {
        b.ToTable("issues");
        b.Property(issue => issue.Description).HasMaxLength(280);
        b.Property(issue => issue.Location).HasColumnType("geography(Point,4326)");
        b.Property(issue => issue.ApproximateAddress).HasMaxLength(200);
        b.Property(issue => issue.TrustScore).HasPrecision(5, 2);

        b.HasOne(issue => issue.Category).WithMany().HasForeignKey(issue => issue.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(issue => issue.Author).WithMany().HasForeignKey(issue => issue.AuthorId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(issue => issue.Location).HasMethod("gist");
        b.HasIndex(issue => new { issue.AuthorId, issue.CreatedAt });
        b.HasIndex(issue => issue.Status);
    }
}
