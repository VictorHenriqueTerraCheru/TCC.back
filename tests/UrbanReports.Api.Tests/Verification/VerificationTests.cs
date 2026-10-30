using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Admin;
using UrbanReports.Api.Users;
using UrbanReports.Api.Verification;
using UrbanReports.Api.Tests.Infra;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Tests.Verification;

[Collection(ApiCollection.Name)]
public sealed class VerificationTests(ApiFactory factory)
{
    private const string Justification = "Moro em Botafogo há dez anos e circulo pelo bairro todos os dias.";

    private static Task<HttpResponseMessage> RequestAsync(Person person, string justification = Justification, string affiliationType = "livesInArea") =>
        person.Client.PostAsJsonAsync("/api/v1/verifications", new { affiliationType, justification }, ApiClient.Json);

    private static async Task<Guid> VerificationRequestIdAsync(Person person)
    {
        var response = await RequestAsync(person);
        response.EnsureSuccessStatusCode();
        return (await response.ReadAsync<RequestVerificationResponse>()).Id;
    }

    private static async Task<string?> ErrorTypeAsync(HttpResponseMessage response) =>
        (await response.ReadJsonAsync()).GetProperty("type").GetString();

    [Fact]
    public async Task Request_creates_pending_and_shows_in_me()
    {
        var person = await factory.NewPersonAsync();

        var response = await RequestAsync(person);
        var body = await response.ReadAsync<RequestVerificationResponse>();
        var me = await (await person.Client.GetAsync("/api/v1/me")).ReadAsync<GetMeResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        body.Status.Should().Be(VerificationStatus.Pending);
        body.AffiliationType.Should().Be(AffiliationType.LivesInArea);
        me.Verification.Should().Be(new GetMeVerificationSummary(VerificationStatus.Pending, AffiliationType.LivesInArea, null, body.CreatedAt));
    }

    [Theory]
    [InlineData(29)]
    [InlineData(501)]
    public async Task Justification_outside_30_to_500_characters_returns_400(int pageSize)
    {
        var person = await factory.NewPersonAsync();

        var response = await RequestAsync(person, new string('a', pageSize));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Second_pending_returns_409()
    {
        var person = await factory.NewPersonAsync();
        await VerificationRequestIdAsync(person);

        var response = await RequestAsync(person);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorTypeAsync(response)).Should().Be("verification-already-pending");
    }

    [Theory]
    [InlineData(Role.VerifiedCitizen)]
    [InlineData(Role.Administrator)]
    public async Task Already_verified_returns_422(Role role)
    {
        var person = await factory.NewPersonAsync(role);

        var response = await RequestAsync(person);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrorTypeAsync(response)).Should().Be("already-verified");
    }

    [Fact]
    public async Task Rejected_allows_new_request()
    {
        var person = await factory.NewPersonAsync();
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var id = await VerificationRequestIdAsync(person);
        await admin.Client.PostAsJsonAsync($"/api/v1/admin/verifications/{id}/reject", new { reason = "Faltam detalhes." }, ApiClient.Json);

        var response = await RequestAsync(person);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
