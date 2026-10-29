using System.Reflection;

namespace UrbanReports.Api.Common;

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization(PoliticasAutorizacao.Autenticado);

        foreach (var modulo in Modulos())
        {
            modulo.GetMethod(nameof(IEndpointModule.Map), BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [api]);
        }

        return app;
    }

    private static IEnumerable<Type> Modulos() =>
        typeof(EndpointRouteBuilderExtensions).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IEndpointModule).IsAssignableFrom(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal);
}
