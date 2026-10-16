using Ocorrencias.Api.Categorias;
using Ocorrencias.Api.Common;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Usuarios;

public sealed record ObterMeResponse(
    Guid Id,
    string Nome,
    string Email,
    Perfil Perfil,
    DateTimeOffset CriadoEm,
    DateTimeOffset? VerificadoEm,
    int TotalRegistros,
    int TotalVotos,
    ObterMeVerificacaoResumo? Verificacao);

/// <summary>Solicitação de verificação mais recente.</summary>
public sealed record ObterMeVerificacaoResumo(
    StatusVerificacao Status, TipoVinculo TipoVinculo, string? MotivoRecusa, DateTimeOffset CriadaEm);

public sealed record AtualizarMeRequest(string Nome);

public enum ObterHistoricoTipoItem
{
    RegistroProblema = 1,
    RegistroIncidente = 2,
    VotoExistencia = 3,
    VotoPersistencia = 4,
}

public enum ObterHistoricoValorItem
{
    Confirma = 1,
    NaoVi = 2,
    Continua = 3,
    Resolvido = 4,
}

/// <param name="Filtro">tudo | registros | votos.</param>
public sealed record ObterHistoricoRequest(string Filtro = "tudo", int Pagina = 1, int Tamanho = Paginacao.TamanhoPadrao);

/// <summary>
/// Para <c>registroIncidente</c>, <c>endereco</c> é o rótulo da área (nulo se a geocodificação falhou) e
/// <c>ocorrenciaId</c> é nulo: o incidente não é navegável.
/// </summary>
public sealed record ObterHistoricoItem(
    ObterHistoricoTipoItem Tipo,
    Guid? OcorrenciaId,
    CategoriaResumo Categoria,
    string? Endereco,
    ObterHistoricoValorItem? Valor,
    StatusProblema? StatusProblema,
    DateTimeOffset Data);
