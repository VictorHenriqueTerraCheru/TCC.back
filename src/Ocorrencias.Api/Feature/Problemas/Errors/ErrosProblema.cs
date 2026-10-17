using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Problemas;

public static class ErrosProblema
{
    public static readonly ErroAplicacao CategoriaInexistente = ErroAplicacao.DeCampo("categoriaId", "Categoria inexistente.");

    public static readonly ErroAplicacao CategoriaNaoEProblema = ErroAplicacao.Regra(
        StatusCodes.Status422UnprocessableEntity, "categoria-nao-e-problema", "A categoria escolhida é de incidente, não de problema.");
}
