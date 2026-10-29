using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Verificacao;

public sealed record SolicitarVerificacaoRequest(TipoVinculo TipoVinculo, string Justificativa);

public sealed record SolicitarVerificacaoResponse(
    Guid Id, TipoVinculo TipoVinculo, string Justificativa, StatusVerificacao Status, DateTimeOffset CriadaEm);
