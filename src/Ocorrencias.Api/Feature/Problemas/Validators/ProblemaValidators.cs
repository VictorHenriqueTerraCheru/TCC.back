using FluentValidation;
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Problemas;

public sealed class RegistrarProblemaValidator : AbstractValidator<RegistrarProblemaRequest>
{
    public RegistrarProblemaValidator()
    {
        RuleFor(r => r.CategoriaId).GreaterThan((short)0);
        RuleFor(r => r.Lat).Latitude();
        RuleFor(r => r.Lng).Longitude();
        RuleFor(r => r.Descricao).MaximumLength(280);
    }
}

public sealed class BuscarDuplicatasValidator : AbstractValidator<BuscarDuplicatasRequest>
{
    public BuscarDuplicatasValidator()
    {
        RuleFor(r => r.CategoriaId).GreaterThan((short)0);
        RuleFor(r => r.Lat).Latitude();
        RuleFor(r => r.Lng).Longitude();
    }
}

public sealed class ListarProblemasNaAreaValidator : AbstractValidator<ListarProblemasNaAreaRequest>
{
    public ListarProblemasNaAreaValidator()
    {
        RuleFor(r => r.MinLat).Latitude();
        RuleFor(r => r.MaxLat).Latitude().GreaterThan(r => r.MinLat);
        RuleFor(r => r.MinLng).Longitude();
        RuleFor(r => r.MaxLng).Longitude().GreaterThan(r => r.MinLng);
        RuleFor(r => r.Grupos).ListaDeGrupos();
        RuleFor(r => r.Status).ListaDeStatus();
    }
}

public sealed class ListarProblemasRecentesValidator : AbstractValidator<ListarProblemasRecentesRequest>
{
    public ListarProblemasRecentesValidator()
    {
        RuleFor(r => r.Lat).Latitude();
        RuleFor(r => r.Lng).Longitude();
        RuleFor(r => r.RaioKm).GreaterThan(0).LessThanOrEqualTo(LimitesProblema.RaioMaximoRecentesKm);
        RuleFor(r => r.Grupos).ListaDeGrupos();
        RuleFor(r => r.Pagina).InclusiveBetween(1, Paginacao.PaginaMaxima);
        RuleFor(r => r.Tamanho).InclusiveBetween(1, Paginacao.TamanhoMaximo);
    }
}
