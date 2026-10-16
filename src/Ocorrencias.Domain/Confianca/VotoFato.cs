namespace Ocorrencias.Domain.Confianca;

/// <summary>
/// O voto guarda fatos, não pesos. <paramref name="DistanciaMetros"/> nulo = votante sem localização.
/// </summary>
public sealed record VotoFato(
    Guid UsuarioId,
    TipoVoto Tipo,
    ValorVoto Valor,
    Perfil PerfilNoMomento,
    double? DistanciaMetros,
    DateTimeOffset RegistradoEm);
