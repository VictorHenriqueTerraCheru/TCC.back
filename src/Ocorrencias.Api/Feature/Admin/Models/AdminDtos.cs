using Ocorrencias.Api.Common;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Admin;

/// <param name="Status">pendente | aprovada | recusada. Vazio = todas.</param>
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

/// <param name="Busca">Trecho do nome ou do e-mail, sem diferenciar maiúsculas.</param>
/// <param name="Perfil">cidadao | cidadaoVerificado | administrador. Vazio = todos.</param>
public sealed record ListarUsuariosRequest(string? Busca = null, string? Perfil = null, int Pagina = 1, int Tamanho = Paginacao.TamanhoPadrao);

public sealed record ListarUsuariosItem(
    Guid Id,
    string Nome,
    string Email,
    Perfil Perfil,
    DateTimeOffset CriadoEm,
    DateTimeOffset? VerificadoEm);

public sealed record AlterarPerfilRequest(Perfil Perfil);
