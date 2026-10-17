using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Saude;

public interface ISaudeService
{
    Task<Resultado<StatusSaudeResponse>> VerificarBancoAsync(CancellationToken ct);
}
