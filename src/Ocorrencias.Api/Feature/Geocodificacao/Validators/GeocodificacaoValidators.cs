using FluentValidation;

namespace Ocorrencias.Api.Geocodificacao;

public sealed class ReversaValidator : AbstractValidator<ReversaRequest>
{
    public ReversaValidator()
    {
        RuleFor(r => r.Lat).InclusiveBetween(-90, 90);
        RuleFor(r => r.Lng).InclusiveBetween(-180, 180);
    }
}
