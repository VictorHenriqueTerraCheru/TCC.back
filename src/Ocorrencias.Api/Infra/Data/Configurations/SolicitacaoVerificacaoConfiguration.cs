using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocorrencias.Api.Infra.Data.Entidades;

namespace Ocorrencias.Api.Infra.Data.Configurations;

public sealed class SolicitacaoVerificacaoConfiguration : IEntityTypeConfiguration<SolicitacaoVerificacao>
{
    public void Configure(EntityTypeBuilder<SolicitacaoVerificacao> b)
    {
        b.ToTable("solicitacoes_verificacao");
        b.Property(s => s.Justificativa).HasMaxLength(500);
        b.Property(s => s.MotivoRecusa).HasMaxLength(500);

        b.HasOne(s => s.Usuario).WithMany().HasForeignKey(s => s.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(s => s.AnalisadaPor).WithMany().HasForeignKey(s => s.AnalisadaPorId).OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(s => new { s.UsuarioId, s.CriadaEm });
        b.HasIndex(s => new { s.Status, s.CriadaEm });
    }
}
