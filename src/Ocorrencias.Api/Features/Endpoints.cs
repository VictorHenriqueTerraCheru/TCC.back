using Ocorrencias.Api.Features.Admin;

namespace Ocorrencias.Api.Features;

/// <summary>Features ainda não migradas para <c>Modules/</c>. Removido quando a última área migrar.</summary>
public static class Endpoints
{
    public static RouteGroupBuilder MapFeaturesLegadas(this RouteGroupBuilder api)
    {
        api.MapListarVerificacoes();
        api.MapAprovarVerificacao();
        api.MapRecusarVerificacao();

        return api;
    }
}
