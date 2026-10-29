using FluentValidation;

namespace UrbanReports.Api.Verificacao;

public sealed class SolicitarVerificacaoValidator : AbstractValidator<SolicitarVerificacaoRequest>
{
    public SolicitarVerificacaoValidator()
    {
        RuleFor(r => r.TipoVinculo).IsInEnum();
        RuleFor(r => r.Justificativa)
            .NotEmpty()
            .Must(j => j?.Trim().Length is >= 30 and <= 500)
            .WithMessage("A justificativa precisa ter entre 30 e 500 caracteres.");
    }
}
