using System.Runtime.CompilerServices;
using System.Text.Json;
using UrbanReports.Api.Tests.Infra;

namespace UrbanReports.Api.Tests.Skeleton;

[Collection(ApiCollection.Name)]
public sealed class OpenapiShapeTests(ApiFactory factory)
{
    [Fact]
    public async Task Openapi_shape_preserved()
    {
        var document = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/openapi/v1.json")).RootElement;
        var current = string.Join('\n', Shape(document)) + '\n';
        var path = ShapePath();
        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI_SHAPE") == "1")
        {
            await File.WriteAllTextAsync(path, current);
        }

        current.Should().Be(await File.ReadAllTextAsync(path), "renaming must not change the contract shape");
    }

    private static IEnumerable<string> Shape(JsonElement document)
    {
        var rows = new List<string>();
        foreach (var path in document.GetProperty("paths").EnumerateObject())
        {
            var segments = path.Name.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;
            foreach (var operation in path.Value.EnumerateObject())
            {
                var op = operation.Value;
                var parameters = op.TryGetProperty("parameters", out var ps)
                    ? string.Join(",", ps.EnumerateArray()
                        .Select(p => $"{p.GetProperty("in").GetString()}:{Signature(p.GetProperty("schema"))}:{(p.TryGetProperty("required", out var r) && r.GetBoolean() ? "required" : "optional")}")
                        .Order(StringComparer.Ordinal))
                    : "";
                var body = op.TryGetProperty("requestBody", out var rb)
                    ? string.Join(",", rb.GetProperty("content").EnumerateObject().Select(c => $"{c.Name}:{Signature(c.Value.GetProperty("schema"))}"))
                    : "-";
                var responses = string.Join(",", op.GetProperty("responses").EnumerateObject()
                    .Select(r => r.Value.TryGetProperty("content", out var c)
                        ? $"{r.Name}:{string.Join("|", c.EnumerateObject().Select(x => $"{x.Name}:{Signature(x.Value.GetProperty("schema"))}"))}"
                        : r.Name)
                    .Order(StringComparer.Ordinal));
                var auth = op.TryGetProperty("security", out _) ? "bearer" : "anonymous";
                rows.Add($"op {operation.Name} segments={segments} params=[{parameters}] body=[{body}] responses=[{responses}] {auth}");
            }
        }

        foreach (var schema in document.GetProperty("components").GetProperty("schemas").EnumerateObject())
        {
            rows.Add($"schema {Signature(schema.Value, root: true)}");
        }

        rows.Add($"tags {document.GetProperty("tags").GetArrayLength()}");
        return rows.Order(StringComparer.Ordinal);
    }

    private static string Signature(JsonElement node, bool root = false)
    {
        if (node.TryGetProperty("$ref", out _))
        {
            return "ref";
        }

        var parts = new List<string>();
        if (node.TryGetProperty("type", out var type))
        {
            parts.Add(type.ValueKind == JsonValueKind.Array
                ? string.Join("|", type.EnumerateArray().Select(t => t.GetString()).Order(StringComparer.Ordinal))
                : type.GetString()!);
        }
        if (node.TryGetProperty("format", out var format))
        {
            parts.Add(format.GetString()!);
        }
        if (node.TryGetProperty("enum", out var values))
        {
            parts.Add($"enum{values.GetArrayLength()}");
        }
        if (node.TryGetProperty("items", out var items))
        {
            parts.Add($"items<{Signature(items)}>");
        }
        foreach (var composition in new[] { "oneOf", "anyOf", "allOf" })
        {
            if (node.TryGetProperty(composition, out var options))
            {
                parts.Add($"{composition}<{string.Join(",", options.EnumerateArray().Select(o => Signature(o)).Order(StringComparer.Ordinal))}>");
            }
        }
        if (node.TryGetProperty("properties", out var properties))
        {
            var required = node.TryGetProperty("required", out var req) ? req.GetArrayLength() : 0;
            var props = properties.EnumerateObject().Select(p => Signature(p.Value)).Order(StringComparer.Ordinal);
            parts.Add($"props{(root ? "" : "-nested")}<{string.Join(",", props)}> required={required}");
        }
        return parts.Count == 0 ? "free" : string.Join(" ", parts);
    }

    private static string ShapePath([CallerFilePath] string file = "") =>
        Path.Combine(Path.GetDirectoryName(file)!, "openapi.shape.txt");
}
