using FluentValidation;

namespace UrbanReports.Api.Verification;

public sealed class RequestVerificationValidator : AbstractValidator<RequestVerificationRequest>
{
    public RequestVerificationValidator()
    {
        RuleFor(r => r.AffiliationType).IsInEnum();
        RuleFor(r => r.Justification)
            .NotEmpty()
            .Must(j => j?.Trim().Length is >= 30 and <= 500)
            .WithMessage("A justificativa precisa ter entre 30 e 500 caracteres.");
    }
}
