using UrbanReports.Api.Common;

namespace UrbanReports.Api.Categories;

public static class CategoryErrors
{
    public static readonly ApplicationError DuplicateName = ApplicationError.Rule(
        StatusCodes.Status409Conflict, "duplicate-name", "Já existe uma categoria com este nome.");

    public static readonly ApplicationError MissingGroup = ApplicationError.FromField("group", "Grupo inexistente.");

    public static readonly ApplicationError GroupTypeMismatch = ApplicationError.Rule(
        StatusCodes.Status422UnprocessableEntity, "group-type-mismatch", "O grupo escolhido é de outro tipo (problema ou incidente).");
}
