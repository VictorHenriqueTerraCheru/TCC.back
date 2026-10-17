using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ocorrencias.Api.Persistence;

public sealed class VotoConfiguration : IEntityTypeConfiguration<Voto>
{
    public void Configure(EntityTypeBuilder<Voto> b)
    {
        b.ToTable("votos");

        b.HasOne(v => v.Problema).WithMany(p => p.Votos).HasForeignKey(v => v.ProblemaId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(v => v.Usuario).WithMany().HasForeignKey(v => v.UsuarioId).OnDelete(DeleteBehavior.Restrict);

        // Um voto por (problema, usuário, tipo): votar de novo faz UPSERT.
        b.HasIndex(v => new { v.ProblemaId, v.UsuarioId, v.Tipo }).IsUnique();
        b.HasIndex(v => new { v.UsuarioId, v.RegistradoEm });
    }
}
