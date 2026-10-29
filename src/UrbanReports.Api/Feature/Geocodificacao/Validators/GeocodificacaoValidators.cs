using FluentValidation;

namespace UrbanReports.Api.Geocoding;

public sealed class ReverseValidator : AbstractValidator<ReverseRequest>
{
    public ReverseValidator()
    {
        RuleFor(r => r.Lat).InclusiveBetween(-90, 90);
        RuleFor(r => r.Lng).InclusiveBetween(-180, 180);
    }
}
