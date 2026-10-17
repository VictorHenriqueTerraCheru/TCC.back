using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Infra.Data.Entidades;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Infra.Data;

/// <summary>
/// Herda de <see cref="IdentityUserContext{TUser, TKey}"/> (sem tabelas de roles): o perfil é coluna de <c>usuarios</c>.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityUserContext<Usuario, Guid>(options)
{
    public DbSet<SolicitacaoVerificacao> SolicitacoesVerificacao => Set<SolicitacaoVerificacao>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Problema> Problemas => Set<Problema>();
    public DbSet<Voto> Votos => Set<Voto>();
    public DbSet<Incidente> Incidentes => Set<Incidente>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("postgis");

        builder.Entity<IdentityUserClaim<Guid>>().ToTable("usuarios_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("usuarios_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("usuarios_tokens");

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        RenomearChavesEstrangeirasParaUsuarios(builder);
    }

    /// <summary>
    /// O nome das FKs para <c>usuarios</c> é calculado pela convenção antes do <c>ToTable</c> do Identity ser
    /// sobrescrito ("asp_net_users"/"users"); aqui ele é recalculado com o nome final da tabela.
    /// </summary>
    private static void RenomearChavesEstrangeirasParaUsuarios(ModelBuilder builder)
    {
        var usuario = builder.Model.FindEntityType(typeof(Usuario))!;
        var chaves = builder.Model.GetEntityTypes()
            .SelectMany(e => e.GetDeclaredForeignKeys())
            .Where(fk => fk.PrincipalEntityType == usuario);

        foreach (var fk in chaves)
        {
            var colunas = string.Join("_", fk.Properties.Select(p => p.GetColumnName()));
            fk.SetConstraintName($"fk_{fk.DeclaringEntityType.GetTableName()}_usuarios_{colunas}");
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Enums gravados como smallint (docs/diagramas/03_modelo_dados.puml).
        builder.Properties<Perfil>().HaveConversion<short>();
        builder.Properties<TipoOcorrencia>().HaveConversion<short>();
        builder.Properties<StatusProblema>().HaveConversion<short>();
        builder.Properties<TipoVoto>().HaveConversion<short>();
        builder.Properties<ValorVoto>().HaveConversion<short>();
        builder.Properties<TipoVinculo>().HaveConversion<short>();
        builder.Properties<StatusVerificacao>().HaveConversion<short>();
    }
}
