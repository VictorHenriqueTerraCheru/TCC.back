using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UrbanReports.Api.Persistence;

public sealed class VoteConfiguration : IEntityTypeConfiguration<Vote>
{
    public void Configure(EntityTypeBuilder<Vote> b)
    {
        b.ToTable("votes");

        b.HasOne(v => v.Issue).WithMany(p => p.Votes).HasForeignKey(v => v.IssueId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(v => v.User).WithMany().HasForeignKey(v => v.UserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(v => new { v.IssueId, v.UserId }).IsUnique();
        b.HasIndex(v => new { v.UserId, v.RegisteredAt });
    }
}
