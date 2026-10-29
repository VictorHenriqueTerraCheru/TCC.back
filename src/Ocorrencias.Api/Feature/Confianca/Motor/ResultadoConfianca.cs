using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Confianca.Motor;

/// <summary><see cref="Indice"/> sem arredondamento: arredonde para 1 casa só na exibição.</summary>
public sealed record ResultadoConfianca(double Indice, FaixaConfianca Faixa, double EPositivo, double ENegativo);
