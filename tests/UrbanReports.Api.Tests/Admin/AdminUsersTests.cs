using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Tests.Infra;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Tests.Admin;

[Collection(ApiCollection.Name)]
public sealed class AdminUsersTests(ApiFactory factory)
{
    private static Task<HttpResponseMessage> ChangeRoleAsync(Person admin, Guid id, string role) =>
        admin.Client.PatchAsJsonAsync($"/api/v1/admin/users/{id}/role", new { role }, ApiClient.Json);

    private async Task<string> UserEmailAsync(Guid id) =>
        (await factory.InDatabaseAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == id))).Email!;

    [Fact]
    public async Task User_admin_routes_require_administrator_role()
    {
        var citizen = await factory.NewPersonAsync();
        var verified = await factory.NewPersonAsync(Role.VerifiedCitizen);

        (await citizen.Client.GetAsync("/api/v1/admin/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ChangeRoleAsync(verified, Guid.NewGuid(), "citizen")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await factory.CreateClient().GetAsync("/api/v1/admin/users")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_returns_user_data_without_exposing_password()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var person = await factory.NewPersonAsync();
        var email = await UserEmailAsync(person.Id);

        var response = await admin.Client.GetAsync($"/api/v1/admin/users?search={email}");
        var json = await response.ReadJsonAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.GetProperty("total").GetInt32().Should().Be(1);
        var item = json.GetProperty("items")[0];
        item.GetProperty("id").GetGuid().Should().Be(person.Id);
        item.GetProperty("name").GetString().Should().Be("Pessoa Teste");
        item.GetProperty("email").GetString().Should().Be(email);
        item.GetProperty("role").GetString().Should().Be("citizen");
        item.GetProperty("createdAt").GetDateTimeOffset().Should().Be(factory.Clock.GetUtcNow());
        item.GetProperty("verifiedAt").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        item.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("id", "name", "email", "role", "createdAt", "verifiedAt");
    }

    [Fact]
    public async Task List_searches_email_fragment_case_insensitively()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var person = await factory.NewPersonAsync();
        var segment = (await UserEmailAsync(person.Id))[..12].ToUpperInvariant();

        var json = await (await admin.Client.GetAsync($"/api/v1/admin/users?search={segment}")).ReadJsonAsync();

        json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().Contain(person.Id);
    }

    [Fact]
    public async Task List_filters_by_role()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var verified = await factory.NewPersonAsync(Role.VerifiedCitizen);
        var citizen = await factory.NewPersonAsync();

        var r = await admin.Client.GetAsync("/api/v1/admin/users?role=verifiedCitizen&pageSize=100");
        var json = await r.ReadJsonAsync();
        var ids = json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        var roles = json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("role").GetString()).Distinct();

        ids.Should().Contain(verified.Id).And.NotContain(citizen.Id);
        roles.Should().BeEquivalentTo("verifiedCitizen");
    }

    [Fact]
    public async Task List_paginates_and_validates_parameters()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);

        var json = await (await admin.Client.GetAsync("/api/v1/admin/users?pageSize=1&page=1")).ReadJsonAsync();

        json.GetProperty("items").GetArrayLength().Should().Be(1);
        json.GetProperty("pageSize").GetInt32().Should().Be(1);
        (await admin.Client.GetAsync("/api/v1/admin/users?pageSize=0")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.Client.GetAsync("/api/v1/admin/users?role=rei")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Change_to_verified_sets_verified_at_and_reverting_clears_it()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var person = await factory.NewPersonAsync();

        (await ChangeRoleAsync(admin, person.Id, "verifiedCitizen")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var verified = await factory.InDatabaseAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == person.Id));

        (await ChangeRoleAsync(admin, person.Id, "citizen")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var demoted = await factory.InDatabaseAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == person.Id));

        verified.Role.Should().Be(Role.VerifiedCitizen);
        verified.VerifiedAt.Should().Be(factory.Clock.GetUtcNow());
        demoted.Role.Should().Be(Role.Citizen);
        demoted.VerifiedAt.Should().BeNull();
    }

    [Fact]
    public async Task Change_to_administrator_keeps_verified_at()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var person = await factory.NewPersonAsync(Role.VerifiedCitizen);
        await factory.InDatabaseAsync(db => db.Users.Where(u => u.Id == person.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.VerifiedAt, factory.Clock.GetUtcNow())));

        (await ChangeRoleAsync(admin, person.Id, "administrator")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var user = await factory.InDatabaseAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == person.Id));

        user.Role.Should().Be(Role.Administrator);
        user.VerifiedAt.Should().Be(factory.Clock.GetUtcNow());
    }

    [Fact]
    public async Task Admin_cannot_change_own_role()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);

        var response = await ChangeRoleAsync(admin, admin.Id, "citizen");
        var json = await response.ReadJsonAsync();
        var user = await factory.InDatabaseAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == admin.Id));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        json.GetProperty("type").GetString().Should().Be("own-role");
        user.Role.Should().Be(Role.Administrator);
    }

    [Fact]
    public async Task Change_role_of_missing_user_returns_404_and_invalid_role_400()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var person = await factory.NewPersonAsync();

        (await ChangeRoleAsync(admin, Guid.NewGuid(), "citizen")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ChangeRoleAsync(admin, person.Id, "rei")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
