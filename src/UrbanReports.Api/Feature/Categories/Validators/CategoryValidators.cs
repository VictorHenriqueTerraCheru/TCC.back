using FluentValidation;

namespace UrbanReports.Api.Categories;

public static class CategoryRules
{
    public static IRuleBuilderOptions<T, string> Name<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(60);

    public static IRuleBuilderOptions<T, string> Group<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(30);

    public static IRuleBuilderOptions<T, string> Icon<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(40)
            .Matches("^[a-z0-9_-]+$").WithMessage("Use apenas minúsculas, números, hífen e sublinhado.");
}

public sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryValidator()
    {
        RuleFor(r => r.Name).Name();
        RuleFor(r => r.Group).Group();
        RuleFor(r => r.Type).IsInEnum();
        RuleFor(r => r.Icon).Icon();
    }
}

public sealed class EditCategoryValidator : AbstractValidator<EditCategoryRequest>
{
    public EditCategoryValidator()
    {
        RuleFor(r => r.Name).Name();
        RuleFor(r => r.Group).Group();
        RuleFor(r => r.Icon).Icon();
    }
}
