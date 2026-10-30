using UrbanReports.Api.Common;
using UrbanReports.Api.Geocoding;

namespace UrbanReports.Api.Modules;

public sealed class GeocodingModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/geocoding/reverse", Reverse)
            .Validate<ReverseRequest>()
            .RequireRateLimiting(RateLimitPolicies.Geocoding)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous()
            .WithTags("Geocoding");
    }

    private static async Task<ReverseResponse> Reverse(
        [AsParameters] ReverseRequest request, IGeocodingService geocoding, CancellationToken ct)
    {
        var e = await geocoding.ReverseAsync(request.Lat, request.Lng, ct);
        return new ReverseResponse(e.Available, e.Address, e.Street, e.Number, e.Neighborhood, e.City, e.State, e.AreaLabel);
    }
}
