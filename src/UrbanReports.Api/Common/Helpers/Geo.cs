using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace UrbanReports.Api.Common;

/// <summary>Geometrias WGS 84 (SRID 4326). Atenção à ordem: X = longitude, Y = latitude.</summary>
public static class Geo
{
    public const int Srid = 4326;

    private static readonly GeometryFactory Fabrica = NtsGeometryServices.Instance.CreateGeometryFactory(Srid);

    public static Point Ponto(double lat, double lng) => Fabrica.CreatePoint(new Coordinate(lng, lat));
}
