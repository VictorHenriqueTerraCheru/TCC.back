using FluentValidation;
using UrbanReports.Api.Common;
using UrbanReports.Api.Persistence;

namespace UrbanReports.Api.Incidentes;

public sealed class RegistrarIncidenteValidator : AbstractValidator<RegistrarIncidenteRequest>
{
    public RegistrarIncidenteValidator()
    {
        RuleFor(r => r.CategoriaId).GreaterThan((short)0);
        RuleFor(r => r.Lat).Latitude();
        RuleFor(r => r.Lng).Longitude();
    }
}

public sealed class ObterCalorIncidentesValidator : AbstractValidator<ObterCalorIncidentesRequest>
{
    public ObterCalorIncidentesValidator(AppDbContext db)
    {
        RuleFor(r => r.MinLat).Latitude();
        RuleFor(r => r.MaxLat).Latitude().GreaterThan(r => r.MinLat);
        RuleFor(r => r.MinLng).Longitude();
        RuleFor(r => r.MaxLng).Longitude().GreaterThan(r => r.MinLng);
        RuleFor(r => r.Dias).InclusiveBetween(1, LimitesIncidente.DiasCalorMaximo);
        RuleFor(r => r.Grupos).ListaDeGrupos(db);
    }
}

public sealed class ObterAreaIncidentesValidator : AbstractValidator<ObterAreaIncidentesRequest>
{
    public ObterAreaIncidentesValidator()
    {
        RuleFor(r => r.Lat).Latitude();
        RuleFor(r => r.Lng).Longitude();
    }
}
