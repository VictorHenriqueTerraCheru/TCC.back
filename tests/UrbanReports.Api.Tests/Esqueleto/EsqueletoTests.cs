using System.Net;
using System.Text.Json;
using UrbanReports.Api.Common;
using UrbanReports.Api.Infrastructure;
using UrbanReports.Api.Tests.Infra;

namespace UrbanReports.Api.Tests.Skeleton;

[Collection(ApiCollection.Name)]
public sealed class SkeletonTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Openapi_document_describes_enums_as_string_without_name_collisions()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var document = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        document.Should().Contain("\"verifiedCitizen\"");
        document.Should().Contain("\"RegisterRequest\"").And.Contain("\"LoginRequest\"");
    }

    [Fact]
    public async Task No_contract_schema_exposes_the_author()
    {
        var document = await (await _client.GetAsync("/openapi/v1.json")).Content.ReadAsStringAsync();

        document.Should().NotContainEquivalentOf("authorId");
    }

    [Fact]
    public async Task Openapi_generates_precise_types_for_the_front()
    {
        var document = await (await _client.GetAsync("/openapi/v1.json")).ReadJsonAsync();

        document.GetRawText().Should().NotContain("\"pattern\"", "números não devem aceitar string (integer|string)");
        foreach (var schema in document.GetProperty("components").GetProperty("schemas").EnumerateObject())
        {
            if (schema.Value.TryGetProperty("enum", out var values))
            {
                values.EnumerateArray().Should().NotContain(v => v.ValueKind == JsonValueKind.Null, $"enum {schema.Name} não deve incluir null");
            }
        }

        foreach (var path in document.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                if (!operation.Value.TryGetProperty("parameters", out var parameters))
                {
                    continue;
                }

                foreach (var parameter in parameters.EnumerateArray().Where(p => p.GetProperty("in").GetString() == "query"))
                {
                    var name = parameter.GetProperty("name").GetString()!;
                    char.IsLower(name[0]).Should().BeTrue($"parâmetro {name} em {path.Name} deve ser camelCase");
                }
            }
        }
    }

    [Fact]
    public async Task Paged_responses_have_distinct_schemas()
    {
        var document = await (await _client.GetAsync("/openapi/v1.json")).ReadJsonAsync();
        string Ref(string path) => document.GetProperty("paths").GetProperty(path).GetProperty("get")
            .GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json")
            .GetProperty("schema").GetProperty("$ref").GetString()!;

        string[] refs = [Ref("/api/v1/issues/recent"), Ref("/api/v1/me/history"), Ref("/api/v1/admin/verifications")];

        refs.Should().OnlyHaveUniqueItems();
        refs[0].Should().EndWith("PagedListRecentIssuesItem");
    }

    private sealed record Envelope<T>(T Value);

    private static class Feature
    {
        public sealed record Item(int Id);
    }

    [Fact]
    public void Generic_schema_name_includes_the_qualified_argument() =>
        OpenApiConfig.SchemaName(typeof(Envelope<Feature.Item>)).Should().Be("SkeletonTestsEnvelopeFeatureItem");

    [Fact]
    public void Nullable_uses_the_same_type_name() =>
        OpenApiConfig.SchemaName(typeof(Api.Common.Role?)).Should().Be("Role");

    [Fact]
    public async Task No_schema_is_a_nullable_variant()
    {
        var document = await (await _client.GetAsync("/openapi/v1.json")).ReadJsonAsync();

        document.GetProperty("components").GetProperty("schemas").EnumerateObject()
            .Select(p => p.Name).Should().NotContain(n => n.StartsWith("Nullable", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/api/v1/auth/login", "post", false)]
    [InlineData("/api/v1/auth/register", "post", false)]
    [InlineData("/api/v1/categories", "get", false)]
    [InlineData("/api/v1/me", "get", true)]
    [InlineData("/api/v1/me", "patch", true)]
    public async Task Openapi_requires_bearer_only_on_protected_operations(string path, string method, bool requiresBearer)
    {
        var document = await (await _client.GetAsync("/openapi/v1.json")).ReadJsonAsync();
        var operation = document.GetProperty("paths").GetProperty(path).GetProperty(method);

        operation.TryGetProperty("security", out _).Should().Be(requiresBearer);
    }

    [Theory]
    [InlineData(ApiFactory.FrontOrigin, true)]
    [InlineData("https://malicioso.example", false)]
    public async Task Cors_allows_only_configured_origins(string origin, bool released)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/health");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().Be(released);
    }

    private enum SampleStatus { PossiblyResolved }

    [Fact]
    public void Enums_travel_as_camel_case_string()
    {
        var options = new JsonSerializerOptions();
        JsonConfig.Configure(options);

        JsonSerializer.Serialize(SampleStatus.PossiblyResolved, options)
            .Should().Be("\"possiblyResolved\"");
    }

    [Fact]
    public void Dates_travel_in_utc_with_z()
    {
        var options = new JsonSerializerOptions();
        JsonConfig.Configure(options);
        var data = new DateTimeOffset(2026, 10, 6, 19, 19, 0, TimeSpan.FromHours(-3));

        JsonSerializer.Serialize(data, options).Should().Be("\"2026-10-06T22:19:00Z\"");
        JsonSerializer.Serialize<DateTimeOffset?>(null, options).Should().Be("null");
    }
}
