using FluentValidation;

namespace Ocorrencias.Api.Grupos;

public sealed class CriarGrupoValidator : AbstractValidator<CriarGrupoRequest>
{
    public CriarGrupoValidator()
    {
        RuleFor(r => r.Rotulo).NotEmpty().MaximumLength(40);
        RuleFor(r => r.Tipo).IsInEnum();
        RuleFor(r => r.Cor).IsInEnum();
    }
}

public sealed class EditarGrupoValidator : AbstractValidator<EditarGrupoRequest>
{
    public EditarGrupoValidator()
    {
        RuleFor(r => r.Rotulo).NotEmpty().MaximumLength(40);
        RuleFor(r => r.Cor).IsInEnum();
        RuleFor(r => r.Ordem).InclusiveBetween(1, 100_000);
    }
}
