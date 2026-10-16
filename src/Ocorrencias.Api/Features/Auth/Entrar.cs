using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Auth;
using Ocorrencias.Api.Persistence;

namespace Ocorrencias.Api.Features.Auth;

public static class Entrar
{
    public sealed record Request(string Email, string Senha);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.Email).NotEmpty();
            RuleFor(r => r.Senha).NotEmpty();
        }
    }

    public static IEndpointRouteBuilder MapEntrar(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/login", Handle)
            .Validar<Request>()
            .AllowAnonymous()
            .WithTags("Auth");
        return app;
    }

    public static async Task<Results<Ok<RespostaAuth>, ProblemHttpResult>> Handle(
        Request request, UserManager<Usuario> usuarios, JwtTokenService jwt)
    {
        var usuario = await usuarios.FindByEmailAsync(request.Email.Trim());
        if (usuario is null || !await usuarios.CheckPasswordAsync(usuario, request.Senha))
        {
            return Erros.CredenciaisInvalidas;
        }

        return TypedResults.Ok(RespostaAuth.Criar(usuario, jwt));
    }
}
