using UrbanReports.Api.Common;

namespace UrbanReports.Api.Incidentes;

public sealed record RegistrarIncidenteRequest(short CategoriaId, double Lat, double Lng, DateTimeOffset OcorridoEm);

public sealed record ObterCalorIncidentesRequest(double MinLat, double MinLng, double MaxLat, double MaxLng, int Dias = 7, string? Grupos = null);

public sealed record ObterCalorIncidentesResponse(double Lat, double Lng, int Quantidade);

public sealed record ObterAreaIncidentesRequest(double Lat, double Lng);

public sealed record ObterAreaIncidentesResponse(
    int RaioMetros, DateTimeOffset AtualizadoEm, NivelConcentracao Nivel, IReadOnlyList<ObterAreaIncidentesContagem> Contagens);

public sealed record ObterAreaIncidentesContagem(short CategoriaId, string Nome, int Dias7, int Dias30);

public sealed record IncidenteRegistrado(double Lat, double Lng, ObterAreaIncidentesResponse Area);
