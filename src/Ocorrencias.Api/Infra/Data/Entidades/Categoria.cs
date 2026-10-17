using Ocorrencias.Domain;

namespace Ocorrencias.Api.Infra.Data.Entidades;

public sealed class Categoria
{
    public short Id { get; set; }
    public required string Nome { get; set; }
    public required string Grupo { get; set; }
    public TipoOcorrencia Tipo { get; set; }
    public required string Icone { get; set; }
}
