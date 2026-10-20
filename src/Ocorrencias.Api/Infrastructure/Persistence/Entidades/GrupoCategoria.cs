using Ocorrencias.Domain;

namespace Ocorrencias.Api.Persistence;

/// <summary>
/// Agrupa categorias (chip do mapa, seção da tela de registro). Tem um tipo só: a categoria entra em grupo do
/// mesmo tipo. A <see cref="Chave"/> é gerada a partir do rótulo e não muda (vai em filtros de URL e na FK).
/// </summary>
public sealed class GrupoCategoria
{
    public required string Chave { get; set; }
    public required string Rotulo { get; set; }
    public TipoOcorrencia Tipo { get; set; }
    public CorGrupo Cor { get; set; }
    public int Ordem { get; set; }
}
