using FluentValidation;

namespace UrbanReports.Api.Auth;

public sealed class CadastrarValidator : AbstractValidator<CadastrarRequest>
{
    public CadastrarValidator()
    {
        RuleFor(r => r.Nome).NotEmpty().MaximumLength(120);
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(r => r.Senha).NotEmpty().MinimumLength(8).WithMessage("A senha precisa ter pelo menos 8 caracteres.");
        RuleFor(r => r.AceitouTermos).Equal(true).WithMessage("É preciso aceitar os termos de uso.");
    }
}

public sealed class EntrarValidator : AbstractValidator<EntrarRequest>
{
    public EntrarValidator()
    {
        RuleFor(r => r.Email).NotEmpty();
        RuleFor(r => r.Senha).NotEmpty();
    }
}
