using System.Net;
using System.Net.Http.Json;
using UrbanReports.Api.Categories;
using UrbanReports.Api.Groups;
using UrbanReports.Api.Tests.Infra;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Tests.Groups;

[Collection(ApiCollection.Name)]
public sealed class GroupsTests(ApiFactory factory)
{
    private static string UniqueLabel(string prefix = "Grupo") => $"{prefix} {Guid.NewGuid():N}"[..28];

    private static Task<HttpResponseMessage> CreateAsync(Person admin, string label, string type = "issue", string color = "blue") =>
        admin.Client.PostAsJsonAsync("/api/v1/admin/groups", new { label, type, color }, ApiClient.Json);

    [Theory]
    [InlineData("Água e esgoto", "aguaEEsgoto")]
    [InlineData("Via pública", "viaPublica")]
    [InlineData("  trânsito   lento! ", "transitoLento")]
    [InlineData("123 obras", "grupo123Obras")]
    [InlineData("!!!", "grupo")]
    [InlineData("123456789012345678901234567890", "grupo1234567890123456789012345")]
    public void Key_is_camelCase_without_accent(string label, string expected) =>
        GroupService.KeyOf(label).Should().Be(expected);

    [Fact]
    public async Task Visitor_lists_the_seed_in_order()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/groups");
        var groups = await response.ReadAsync<List<GroupResponse>>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        groups.Take(5).Select(g => g.Key).Should().Equal("viaPublica", "eletrica", "limpeza", "seguranca", "transito");
        groups[0].Should().Be(new GroupResponse("viaPublica", "Via pública", ReportType.Issue, GroupColor.Laranja, 1));
        groups[3].Type.Should().Be(ReportType.Incident);
    }

    [Fact]
    public async Task Admin_routes_require_administrator_role()
    {
        var citizen = await factory.NewPersonAsync();

        (await CreateAsync(citizen, UniqueLabel())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await citizen.Client.PutAsJsonAsync("/api/v1/admin/groups/limpeza", new { label = "x", color = "blue", sortOrder = 1 }, ApiClient.Json))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await citizen.Client.DeleteAsync("/api/v1/admin/groups/limpeza")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await factory.CreateClient().DeleteAsync("/api/v1/admin/groups/limpeza")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_generates_key_without_accent_and_order_at_the_end()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var label = UniqueLabel("Água");
        var greater = (await (await factory.CreateClient().GetAsync("/api/v1/groups")).ReadAsync<List<GroupResponse>>()).Max(g => g.SortOrder);

        var response = await CreateAsync(admin, label, "incident", "teal");
        var created = await response.ReadAsync<GroupResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Key.Should().StartWith("agua").And.MatchRegex("^[a-z][a-zA-Z0-9]*$");
        created.Should().Be(new GroupResponse(created.Key, label, ReportType.Incident, GroupColor.Teal, greater + 1));
    }

    [Fact]
    public async Task Duplicate_label_ignores_case_and_repeated_key_gets_suffix()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var label = UniqueLabel("Zona");

        var first = await (await CreateAsync(admin, label)).ReadAsync<GroupResponse>();
        var duplicate = await CreateAsync(admin, label.ToUpperInvariant());
        var sameKey = await CreateAsync(admin, label + "!");

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await duplicate.ReadJsonAsync()).GetProperty("type").GetString().Should().Be("duplicate-group-label");
        sameKey.StatusCode.Should().Be(HttpStatusCode.Created);
        (await sameKey.ReadAsync<GroupResponse>()).Key.Should().Be(first.Key + "2");
    }

    [Theory]
    [InlineData("", "issue", "blue")]
    [InlineData("Ok", "other", "blue")]
    [InlineData("Ok", "issue", "turquesa")]
    public async Task Create_validates_the_body(string label, string type, string color)
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);

        (await CreateAsync(admin, label, type, color)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Edit_changes_label_color_and_order_but_not_type_or_key()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var created = await (await CreateAsync(admin, UniqueLabel())).ReadAsync<GroupResponse>();
        var newLabel = UniqueLabel("Novo");

        var response = await admin.Client.PutAsJsonAsync($"/api/v1/admin/groups/{created.Key}",
            new { label = newLabel, color = "purple", sortOrder = 99, type = "incident" }, ApiClient.Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadAsync<GroupResponse>()).Should().Be(new GroupResponse(created.Key, newLabel, ReportType.Issue, GroupColor.Purple, 99));
    }

    [Fact]
    public async Task Edit_to_another_group_label_returns_409_and_missing_key_404()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var created = await (await CreateAsync(admin, UniqueLabel())).ReadAsync<GroupResponse>();

        var conflict = await admin.Client.PutAsJsonAsync($"/api/v1/admin/groups/{created.Key}",
            new { label = "limpeza", color = "blue", sortOrder = 9 }, ApiClient.Json);
        var missing = await admin.Client.PutAsJsonAsync("/api/v1/admin/groups/naoExiste",
            new { label = "x", color = "blue", sortOrder = 9 }, ApiClient.Json);

        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Parallel_rename_to_the_same_label_lets_only_one_win()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var a = await (await CreateAsync(admin, UniqueLabel())).ReadAsync<GroupResponse>();
        var b = await (await CreateAsync(admin, UniqueLabel())).ReadAsync<GroupResponse>();
        var target = UniqueLabel();

        var responses = await Task.WhenAll(
            admin.Client.PutAsJsonAsync($"/api/v1/admin/groups/{a.Key}",
                new { label = target.ToLowerInvariant(), color = "blue", sortOrder = 9 }, ApiClient.Json),
            admin.Client.PutAsJsonAsync($"/api/v1/admin/groups/{b.Key}",
                new { label = target.ToUpperInvariant(), color = "blue", sortOrder = 9 }, ApiClient.Json));

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
    }

    [Fact]
    public async Task Delete_only_works_on_an_empty_group()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var empty = await (await CreateAsync(admin, UniqueLabel())).ReadAsync<GroupResponse>();
        var busy = await (await CreateAsync(admin, UniqueLabel())).ReadAsync<GroupResponse>();
        var category = await (await admin.Client.PostAsJsonAsync("/api/v1/admin/categories",
            new { name = UniqueLabel("Cat"), group = busy.Key, type = "issue", icon = "x" }, ApiClient.Json)).ReadAsync<CategoryAdminResponse>();
        try
        {
            var deleted = await admin.Client.DeleteAsync($"/api/v1/admin/groups/{empty.Key}");
            var blocked = await admin.Client.DeleteAsync($"/api/v1/admin/groups/{busy.Key}");
            await admin.Client.PatchAsJsonAsync($"/api/v1/admin/categories/{category.Id}/active", new { active = false }, ApiClient.Json);
            var stillBlocks = await admin.Client.DeleteAsync($"/api/v1/admin/groups/{busy.Key}");
            var again = await admin.Client.DeleteAsync($"/api/v1/admin/groups/{empty.Key}");

            deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
            blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await blocked.ReadJsonAsync()).GetProperty("type").GetString().Should().Be("group-in-use");
            stillBlocks.StatusCode.Should().Be(HttpStatusCode.Conflict);
            again.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            await admin.Client.PatchAsJsonAsync($"/api/v1/admin/categories/{category.Id}/active", new { active = false }, ApiClient.Json);
        }
    }
}
