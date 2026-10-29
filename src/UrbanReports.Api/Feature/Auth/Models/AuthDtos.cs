using UrbanReports.Api.Common;

namespace UrbanReports.Api.Auth;

public sealed record CadastrarRequest(string Nome, string Email, string Senha, bool AceitouTermos);

public sealed record EntrarRequest(string Email, string Senha);

public sealed record RespostaAuth(string Token, DateTimeOffset ExpiraEm, RespostaAuth.UsuarioResumo Usuario)
{
    public sealed record UsuarioResumo(Guid Id, string Nome, string Email, Perfil Perfil);
}
