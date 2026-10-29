using UrbanReports.Api.Categorias;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Usuarios;

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

public sealed record ObterMeVerificacaoResumo(
    StatusVerificacao Status, TipoVinculo TipoVinculo, string? MotivoRecusa, DateTimeOffset CriadaEm);

public sealed record AtualizarMeRequest(string Nome);

public enum ObterHistoricoTipoItem
{
    RegistroProblema = 1,
    RegistroIncidente = 2,
    Voto = 3,
}

public enum ObterHistoricoValorItem
{
    Sim = 1,
    Nao = 2,
}

public sealed record ObterHistoricoRequest(string Filtro = "tudo", int Pagina = 1, int Tamanho = Paginacao.TamanhoPadrao);

public sealed record ObterHistoricoItem(
    ObterHistoricoTipoItem Tipo,
    Guid? OcorrenciaId,
    CategoriaResumo Categoria,
    string? Endereco,
    ObterHistoricoValorItem? Valor,
    StatusProblema? StatusProblema,
    DateTimeOffset Data);
