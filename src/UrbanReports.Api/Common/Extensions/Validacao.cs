using System.Globalization;
using System.Text.Json;
using FluentValidation;

namespace UrbanReports.Api.Common;

public static class Validacao
{
    public static void ConfigurarGlobal()
    {
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("pt-BR");
        ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;
        ValidatorOptions.Global.PropertyNameResolver = (_, membro, _) =>
            membro is null ? null : JsonNamingPolicy.CamelCase.ConvertName(membro.Name);
    }

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
