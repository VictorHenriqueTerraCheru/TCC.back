using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ocorrencias.Api.Common;

public static class JsonConfig
{
    /// <summary>JSON camelCase e enums como string camelCase (docs/API.md).</summary>
    public static void Configurar(JsonSerializerOptions opcoes)
    {
        opcoes.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        opcoes.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }
}
