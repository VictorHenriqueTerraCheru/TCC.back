using System.Reflection;

namespace Ocorrencias.Api.Common;

public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Registra todos os <see cref="IEndpointModule"/> sob <c>/api/v1</c>. Por padrão exige autenticação; endpoints de
    /// visitante (🔓 em docs/API.md) chamam <c>AllowAnonymous()</c> no próprio módulo.
    /// </summary>
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
