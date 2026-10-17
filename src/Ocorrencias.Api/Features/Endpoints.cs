using Ocorrencias.Api.Features.Auth;
using Ocorrencias.Api.Features.Categorias;
using Ocorrencias.Api.Features.Usuarios;
using Ocorrencias.Api.Infra.Auth;

namespace Ocorrencias.Api.Features;

public static class Endpoints
{
    /// <summary>
    /// Registra todas as features sob <c>/api/v1</c>. Por padrão exige autenticação; endpoints de visitante
    /// (🔓 em docs/API.md) chamam <c>AllowAnonymous()</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapFeatures(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization(AuthConfig.Autenticado);

        api.MapCadastrar();
        api.MapEntrar();

        api.MapObterMe();
        api.MapAtualizarMe();

        api.MapListarCategorias();

        return app;
    }
}
