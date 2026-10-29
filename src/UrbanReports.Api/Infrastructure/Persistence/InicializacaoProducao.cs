using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Persistence;

/// <summary>
/// Só em Production: aplica as migrations pendentes e garante um administrador. Sem tela de promoção a admin,
/// o primeiro vem de <c>AdminInicial__Email</c> e <c>AdminInicial__Senha</c> (variáveis do host, fora do código).
/// </summary>
public static class InicializacaoProducao
{
    public static async Task InicializarBancoProducaoAsync(this WebApplication app)
    {
        await using var escopo = app.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        if (await db.Users.AnyAsync(u => u.Perfil == Perfil.Administrador))
        {
            return;
        }

        var email = app.Configuration["AdminInicial:Email"];
        var senha = app.Configuration["AdminInicial:Senha"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
        {
            app.Logger.LogWarning("Nenhum administrador no banco e AdminInicial__Email/AdminInicial__Senha não configurados.");
            return;
        }

        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>();

        // Já cadastrado pela tela (como cidadão): só promove, sem trocar a senha.
        var existente = await usuarios.FindByEmailAsync(email);
        if (existente is not null)
        {
            existente.Perfil = Perfil.Administrador;
            Garantir(await usuarios.UpdateAsync(existente), email);
            app.Logger.LogInformation("Usuário existente promovido a administrador.");
            return;
        }

        var admin = new Usuario
        {
            Id = Guid.CreateVersion7(),
            Nome = app.Configuration["AdminInicial:Nome"] ?? "Administrador",
            Email = email,
            UserName = email,
            Perfil = Perfil.Administrador,
            CriadoEm = escopo.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow(),
        };
        Garantir(await usuarios.CreateAsync(admin, senha), email);
        app.Logger.LogInformation("Administrador inicial criado.");
    }

    private static void Garantir(IdentityResult resultado, string email)
    {
        if (!resultado.Succeeded)
        {
            throw new InvalidOperationException($"Administrador inicial falhou para {email}: {string.Join("; ", resultado.Errors.Select(e => e.Code))}");
        }
    }
}
