using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Common;
using UrbanReports.Api.Persistence;

namespace UrbanReports.Api.Categories;

public sealed class CategoryService(AppDbContext db) : ICategoryService
{
    public Task<List<ListCategoriesResponse>> ListAsync(CancellationToken ct) =>
        db.Categories
            .Where(c => c.Active)
            .OrderBy(c => c.Id)
            .Select(c => new ListCategoriesResponse(c.Id, c.Name, c.Group, c.Type, c.Icon))
            .ToListAsync(ct);

    public Task<List<CategoryAdminResponse>> ListAllAsync(CancellationToken ct) =>
        db.Categories
            .OrderBy(c => c.Id)
            .Select(c => new CategoryAdminResponse(c.Id, c.Name, c.Group, c.Type, c.Icon, c.Active))
            .ToListAsync(ct);

    public async Task<Result<CategoryAdminResponse>> CreateAsync(CreateCategoryRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.LockCatalogAsync(ct);

        var group = await db.Groups.FindAsync([request.Group], ct);
        if (group is null)
        {
            return CategoryErrors.MissingGroup;
        }

        if (group.Type != request.Type)
        {
            return CategoryErrors.GroupTypeMismatch;
        }

        if (await NameInUseAsync(name, null, ct))
        {
            return CategoryErrors.DuplicateName;
        }

        var greater = await db.Categories.MaxAsync(c => (short?)c.Id, ct) ?? 0;
        if (greater >= short.MaxValue)
        {
            return CategoryErrors.CategoryLimitReached;
        }

        var category = new Category
        {
            Id = (short)(greater + 1),
            Name = name,
            Group = request.Group,
            Type = request.Type,
            Icon = request.Icon,
        };
        db.Categories.Add(category);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.ViolatedUniqueness())
        {
            return CategoryErrors.DuplicateName;
        }
        catch (DbUpdateException e) when (e.ViolatedForeignKey())
        {
            return CategoryErrors.MissingGroup;
        }

        await transaction.CommitAsync(ct);

        return ToAdmin(category);
    }

    public async Task<Result<CategoryAdminResponse>> EditAsync(short id, EditCategoryRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.LockCatalogAsync(ct);

        var category = await db.Categories.FindAsync([id], ct);
        if (category is null)
        {
            return ApplicationError.NotFound;
        }

        var group = await db.Groups.FindAsync([request.Group], ct);
        if (group is null)
        {
            return CategoryErrors.MissingGroup;
        }

        if (group.Type != category.Type)
        {
            return CategoryErrors.GroupTypeMismatch;
        }

        if (await NameInUseAsync(name, id, ct))
        {
            return CategoryErrors.DuplicateName;
        }

        category.Name = name;
        category.Group = request.Group;
        category.Icon = request.Icon;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.ViolatedUniqueness())
        {
            return CategoryErrors.DuplicateName;
        }
        catch (DbUpdateException e) when (e.ViolatedForeignKey())
        {
            return CategoryErrors.MissingGroup;
        }

        await transaction.CommitAsync(ct);

        return ToAdmin(category);
    }

    public async Task<Result<Empty>> SetActiveAsync(short id, bool active, CancellationToken ct)
    {
        var rows = await db.Categories
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Active, active), ct);

        return rows == 0 ? ApplicationError.NotFound : new Empty();
    }

    private Task<bool> NameInUseAsync(string name, short? ignoreId, CancellationToken ct)
    {
        var lowercase = name.ToLower();
        return db.Categories.AnyAsync(c => c.Name.ToLower() == lowercase && c.Id != ignoreId, ct);
    }

    private static CategoryAdminResponse ToAdmin(Category c) => new(c.Id, c.Name, c.Group, c.Type, c.Icon, c.Active);
}
