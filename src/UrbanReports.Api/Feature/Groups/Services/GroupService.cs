using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UrbanReports.Api.Common;
using UrbanReports.Api.Persistence;

namespace UrbanReports.Api.Groups;

public sealed class GroupService(AppDbContext db) : IGroupService
{
    private const int MaxKeyLength = 30;

    public Task<List<GroupResponse>> ListAsync(CancellationToken ct) =>
        db.Groups
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.Label)
            .Select(g => new GroupResponse(g.Key, g.Label, g.Type, g.Color, g.SortOrder))
            .ToListAsync(ct);

    public async Task<Result<GroupResponse>> CreateAsync(CreateGroupRequest request, CancellationToken ct)
    {
        var label = request.Label.Trim();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.LockCatalogAsync(ct);

        if (await LabelInUseAsync(label, null, ct))
        {
            return GroupErrors.DuplicateLabel;
        }

        var key = await UniqueKeyAsync(label, ct);
        var sortOrder = (await db.Groups.MaxAsync(g => (int?)g.SortOrder, ct) ?? 0) + 1;
        var group = new CategoryGroup { Key = key, Label = label, Type = request.Type, Color = request.Color, SortOrder = sortOrder };
        db.Groups.Add(group);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.ViolatedUniqueness())
        {
            return GroupErrors.DuplicateLabel;
        }

        await transaction.CommitAsync(ct);

        return ToResponse(group);
    }

    public async Task<Result<GroupResponse>> EditAsync(string key, EditGroupRequest request, CancellationToken ct)
    {
        var label = request.Label.Trim();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.LockCatalogAsync(ct);

        var group = await db.Groups.FindAsync([key], ct);
        if (group is null)
        {
            return ApplicationError.NotFound;
        }

        if (await LabelInUseAsync(label, key, ct))
        {
            return GroupErrors.DuplicateLabel;
        }

        group.Label = label;
        group.Color = request.Color;
        group.SortOrder = request.SortOrder;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.ViolatedUniqueness())
        {
            return GroupErrors.DuplicateLabel;
        }

        await transaction.CommitAsync(ct);

        return ToResponse(group);
    }

    public async Task<Result<Empty>> DeleteAsync(string key, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.LockCatalogAsync(ct);

        if (await db.Categories.AnyAsync(c => c.Group == key, ct))
        {
            return GroupErrors.InUse;
        }

        try
        {
            var rows = await db.Groups.Where(g => g.Key == key).ExecuteDeleteAsync(ct);
            if (rows == 0)
            {
                return ApplicationError.NotFound;
            }

            await transaction.CommitAsync(ct);
            return new Empty();
        }
        catch (Exception e) when (e is DbUpdateException || e is PostgresException)
        {
            if (!e.ViolatedForeignKey()) throw;
            return GroupErrors.InUse;
        }
    }

    private Task<bool> LabelInUseAsync(string label, string? ignoreKey, CancellationToken ct)
    {
        var lowercase = label.ToLower();
        return db.Groups.AnyAsync(g => g.Label.ToLower() == lowercase && g.Key != ignoreKey, ct);
    }

    private async Task<string> UniqueKeyAsync(string label, CancellationToken ct)
    {
        var baseKey = KeyOf(label);
        var key = baseKey;
        for (var n = 2; await db.Groups.AnyAsync(g => g.Key == key, ct); n++)
        {
            var suffix = n.ToString(CultureInfo.InvariantCulture);
            key = baseKey[..Math.Min(baseKey.Length, MaxKeyLength - suffix.Length)] + suffix;
        }

        return key;
    }

    public static string KeyOf(string label)
    {
        var withoutAccent = new StringBuilder();
        foreach (var c in label.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                withoutAccent.Append(c);
            }
        }

        var words = new string(withoutAccent.ToString().Select(c => char.IsAsciiLetterOrDigit(c) ? c : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var key = new StringBuilder();
        foreach (var word in words)
        {
            var lowercase = word.ToLowerInvariant();
            key.Append(key.Length == 0 ? lowercase : char.ToUpperInvariant(lowercase[0]) + lowercase[1..]);
        }

        var text = key.ToString();
        if (text.Length == 0 || !char.IsAsciiLetter(text[0]))
        {
            text = "grupo" + text;
        }

        return text.Length > MaxKeyLength ? text[..MaxKeyLength] : text;
    }

    private static GroupResponse ToResponse(CategoryGroup g) => new(g.Key, g.Label, g.Type, g.Color, g.SortOrder);
}
