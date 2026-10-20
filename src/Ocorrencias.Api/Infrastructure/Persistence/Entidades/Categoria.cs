using Ocorrencias.Domain;

namespace Ocorrencias.Api.Persistence;

public sealed class Categoria
{
    public short Id { get; set; }
    public required string Nome { get; set; }
    public required string Grupo { get; set; }
    public TipoOcorrencia Tipo { get; set; }
    public required string Icone { get; set; }

    /// <summary>Categoria desativada some das listagens e não aceita registros novos; os antigos continuam apontando para ela.</summary>
    public bool Ativa { get; set; } = true;
}
