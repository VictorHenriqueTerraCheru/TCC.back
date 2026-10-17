using NetTopologySuite.Geometries;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Infra.Data.Entidades;

public sealed class Problema
{
    public Guid Id { get; set; }
    public short CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;
    public Guid AutorId { get; set; }
    public Usuario Autor { get; set; } = null!;
    public string? Descricao { get; set; }

    /// <summary>geography(Point, 4326): X = longitude, Y = latitude.</summary>
    public required Point Localizacao { get; set; }

    public required string EnderecoAproximado { get; set; }
    public StatusProblema Status { get; set; } = StatusProblema.Nova;

    /// <summary>Último índice calculado pelo motor (numeric(5,2)); nulo antes do primeiro voto.</summary>
    public decimal? Confianca { get; set; }

    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset StatusAlteradoEm { get; set; }
    public DateTimeOffset? CondicaoResolucaoDesde { get; set; }
    public List<Voto> Votos { get; set; } = [];
}
