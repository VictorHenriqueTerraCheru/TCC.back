using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Verificacao;

public interface IVerificacaoService
{
    Task<Resultado<SolicitarVerificacaoResponse>> SolicitarAsync(Guid usuarioId, SolicitarVerificacaoRequest request, CancellationToken ct);
}
