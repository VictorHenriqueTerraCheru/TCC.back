using FluentValidation;
using Ocorrencias.Api.Common;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Admin;

public sealed class ListarVerificacoesValidator : AbstractValidator<ListarVerificacoesRequest>
{
    private static readonly HashSet<string> NomesStatus = new(Enum.GetNames<StatusVerificacao>(), StringComparer.OrdinalIgnoreCase);

    public ListarVerificacoesValidator()
    {
        RuleFor(r => r.Status)
            .Must(s => s is null || NomesStatus.Contains(s))
            .WithMessage("Status desconhecido. Valores aceitos: pendente, aprovada, recusada.");
        RuleFor(r => r.Pagina).InclusiveBetween(1, Paginacao.PaginaMaxima);
        RuleFor(r => r.Tamanho).InclusiveBetween(1, Paginacao.TamanhoMaximo);
    }
}

public sealed class RecusarVerificacaoValidator : AbstractValidator<RecusarVerificacaoRequest>
{
    public RecusarVerificacaoValidator()
    {
        RuleFor(r => r.Motivo).NotEmpty().MaximumLength(500);
    }
}
