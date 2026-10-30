using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Auth;
using UrbanReports.Api.Issues;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Tests.Infra;

public sealed record Person(Guid Id, HttpClient Client);

public static class Scenarios
{
    public const double MetersPerDegreeLat = 111_320;

    public static double North(double lat, double meters) => lat + meters / MetersPerDegreeLat;

    public static async Task<Person> NewPersonAsync(this ApiFactory factory, Role role = Role.Citizen)
    {
        var client = factory.CreateClient();
        var auth = await client.RegisterAsync();
        client.Authenticated(auth.Token);

        if (role != Role.Citizen)
        {
            await factory.InDatabaseAsync(db => db.Users
                .Where(u => u.Id == auth.User.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, role)));

            var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = auth.User.Email, password = "senha-forte" }, ApiClient.Json);
            login.EnsureSuccessStatusCode();
            client.Authenticated((await login.ReadAsync<AuthResponse>()).Token);
        }

        return new Person(auth.User.Id, client);
    }

    public static Task<HttpResponseMessage> PostIssueAsync(
        this Person person, double lat, double lng, short categoryId = 1, string? description = null) =>
        person.Client.PostAsJsonAsync("/api/v1/issues", new { categoryId, lat, lng, description }, ApiClient.Json);

    public static async Task<GetIssueDetailResponse> CreateIssueAsync(
        this Person person, double lat, double lng, short categoryId = 1)
    {
        var response = await person.PostIssueAsync(lat, lng, categoryId);
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<GetIssueDetailResponse>();
    }

    public static Task<HttpResponseMessage> VoteAsync(
        this Person person, Guid issueId, string value, double? lat = null, double? lng = null) =>
        person.Client.PutAsJsonAsync($"/api/v1/issues/{issueId}/vote", new { value, lat, lng }, ApiClient.Json);

    public static async Task<GetIssueDetailResponse> ActiveIssueAsync(this ApiFactory factory, double lat, double lng)
    {
        var author = await factory.NewPersonAsync();
        var issue = await author.CreateIssueAsync(lat, lng);

        GetIssueDetailResponse? detail = null;
        for (var i = 0; i < 5; i++)
        {
            var voter = await factory.NewPersonAsync();
            var response = await voter.VoteAsync(issue.Id, "yes");
            response.EnsureSuccessStatusCode();
            detail = await response.ReadAsync<GetIssueDetailResponse>();
        }

        detail!.Status.Should().Be(IssueStatus.Active);
        return detail;
    }
}
