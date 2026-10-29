using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using UrbanReports.Api.Common;
using UrbanReports.Api.Persistence;

namespace UrbanReports.Api.Auth;

public sealed class AuthService(UserManager<Usuario> usuarios, JwtTokenService jwt, TimeProvider relogio, AppDbContext db, IOptions<IdentityOptions> identity) : IAuthService
{
    public async Task<Resultado<RespostaAuth>> CadastrarAsync(CadastrarRequest request)
    {
        var email = request.Email.Trim();
        if (await usuarios.FindByEmailAsync(email) is not null)
        {
            return ErrosAuth.EmailJaCadastrado;
        }

        var usuario = new Usuario
        {
            Id = Guid.CreateVersion7(),
            Nome = request.Nome.Trim(),
            Email = email,
            UserName = email,
            CriadoEm = relogio.GetUtcNow(),
        };

        IdentityResult resultado;
        try
        {
            resultado = await usuarios.CreateAsync(usuario, request.Senha);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return ErrosAuth.EmailJaCadastrado;
        }

        if (!resultado.Succeeded)
        {
            if (resultado.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
            {
                return ErrosAuth.EmailJaCadastrado;
            }

            throw new InvalidOperationException(
                "Falha ao criar usuário: " + string.Join("; ", resultado.Errors.Select(e => e.Code)));
        }

        return CriarResposta(usuario);
    }

    public async Task<Resultado<RespostaAuth>> EntrarAsync(EntrarRequest request)
    {
        var usuario = await usuarios.FindByEmailAsync(request.Email.Trim());
        if (usuario is null)
        {
            return ErrosAuth.CredenciaisInvalidas;
        }

        if (await usuarios.IsLockedOutAsync(usuario))
        {
            return ErrosAuth.CredenciaisInvalidas;
        }

        if (!await usuarios.CheckPasswordAsync(usuario, request.Senha))
        {
            await RegistrarFalhaAsync(usuario.Id);
            return ErrosAuth.CredenciaisInvalidas;
        }

        await db.Database.ExecuteSqlAsync(
            $"UPDATE usuarios SET access_failed_count = 0 WHERE id = {usuario.Id} AND access_failed_count <> 0");
        return CriarResposta(usuario);
    }

    private async Task RegistrarFalhaAsync(Guid usuarioId)
    {
        var lockout = identity.Value.Lockout;
        var maximo = lockout.MaxFailedAccessAttempts;
        var minutos = lockout.DefaultLockoutTimeSpan.TotalMinutes;

        await db.Database.ExecuteSqlAsync($"""
            UPDATE usuarios
            SET access_failed_count = CASE WHEN access_failed_count + 1 >= {maximo} THEN 0 ELSE access_failed_count + 1 END,
                lockout_end = CASE WHEN access_failed_count + 1 >= {maximo} THEN now() + {minutos} * interval '1 minute' ELSE lockout_end END
            WHERE id = {usuarioId}
            """);
    }

    private RespostaAuth CriarResposta(Usuario usuario)
    {
        var (token, expiraEm) = jwt.Gerar(usuario);
        return new RespostaAuth(token, expiraEm, new RespostaAuth.UsuarioResumo(usuario.Id, usuario.Nome, usuario.Email!, usuario.Perfil));
    }
}
