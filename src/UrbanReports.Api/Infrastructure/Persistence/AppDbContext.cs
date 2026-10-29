using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityUserContext<User, Guid>(options)
{
    public DbSet<VerificationRequest> VerificationRequests => Set<VerificationRequest>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CategoryGroup> Groups => Set<CategoryGroup>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<Incident> Incidents => Set<Incident>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("postgis");

        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        RenameForeignKeysToUsers(builder);
    }

    private static void RenameForeignKeysToUsers(ModelBuilder builder)
    {
        var user = builder.Model.FindEntityType(typeof(User))!;
        var keys = builder.Model.GetEntityTypes()
            .SelectMany(e => e.GetDeclaredForeignKeys())
            .Where(fk => fk.PrincipalEntityType == user);

        foreach (var fk in keys)
        {
            var columns = string.Join("_", fk.Properties.Select(p => p.GetColumnName()));
            fk.SetConstraintName($"fk_{fk.DeclaringEntityType.GetTableName()}_users_{columns}");
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<Role>().HaveConversion<short>();
        builder.Properties<ReportType>().HaveConversion<short>();
        builder.Properties<GroupColor>().HaveConversion<short>();
        builder.Properties<IssueStatus>().HaveConversion<short>();
        builder.Properties<VoteValue>().HaveConversion<short>();
        builder.Properties<AffiliationType>().HaveConversion<short>();
        builder.Properties<VerificationStatus>().HaveConversion<short>();
    }
}
