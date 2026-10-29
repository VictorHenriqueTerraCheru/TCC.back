using UrbanReports.Api.Common;

namespace UrbanReports.Api.Confianca.Motor;

public sealed record VotoFato(
    Guid UsuarioId,
    ValorVoto Valor,
    Perfil PerfilNoMomento,
    double? DistanciaMetros,
    DateTimeOffset RegistradoEm);
