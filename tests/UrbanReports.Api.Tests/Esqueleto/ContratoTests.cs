using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UrbanReports.Api.Tests.Infra;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Tests.Skeleton;

[Collection(ApiCollection.Name)]
public sealed class ContractTests(ApiFactory factory)
{
    private const string Justification = "Moro em Botafogo há dez anos e circulo pelo bairro todos os dias.";

    private static readonly string[] ExpectedRoutes =
    [
        "POST /api/v1/auth/register [anonymous]",
        "POST /api/v1/auth/login [anonymous]",
        "GET /api/v1/me",
        "PATCH /api/v1/me",
        "GET /api/v1/me/history",
        "GET /api/v1/categories [anonymous]",
        "GET /api/v1/health [anonymous]",
        "GET /api/v1/health/database [anonymous]",
        "GET /api/v1/geocoding/reverse [anonymous]",
        "GET /api/v1/issues [anonymous]",
        "GET /api/v1/issues/recent [anonymous]",
        "GET /api/v1/issues/duplicates",
        "GET /api/v1/issues/{id:guid} [anonymous]",
        "POST /api/v1/issues",
        "PUT /api/v1/issues/{id:guid}/vote",
        "POST /api/v1/incidents",
        "GET /api/v1/incidents/heatmap [anonymous]",
        "GET /api/v1/incidents/area [anonymous]",
        "POST /api/v1/verifications",
        "GET /api/v1/admin/verifications [admin]",
        "POST /api/v1/admin/verifications/{id:guid}/approve [admin]",
        "POST /api/v1/admin/verifications/{id:guid}/reject [admin]",
        "GET /api/v1/admin/users [admin]",
        "PATCH /api/v1/admin/users/{id:guid}/role [admin]",
        "GET /api/v1/admin/categories [admin]",
        "POST /api/v1/admin/categories [admin]",
        "PUT /api/v1/admin/categories/{id:int} [admin]",
        "PATCH /api/v1/admin/categories/{id:int}/active [admin]",
        "GET /api/v1/groups [anonymous]",
        "POST /api/v1/admin/groups [admin]",
        "PUT /api/v1/admin/groups/{key} [admin]",
        "DELETE /api/v1/admin/groups/{key} [admin]",
    ];

    [Fact]
    public async Task Openapi_equals_snapshot()
    {
        var current = Canonical(await factory.CreateClient().GetStringAsync("/openapi/v1.json"));
        var path = SnapshotPath();
        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI_SNAPSHOT") == "1")
        {
            await File.WriteAllTextAsync(path, current);
        }

        var expected = Canonical(await File.ReadAllTextAsync(path));

        current.Should().Be(expected, "o contrato do front sai do OpenAPI; regrave o snapshot só para mudança de contrato aprovada");
    }

    [Fact]
    public void Contract_routes_and_policies()
    {
        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/v1", StringComparison.Ordinal) == true)
            .SelectMany(e =>
            {
                var anonymous = e.Metadata.GetMetadata<IAllowAnonymous>() is not null ? " [anonymous]" : "";
                var admin = e.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => a.Policy == "Administrator") ? " [admin]" : "";
                return e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods
                    .Select(m => $"{m} {e.RoutePattern.RawText}{anonymous}{admin}");
            });

        routes.Should().BeEquivalentTo(ExpectedRoutes);
    }

    [Fact]
    public async Task Ef_model_has_no_pending_changes() =>
        (await factory.InDatabaseAsync(db => Task.FromResult(db.Database.HasPendingModelChanges()))).Should().BeFalse();

    [Fact]
    public async Task Register_and_verification_location_points_to_me()
    {
        var registration = await factory.CreateClient().PostRegisterAsync(ApiClient.UniqueEmail());
        var person = await factory.NewPersonAsync();
        var verification = await person.Client.PostAsJsonAsync(
            "/api/v1/verifications", new { affiliationType = "livesInArea", justification = Justification }, ApiClient.Json);

        registration.StatusCode.Should().Be(HttpStatusCode.Created);
        registration.Headers.Location!.ToString().Should().Be("/api/v1/me");
        verification.StatusCode.Should().Be(HttpStatusCode.Created);
        verification.Headers.Location!.ToString().Should().Be("/api/v1/me");
    }

    [Fact]
    public async Task Incident_location_uses_the_rounded_point_in_invariant_culture()
    {
        var person = await factory.NewPersonAsync();
        var (baseLat, baseLng) = ApiFactory.IsolatedArea();
        var (lat, lng) = (baseLat + 0.00041, baseLng - 0.00067);
        var originalCulture = CultureInfo.CurrentCulture;
        HttpResponseMessage response;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
            response = await person.Client.PostAsJsonAsync(
                "/api/v1/incidents",
                new { categoryId = 7, lat, lng, occurredAt = factory.Clock.GetUtcNow().AddMinutes(-30) },
                ApiClient.Json);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        var roundedLat = Math.Round(lat, 3, MidpointRounding.AwayFromZero);
        var roundedLng = Math.Round(lng, 3, MidpointRounding.AwayFromZero);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().Be(
            FormattableString.Invariant($"/api/v1/incidents/area?lat={roundedLat}&lng={roundedLng}"));
    }

    [Fact]
    public async Task Removed_user_token_gets_401()
    {
        var author = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.UniquePoint();
        var issue = await author.CreateIssueAsync(lat, lng);
        var removed = await factory.NewPersonAsync();
        await factory.InDatabaseAsync(db => db.Users.Where(u => u.Id == removed.Id).ExecuteDeleteAsync());

        (await removed.Client.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await removed.Client.PatchAsJsonAsync("/api/v1/me", new { name = "Outro Nome" }, ApiClient.Json))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await removed.Client.PostAsJsonAsync("/api/v1/verifications", new { affiliationType = "livesInArea", justification = Justification }, ApiClient.Json))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await removed.VoteAsync(issue.Id, "yes")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Missing_resources_return_404()
    {
        var person = await factory.NewPersonAsync();
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var id = Guid.NewGuid();

        (await factory.CreateClient().GetAsync($"/api/v1/issues/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await person.VoteAsync(id, "yes")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.Client.PostAsync($"/api/v1/admin/verifications/{id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.Client.PostAsJsonAsync($"/api/v1/admin/verifications/{id}/reject", new { reason = "Sem vínculo com a região." }, ApiClient.Json))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static string SnapshotPath([CallerFilePath] string file = "") =>
        Path.Combine(Path.GetDirectoryName(file)!, "openapi.snapshot.json");

    private static string Canonical(string json) =>
        Sort(JsonNode.Parse(json))!.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

    private static JsonNode? Sort(JsonNode? node) => node switch
    {
        JsonObject jsonObject => new JsonObject(jsonObject
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => KeyValuePair.Create(p.Key, Sort(p.Value)))),
        JsonArray list => new JsonArray(list.Select(Sort).ToArray()),
        null => null,
        _ => node.DeepClone(),
    };
}
