using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Categories;
using UrbanReports.Api.Groups;
using UrbanReports.Api.Tests.Infra;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Tests.Categories;

[Collection(ApiCollection.Name)]
public sealed class CategoriesAdminTests(ApiFactory factory)
{
    private static string UniqueName() => $"Categoria {Guid.NewGuid():N}"[..30];

    private static Task<HttpResponseMessage> CreateAsync(Person admin, string name, string type = "incident", string group = "seguranca", string icon = "inundacao") =>
        admin.Client.PostAsJsonAsync("/api/v1/admin/categories", new { name, group, type, icon }, ApiClient.Json);

    private async Task DeactivateAsync(Person admin, params short[] ids)
    {
        foreach (var id in ids)
        {
            await admin.Client.PatchAsJsonAsync($"/api/v1/admin/categories/{id}/active", new { active = false }, ApiClient.Json);
        }
    }

    [Fact]
    public async Task Admin_routes_require_administrator_role()
    {
        var citizen = await factory.NewPersonAsync();
        var body = new { name = UniqueName(), group = "seguranca", type = "incident", icon = "inundacao" };

        (await citizen.Client.GetAsync("/api/v1/admin/categories")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await citizen.Client.PostAsJsonAsync("/api/v1/admin/categories", body, ApiClient.Json)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await citizen.Client.PutAsJsonAsync("/api/v1/admin/categories/1", new { name = "x", group = "seguranca", icon = "x" }, ApiClient.Json))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await citizen.Client.PatchAsJsonAsync("/api/v1/admin/categories/1/active", new { active = false }, ApiClient.Json))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await factory.CreateClient().GetAsync("/api/v1/admin/categories")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_generates_the_next_id_and_the_category_already_works_for_incidents()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var person = await factory.NewPersonAsync();
        var greater = await factory.InDatabaseAsync(db => db.Categories.MaxAsync(c => c.Id));
        var name = UniqueName();

