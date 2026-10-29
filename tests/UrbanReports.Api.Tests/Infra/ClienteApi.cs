using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UrbanReports.Api.Common;
using UrbanReports.Api.Auth;

namespace UrbanReports.Api.Tests.Infra;

public static class ApiClient
{
    public static readonly JsonSerializerOptions Json = CreateJson();

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        JsonConfig.Configure(options);
        return options;
    }

    public static string UniqueEmail() => $"u{Guid.NewGuid():N}@teste.local";

    public static Task<HttpResponseMessage> PostRegisterAsync(this HttpClient client, string email, string password = "senha-forte", string name = "Pessoa Teste") =>
        client.PostAsJsonAsync("/api/v1/auth/register", new { name, email, password, acceptedTerms = true }, Json);

    public static async Task<AuthResponse> RegisterAsync(this HttpClient client, string? email = null)
    {
        var response = await client.PostRegisterAsync(email ?? UniqueEmail());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
    }

    public static HttpClient Authenticated(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Json);
}
