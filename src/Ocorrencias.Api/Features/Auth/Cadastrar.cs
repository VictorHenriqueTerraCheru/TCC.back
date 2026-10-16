using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Auth;
using Ocorrencias.Api.Persistence;

namespace Ocorrencias.Api.Features.Auth;

public static class Cadastrar
{
    public sealed record Request(string Nome, string Email, string Senha, bool AceitouTermos);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.Nome).NotEmpty().MaximumLength(120);
            RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(256);
            RuleFor(r => r.Senha).NotEmpty().MinimumLength(8).WithMessage("A senha precisa ter pelo menos 8 caracteres.");
            RuleFor(r => r.AceitouTermos).Equal(true).WithMessage("É preciso aceitar os termos de uso.");
        }
    }

    public static IEndpointRouteBuilder MapCadastrar(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/cadastro", Handle)
            .Validar<Request>()
            .AllowAnonymous()
            .WithTags("Auth");
        return app;
    }

    public static async Task<Results<Created<RespostaAuth>, ProblemHttpResult>> Handle(
        Request request, UserManager<Usuario> usuarios, JwtTokenService jwt, TimeProvider relogio)
    {
        var email = request.Email.Trim();
        if (await usuarios.FindByEmailAsync(email) is not null)
        {
            return Erros.EmailJaCadastrado;
        }

        var usuario = new Usuario
        {
            Id = Guid.CreateVersion7(),
            Nome = request.Nome.Trim(),
            Email = email,
            UserName = email,
            CriadoEm = relogio.GetUtcNow(),
        };

        IdentityResult resultado;
        try
        {
            resultado = await usuarios.CreateAsync(usuario, request.Senha);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Corrida entre a checagem acima e o INSERT: o índice único de e-mail barra no banco.
            return Erros.EmailJaCadastrado;
        }

        if (!resultado.Succeeded)
        {
            if (resultado.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
            {
                return Erros.EmailJaCadastrado;
            }

            throw new InvalidOperationException(
                "Falha ao criar usuário: " + string.Join("; ", resultado.Errors.Select(e => e.Code)));
        }

        return TypedResults.Created("/api/v1/me", RespostaAuth.Criar(usuario, jwt));
    }
}
