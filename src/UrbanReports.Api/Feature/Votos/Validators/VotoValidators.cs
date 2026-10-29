using FluentValidation;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Votes;

public sealed class VoteValidator : AbstractValidator<VoteRequest>
{
    public VoteValidator()
    {
        RuleFor(r => r.Value).IsInEnum();
        VotePositionRules.Apply(this, r => r.Lat, r => r.Lng);
    }
}

internal static class VotePositionRules
{
    public static void Apply<T>(AbstractValidator<T> validator, Func<T, double?> lat, Func<T, double?> lng)
    {
        validator.RuleFor(r => lat(r)).Latitude().OverridePropertyName("lat");
        validator.RuleFor(r => lng(r)).Longitude().OverridePropertyName("lng");
        validator.RuleFor(r => r)
            .Must(r => lat(r).HasValue == lng(r).HasValue)
            .OverridePropertyName("lat")
            .WithMessage("Informe lat e lng juntos, ou nenhum dos dois.");
    }
}
