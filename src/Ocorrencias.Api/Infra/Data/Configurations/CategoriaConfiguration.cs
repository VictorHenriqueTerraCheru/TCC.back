using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocorrencias.Api.Infra.Data.Entidades;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Infra.Data.Configurations;

public sealed class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> b)
    {
        b.ToTable("categorias");
        b.Property(c => c.Id).ValueGeneratedNever();
        b.Property(c => c.Nome).HasMaxLength(60);
        b.Property(c => c.Grupo).HasMaxLength(30);
        b.Property(c => c.Icone).HasMaxLength(40);

        // Seed fixo de docs/API.md.
        b.HasData(
            new Categoria { Id = 1, Nome = "Buraco na via", Grupo = "viaPublica", Tipo = TipoOcorrencia.Problema, Icone = "buraco" },
            new Categoria { Id = 2, Nome = "Calçada danificada", Grupo = "viaPublica", Tipo = TipoOcorrencia.Problema, Icone = "calcada" },
            new Categoria { Id = 3, Nome = "Poste apagado", Grupo = "eletrica", Tipo = TipoOcorrencia.Problema, Icone = "poste" },
            new Categoria { Id = 4, Nome = "Fio caído", Grupo = "eletrica", Tipo = TipoOcorrencia.Problema, Icone = "fio" },
            new Categoria { Id = 5, Nome = "Descarte irregular", Grupo = "limpeza", Tipo = TipoOcorrencia.Problema, Icone = "descarte" },
            new Categoria { Id = 6, Nome = "Lixo acumulado", Grupo = "limpeza", Tipo = TipoOcorrencia.Problema, Icone = "lixo" },
            new Categoria { Id = 7, Nome = "Roubo", Grupo = "seguranca", Tipo = TipoOcorrencia.Incidente, Icone = "roubo" },
            new Categoria { Id = 8, Nome = "Acidente de trânsito", Grupo = "transito", Tipo = TipoOcorrencia.Incidente, Icone = "acidente" });
    }
}
