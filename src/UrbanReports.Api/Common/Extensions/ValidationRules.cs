using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Persistence;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Common;

public static class ValidationRules
{
    private static readonly HashSet<string> StatusNames = new(Enum.GetNames<IssueStatus>(), StringComparer.OrdinalIgnoreCase);

    public static IRuleBuilderOptions<T, double> Latitude<T>(this IRuleBuilder<T, double> rule) =>
        rule.InclusiveBetween(-90, 90);

    public static IRuleBuilderOptions<T, double> Longitude<T>(this IRuleBuilder<T, double> rule) =>
        rule.InclusiveBetween(-180, 180);

    public static IRuleBuilderOptions<T, double?> Latitude<T>(this IRuleBuilder<T, double?> rule) =>
        rule.InclusiveBetween(-90, 90);

    public static IRuleBuilderOptions<T, double?> Longitude<T>(this IRuleBuilder<T, double?> rule) =>
        rule.InclusiveBetween(-180, 180);

    public static IRuleBuilderOptionsConditions<T, string?> GroupList<T>(this IRuleBuilder<T, string?> rule, AppDbContext db) =>
        rule.CustomAsync(async (value, context, ct) =>
        {
            var requests = Lists.Split(value);
            if (requests.Length == 0)
            {
                return;
            }

            var known = await db.Groups.OrderBy(g => g.SortOrder).Select(g => g.Key).ToListAsync(ct);
            if (!requests.All(known.Contains))
            {
                context.AddFailure("Grupo desconhecido. Valores aceitos: " + string.Join(", ", known) + ".");
            }
        });

    public static IRuleBuilderOptions<T, string?> StatusList<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(s => Lists.Split(s).All(v => StatusNames.Contains(v)))
            .WithMessage("Status desconhecido. Valores aceitos: nova, ativa, possivelmenteResolvida, resolvida.");
}

public static class Lists
{
    public static string[] Split(string? values) =>
        string.IsNullOrWhiteSpace(values)
            ? []
            : values.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
