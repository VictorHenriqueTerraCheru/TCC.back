using System.Text.Json;
using System.Text.Json.Serialization;

namespace UrbanReports.Api.Common;

public static class JsonConfig
{
    /// <summary>JSON camelCase, enums como string camelCase e datas em UTC com "Z" (docs/API.md).</summary>
    public static void Configurar(JsonSerializerOptions opcoes)
    {
        opcoes.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        // Números só como número: os defaults web aceitam "12" e fariam o OpenAPI declarar integer|string.
        opcoes.NumberHandling = JsonNumberHandling.Strict;
        // Enum só como string: sem allowIntegerValues o conversor aceitaria "tipo": 1 e IsInEnum() o validaria.
        opcoes.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        opcoes.Converters.Add(new DataUtcConverter());
    }

    /// <summary>Escreve <see cref="DateTimeOffset"/> sempre em UTC (<c>2026-10-06T22:19:00Z</c>).</summary>
    private sealed class DataUtcConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.GetDateTimeOffset();

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.UtcDateTime);
    }
}
