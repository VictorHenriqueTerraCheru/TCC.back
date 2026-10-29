using UrbanReports.Api.Common;

namespace UrbanReports.Api.Incidentes;

public sealed record RegistrarIncidenteRequest(short CategoriaId, double Lat, double Lng, DateTimeOffset OcorridoEm);

public sealed record ObterCalorIncidentesRequest(double MinLat, double MinLng, double MaxLat, double MaxLng, int Dias = 7, string? Grupos = null);

/// <summary>Célula do mapa de calor: o ponto já é arredondado em 3 casas na gravação.</summary>
public sealed record ObterCalorIncidentesResponse(double Lat, double Lng, int Quantidade);

public sealed record ObterAreaIncidentesRequest(double Lat, double Lng);

public sealed record ObterAreaIncidentesResponse(
    int RaioMetros, DateTimeOffset AtualizadoEm, NivelConcentracao Nivel, IReadOnlyList<ObterAreaIncidentesContagem> Contagens);

public sealed record ObterAreaIncidentesContagem(short CategoriaId, string Nome, int Dias7, int Dias30);

/// <summary>Resultado interno do registro: o ponto arredondado (para o <c>Location</c>) e a área. Não é DTO de contrato.</summary>
public sealed record IncidenteRegistrado(double Lat, double Lng, ObterAreaIncidentesResponse Area);
