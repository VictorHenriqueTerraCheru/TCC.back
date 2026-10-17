using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Votos;

public static class ErrosVoto
{
    public static readonly ErroAplicacao AutorNaoVotaExistencia = ErroAplicacao.Regra(
        StatusCodes.Status422UnprocessableEntity, "autor-nao-vota-existencia", "Quem registrou o problema não vota na existência dele.");

    public static readonly ErroAplicacao PersistenciaIndisponivelEmNova = ErroAplicacao.Regra(
        StatusCodes.Status422UnprocessableEntity, "persistencia-indisponivel-em-nova", "\"Ainda está lá?\" só abre depois que o problema sai de Nova.");

    public static readonly ErroAplicacao ProblemaResolvido = ErroAplicacao.Regra(
        StatusCodes.Status422UnprocessableEntity, "problema-resolvido", "O problema já foi resolvido e não recebe mais votos.");
}
