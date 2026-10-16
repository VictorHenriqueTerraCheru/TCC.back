using Ocorrencias.Domain;

namespace Ocorrencias.Api.Features.Votos;

/// <summary>Voto de existência no contrato: <c>confirma</c> | <c>naoVi</c>. Mesmos números de <see cref="ValorVoto"/>.</summary>
public enum ValorExistencia
{
    Confirma = ValorVoto.Positivo,
    NaoVi = ValorVoto.Negativo,
}

/// <summary>Voto de persistência no contrato: <c>continua</c> | <c>resolvido</c>. Mesmos números de <see cref="ValorVoto"/>.</summary>
public enum ValorPersistencia
{
    Continua = ValorVoto.Positivo,
    Resolvido = ValorVoto.Negativo,
}
