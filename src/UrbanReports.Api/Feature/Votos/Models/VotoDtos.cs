using UrbanReports.Api.Common;

namespace UrbanReports.Api.Votos;

/// <summary>Resposta a "Ainda está lá?" no contrato: <c>sim</c> | <c>nao</c>. Mesmos números de <see cref="ValorVoto"/>.</summary>
public enum ValorVotoContrato
{
    Sim = ValorVoto.Positivo,
    Nao = ValorVoto.Negativo,
}

/// <summary><c>lat</c>/<c>lng</c> opcionais: sem localização, o peso de proximidade é 1.</summary>
public sealed record VotarRequest(ValorVotoContrato Valor, double? Lat, double? Lng);
