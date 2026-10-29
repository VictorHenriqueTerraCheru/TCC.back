using UrbanReports.Api.Common;

namespace UrbanReports.Api.Categories;

public sealed record CategorySummary(short Id, string Name, string Group, string Icon);

public sealed record ListCategoriesResponse(short Id, string Name, string Group, ReportType Type, string Icon);

public sealed record CategoryAdminResponse(short Id, string Name, string Group, ReportType Type, string Icon, bool Active);

public sealed record CreateCategoryRequest(string Name, string Group, ReportType Type, string Icon);

public sealed record EditCategoryRequest(string Name, string Group, string Icon);

public sealed record SetCategoryActiveRequest
{
    public required bool Active { get; init; }
}
