using UrbanReports.Api.Common;

namespace UrbanReports.Api.Persistence;

public sealed class GrupoCategoria
{
    public required string Chave { get; set; }
    public required string Rotulo { get; set; }
    public TipoOcorrencia Tipo { get; set; }
    public CorGrupo Cor { get; set; }
    public int Ordem { get; set; }
}
