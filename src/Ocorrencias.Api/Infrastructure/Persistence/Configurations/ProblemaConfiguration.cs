using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ocorrencias.Api.Persistence;

public sealed class ProblemaConfiguration : IEntityTypeConfiguration<Problema>
{
    public void Configure(EntityTypeBuilder<Problema> b)
    {
        b.ToTable("problemas");
        b.Property(p => p.Descricao).HasMaxLength(280);
        b.Property(p => p.Localizacao).HasColumnType("geography(Point,4326)");
        b.Property(p => p.EnderecoAproximado).HasMaxLength(200);
        b.Property(p => p.Confianca).HasPrecision(5, 2);

        b.HasOne(p => p.Categoria).WithMany().HasForeignKey(p => p.CategoriaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Autor).WithMany().HasForeignKey(p => p.AutorId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(p => p.Localizacao).HasMethod("gist");
        b.HasIndex(p => new { p.AutorId, p.CriadoEm });
        b.HasIndex(p => p.Status);
    }
}
