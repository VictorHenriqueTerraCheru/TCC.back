namespace Ocorrencias.Domain.Confianca;

/// <summary>
/// O voto guarda fatos, não pesos. <paramref name="DistanciaMetros"/> nulo = votante sem localização.
/// Um voto por usuário e problema: alimenta o índice de confiança e o ciclo de vida.
/// </summary>
public sealed record VotoFato(
    Guid UsuarioId,
    ValorVoto Valor,
    Perfil PerfilNoMomento,
    double? DistanciaMetros,
    DateTimeOffset RegistradoEm);
