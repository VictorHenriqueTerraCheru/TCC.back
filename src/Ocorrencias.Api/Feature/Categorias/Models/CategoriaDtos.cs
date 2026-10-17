using Ocorrencias.Domain;

namespace Ocorrencias.Api.Categorias;

/// <summary>Categoria embutida em outras respostas (<c>{ id, nome, grupo, icone }</c>).</summary>
public sealed record CategoriaResumo(short Id, string Nome, string Grupo, string Icone);

public sealed record ListarCategoriasResponse(short Id, string Nome, string Grupo, TipoOcorrencia Tipo, string Icone);
