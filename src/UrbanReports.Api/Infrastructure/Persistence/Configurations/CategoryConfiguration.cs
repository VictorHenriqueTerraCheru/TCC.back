using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Persistence;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("categories");
        b.Property(c => c.Id).ValueGeneratedNever();
        b.Property(c => c.Name).HasMaxLength(60);
        b.Property(c => c.Group).HasMaxLength(30);
        b.Property(c => c.Icon).HasMaxLength(40);
        b.Property(c => c.Active).HasDefaultValue(true);
        b.HasIndex(c => c.Name).IsUnique();
        b.HasOne<CategoryGroup>().WithMany().HasForeignKey(c => c.Group).OnDelete(DeleteBehavior.Restrict);

        b.HasData(
            new Category { Id = 1, Name = "Buraco na via", Group = "viaPublica", Type = ReportType.Issue, Icon = "buraco", Active = true },
            new Category { Id = 2, Name = "Calçada danificada", Group = "viaPublica", Type = ReportType.Issue, Icon = "calcada", Active = true },
            new Category { Id = 3, Name = "Poste apagado", Group = "eletrica", Type = ReportType.Issue, Icon = "poste", Active = true },
            new Category { Id = 4, Name = "Fio caído", Group = "eletrica", Type = ReportType.Issue, Icon = "fio", Active = true },
            new Category { Id = 5, Name = "Descarte irregular", Group = "limpeza", Type = ReportType.Issue, Icon = "descarte", Active = true },
            new Category { Id = 6, Name = "Lixo acumulado", Group = "limpeza", Type = ReportType.Issue, Icon = "lixo", Active = true },
            new Category { Id = 7, Name = "Roubo", Group = "seguranca", Type = ReportType.Incident, Icon = "roubo", Active = true },
            new Category { Id = 8, Name = "Acidente de trânsito", Group = "transito", Type = ReportType.Incident, Icon = "acidente", Active = true });
    }
}
