using UrbanReports.Api.Common;

namespace UrbanReports.Api.Grupos;

public sealed record GrupoResponse(string Chave, string Rotulo, TipoOcorrencia Tipo, CorGrupo Cor, int Ordem);

public sealed record CriarGrupoRequest(string Rotulo, TipoOcorrencia Tipo, CorGrupo Cor);

public sealed record EditarGrupoRequest(string Rotulo, CorGrupo Cor, int Ordem);