        var response = await CreateAsync(admin, name);
        var created = await response.ReadAsync<CategoryAdminResponse>();
        try
        {
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            created.Should().Be(new CategoryAdminResponse((short)(greater + 1), name, "seguranca", ReportType.Incident, "inundacao", true));

            var publicList = (await (await factory.CreateClient().GetAsync("/api/v1/categories")).ReadJsonAsync()).EnumerateArray();
            publicList.Select(c => c.GetProperty("id").GetInt32()).Should().Contain(created.Id);

            var (lat, lng) = ApiFactory.IsolatedArea();
            var incident = await person.Client.PostAsJsonAsync("/api/v1/incidents",
                new { categoryId = created.Id, lat, lng, occurredAt = factory.Clock.GetUtcNow().AddMinutes(-30) }, ApiClient.Json);
            incident.StatusCode.Should().Be(HttpStatusCode.Created);
        }
        finally
        {
            await DeactivateAsync(admin, created.Id);
        }
    }

    [Fact]
    public async Task Duplicate_name_ignores_case_and_returns_409()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);

        var response = await CreateAsync(admin, "BURACO NA VIA");
        var edit = await admin.Client.PutAsJsonAsync("/api/v1/admin/categories/2",
            new { name = "buraco na via", group = "viaPublica", icon = "calcada" }, ApiClient.Json);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.ReadJsonAsync()).GetProperty("type").GetString().Should().Be("duplicate-name");
        edit.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("", "clima", "incident", "inundacao")]
    [InlineData("Nome ok", "", "incident", "inundacao")]
    [InlineData("Nome ok", "clima", "incident", "Icone Invalido")]
    [InlineData("Nome ok", "clima", "other", "inundacao")]
    public async Task Create_validates_the_body(string name, string group, string type, string icon)
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);

        (await CreateAsync(admin, name, type, group, icon)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deactivate_hides_from_public_list_blocks_new_reports_and_keeps_history()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var person = await factory.NewPersonAsync();
        var created = await (await CreateAsync(admin, UniqueName(), "issue", "limpeza", "entulho")).ReadAsync<CategoryAdminResponse>();
        try
        {
            await CheckDeactivationAsync(admin, person, created);
        }
        finally
        {
            await DeactivateAsync(admin, created.Id);
        }
    }

    private async Task CheckDeactivationAsync(Person admin, Person person, CategoryAdminResponse created)
    {
        var (lat, lng) = ApiFactory.UniquePoint();

        var before = await person.Client.PostAsJsonAsync("/api/v1/issues", new { categoryId = created.Id, lat, lng }, ApiClient.Json);
        var id = (await before.ReadJsonAsync()).GetProperty("id").GetGuid();
        var deactivate = await admin.Client.PatchAsJsonAsync($"/api/v1/admin/categories/{created.Id}/active", new { active = false }, ApiClient.Json);

        var publicList = (await (await factory.CreateClient().GetAsync("/api/v1/categories")).ReadJsonAsync()).EnumerateArray();
        var all = (await (await admin.Client.GetAsync("/api/v1/admin/categories")).ReadJsonAsync()).EnumerateArray();
        var (lat2, lng2) = ApiFactory.UniquePoint();
        var after = await person.Client.PostAsJsonAsync("/api/v1/issues", new { categoryId = created.Id, lat = lat2, lng = lng2 }, ApiClient.Json);
        var detail = await factory.CreateClient().GetAsync($"/api/v1/issues/{id}");

        before.StatusCode.Should().Be(HttpStatusCode.Created);
        deactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);
        publicList.Select(c => c.GetProperty("id").GetInt32()).Should().NotContain(created.Id);
        all.Single(c => c.GetProperty("id").GetInt32() == created.Id).GetProperty("active").GetBoolean().Should().BeFalse();
        after.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await after.ReadJsonAsync()).GetProperty("errors").TryGetProperty("categoryId", out _).Should().BeTrue();
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        (await detail.ReadJsonAsync()).GetProperty("category").GetProperty("id").GetInt32().Should().Be(created.Id);
    }

    [Fact]
    public async Task Edit_changes_name_group_and_icon_but_not_type()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var created = await (await CreateAsync(admin, UniqueName())).ReadAsync<CategoryAdminResponse>();
        var newName = UniqueName();
        try
        {
            var response = await admin.Client.PutAsJsonAsync($"/api/v1/admin/categories/{created.Id}",
                new { name = newName, group = "transito", icon = "chuva", type = "issue" }, ApiClient.Json);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.ReadAsync<CategoryAdminResponse>()).Should().Be(
                new CategoryAdminResponse(created.Id, newName, "transito", ReportType.Incident, "chuva", true));
        }
        finally
        {
            await DeactivateAsync(admin, created.Id);
        }
    }

    [Fact]
    public async Task Set_active_without_the_field_returns_400_and_does_not_deactivate()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var created = await (await CreateAsync(admin, UniqueName())).ReadAsync<CategoryAdminResponse>();
        try
        {
            var empty = await admin.Client.PatchAsJsonAsync($"/api/v1/admin/categories/{created.Id}/active", new { }, ApiClient.Json);
            var wrong = await admin.Client.PatchAsJsonAsync($"/api/v1/admin/categories/{created.Id}/active", new { isActive = false }, ApiClient.Json);

            empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            wrong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var publicList = (await (await factory.CreateClient().GetAsync("/api/v1/categories")).ReadJsonAsync()).EnumerateArray();
            publicList.Select(c => c.GetProperty("id").GetInt32()).Should().Contain(created.Id);
        }
        finally
        {
            await DeactivateAsync(admin, created.Id);
        }
    }

    [Fact]
    public async Task Numeric_type_and_active_body_without_the_field_return_400()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var name = UniqueName();

        var numeric = await admin.Client.PostAsJsonAsync("/api/v1/admin/categories",
            new { name, group = "seguranca", type = 2, icon = "x" }, ApiClient.Json);
        var withoutActive = await admin.Client.PatchAsJsonAsync("/api/v1/admin/categories/1/active", new { }, ApiClient.Json);

        numeric.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        withoutActive.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await factory.InDatabaseAsync(db => db.Categories.Where(c => c.Name == name).AnyAsync())).Should().BeFalse();
        (await factory.InDatabaseAsync(db => db.Categories.Where(c => c.Id == 1).Select(c => c.Active).SingleAsync())).Should().BeTrue();
    }

    [Fact]
    public async Task Missing_id_returns_404()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);

        (await admin.Client.PutAsJsonAsync("/api/v1/admin/categories/30000", new { name = "x", group = "seguranca", icon = "x" }, ApiClient.Json))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.Client.PatchAsJsonAsync("/api/v1/admin/categories/30000/active", new { active = true }, ApiClient.Json))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Missing_group_returns_400_and_group_of_other_type_returns_422()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);

        var missing = await CreateAsync(admin, UniqueName(), "incident", "naoExiste");
        var incompatible = await CreateAsync(admin, UniqueName(), "issue", "seguranca");
        var edit = await admin.Client.PutAsJsonAsync("/api/v1/admin/categories/1",
            new { name = "Buraco na via", group = "seguranca", icon = "buraco" }, ApiClient.Json);

        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await missing.ReadJsonAsync()).GetProperty("errors").TryGetProperty("group", out _).Should().BeTrue();
        incompatible.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await incompatible.ReadJsonAsync()).GetProperty("type").GetString().Should().Be("group-type-mismatch");
        edit.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task New_group_works_in_issue_filter()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var group = await (await admin.Client.PostAsJsonAsync("/api/v1/admin/groups",
            new { label = $"Grupo {Guid.NewGuid():N}"[..20], type = "issue", color = "blue" }, ApiClient.Json)).ReadAsync<GroupResponse>();
        var created = await (await CreateAsync(admin, UniqueName(), "issue", group.Key, "x")).ReadAsync<CategoryAdminResponse>();
        try
        {
            var area = "minLat=-23&maxLat=-22&minLng=-44&maxLng=-43";

            (await factory.CreateClient().GetAsync($"/api/v1/issues?{area}&groups={group.Key}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await factory.CreateClient().GetAsync($"/api/v1/issues?{area}&groups={group.Key}x")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            await DeactivateAsync(admin, created.Id);
        }
    }
}
