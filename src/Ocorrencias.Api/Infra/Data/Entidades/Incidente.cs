using NetTopologySuite.Geometries;

namespace Ocorrencias.Api.Infra.Data.Entidades;

/// <summary>Incidente com ponto arredondado em 3 casas antes de gravar. O autor nunca é exposto.</summary>
public sealed class Incidente
{
    public Guid Id { get; set; }
    public short CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;
    public Guid AutorId { get; set; }
    public Usuario Autor { get; set; } = null!;

    /// <summary>geography(Point, 4326) já arredondado: X = longitude, Y = latitude.</summary>
    public required Point AreaAproximada { get; set; }

    public short RaioMetros { get; set; }
    public string? RotuloArea { get; set; }
    public DateTimeOffset OcorridoEm { get; set; }
    public DateTimeOffset RegistradoEm { get; set; }
}
