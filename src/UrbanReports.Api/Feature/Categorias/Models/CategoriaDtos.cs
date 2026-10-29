using UrbanReports.Api.Common;

namespace UrbanReports.Api.Categorias;

public sealed record CategoriaResumo(short Id, string Nome, string Grupo, string Icone);

public sealed record ListarCategoriasResponse(short Id, string Nome, string Grupo, TipoOcorrencia Tipo, string Icone);

public sealed record CategoriaAdminResponse(short Id, string Nome, string Grupo, TipoOcorrencia Tipo, string Icone, bool Ativa);

public sealed record CriarCategoriaRequest(string Nome, string Grupo, TipoOcorrencia Tipo, string Icone);

public sealed record EditarCategoriaRequest(string Nome, string Grupo, string Icone);

public sealed record AlterarAtivaCategoriaRequest
{
    public required bool Ativa { get; init; }
}
