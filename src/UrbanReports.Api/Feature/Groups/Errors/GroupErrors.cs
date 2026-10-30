using UrbanReports.Api.Common;

namespace UrbanReports.Api.Groups;

public static class GroupErrors
{
    public static readonly ApplicationError DuplicateLabel = ApplicationError.Rule(
        StatusCodes.Status409Conflict, "duplicate-group-label", "Já existe um grupo com este nome.");

    public static readonly ApplicationError InUse = ApplicationError.Rule(
        StatusCodes.Status409Conflict, "group-in-use", "O grupo tem categorias e não pode ser apagado.");
}
