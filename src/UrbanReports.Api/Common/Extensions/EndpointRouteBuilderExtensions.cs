using System.Reflection;

namespace UrbanReports.Api.Common;

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization(AuthorizationPolicies.Authenticated);

        foreach (var module in Modules())
        {
            module.GetMethod(nameof(IEndpointModule.Map), BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [api]);
        }

        return app;
    }

    private static IEnumerable<Type> Modules() =>
        typeof(EndpointRouteBuilderExtensions).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IEndpointModule).IsAssignableFrom(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal);
}
