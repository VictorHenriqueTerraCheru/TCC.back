using Ocorrencias.Api.Features.Admin;
using Ocorrencias.Api.Features.Auth;
using Ocorrencias.Api.Features.Categorias;
using Ocorrencias.Api.Features.Geocodificacao;
using Ocorrencias.Api.Features.Incidentes;
using Ocorrencias.Api.Features.Problemas;
using Ocorrencias.Api.Features.Usuarios;
using Ocorrencias.Api.Features.Verificacao;
using Ocorrencias.Api.Features.Votos;
using Ocorrencias.Api.Infrastructure;

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
        api.MapObterHistorico();

        api.MapListarCategorias();

        api.MapReversa();

        api.MapListarProblemasNaArea();
        api.MapListarProblemasRecentes();
        api.MapBuscarDuplicatas();
        api.MapObterDetalheProblema();
        api.MapRegistrarProblema();

        api.MapVotarExistencia();
        api.MapVotarPersistencia();

        api.MapRegistrarIncidente();
        api.MapObterCalorIncidentes();
        api.MapObterAreaIncidentes();

        api.MapSolicitarVerificacao();

        api.MapListarVerificacoes();
        api.MapAprovarVerificacao();
        api.MapRecusarVerificacao();

        return app;
    }
}
