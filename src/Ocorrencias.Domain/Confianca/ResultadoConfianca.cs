namespace Ocorrencias.Domain.Confianca;

/// <summary><see cref="Indice"/> sem arredondamento: arredonde para 1 casa só na exibição.</summary>
public sealed record ResultadoConfianca(double Indice, FaixaConfianca Faixa, double EPositivo, double ENegativo);
