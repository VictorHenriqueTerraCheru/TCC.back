using UrbanReports.Api.Common;

namespace UrbanReports.Api.Categories;

public interface ICategoryService
{
    Task<List<ListCategoriesResponse>> ListAsync(CancellationToken ct);

    Task<List<CategoryAdminResponse>> ListAllAsync(CancellationToken ct);

    Task<Result<CategoryAdminResponse>> CreateAsync(CreateCategoryRequest request, CancellationToken ct);

    Task<Result<CategoryAdminResponse>> EditAsync(short id, EditCategoryRequest request, CancellationToken ct);

    Task<Result<Empty>> SetActiveAsync(short id, bool active, CancellationToken ct);
}
