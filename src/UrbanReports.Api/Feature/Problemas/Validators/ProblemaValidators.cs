using FluentValidation;
using UrbanReports.Api.Common;
using UrbanReports.Api.Persistence;

namespace UrbanReports.Api.Issues;

public sealed class CreateIssueValidator : AbstractValidator<CreateIssueRequest>
{
    public CreateIssueValidator()
    {
        RuleFor(r => r.CategoryId).GreaterThan((short)0);
        RuleFor(r => r.Lat).Latitude();
        RuleFor(r => r.Lng).Longitude();
        RuleFor(r => r.Description).MaximumLength(280);
    }
}

public sealed class SearchDuplicatesValidator : AbstractValidator<SearchDuplicatesRequest>
{
    public SearchDuplicatesValidator()
    {
        RuleFor(r => r.CategoryId).GreaterThan((short)0);
        RuleFor(r => r.Lat).Latitude();
        RuleFor(r => r.Lng).Longitude();
    }
}

public sealed class ListIssuesInAreaValidator : AbstractValidator<ListIssuesInAreaRequest>
{
    public ListIssuesInAreaValidator(AppDbContext db)
    {
        RuleFor(r => r.MinLat).Latitude();
        RuleFor(r => r.MaxLat).Latitude().GreaterThan(r => r.MinLat);
        RuleFor(r => r.MinLng).Longitude();
        RuleFor(r => r.MaxLng).Longitude().GreaterThan(r => r.MinLng);
        RuleFor(r => r.Groups).GroupList(db);
        RuleFor(r => r.Status).StatusList();
    }
}

public sealed class ListRecentIssuesValidator : AbstractValidator<ListRecentIssuesRequest>
{
    public ListRecentIssuesValidator(AppDbContext db)
    {
        RuleFor(r => r.Lat).Latitude();
        RuleFor(r => r.Lng).Longitude();
        RuleFor(r => r.RadiusKm).GreaterThan(0).LessThanOrEqualTo(IssueLimits.MaxRecentRadiusKm);
        RuleFor(r => r.Groups).GroupList(db);
        RuleFor(r => r.Page).InclusiveBetween(1, Pagination.MaxPage);
        RuleFor(r => r.PageSize).InclusiveBetween(1, Pagination.MaxPageSize);
    }
}
