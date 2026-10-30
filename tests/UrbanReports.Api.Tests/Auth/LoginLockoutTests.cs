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
}
