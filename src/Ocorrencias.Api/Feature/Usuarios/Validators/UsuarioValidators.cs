using FluentValidation;
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Usuarios;

public sealed class AtualizarMeValidator : AbstractValidator<AtualizarMeRequest>
{
    public AtualizarMeValidator()
    {
        RuleFor(r => r.Nome).NotEmpty().MaximumLength(120);
    }
}

public sealed class ObterHistoricoValidator : AbstractValidator<ObterHistoricoRequest>
{
    private static readonly string[] Filtros = ["tudo", "registros", "votos"];

    public ObterHistoricoValidator()
    {
        RuleFor(r => r.Filtro).Must(f => Filtros.Contains(f)).WithMessage("Filtro deve ser tudo, registros ou votos.");
        RuleFor(r => r.Pagina).InclusiveBetween(1, Paginacao.PaginaMaxima);
        RuleFor(r => r.Tamanho).InclusiveBetween(1, Paginacao.TamanhoMaximo);
    }
}
