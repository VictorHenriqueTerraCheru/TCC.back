using UrbanReports.Api.Common;

namespace UrbanReports.Api.Admin;

public sealed record ListarVerificacoesRequest(string? Status = null, int Pagina = 1, int Tamanho = Paginacao.TamanhoPadrao);

public sealed record ListarVerificacoesItem(
    Guid Id,
    ListarVerificacoesUsuarioResumo Usuario,
    TipoVinculo TipoVinculo,
    string Justificativa,
    StatusVerificacao Status,
    DateTimeOffset CriadaEm,
    string? MotivoRecusa);

public sealed record ListarVerificacoesUsuarioResumo(Guid Id, string Nome, string Iniciais);

public sealed record RecusarVerificacaoRequest(string Motivo);

public sealed record ListarUsuariosRequest(string? Busca = null, string? Perfil = null, int Pagina = 1, int Tamanho = Paginacao.TamanhoPadrao);

public sealed record ListarUsuariosItem(
    Guid Id,
    string Nome,
    string Email,
    Perfil Perfil,
    DateTimeOffset CriadoEm,
    DateTimeOffset? VerificadoEm);

public sealed record AlterarPerfilRequest(Perfil Perfil);
