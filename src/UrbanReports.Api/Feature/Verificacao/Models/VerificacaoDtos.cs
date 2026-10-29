using UrbanReports.Api.Common;

namespace UrbanReports.Api.Verificacao;

public sealed record SolicitarVerificacaoRequest(TipoVinculo TipoVinculo, string Justificativa);

public sealed record SolicitarVerificacaoResponse(
    Guid Id, TipoVinculo TipoVinculo, string Justificativa, StatusVerificacao Status, DateTimeOffset CriadaEm);
