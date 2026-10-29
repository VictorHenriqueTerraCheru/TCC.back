using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Persistence;

public static class SeedDesenvolvimento
{
    public const string Senha = "tcc12345";

    public static async Task InicializarBancoDesenvolvimentoAsync(this WebApplication app)
    {
        await using var escopo = app.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        if (await db.Users.AnyAsync())
        {
            return;
        }

        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var agora = escopo.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();

        var admin = await Criar(usuarios, "admin@tcc.local", "Administrador TCC", Perfil.Administrador, agora);
        var verificado = await Criar(usuarios, "verificado@tcc.local", "Carlos Menezes", Perfil.CidadaoVerificado, agora, verificadoEm: agora);
        await Criar(usuarios, "cidadao1@tcc.local", "Mariana Souza", Perfil.Cidadao, agora);
        await Criar(usuarios, "cidadao2@tcc.local", "João Pereira", Perfil.Cidadao, agora);
        await Criar(usuarios, "cidadao3@tcc.local", "Ana Lima", Perfil.Cidadao, agora);

        db.SolicitacoesVerificacao.Add(new SolicitacaoVerificacao
        {
            Id = Guid.CreateVersion7(),
            UsuarioId = verificado.Id,
            TipoVinculo = TipoVinculo.MoradorRegiao,
            Justificativa = "Moro em Botafogo há 10 anos e circulo diariamente pelo bairro.",
            Status = StatusVerificacao.Aprovada,
            AnalisadaPorId = admin.Id,
            CriadaEm = agora,
            AnalisadaEm = agora,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<Usuario> Criar(
        UserManager<Usuario> usuarios, string email, string nome, Perfil perfil, DateTimeOffset agora, DateTimeOffset? verificadoEm = null)
    {
        var usuario = new Usuario
        {
            Id = Guid.CreateVersion7(),
            Nome = nome,
            Email = email,
            UserName = email,
            Perfil = perfil,
            CriadoEm = agora,
            VerificadoEm = verificadoEm,
        };

        var resultado = await usuarios.CreateAsync(usuario, Senha);
        if (!resultado.Succeeded)
        {
            throw new InvalidOperationException($"Seed falhou para {email}: {string.Join("; ", resultado.Errors.Select(e => e.Code))}");
        }

        return usuario;
    }
}
