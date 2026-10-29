using UrbanReports.Api.Common;

namespace UrbanReports.Api.Confianca.Motor;

public sealed record EstadoProblema(
    StatusProblema Status,
    DateTimeOffset CriadoEm,
    DateTimeOffset StatusAlteradoEm,
    DateTimeOffset? CondicaoResolucaoDesde);
