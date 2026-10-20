using Ocorrencias.Domain;

namespace Ocorrencias.Api.Categorias;

/// <summary>Categoria embutida em outras respostas (<c>{ id, nome, grupo, icone }</c>).</summary>
public sealed record CategoriaResumo(short Id, string Nome, string Grupo, string Icone);

public sealed record ListarCategoriasResponse(short Id, string Nome, string Grupo, TipoOcorrencia Tipo, string Icone);

/// <summary>Visão do administrador: inclui as desativadas.</summary>
public sealed record CategoriaAdminResponse(short Id, string Nome, string Grupo, TipoOcorrencia Tipo, string Icone, bool Ativa);

/// <param name="Grupo">camelCase, até 30 caracteres (ex.: <c>clima</c>). Grupo novo nasce ao ser usado.</param>
/// <param name="Icone">Chave do ícone no front (minúsculas, números e hífen).</param>
public sealed record CriarCategoriaRequest(string Nome, string Grupo, TipoOcorrencia Tipo, string Icone);

/// <summary>O tipo (problema ou incidente) não muda depois de criada.</summary>
public sealed record EditarCategoriaRequest(string Nome, string Grupo, string Icone);

/// <summary><c>required</c>: corpo sem <c>ativa</c> dá 400, em vez de assumir <c>false</c> e desativar a categoria.</summary>
public sealed record AlterarAtivaCategoriaRequest
{
    public required bool Ativa { get; init; }
}
