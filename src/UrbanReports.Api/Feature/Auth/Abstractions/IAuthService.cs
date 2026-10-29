using UrbanReports.Api.Common;

namespace UrbanReports.Api.Auth;

public interface IAuthService
{
    Task<Resultado<RespostaAuth>> CadastrarAsync(CadastrarRequest request);

    Task<Resultado<RespostaAuth>> EntrarAsync(EntrarRequest request);
}
