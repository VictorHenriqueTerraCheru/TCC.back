using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Persistence;

public static class ProductionStartup
{
    public static async Task InitializeProductionDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        if (await db.Users.AnyAsync(u => u.Role == Role.Administrator))
        {
            return;
        }

        var email = app.Configuration["InitialAdmin:Email"];
        var password = app.Configuration["InitialAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            app.Logger.LogWarning("Nenhum administrador no banco e InitialAdmin__Email/InitialAdmin__Password não configurados.");
            return;
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            existing.Role = Role.Administrator;
            Ensure(await users.UpdateAsync(existing), email);
            app.Logger.LogInformation("Usuário existente promovido a administrador.");
            return;
        }

        var admin = new User
        {
            Id = Guid.CreateVersion7(),
            Name = app.Configuration["InitialAdmin:Name"] ?? "Administrador",
            Email = email,
            UserName = email,
            Role = Role.Administrator,
            CreatedAt = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow(),
        };
        Ensure(await users.CreateAsync(admin, password), email);
        app.Logger.LogInformation("Administrador inicial criado.");
    }

    private static void Ensure(IdentityResult result, string email)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Administrador inicial falhou para {email}: {string.Join("; ", result.Errors.Select(e => e.Code))}");
        }
    }
}
