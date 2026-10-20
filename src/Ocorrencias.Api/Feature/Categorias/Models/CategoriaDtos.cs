using Ocorrencias.Domain;

namespace Ocorrencias.Api.Categorias;

/// <summary>Categoria embutida em outras respostas (<c>{ id, nome, grupo, icone }</c>).</summary>
public sealed record CategoriaResumo(short Id, string Nome, string Grupo, string Icone);

public sealed record ListarCategoriasResponse(short Id, string Nome, string Grupo, TipoOcorrencia Tipo, string Icone);

/// <summary>Visão do administrador: inclui as desativadas.</summary>
public sealed record CategoriaAdminResponse(short Id, string Nome, string Grupo, TipoOcorrencia Tipo, string Icone, bool Ativa);

/// <param name="Grupo">Chave de um grupo existente (<c>GET /grupos</c>), do mesmo tipo da categoria.</param>
/// <param name="Icone">Nome do ícone (Material Symbols, ex.: <c>flood</c>): minúsculas, números, hífen e sublinhado.</param>
public sealed record CriarCategoriaRequest(string Nome, string Grupo, TipoOcorrencia Tipo, string Icone);

/// <summary>O tipo (problema ou incidente) não muda depois de criada.</summary>
public sealed record EditarCategoriaRequest(string Nome, string Grupo, string Icone);

public sealed record AlterarAtivaCategoriaRequest(bool Ativa);
