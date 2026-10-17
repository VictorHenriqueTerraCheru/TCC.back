using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Problemas;

namespace Ocorrencias.Api.Modules;

public sealed class ProblemaModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/problemas", ListarNaArea)
            .Validar<ListarProblemasNaAreaRequest>()
            .AllowAnonymous()
            .WithTags("Problemas");

        api.MapGet("/problemas/recentes", ListarRecentes)
            .Validar<ListarProblemasRecentesRequest>()
            .AllowAnonymous()
            .WithTags("Problemas");

        api.MapGet("/problemas/duplicatas", BuscarDuplicatas)
            .Validar<BuscarDuplicatasRequest>()
            .WithTags("Problemas");

        api.MapGet("/problemas/{id:guid}", ObterDetalhe)
            .AllowAnonymous()
            .WithTags("Problemas");

        api.MapPost("/problemas", Registrar)
            .Validar<RegistrarProblemaRequest>()
            .WithTags("Problemas");
    }

    private static Task<List<ListarProblemasNaAreaResponse>> ListarNaArea(
        [AsParameters] ListarProblemasNaAreaRequest request, IProblemaService service, CancellationToken ct) =>
        service.ListarNaAreaAsync(request, ct);

    private static Task<Paginado<ListarProblemasRecentesItem>> ListarRecentes(
        [AsParameters] ListarProblemasRecentesRequest request, IProblemaService service, CancellationToken ct) =>
        service.ListarRecentesAsync(request, ct);

    private static Task<List<BuscarDuplicatasResponse>> BuscarDuplicatas(
        [AsParameters] BuscarDuplicatasRequest request, IProblemaService service, CancellationToken ct) =>
        service.BuscarDuplicatasAsync(request, ct);

    private static async Task<Results<Ok<ObterDetalheProblemaResponse>, NotFound>> ObterDetalhe(
        Guid id, ClaimsPrincipal principal, IProblemaService service, CancellationToken ct)
    {
        var r = await service.ObterDetalheAsync(id, principal.ObterUsuarioIdOuNulo(), ct);
        return r.Erro switch
        {
            null => TypedResults.Ok(r.Valor!),
            var e => e.ParaNaoEncontrado(),
        };
    }

    private static async Task<Results<Created<ObterDetalheProblemaResponse>, ValidationProblem, ProblemHttpResult>> Registrar(
        RegistrarProblemaRequest request, ClaimsPrincipal principal, IProblemaService service, CancellationToken ct)
    {
        var r = await service.RegistrarAsync(request, principal.ObterUsuarioId(), ct);
        return r.Erro switch
        {
            null => TypedResults.Created($"/api/v1/problemas/{r.Valor!.Id}", r.Valor),
            { Tipo: TipoErro.Campo } e => e.ParaValidacao(),
            var e => e.ParaProblema(),
        };
    }
}
