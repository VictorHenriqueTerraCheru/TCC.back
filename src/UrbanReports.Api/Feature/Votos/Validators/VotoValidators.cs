using FluentValidation;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Votos;

public sealed class VotarValidator : AbstractValidator<VotarRequest>
{
    public VotarValidator()
    {
        RuleFor(r => r.Valor).IsInEnum();
        RegrasPosicaoVoto.Aplicar(this, r => r.Lat, r => r.Lng);
    }
}

internal static class RegrasPosicaoVoto
{
    public static void Aplicar<T>(AbstractValidator<T> validador, Func<T, double?> lat, Func<T, double?> lng)
    {
        validador.RuleFor(r => lat(r)).Latitude().OverridePropertyName("lat");
        validador.RuleFor(r => lng(r)).Longitude().OverridePropertyName("lng");
        validador.RuleFor(r => r)
            .Must(r => lat(r).HasValue == lng(r).HasValue)
            .OverridePropertyName("lat")
            .WithMessage("Informe lat e lng juntos, ou nenhum dos dois.");
    }
}
