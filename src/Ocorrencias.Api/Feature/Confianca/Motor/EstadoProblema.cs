using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Confianca.Motor;

public sealed record EstadoProblema(
    StatusProblema Status,
    DateTimeOffset CriadoEm,
    DateTimeOffset StatusAlteradoEm,
    DateTimeOffset? CondicaoResolucaoDesde);
