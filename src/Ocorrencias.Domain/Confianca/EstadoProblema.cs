namespace Ocorrencias.Domain.Confianca;

public sealed record EstadoProblema(
    StatusProblema Status,
    DateTimeOffset CriadoEm,
    DateTimeOffset StatusAlteradoEm,
    DateTimeOffset? CondicaoResolucaoDesde);
