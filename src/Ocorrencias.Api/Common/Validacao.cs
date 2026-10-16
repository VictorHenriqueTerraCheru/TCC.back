using System.Globalization;
using System.Text.Json;
using FluentValidation;

namespace Ocorrencias.Api.Common;

public static class Validacao
{
    /// <summary>Mensagens em português e nomes de campo em camelCase, como no JSON.</summary>
    public static void ConfigurarGlobal()
    {
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("pt-BR");
        ValidatorOptions.Global.PropertyNameResolver = (_, membro, _) =>
            membro is null ? null : JsonNamingPolicy.CamelCase.ConvertName(membro.Name);
    }

    /// <summary>Valida o argumento <typeparamref name="T"/> com FluentValidation; falha → 400 ValidationProblem.</summary>
    public static RouteHandlerBuilder Validar<T>(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<FiltroValidacao<T>>().ProducesValidationProblem();

    private sealed class FiltroValidacao<T>(IValidator<T> validador) : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext contexto, EndpointFilterDelegate proximo)
        {
            var argumento = contexto.Arguments.OfType<T>().FirstOrDefault();
            if (argumento is null)
            {
                return await proximo(contexto);
            }

            var resultado = await validador.ValidateAsync(argumento, contexto.HttpContext.RequestAborted);
            return resultado.IsValid
                ? await proximo(contexto)
                : TypedResults.ValidationProblem(resultado.ToDictionary());
        }
    }
}
