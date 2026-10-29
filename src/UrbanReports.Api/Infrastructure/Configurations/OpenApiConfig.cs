using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace UrbanReports.Api.Infrastructure;

public static class OpenApiConfig
{
    public static IServiceCollection AddOpenApiConfigurado(this IServiceCollection services) =>
        services.AddOpenApi(o =>
        {
            o.CreateSchemaReferenceId = info =>
                OpenApiOptions.CreateDefaultSchemaReferenceId(info) is null ? null : NomeSchema(info.Type);

            o.AddSchemaTransformer((schema, _, _) =>
            {
                if (schema.Enum is { Count: > 0 } valores)
                {
                    var semNulo = valores.Where(v => v is not null && v.GetValueKind() != System.Text.Json.JsonValueKind.Null).ToList();
                    if (semNulo.Count != valores.Count)
                    {
                        schema.Enum = semNulo;
                    }
                }

                return Task.CompletedTask;
            });

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
                return Task.CompletedTask;
            });

            o.AddOperationTransformer((operacao, contexto, _) =>
            {
                foreach (var parametro in operacao.Parameters?.OfType<OpenApiParameter>() ?? [])
                {
                    if (parametro.In == ParameterLocation.Query && parametro.Name is { Length: > 0 } nome)
                    {
                        parametro.Name = System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(nome);
                    }
                }

                var anonima = contexto.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
                if (!anonima)
                {
                    operacao.Security =
                    [
                        new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", contexto.Document)] = [] },
                    ];
                }

                return Task.CompletedTask;
            });

            o.AddDocumentTransformer((documento, _, _) =>
            {
                if (documento.Tags is { Count: > 0 } tags)
                {
                    documento.Tags = new SortedSet<OpenApiTag>(tags, Comparer<OpenApiTag>.Create((a, b) =>
                    {
                        var (pa, pb) = (PosicaoTag(a.Name), PosicaoTag(b.Name));
                        return pa != pb ? pa.CompareTo(pb) : string.CompareOrdinal(a.Name, b.Name);
                    }));
                }

                return Task.CompletedTask;
            });
        });

    private static readonly string[] OrdemTags =
        ["Auth", "Usuários", "Categorias", "Geocodificação", "Problemas", "Votos", "Incidentes", "Verificação", "Admin"];

    private static int PosicaoTag(string? nome)
    {
        var posicao = Array.IndexOf(OrdemTags, nome);
        return posicao < 0 ? int.MaxValue : posicao;
    }

    public static string NomeSchema(Type tipo)
    {
        tipo = Nullable.GetUnderlyingType(tipo) ?? tipo;
        var nome = tipo.IsGenericType ? tipo.Name[..tipo.Name.IndexOf('`')] : tipo.Name;
        if (tipo.IsNested && tipo.DeclaringType is { } externo && !tipo.IsGenericParameter)
        {
            nome = externo.Name + nome;
        }

        return tipo.IsGenericType
            ? nome + string.Concat(tipo.GetGenericArguments().Select(NomeSchema))
            : nome;
    }
}
