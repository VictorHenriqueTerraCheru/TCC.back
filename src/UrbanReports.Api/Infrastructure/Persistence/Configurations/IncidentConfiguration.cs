using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UrbanReports.Api.Persistence;

public sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> b)
    {
        b.ToTable("incidents");
        b.Property(i => i.ApproximateArea).HasColumnType("geography(Point,4326)");
        b.Property(i => i.AreaLabel).HasMaxLength(120);

        b.HasOne(i => i.Category).WithMany().HasForeignKey(i => i.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(i => i.Author).WithMany().HasForeignKey(i => i.AuthorId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(i => i.ApproximateArea).HasMethod("gist");
        b.HasIndex(i => i.OccurredAt);
        b.HasIndex(i => new { i.AuthorId, i.RegisteredAt });
    }
}
