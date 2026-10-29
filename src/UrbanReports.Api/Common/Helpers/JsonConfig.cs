using System.Text.Json;
using System.Text.Json.Serialization;

namespace UrbanReports.Api.Common;

public static class JsonConfig
{
    public static void Configurar(JsonSerializerOptions opcoes)
    {
        opcoes.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        opcoes.NumberHandling = JsonNumberHandling.Strict;
        opcoes.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        opcoes.Converters.Add(new DataUtcConverter());
    }

    private sealed class DataUtcConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.GetDateTimeOffset();

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.UtcDateTime);
    }
}
