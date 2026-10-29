using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Grupos;

/// <param name="Chave">camelCase sem acento, gerada pela API; é o que categorias e filtros <c>grupos=</c> usam.</param>
public sealed record GrupoResponse(string Chave, string Rotulo, TipoOcorrencia Tipo, CorGrupo Cor, int Ordem);

/// <param name="Rotulo">Nome exibido (até 40), único sem diferenciar maiúsculas.</param>
public sealed record CriarGrupoRequest(string Rotulo, TipoOcorrencia Tipo, CorGrupo Cor);

/// <summary>O tipo não muda depois de criado.</summary>
public sealed record EditarGrupoRequest(string Rotulo, CorGrupo Cor, int Ordem);
