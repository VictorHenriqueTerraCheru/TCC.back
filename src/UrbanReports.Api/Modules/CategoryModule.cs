using Microsoft.AspNetCore.Http.HttpResults;
using UrbanReports.Api.Categories;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Modules;

public sealed class CategoryModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/categories", List)
            .AllowAnonymous()
            .WithTags("Categories");

        api.MapGet("/admin/categories", ListAll)
            .RequireAuthorization(AuthorizationPolicies.Administrator)
            .WithTags("Admin");

        api.MapPost("/admin/categories", Create)
            .Validate<CreateCategoryRequest>()
            .RequireAuthorization(AuthorizationPolicies.Administrator)
            .WithTags("Admin");

        api.MapPut("/admin/categories/{id:int}", Edit)
            .Validate<EditCategoryRequest>()
            .RequireAuthorization(AuthorizationPolicies.Administrator)
            .WithTags("Admin");

        api.MapPatch("/admin/categories/{id:int}/active", SetActive)
            .RequireAuthorization(AuthorizationPolicies.Administrator)
            .WithTags("Admin");
    }

    private static Task<List<ListCategoriesResponse>> List(ICategoryService service, CancellationToken ct) =>
        service.ListAsync(ct);

    private static Task<List<CategoryAdminResponse>> ListAll(ICategoryService service, CancellationToken ct) =>
        service.ListAllAsync(ct);

    private static async Task<Results<Created<CategoryAdminResponse>, ValidationProblem, ProblemHttpResult>> Create(
        CreateCategoryRequest request, ICategoryService service, CancellationToken ct)
    {
        var r = await service.CreateAsync(request, ct);
        return r.Error switch
        {
            null => TypedResults.Created("/api/v1/admin/categories", r.Value),
            { Kind: ErrorType.Field } e => e.ToValidation(),
            var e => e.ToProblem(),
        };
    }

    private static async Task<Results<Ok<CategoryAdminResponse>, NotFound, ValidationProblem, ProblemHttpResult>> Edit(
        int id, EditCategoryRequest request, ICategoryService service, CancellationToken ct)
    {
        if (id is < 1 or > short.MaxValue)
        {
            return TypedResults.NotFound();
        }

        var r = await service.EditAsync((short)id, request, ct);
        return r.Error switch
        {
            null => TypedResults.Ok(r.Value),
            { Kind: ErrorType.NotFound } e => e.ToNotFound(),
            { Kind: ErrorType.Field } e => e.ToValidation(),
            var e => e.ToProblem(),
        };
    }

    private static async Task<Results<NoContent, NotFound>> SetActive(
        int id, SetCategoryActiveRequest request, ICategoryService service, CancellationToken ct)
    {
        if (id is < 1 or > short.MaxValue)
        {
            return TypedResults.NotFound();
        }

        var r = await service.SetActiveAsync((short)id, request.Active, ct);
        return r.Error is null ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
