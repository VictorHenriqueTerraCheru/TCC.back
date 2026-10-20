using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Grupos;

public static class ErrosGrupo
{
    public static readonly ErroAplicacao RotuloDuplicado = ErroAplicacao.Regra(
        StatusCodes.Status409Conflict, "grupo-rotulo-duplicado", "Já existe um grupo com este nome.");

    public static readonly ErroAplicacao EmUso = ErroAplicacao.Regra(
        StatusCodes.Status409Conflict, "grupo-em-uso", "O grupo tem categorias e não pode ser apagado.");
}
