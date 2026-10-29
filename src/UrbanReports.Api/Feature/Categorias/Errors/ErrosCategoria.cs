using UrbanReports.Api.Common;

namespace UrbanReports.Api.Categorias;

public static class ErrosCategoria
{
    public static readonly ErroAplicacao NomeDuplicado = ErroAplicacao.Regra(
        StatusCodes.Status409Conflict, "nome-duplicado", "Já existe uma categoria com este nome.");

    public static readonly ErroAplicacao LimiteDeCategorias = ErroAplicacao.Regra(
        StatusCodes.Status409Conflict, "limite-de-categorias", "Não cabem mais categorias.");

    public static readonly ErroAplicacao GrupoInexistente = ErroAplicacao.DeCampo("grupo", "Grupo inexistente.");

    public static readonly ErroAplicacao GrupoTipoIncompativel = ErroAplicacao.Regra(
        StatusCodes.Status422UnprocessableEntity, "grupo-tipo-incompativel", "O grupo escolhido é de outro tipo (problema ou incidente).");
}
