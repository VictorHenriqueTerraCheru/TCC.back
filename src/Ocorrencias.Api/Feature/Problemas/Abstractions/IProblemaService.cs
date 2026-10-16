using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Problemas;

public interface IProblemaService
{
    Task<Resultado<ObterDetalheProblemaResponse>> RegistrarAsync(RegistrarProblemaRequest request, Guid autorId, CancellationToken ct);

    Task<Resultado<ObterDetalheProblemaResponse>> ObterDetalheAsync(Guid id, Guid? usuarioId, CancellationToken ct);

    /// <summary>Detalhe no instante <paramref name="agora"/> (o voto usa o mesmo instante do recálculo). Nulo se não existe.</summary>
    Task<ObterDetalheProblemaResponse?> ConsultarDetalheAsync(Guid id, Guid? usuarioId, DateTimeOffset agora, CancellationToken ct);

    Task<List<BuscarDuplicatasResponse>> BuscarDuplicatasAsync(BuscarDuplicatasRequest request, CancellationToken ct);

    Task<List<ListarProblemasNaAreaResponse>> ListarNaAreaAsync(ListarProblemasNaAreaRequest request, CancellationToken ct);

    Task<Paginado<ListarProblemasRecentesItem>> ListarRecentesAsync(ListarProblemasRecentesRequest request, CancellationToken ct);
}
