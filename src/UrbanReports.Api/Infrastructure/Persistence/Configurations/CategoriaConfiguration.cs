using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Persistence;

public sealed class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> b)
    {
        b.ToTable("categorias");
        b.Property(c => c.Id).ValueGeneratedNever();
        b.Property(c => c.Nome).HasMaxLength(60);
        b.Property(c => c.Grupo).HasMaxLength(30);
        b.Property(c => c.Icone).HasMaxLength(40);
        b.Property(c => c.Ativa).HasDefaultValue(true);
        b.HasIndex(c => c.Nome).IsUnique();
        b.HasOne<GrupoCategoria>().WithMany().HasForeignKey(c => c.Grupo).OnDelete(DeleteBehavior.Restrict);

        // Dado inicial de docs/API.md; novas categorias nascem pelo CRUD de admin.
        b.HasData(
            new Categoria { Id = 1, Nome = "Buraco na via", Grupo = "viaPublica", Tipo = TipoOcorrencia.Problema, Icone = "buraco", Ativa = true },
            new Categoria { Id = 2, Nome = "Calçada danificada", Grupo = "viaPublica", Tipo = TipoOcorrencia.Problema, Icone = "calcada", Ativa = true },
            new Categoria { Id = 3, Nome = "Poste apagado", Grupo = "eletrica", Tipo = TipoOcorrencia.Problema, Icone = "poste", Ativa = true },
            new Categoria { Id = 4, Nome = "Fio caído", Grupo = "eletrica", Tipo = TipoOcorrencia.Problema, Icone = "fio", Ativa = true },
            new Categoria { Id = 5, Nome = "Descarte irregular", Grupo = "limpeza", Tipo = TipoOcorrencia.Problema, Icone = "descarte", Ativa = true },
            new Categoria { Id = 6, Nome = "Lixo acumulado", Grupo = "limpeza", Tipo = TipoOcorrencia.Problema, Icone = "lixo", Ativa = true },
            new Categoria { Id = 7, Nome = "Roubo", Grupo = "seguranca", Tipo = TipoOcorrencia.Incidente, Icone = "roubo", Ativa = true },
            new Categoria { Id = 8, Nome = "Acidente de trânsito", Grupo = "transito", Tipo = TipoOcorrencia.Incidente, Icone = "acidente", Ativa = true });
    }
}
