using UrbanReports.Api.Common;

namespace UrbanReports.Api.Confianca.Motor;

public sealed record ResultadoConfianca(double Indice, FaixaConfianca Faixa, double EPositivo, double ENegativo);
