using FluentValidation;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Admin;

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

public sealed class ListarUsuariosValidator : AbstractValidator<ListarUsuariosRequest>
{
    private static readonly HashSet<string> NomesPerfil = new(Enum.GetNames<Perfil>(), StringComparer.OrdinalIgnoreCase);

    public ListarUsuariosValidator()
    {
        RuleFor(r => r.Busca).MaximumLength(100);
        RuleFor(r => r.Perfil)
            .Must(p => p is null || NomesPerfil.Contains(p))
            .WithMessage("Perfil desconhecido. Valores aceitos: cidadao, cidadaoVerificado, administrador.");
        RuleFor(r => r.Pagina).InclusiveBetween(1, Paginacao.PaginaMaxima);
        RuleFor(r => r.Tamanho).InclusiveBetween(1, Paginacao.TamanhoMaximo);
    }
}

public sealed class AlterarPerfilValidator : AbstractValidator<AlterarPerfilRequest>
{
    public AlterarPerfilValidator()
    {
        RuleFor(r => r.Perfil).IsInEnum();
    }
}
