using FluentValidation;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Admin;

public sealed class ListVerificationsValidator : AbstractValidator<ListVerificationsRequest>
{
    private static readonly HashSet<string> StatusNames = new(Enum.GetNames<VerificationStatus>(), StringComparer.OrdinalIgnoreCase);

    public ListVerificationsValidator()
    {
        RuleFor(r => r.Status)
            .Must(s => s is null || StatusNames.Contains(s))
            .WithMessage($"Status desconhecido. Valores aceitos: {Lists.ValuesOf<VerificationStatus>()}.");
        RuleFor(r => r.Page).InclusiveBetween(1, Pagination.MaxPage);
        RuleFor(r => r.PageSize).InclusiveBetween(1, Pagination.MaxPageSize);
    }
}

public sealed class RejectVerificationValidator : AbstractValidator<RejectVerificationRequest>
{
    public RejectVerificationValidator()
    {
        RuleFor(r => r.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class ListUsersValidator : AbstractValidator<ListUsersRequest>
{
    private static readonly HashSet<string> RoleNames = new(Enum.GetNames<Role>(), StringComparer.OrdinalIgnoreCase);

    public ListUsersValidator()
    {
        RuleFor(r => r.Search).MaximumLength(100);
        RuleFor(r => r.Role)
            .Must(p => p is null || RoleNames.Contains(p))
            .WithMessage($"Perfil desconhecido. Valores aceitos: {Lists.ValuesOf<Role>()}.");
        RuleFor(r => r.Page).InclusiveBetween(1, Pagination.MaxPage);
        RuleFor(r => r.PageSize).InclusiveBetween(1, Pagination.MaxPageSize);
    }
}

public sealed class ChangeRoleValidator : AbstractValidator<ChangeRoleRequest>
{
    public ChangeRoleValidator()
    {
        RuleFor(r => r.Role).IsInEnum();
    }
}
