using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocorrencias.Api.Infra.Data.Entidades;

namespace Ocorrencias.Api.Infra.Data.Configurations;

public sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("usuarios");
        b.Property(u => u.Nome).HasMaxLength(120);
        b.Property(u => u.PasswordHash).HasColumnName("senha_hash");

        // O Identity cria "EmailIndex" não único; aqui o e-mail é único também no banco.
        b.HasIndex(u => u.NormalizedEmail).IsUnique().HasDatabaseName("ix_usuarios_normalized_email");
        b.HasIndex(u => u.NormalizedUserName).IsUnique().HasDatabaseName("ix_usuarios_normalized_user_name");
    }
}
