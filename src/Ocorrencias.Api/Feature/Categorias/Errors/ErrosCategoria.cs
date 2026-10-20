using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Categorias;

public static class ErrosCategoria
{
    public static readonly ErroAplicacao NomeDuplicado = ErroAplicacao.Regra(
        StatusCodes.Status409Conflict, "nome-duplicado", "Já existe uma categoria com este nome.");
}
