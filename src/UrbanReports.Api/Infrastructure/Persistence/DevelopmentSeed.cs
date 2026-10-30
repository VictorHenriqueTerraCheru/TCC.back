using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Persistence;

public static class DevelopmentSeed
{
    public const string Password = "tcc12345";

    public static async Task InitializeDevelopmentDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        if (await db.Users.AnyAsync())
        {
            return;
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();

        var admin = await Create(users, "admin@tcc.local", "Administrador TCC", Role.Administrator, now);
        var verified = await Create(users, "verificado@tcc.local", "Carlos Menezes", Role.VerifiedCitizen, now, verifiedAt: now);
        await Create(users, "cidadao1@tcc.local", "Mariana Souza", Role.Citizen, now);
        await Create(users, "cidadao2@tcc.local", "João Pereira", Role.Citizen, now);
        await Create(users, "cidadao3@tcc.local", "Ana Lima", Role.Citizen, now);

        db.VerificationRequests.Add(new VerificationRequest
        {
            Id = Guid.CreateVersion7(),
            UserId = verified.Id,
            AffiliationType = AffiliationType.LivesInArea,
            Justification = "Moro em Botafogo há 10 anos e circulo diariamente pelo bairro.",
            Status = VerificationStatus.Approved,
            ReviewedById = admin.Id,
            CreatedAt = now,
            ReviewedAt = now,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<User> Create(
        UserManager<User> users, string email, string name, Role role, DateTimeOffset now, DateTimeOffset? verifiedAt = null)
    {
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Email = email,
            UserName = email,
            Role = role,
            CreatedAt = now,
            VerifiedAt = verifiedAt,
        };

        var result = await users.CreateAsync(user, Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Seed falhou para {email}: {string.Join("; ", result.Errors.Select(e => e.Code))}");
        }

        return user;
    }
}
