using UrbanReports.Api.Common;

namespace UrbanReports.Api.Admin;

public interface IAdminService
{
    Task<Paginado<ListarVerificacoesItem>> ListarVerificacoesAsync(ListarVerificacoesRequest request, CancellationToken ct);

    Task<Resultado<Vazio>> AprovarAsync(Guid id, Guid adminId, CancellationToken ct);

    Task<Resultado<Vazio>> RecusarAsync(Guid id, Guid adminId, RecusarVerificacaoRequest request, CancellationToken ct);

    Task<Paginado<ListarUsuariosItem>> ListarUsuariosAsync(ListarUsuariosRequest request, CancellationToken ct);

    Task<Resultado<Vazio>> AlterarPerfilAsync(Guid id, Guid adminId, AlterarPerfilRequest request, CancellationToken ct);
}
