using Ocorrencias.Api.Features.Admin;
using Ocorrencias.Api.Features.Geocodificacao;
using Ocorrencias.Api.Features.Incidentes;
using Ocorrencias.Api.Features.Problemas;
using Ocorrencias.Api.Features.Usuarios;
using Ocorrencias.Api.Features.Verificacao;
using Ocorrencias.Api.Features.Votos;

namespace Ocorrencias.Api.Features;

/// <summary>Features ainda não migradas para <c>Modules/</c>. Removido quando a última área migrar.</summary>
public static class Endpoints
{
    public static RouteGroupBuilder MapFeaturesLegadas(this RouteGroupBuilder api)
    {
        api.MapObterMe();
        api.MapAtualizarMe();
        api.MapObterHistorico();

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

        return api;
    }
}
