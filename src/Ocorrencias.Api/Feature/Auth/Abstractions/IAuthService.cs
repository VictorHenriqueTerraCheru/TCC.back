using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Auth;

public interface IAuthService
{
    Task<Resultado<RespostaAuth>> CadastrarAsync(CadastrarRequest request);

    Task<Resultado<RespostaAuth>> EntrarAsync(EntrarRequest request);
}
