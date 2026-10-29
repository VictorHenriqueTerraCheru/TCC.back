using NetTopologySuite.Geometries;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Persistence;

public sealed class Problema
{
    public Guid Id { get; set; }
    public short CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;
    public Guid AutorId { get; set; }
    public Usuario Autor { get; set; } = null!;
    public string? Descricao { get; set; }

    public required Point Localizacao { get; set; }

    public required string EnderecoAproximado { get; set; }
    public StatusProblema Status { get; set; } = StatusProblema.Nova;

    public decimal? Confianca { get; set; }

    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset StatusAlteradoEm { get; set; }
    public DateTimeOffset? CondicaoResolucaoDesde { get; set; }
    public List<Voto> Votos { get; set; } = [];
}
