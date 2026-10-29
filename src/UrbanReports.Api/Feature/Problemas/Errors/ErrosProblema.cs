using UrbanReports.Api.Common;

namespace UrbanReports.Api.Issues;

public static class IssueErrors
{
    public static readonly ApplicationError MissingCategory = ApplicationError.FromField("categoryId", "Categoria inexistente.");

    public static readonly ApplicationError CategoryNotIssue = ApplicationError.Rule(
        StatusCodes.Status422UnprocessableEntity, "category-not-issue", "A categoria escolhida é de incidente, não de problema.");
}
