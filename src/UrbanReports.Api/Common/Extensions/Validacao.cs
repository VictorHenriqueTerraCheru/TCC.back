using System.Globalization;
using System.Text.Json;
using FluentValidation;

namespace UrbanReports.Api.Common;

public static class Validation
{
    public static void ConfigureGlobal()
    {
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("pt-BR");
        ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;
        ValidatorOptions.Global.PropertyNameResolver = (_, member, _) =>
            member is null ? null : JsonNamingPolicy.CamelCase.ConvertName(member.Name);
    }

    public static RouteHandlerBuilder Validate<T>(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<ValidationFilter<T>>().ProducesValidationProblem();

    private sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var argument = context.Arguments.OfType<T>().FirstOrDefault();
            if (argument is null)
            {
                return await next(context);
            }

            var result = await validator.ValidateAsync(argument, context.HttpContext.RequestAborted);
            return result.IsValid
                ? await next(context)
                : TypedResults.ValidationProblem(result.ToDictionary());
        }
    }
}
