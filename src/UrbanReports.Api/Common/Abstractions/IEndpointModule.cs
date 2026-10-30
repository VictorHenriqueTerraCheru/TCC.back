namespace UrbanReports.Api.Common;

public interface IEndpointModule
{
    static abstract void Map(IEndpointRouteBuilder api);
}
