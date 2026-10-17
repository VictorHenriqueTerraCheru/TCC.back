namespace Ocorrencias.Api.Common;

/// <summary>
/// Endpoints de uma área. <c>MapEndpoints</c> encontra as implementações por reflexão e chama <see cref="Map"/>
/// com o grupo <c>/api/v1</c>, que já exige autenticação.
/// </summary>
public interface IEndpointModule
{
    static abstract void Map(IEndpointRouteBuilder api);
}
