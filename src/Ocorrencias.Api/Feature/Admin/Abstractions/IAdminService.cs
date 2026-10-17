using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Admin;

public interface IAdminService
{
    Task<Paginado<ListarVerificacoesItem>> ListarVerificacoesAsync(ListarVerificacoesRequest request, CancellationToken ct);

    Task<Resultado<Vazio>> AprovarAsync(Guid id, Guid adminId, CancellationToken ct);

    Task<Resultado<Vazio>> RecusarAsync(Guid id, Guid adminId, RecusarVerificacaoRequest request, CancellationToken ct);
}
