using FluentValidation;

namespace UrbanReports.Api.Groups;

public sealed class CreateGroupValidator : AbstractValidator<CreateGroupRequest>
{
    public CreateGroupValidator()
    {
        RuleFor(r => r.Label).NotEmpty().MaximumLength(40);
        RuleFor(r => r.Type).IsInEnum();
        RuleFor(r => r.Color).IsInEnum();
    }
}

public sealed class EditGroupValidator : AbstractValidator<EditGroupRequest>
{
    public EditGroupValidator()
    {
        RuleFor(r => r.Label).NotEmpty().MaximumLength(40);
        RuleFor(r => r.Color).IsInEnum();
        RuleFor(r => r.SortOrder).InclusiveBetween(1, 100_000);
    }
}
