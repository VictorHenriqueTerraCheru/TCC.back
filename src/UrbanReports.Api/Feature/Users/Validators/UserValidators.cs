using FluentValidation;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Users;

public sealed class UpdateMeValidator : AbstractValidator<UpdateMeRequest>
{
    public UpdateMeValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(120);
    }
}

public sealed class GetHistoryValidator : AbstractValidator<GetHistoryRequest>
{
    private static readonly string[] Filters = ["all", "reports", "votes"];

    public GetHistoryValidator()
    {
        RuleFor(r => r.Filter).Must(f => Filters.Contains(f)).WithMessage("Filtro deve ser tudo, registros ou votos.");
        RuleFor(r => r.Page).InclusiveBetween(1, Pagination.MaxPage);
        RuleFor(r => r.PageSize).InclusiveBetween(1, Pagination.MaxPageSize);
    }
}
