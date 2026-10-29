using FluentValidation;

namespace UrbanReports.Api.Categorias;

public static class RegrasCategoria
{
    public static IRuleBuilderOptions<T, string> Nome<T>(this IRuleBuilder<T, string> regra) =>
        regra.NotEmpty().MaximumLength(60);

    public static IRuleBuilderOptions<T, string> Grupo<T>(this IRuleBuilder<T, string> regra) =>
        regra.NotEmpty().MaximumLength(30);

    public static IRuleBuilderOptions<T, string> Icone<T>(this IRuleBuilder<T, string> regra) =>
        regra.NotEmpty().MaximumLength(40)
            .Matches("^[a-z0-9_-]+$").WithMessage("Use apenas minúsculas, números, hífen e sublinhado.");
}

public sealed class CriarCategoriaValidator : AbstractValidator<CriarCategoriaRequest>
{
    public CriarCategoriaValidator()
    {
        RuleFor(r => r.Nome).Nome();
        RuleFor(r => r.Grupo).Grupo();
        RuleFor(r => r.Tipo).IsInEnum();
        RuleFor(r => r.Icone).Icone();
    }
}

public sealed class EditarCategoriaValidator : AbstractValidator<EditarCategoriaRequest>
{
    public EditarCategoriaValidator()
    {
        RuleFor(r => r.Nome).Nome();
        RuleFor(r => r.Grupo).Grupo();
        RuleFor(r => r.Icone).Icone();
    }
}
