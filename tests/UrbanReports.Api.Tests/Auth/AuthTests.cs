using System.Net;
using System.Net.Http.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using UrbanReports.Api.Auth;
using UrbanReports.Api.Tests.Infra;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
public sealed class AuthTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_creates_citizen_and_returns_7_day_token()
    {
        var email = ApiClient.UniqueEmail();

        var response = await _client.PostRegisterAsync(email, name: "Mariana Souza");
        var body = await response.ReadAsync<AuthResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        body.User.Email.Should().Be(email);
        body.User.Name.Should().Be("Mariana Souza");
        body.User.Role.Should().Be(Role.Citizen);
        body.ExpiresAt.Should().Be(factory.Clock.GetUtcNow().AddDays(7));

        var token = new JsonWebToken(body.Token);
        token.Subject.Should().Be(body.User.Id.ToString());
        token.GetClaim("email").Value.Should().Be(email);
        token.GetClaim("name").Value.Should().Be("Mariana Souza");
        token.GetClaim("role").Value.Should().Be("citizen");
    }

    [Theory]
    [InlineData("o'brien")]
    [InlineData("joão.conceição")]
    public async Task Register_accepts_valid_emails_with_apostrophe_or_accent(string local)
    {
        var response = await _client.PostRegisterAsync($"{local}.{Guid.NewGuid():N}@teste.local");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Register_with_existing_email_returns_409_email_already_registered()
    {
        var email = ApiClient.UniqueEmail();
        await _client.RegisterAsync(email);

        var response = await _client.PostRegisterAsync(email.ToUpperInvariant());
        var issue = await response.ReadJsonAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        issue.GetProperty("type").GetString().Should().Be("email-already-registered");
    }

    [Fact]
    public async Task Invalid_register_returns_400_with_camel_case_fields()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new { name = "", email = "nao-e-email", password = "curta", acceptedTerms = false }, ApiClient.Json);
        var errors = (await response.ReadJsonAsync()).GetProperty("errors");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        errors.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("name", "email", "password", "acceptedTerms");
    }

    [Fact]
    public async Task Login_with_right_credentials_returns_200()
    {
        var email = ApiClient.UniqueEmail();
        await _client.RegisterAsync(email);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "senha-forte" }, ApiClient.Json);
        var body = await response.ReadAsync<AuthResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.User.Email.Should().Be(email);
        body.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Invalid_login_returns_401(bool emailExists)
    {
        var email = ApiClient.UniqueEmail();
        if (emailExists)
        {
            await _client.RegisterAsync(email);
        }

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "senha-errada" }, ApiClient.Json);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_expires_in_7_days_by_the_application_clock()
    {
        var auth = await _client.RegisterAsync();
        var client = factory.CreateClient().Authenticated(auth.Token);
        (await client.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        factory.Clock.Advance(TimeSpan.FromDays(7) + TimeSpan.FromMinutes(6));

        (await client.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
