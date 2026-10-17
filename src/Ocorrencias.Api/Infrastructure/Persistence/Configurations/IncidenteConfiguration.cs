using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ocorrencias.Api.Persistence;

public sealed class IncidenteConfiguration : IEntityTypeConfiguration<Incidente>
{
    public void Configure(EntityTypeBuilder<Incidente> b)
    {
        b.ToTable("incidentes");
        b.Property(i => i.AreaAproximada).HasColumnType("geography(Point,4326)");
        b.Property(i => i.RotuloArea).HasMaxLength(120);

        b.HasOne(i => i.Categoria).WithMany().HasForeignKey(i => i.CategoriaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(i => i.Autor).WithMany().HasForeignKey(i => i.AutorId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(i => i.AreaAproximada).HasMethod("gist");
        b.HasIndex(i => i.OcorridoEm);
        b.HasIndex(i => new { i.AutorId, i.RegistradoEm }); // limite de 10 por 24 h
    }
}
