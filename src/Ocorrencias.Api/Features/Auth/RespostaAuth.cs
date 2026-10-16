using Ocorrencias.Api.Auth;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Features.Auth;

/// <summary>Corpo de resposta comum a cadastro e login.</summary>
public sealed record RespostaAuth(string Token, DateTimeOffset ExpiraEm, RespostaAuth.UsuarioResumo Usuario)
{
    public sealed record UsuarioResumo(Guid Id, string Nome, string Email, Perfil Perfil);

    public static RespostaAuth Criar(Usuario usuario, JwtTokenService jwt)
    {
        var (token, expiraEm) = jwt.Gerar(usuario);
        return new RespostaAuth(token, expiraEm, new UsuarioResumo(usuario.Id, usuario.Nome, usuario.Email!, usuario.Perfil));
    }
}
