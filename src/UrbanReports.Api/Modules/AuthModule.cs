using Microsoft.AspNetCore.Http.HttpResults;
using UrbanReports.Api.Auth;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Modules;

public sealed class AuthModule : IEndpointModule
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapPost("/auth/register", Register)
            .Validate<RegisterRequest>()
            .AllowAnonymous()
            .WithTags("Auth");

        api.MapPost("/auth/login", Login)
            .Validate<LoginRequest>()
            .AllowAnonymous()
            .WithTags("Auth");
    }

    private static async Task<Results<Created<AuthResponse>, ProblemHttpResult>> Register(RegisterRequest request, IAuthService service)
    {
        var r = await service.RegisterAsync(request);
        return r.Error switch
        {
            null => TypedResults.Created("/api/v1/me", r.Value!),
            var e => e.ToProblem(),
        };
    }

    private static async Task<Results<Ok<AuthResponse>, ProblemHttpResult>> Login(LoginRequest request, IAuthService service)
    {
        var r = await service.LoginAsync(request);
        return r.Error switch
        {
            null => TypedResults.Ok(r.Value!),
            var e => e.ToProblem(),
        };
    }
}
