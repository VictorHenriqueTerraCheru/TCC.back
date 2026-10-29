using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Auth;

public sealed record CadastrarRequest(string Nome, string Email, string Senha, bool AceitouTermos);

public sealed record EntrarRequest(string Email, string Senha);

/// <summary>Corpo de resposta comum a cadastro e login.</summary>
public sealed record RespostaAuth(string Token, DateTimeOffset ExpiraEm, RespostaAuth.UsuarioResumo Usuario)
{
    public sealed record UsuarioResumo(Guid Id, string Nome, string Email, Perfil Perfil);
}
