using System.Runtime.CompilerServices;
using System.Text.Json;
using UrbanReports.Api.Tests.Infra;

namespace UrbanReports.Api.Tests.Esqueleto;

[Collection(ApiCollection.Nome)]
public sealed class FormaOpenapiTests(ApiFactory fabrica)
{
    [Fact]
    public async Task Forma_do_openapi_preservada()
    {
        var documento = JsonDocument.Parse(await fabrica.CreateClient().GetStringAsync("/openapi/v1.json")).RootElement;
        var atual = string.Join('\n', Forma(documento)) + '\n';
        var caminho = CaminhoForma();
        if (Environment.GetEnvironmentVariable("ATUALIZAR_FORMA_OPENAPI") == "1")
        {
            await File.WriteAllTextAsync(caminho, atual);
        }

        atual.Should().Be(await File.ReadAllTextAsync(caminho), "o refactor de nomes não pode mudar a forma do contrato");
    }

    private static IEnumerable<string> Forma(JsonElement documento)
    {
        var linhas = new List<string>();
        foreach (var caminho in documento.GetProperty("paths").EnumerateObject())
        {
            var segmentos = caminho.Name.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;
            foreach (var operacao in caminho.Value.EnumerateObject())
            {
                var op = operacao.Value;
                var parametros = op.TryGetProperty("parameters", out var ps)
                    ? string.Join(",", ps.EnumerateArray()
                        .Select(p => $"{p.GetProperty("in").GetString()}:{Assinatura(p.GetProperty("schema"))}:{(p.TryGetProperty("required", out var r) && r.GetBoolean() ? "obrigatorio" : "opcional")}")
                        .Order(StringComparer.Ordinal))
                    : "";
                var corpo = op.TryGetProperty("requestBody", out var rb)
                    ? string.Join(",", rb.GetProperty("content").EnumerateObject().Select(c => $"{c.Name}:{Assinatura(c.Value.GetProperty("schema"))}"))
                    : "-";
                var respostas = string.Join(",", op.GetProperty("responses").EnumerateObject()
                    .Select(r => r.Value.TryGetProperty("content", out var c)
                        ? $"{r.Name}:{string.Join("|", c.EnumerateObject().Select(x => $"{x.Name}:{Assinatura(x.Value.GetProperty("schema"))}"))}"
                        : r.Name)
                    .Order(StringComparer.Ordinal));
                var seguro = op.TryGetProperty("security", out _) ? "bearer" : "anonimo";
                linhas.Add($"op {operacao.Name} seg={segmentos} params=[{parametros}] corpo=[{corpo}] respostas=[{respostas}] {seguro}");
            }
        }

        foreach (var schema in documento.GetProperty("components").GetProperty("schemas").EnumerateObject())
        {
            linhas.Add($"schema {Assinatura(schema.Value, raiz: true)}");
        }

        linhas.Add($"tags {documento.GetProperty("tags").GetArrayLength()}");
        return linhas.Order(StringComparer.Ordinal);
    }

    private static string Assinatura(JsonElement no, bool raiz = false)
    {
        if (no.TryGetProperty("$ref", out _))
        {
            return "ref";
        }

        var partes = new List<string>();
        if (no.TryGetProperty("type", out var tipo))
        {
            partes.Add(tipo.ValueKind == JsonValueKind.Array
                ? string.Join("|", tipo.EnumerateArray().Select(t => t.GetString()).Order(StringComparer.Ordinal))
                : tipo.GetString()!);
        }
        if (no.TryGetProperty("format", out var formato))
        {
            partes.Add(formato.GetString()!);
        }
        if (no.TryGetProperty("enum", out var valores))
        {
            partes.Add($"enum{valores.GetArrayLength()}");
        }
        if (no.TryGetProperty("items", out var itens))
        {
            partes.Add($"itens<{Assinatura(itens)}>");
        }
        foreach (var composicao in new[] { "oneOf", "anyOf", "allOf" })
        {
            if (no.TryGetProperty(composicao, out var opcoes))
            {
                partes.Add($"{composicao}<{string.Join(",", opcoes.EnumerateArray().Select(o => Assinatura(o)).Order(StringComparer.Ordinal))}>");
            }
        }
        if (no.TryGetProperty("properties", out var propriedades))
        {
            var obrigatorias = no.TryGetProperty("required", out var req) ? req.GetArrayLength() : 0;
            var props = propriedades.EnumerateObject().Select(p => Assinatura(p.Value)).Order(StringComparer.Ordinal);
            partes.Add($"props{(raiz ? "" : "-aninhado")}<{string.Join(",", props)}> obrigatorias={obrigatorias}");
        }
        return partes.Count == 0 ? "livre" : string.Join(" ", partes);
    }

    private static string CaminhoForma([CallerFilePath] string arquivo = "") =>
        Path.Combine(Path.GetDirectoryName(arquivo)!, "openapi.forma.txt");
}
