using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UrbanReports.Api.Persistence;

public sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("usuarios");
        b.Property(u => u.Nome).HasMaxLength(120);
        b.Property(u => u.PasswordHash).HasColumnName("senha_hash");

        b.HasIndex(u => u.NormalizedEmail).IsUnique().HasDatabaseName("ix_usuarios_normalized_email");
        b.HasIndex(u => u.NormalizedUserName).IsUnique().HasDatabaseName("ix_usuarios_normalized_user_name");
    }
}
