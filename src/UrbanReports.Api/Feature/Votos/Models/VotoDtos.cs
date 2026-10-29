using UrbanReports.Api.Common;

namespace UrbanReports.Api.Votos;

public enum ValorVotoContrato
{
    Sim = ValorVoto.Positivo,
    Nao = ValorVoto.Negativo,
}

public sealed record VotarRequest(ValorVotoContrato Valor, double? Lat, double? Lng);
