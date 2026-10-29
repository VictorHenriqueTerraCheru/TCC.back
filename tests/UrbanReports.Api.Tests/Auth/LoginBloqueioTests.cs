using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Tests.Infra;

namespace UrbanReports.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
public sealed class LoginLockoutTests(ApiFactory factory)
{
    private const int MaxFailures = 5;
    private readonly HttpClient _client = factory.CreateClient();

    private Task<HttpResponseMessage> Login(string email, string password) =>
        _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, ApiClient.Json);

    private async Task Fail(string email, int times)
    {
        for (var i = 0; i < times; i++)
        {
            (await Login(email, "senha-errada")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }

    private static async Task<string> WithoutTraceId(HttpResponseMessage response) =>
        string.Join(";", (await response.ReadJsonAsync()).EnumerateObject()
            .Where(p => p.Name != "traceId")
            .Select(p => $"{p.Name}={p.Value}"));

    [Fact]
    public async Task After_N_failures_the_account_locks_even_with_the_right_password()
    {
        var email = ApiClient.UniqueEmail();
        await _client.RegisterAsync(email);

        await Fail(email, MaxFailures);

        (await Login(email, "senha-forte")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Concurrent_failures_all_count_and_lock()
    {
        var email = ApiClient.UniqueEmail();
        await _client.RegisterAsync(email);

        var responses = await Task.WhenAll(Enumerable.Range(0, MaxFailures).Select(_ => Login(email, "senha-errada")));
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Unauthorized);

        (await Login(email, "senha-forte")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Before_N_failures_the_right_password_logs_in_and_resets_the_counter()
    {
        var email = ApiClient.UniqueEmail();
        await _client.RegisterAsync(email);

        await Fail(email, MaxFailures - 1);
        (await Login(email, "senha-forte")).StatusCode.Should().Be(HttpStatusCode.OK);

        await Fail(email, MaxFailures - 1);
        (await Login(email, "senha-forte")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Locked_account_response_equals_invalid_credentials_response()
    {
        var email = ApiClient.UniqueEmail();
        await _client.RegisterAsync(email);
        var invalid = await Login(email, "senha-errada");
        await Fail(email, MaxFailures);

        var locked = await Login(email, "senha-forte");

        locked.StatusCode.Should().Be(invalid.StatusCode);
        (await WithoutTraceId(locked)).Should().Be(await WithoutTraceId(invalid));
        locked.Headers.Contains("Retry-After").Should().BeFalse();
    }

    [Fact]
    public async Task Lockout_of_one_account_does_not_affect_another_login()
    {
        var locked = ApiClient.UniqueEmail();
        var other = ApiClient.UniqueEmail();
        await _client.RegisterAsync(locked);
        await _client.RegisterAsync(other);

        await Fail(locked, MaxFailures);

        (await Login(other, "senha-forte")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Account_logs_in_again_when_the_lockout_ends()
    {
        var email = ApiClient.UniqueEmail();
        await _client.RegisterAsync(email);
        await Fail(email, MaxFailures);
        (await Login(email, "senha-forte")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await factory.InDatabaseAsync(db => db.Database.ExecuteSqlAsync(
            $"UPDATE users SET lockout_end = now() - interval '1 minute' WHERE normalized_email = {email.ToUpperInvariant()}"));

        (await Login(email, "senha-forte")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
