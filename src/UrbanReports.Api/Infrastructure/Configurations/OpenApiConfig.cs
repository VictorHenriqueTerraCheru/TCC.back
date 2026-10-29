using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace UrbanReports.Api.Infrastructure;

public static class OpenApiConfig
{
    public static IServiceCollection AddConfiguredOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(o =>
        {
            o.CreateSchemaReferenceId = info =>
                OpenApiOptions.CreateDefaultSchemaReferenceId(info) is null ? null : SchemaName(info.Type);

            o.AddSchemaTransformer((schema, _, _) =>
            {
                if (schema.Enum is { Count: > 0 } values)
                {
                    var withoutNull = values.Where(v => v is not null && v.GetValueKind() != System.Text.Json.JsonValueKind.Null).ToList();
                    if (withoutNull.Count != values.Count)
                    {
                        schema.Enum = withoutNull;
                    }
                }

                return Task.CompletedTask;
            });

            o.AddDocumentTransformer((document, _, _) =>
            {
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                };
                return Task.CompletedTask;
            });

            o.AddOperationTransformer((operation, context, _) =>
            {
                foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
                {
                    if (parameter.In == ParameterLocation.Query && parameter.Name is { Length: > 0 } name)
                    {
                        parameter.Name = System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(name);
                    }
                }

                var anonymous = context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
                if (!anonymous)
                {
                    operation.Security =
                    [
                        new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [] },
                    ];
                }

                return Task.CompletedTask;
            });

            o.AddDocumentTransformer((document, _, _) =>
            {
                if (document.Tags is { Count: > 0 } tags)
                {
                    document.Tags = new SortedSet<OpenApiTag>(tags, Comparer<OpenApiTag>.Create((a, b) =>
                    {
                        var (pa, pb) = (TagPosition(a.Name), TagPosition(b.Name));
                        return pa != pb ? pa.CompareTo(pb) : string.CompareOrdinal(a.Name, b.Name);
                    }));
                }

                return Task.CompletedTask;
            });
        });

    private static readonly string[] TagOrder =
        ["Auth", "Users", "Categories", "Geocoding", "Issues", "Votes", "Incidents", "Verification", "Admin"];

    private static int TagPosition(string? name)
    {
        var position = Array.IndexOf(TagOrder, name);
        return position < 0 ? int.MaxValue : position;
    }

    public static string SchemaName(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        var name = type.IsGenericType ? type.Name[..type.Name.IndexOf('`')] : type.Name;
        if (type.IsNested && type.DeclaringType is { } external && !type.IsGenericParameter)
        {
            name = external.Name + name;
        }

        return type.IsGenericType
            ? name + string.Concat(type.GetGenericArguments().Select(SchemaName))
            : name;
    }
}
