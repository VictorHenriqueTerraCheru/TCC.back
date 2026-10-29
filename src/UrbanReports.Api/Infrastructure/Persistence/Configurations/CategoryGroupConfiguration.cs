using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Persistence;

public sealed class CategoryGroupConfiguration : IEntityTypeConfiguration<CategoryGroup>
{
    public void Configure(EntityTypeBuilder<CategoryGroup> b)
    {
        b.ToTable("category_groups");
        b.HasKey(g => g.Key);
        b.Property(g => g.Key).HasMaxLength(30);
        b.Property(g => g.Label).HasMaxLength(40);
        b.HasIndex(g => g.Label).IsUnique();

        b.HasData(
            new CategoryGroup { Key = "viaPublica", Label = "Via pública", Type = ReportType.Issue, Color = GroupColor.Laranja, SortOrder = 1 },
            new CategoryGroup { Key = "eletrica", Label = "Elétrica", Type = ReportType.Issue, Color = GroupColor.Ochre, SortOrder = 2 },
            new CategoryGroup { Key = "limpeza", Label = "Limpeza", Type = ReportType.Issue, Color = GroupColor.Brown, SortOrder = 3 },
            new CategoryGroup { Key = "seguranca", Label = "Segurança", Type = ReportType.Incident, Color = GroupColor.Red, SortOrder = 4 },
            new CategoryGroup { Key = "transito", Label = "Trânsito", Type = ReportType.Incident, Color = GroupColor.Terracotta, SortOrder = 5 });
    }
}
