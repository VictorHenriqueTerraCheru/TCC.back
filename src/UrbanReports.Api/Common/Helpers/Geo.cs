using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace UrbanReports.Api.Common;

public static class Geo
{
    public const int Srid = 4326;

    private static readonly GeometryFactory Factory = NtsGeometryServices.Instance.CreateGeometryFactory(Srid);

    public static Point ToPoint(double lat, double lng) => Factory.CreatePoint(new Coordinate(lng, lat));
}
