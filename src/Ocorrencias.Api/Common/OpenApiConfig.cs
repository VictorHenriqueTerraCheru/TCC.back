using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Ocorrencias.Api.Common;

public static class OpenApiConfig
{
    public static IServiceCollection AddOpenApiConfigurado(this IServiceCollection services) =>
        services.AddOpenApi(o =>
        {
            // Cada feature declara records aninhados "Request"/"Response": qualifica pelo tipo externo
            // para não colidir no OpenAPI (o front gera os tipos TypeScript a partir dele).
            o.CreateSchemaReferenceId = info => info.Type.IsNested && info.Type.DeclaringType is { } externo
                ? externo.Name + OpenApiOptions.CreateDefaultSchemaReferenceId(info)
                : OpenApiOptions.CreateDefaultSchemaReferenceId(info);

            o.AddDocumentTransformer((documento, _, _) =>
            {
                documento.Components ??= new OpenApiComponents();
                documento.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                documento.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                };
                documento.Security =
                [
                    new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", documento)] = [] },
                ];
                return Task.CompletedTask;
            });
        });
}
