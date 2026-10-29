using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Persistence;

public sealed class GrupoCategoriaConfiguration : IEntityTypeConfiguration<GrupoCategoria>
{
    public void Configure(EntityTypeBuilder<GrupoCategoria> b)
    {
        b.ToTable("grupos_categoria");
        b.HasKey(g => g.Chave);
        b.Property(g => g.Chave).HasMaxLength(30);
        b.Property(g => g.Rotulo).HasMaxLength(40);
        b.HasIndex(g => g.Rotulo).IsUnique();

        // Dado inicial de docs/API.md; novos grupos nascem pelo CRUD de admin.
        b.HasData(
            new GrupoCategoria { Chave = "viaPublica", Rotulo = "Via pública", Tipo = TipoOcorrencia.Problema, Cor = CorGrupo.Laranja, Ordem = 1 },
            new GrupoCategoria { Chave = "eletrica", Rotulo = "Elétrica", Tipo = TipoOcorrencia.Problema, Cor = CorGrupo.Ocre, Ordem = 2 },
            new GrupoCategoria { Chave = "limpeza", Rotulo = "Limpeza", Tipo = TipoOcorrencia.Problema, Cor = CorGrupo.Marrom, Ordem = 3 },
            new GrupoCategoria { Chave = "seguranca", Rotulo = "Segurança", Tipo = TipoOcorrencia.Incidente, Cor = CorGrupo.Vermelho, Ordem = 4 },
            new GrupoCategoria { Chave = "transito", Rotulo = "Trânsito", Tipo = TipoOcorrencia.Incidente, Cor = CorGrupo.Terracota, Ordem = 5 });
    }
}
