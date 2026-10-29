using FluentValidation;
using UrbanReports.Api.Common;
using UrbanReports.Api.Persistence;

namespace UrbanReports.Api.Incidents;

public sealed class CreateIncidentValidator : AbstractValidator<CreateIncidentRequest>
{
    public CreateIncidentValidator()
    {
        RuleFor(r => r.CategoryId).GreaterThan((short)0);
        RuleFor(r => r.Lat).Latitude();
        RuleFor(r => r.Lng).Longitude();
    }
}

public sealed class GetIncidentHeatmapValidator : AbstractValidator<GetIncidentHeatmapRequest>
{
    public GetIncidentHeatmapValidator(AppDbContext db)
    {
        RuleFor(r => r.MinLat).Latitude();
        RuleFor(r => r.MaxLat).Latitude().GreaterThan(r => r.MinLat);
        RuleFor(r => r.MinLng).Longitude();
        RuleFor(r => r.MaxLng).Longitude().GreaterThan(r => r.MinLng);
        RuleFor(r => r.Days).InclusiveBetween(1, IncidentLimits.MaxHeatmapDays);
        RuleFor(r => r.Groups).GroupList(db);
    }
}

public sealed class GetIncidentAreaValidator : AbstractValidator<GetIncidentAreaRequest>
{
    public GetIncidentAreaValidator()
    {
        RuleFor(r => r.Lat).Latitude();
        RuleFor(r => r.Lng).Longitude();
    }
}
