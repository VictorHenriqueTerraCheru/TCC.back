using System.Net;
using System.Net.Http.Json;
using UrbanReports.Api.Users;
using UrbanReports.Api.Tests.Infra;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Tests.Users;

[Collection(ApiCollection.Name)]
public sealed class MeTests(ApiFactory factory)
{
    [Fact]
    public async Task Me_without_token_returns_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_with_invalid_token_returns_401()
    {
        var client = factory.CreateClient().Authenticated("nao.e.um-jwt");

        var response = await client.GetAsync("/api/v1/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_login_and_me_work_in_sequence()
    {
        var client = factory.CreateClient();
        var auth = await client.RegisterAsync();

        var response = await client.Authenticated(auth.Token).GetAsync("/api/v1/me");
        var me = await response.ReadAsync<GetMeResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        me.Id.Should().Be(auth.User.Id);
        me.Email.Should().Be(auth.User.Email);
        me.Role.Should().Be(Role.Citizen);
        me.CreatedAt.Should().Be(factory.Clock.GetUtcNow());
        me.VerifiedAt.Should().BeNull();
        me.TotalReports.Should().Be(0);
        me.TotalVotes.Should().Be(0);
        me.Verification.Should().BeNull();
    }

    [Fact]
    public async Task Patch_me_updates_the_name()
    {
        var client = factory.CreateClient();
        var auth = await client.RegisterAsync();
        client.Authenticated(auth.Token);

        var response = await client.PatchAsJsonAsync("/api/v1/me", new { name = "  Mariana S.  " }, ApiClient.Json);
        var me = await response.ReadAsync<GetMeResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        me.Name.Should().Be("Mariana S.");
        (await (await client.GetAsync("/api/v1/me")).ReadAsync<GetMeResponse>()).Name.Should().Be("Mariana S.");
    }

    [Fact]
    public async Task Patch_me_with_empty_name_returns_400()
    {
        var client = factory.CreateClient();
        client.Authenticated((await client.RegisterAsync()).Token);

        var response = await client.PatchAsJsonAsync("/api/v1/me", new { name = "" }, ApiClient.Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
