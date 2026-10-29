using NetTopologySuite.Geometries;

namespace UrbanReports.Api.Persistence;

public sealed class Incidente
{
    public Guid Id { get; set; }
    public short CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;
    public Guid AutorId { get; set; }
    public Usuario Autor { get; set; } = null!;

    public required Point AreaAproximada { get; set; }

    public short RaioMetros { get; set; }
    public string? RotuloArea { get; set; }
    public DateTimeOffset OcorridoEm { get; set; }
    public DateTimeOffset RegistradoEm { get; set; }
}
