using UrbanReports.Api.Common;

namespace UrbanReports.Api.Saude;

public interface ISaudeService
{
    Task<Resultado<StatusSaudeResponse>> VerificarBancoAsync(CancellationToken ct);
}
