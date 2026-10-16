using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace Ocorrencias.Api.Common;

/// <summary>Geometrias WGS 84 (SRID 4326). Atenção à ordem: X = longitude, Y = latitude.</summary>
public static class Geo
{
    public const int Srid = 4326;

    private static readonly GeometryFactory Fabrica = NtsGeometryServices.Instance.CreateGeometryFactory(Srid);

    public static Point Ponto(double lat, double lng) => Fabrica.CreatePoint(new Coordinate(lng, lat));

    /// <summary>Retângulo da área visível do mapa.</summary>
    public static Polygon Retangulo(double minLat, double minLng, double maxLat, double maxLng) =>
        Fabrica.CreatePolygon(
        [
            new Coordinate(minLng, minLat),
            new Coordinate(maxLng, minLat),
            new Coordinate(maxLng, maxLat),
            new Coordinate(minLng, maxLat),
            new Coordinate(minLng, minLat),
        ]);
}
