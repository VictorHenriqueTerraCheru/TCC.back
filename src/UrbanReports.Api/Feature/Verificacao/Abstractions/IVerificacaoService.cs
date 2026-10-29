using UrbanReports.Api.Common;

namespace UrbanReports.Api.Verificacao;

public interface IVerificacaoService
{
    Task<Resultado<SolicitarVerificacaoResponse>> SolicitarAsync(Guid usuarioId, SolicitarVerificacaoRequest request, CancellationToken ct);
}
