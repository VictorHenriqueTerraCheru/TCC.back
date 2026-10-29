using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace UrbanReports.Api.Infrastructure;

public static class OpenApiConfig
{
    public static IServiceCollection AddOpenApiConfigurado(this IServiceCollection services) =>
        services.AddOpenApi(o =>
        {
            // O front gera os tipos TypeScript a partir deste documento: nomes de schema não podem colidir.
            o.CreateSchemaReferenceId = info =>
                OpenApiOptions.CreateDefaultSchemaReferenceId(info) is null ? null : NomeSchema(info.Type);

            // Enums anuláveis já aparecem como oneOf [null, $ref]; o null dentro do próprio enum tornaria o
            // tipo anulável em todos os usos.
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

            // Bearer só nas operações protegidas: as marcadas com AllowAnonymous (🔓) ficam sem requisito.
            o.AddOperationTransformer((operacao, contexto, _) =>
            {
                // Records [AsParameters] geram nomes em PascalCase; o contrato (e o binding, que ignora a caixa) usa camelCase.
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

            // Ordem das seções no Scalar: a de docs/API.md, independente da ordem de registro dos módulos.
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

    /// <summary>
    /// Records aninhados ganham o nome da feature como prefixo (<c>CadastrarRequest</c>); genéricos incluem os
    /// argumentos já qualificados (<c>Paginado&lt;ListarProblemasRecentes.Item&gt;</c> →
    /// <c>PaginadoListarProblemasRecentesItem</c>), em vez do padrão <c>PaginadoOfItem</c>, que colidiria.
    /// </summary>
    public static string NomeSchema(Type tipo)
    {
        // Enum anulável usa o mesmo schema do enum (a anulabilidade fica no uso, via oneOf).
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
