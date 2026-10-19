using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Votos;

public static class ErrosVoto
{
    public static readonly ErroAplicacao AutorNaoVota = ErroAplicacao.Regra(
        StatusCodes.Status422UnprocessableEntity, "autor-nao-vota", "Quem registrou o problema não vota nele.");

    public static readonly ErroAplicacao ProblemaResolvido = ErroAplicacao.Regra(
        StatusCodes.Status422UnprocessableEntity, "problema-resolvido", "O problema já foi resolvido e não recebe mais votos.");
}
