namespace Ocorrencias.Api.Features.Categorias;

/// <summary>Categoria embutida em outras respostas (<c>{ id, nome, grupo, icone }</c>).</summary>
public sealed record CategoriaResumo(short Id, string Nome, string Grupo, string Icone);
