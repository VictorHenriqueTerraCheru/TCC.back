using Microsoft.AspNetCore.Http.HttpResults;
using UrbanReports.Api.Auth;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Modules;

public sealed class AuthModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapPost("/auth/cadastro", Cadastrar)
            .Validar<CadastrarRequest>()
            .AllowAnonymous()
            .WithTags("Auth");

        api.MapPost("/auth/login", Entrar)
            .Validar<EntrarRequest>()
            .AllowAnonymous()
            .WithTags("Auth");
    }

    private static async Task<Results<Created<RespostaAuth>, ProblemHttpResult>> Cadastrar(CadastrarRequest request, IAuthService service)
    {
        var r = await service.CadastrarAsync(request);
        return r.Erro switch
        {
            null => TypedResults.Created("/api/v1/me", r.Valor!),
            var e => e.ParaProblema(),
        };
    }

    private static async Task<Results<Ok<RespostaAuth>, ProblemHttpResult>> Entrar(EntrarRequest request, IAuthService service)
    {
        var r = await service.EntrarAsync(request);
        return r.Erro switch
        {
            null => TypedResults.Ok(r.Valor!),
            var e => e.ParaProblema(),
        };
    }
}
