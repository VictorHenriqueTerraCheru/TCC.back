using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Persistence;

namespace Ocorrencias.Api.Auth;

public sealed class AuthService(UserManager<Usuario> usuarios, JwtTokenService jwt, TimeProvider relogio) : IAuthService
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
            // Corrida entre a checagem acima e o INSERT: o índice único de e-mail barra no banco.
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
        if (usuario is null || !await usuarios.CheckPasswordAsync(usuario, request.Senha))
        {
            return ErrosAuth.CredenciaisInvalidas;
        }

        return CriarResposta(usuario);
    }

    private RespostaAuth CriarResposta(Usuario usuario)
    {
        var (token, expiraEm) = jwt.Gerar(usuario);
        return new RespostaAuth(token, expiraEm, new RespostaAuth.UsuarioResumo(usuario.Id, usuario.Nome, usuario.Email!, usuario.Perfil));
    }
}
