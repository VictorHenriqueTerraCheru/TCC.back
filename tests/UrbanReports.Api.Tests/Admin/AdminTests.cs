using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Common;
using UrbanReports.Api.Admin;
using UrbanReports.Api.Users;
using UrbanReports.Api.Verification;
using UrbanReports.Api.Tests.Infra;

namespace UrbanReports.Api.Tests.Admin;

[Collection(ApiCollection.Name)]
public sealed class AdminTests(ApiFactory factory)
{
    private static async Task<Guid> RequestAsync(Person person)
    {
        var response = await person.Client.PostAsJsonAsync("/api/v1/verifications",
            new { affiliationType = "worksInArea", justification = "Trabalho no comércio da região há mais de cinco anos." }, ApiClient.Json);
        response.EnsureSuccessStatusCode();
        return (await response.ReadAsync<RequestVerificationResponse>()).Id;
    }

    private static Task<HttpResponseMessage> ApproveAsync(Person admin, Guid id) =>
        admin.Client.PostAsync($"/api/v1/admin/verifications/{id}/approve", null);

    private static Task<HttpResponseMessage> RejectAsync(Person admin, Guid id, string reason = "Justificativa insuficiente.") =>
        admin.Client.PostAsJsonAsync($"/api/v1/admin/verifications/{id}/reject", new { reason }, ApiClient.Json);

    [Fact]
    public async Task Admin_routes_require_administrator_role()
    {
        var citizen = await factory.NewPersonAsync();
        var verified = await factory.NewPersonAsync(Role.VerifiedCitizen);

        (await citizen.Client.GetAsync("/api/v1/admin/verifications")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await verified.Client.PostAsync($"/api/v1/admin/verifications/{Guid.NewGuid()}/approve", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await factory.CreateClient().GetAsync("/api/v1/admin/verifications")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Approve_makes_the_user_verified()
    {
        var person = await factory.NewPersonAsync();
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var id = await RequestAsync(person);

        var response = await ApproveAsync(admin, id);
        var me = await (await person.Client.GetAsync("/api/v1/me")).ReadAsync<GetMeResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        me.Role.Should().Be(Role.VerifiedCitizen);
        me.VerifiedAt.Should().Be(factory.Clock.GetUtcNow());
        me.Verification!.Status.Should().Be(VerificationStatus.Approved);

        var request = await factory.InDatabaseAsync(db => db.VerificationRequests.AsNoTracking().SingleAsync(s => s.Id == id));
        request.ReviewedById.Should().Be(admin.Id);
        request.ReviewedAt.Should().Be(factory.Clock.GetUtcNow());
    }

    [Fact]
    public async Task Reject_keeps_the_reason_and_stays_citizen()
    {
        var person = await factory.NewPersonAsync();
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var id = await RequestAsync(person);

        var response = await RejectAsync(admin, id, "  Não comprovou vínculo.  ");
        var me = await (await person.Client.GetAsync("/api/v1/me")).ReadAsync<GetMeResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        me.Role.Should().Be(Role.Citizen);
        me.Verification!.Status.Should().Be(VerificationStatus.Rejected);
        me.Verification.RejectionReason.Should().Be("Não comprovou vínculo.");
    }

    [Fact]
    public async Task Reject_without_reason_returns_400()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var id = await RequestAsync(await factory.NewPersonAsync());

        (await RejectAsync(admin, id, "")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reviewing_again_returns_409_and_missing_404()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var id = await RequestAsync(await factory.NewPersonAsync());
        await ApproveAsync(admin, id);

        var approveAgain = await ApproveAsync(admin, id);
        var rejectAfterwards = await RejectAsync(admin, id);
        var missing = await ApproveAsync(admin, Guid.NewGuid());

        approveAgain.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await approveAgain.ReadJsonAsync()).GetProperty("type").GetString().Should().Be("verification-already-reviewed");
        rejectAfterwards.StatusCode.Should().Be(HttpStatusCode.Conflict);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Concurrent_approve_and_reject_only_one_wins()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var id = await RequestAsync(await factory.NewPersonAsync());

        var responses = await Task.WhenAll(ApproveAsync(admin, id), RejectAsync(admin, id), ApproveAsync(admin, id));

        responses.Count(r => r.StatusCode == HttpStatusCode.NoContent).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(2);
    }

    [Fact]
    public async Task List_filters_by_status_with_initials_and_oldest_pending_first()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var a = await factory.NewPersonAsync();
        await factory.InDatabaseAsync(db => db.Users.Where(u => u.Id == a.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Name, "carlos de menezes")));
        var first = await RequestAsync(a);
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = await RequestAsync(await factory.NewPersonAsync());

        var page = await (await admin.Client.GetAsync($"/api/v1/admin/verifications?status=pending&pageSize={Pagination.MaxPageSize}"))
            .ReadAsync<Paged<ListVerificationsItem>>();
        var my = page.Items.Where(i => i.Id == first || i.Id == second).ToList();

        page.Items.Should().AllSatisfy(i => i.Status.Should().Be(VerificationStatus.Pending));
        my.Select(i => i.Id).Should().Equal(first, second);
        my[0].User.Should().Be(new ListVerificationsUserSummary(a.Id, "carlos de menezes", "CM"));
        my[0].AffiliationType.Should().Be(AffiliationType.WorksInArea);
    }

    [Theory]
    [InlineData("Carlos Menezes", "CM")]
    [InlineData("Ana", "A")]
    [InlineData("  maria  da  silva ", "MS")]
    public void Initials(string name, string expected) =>
        AdminService.Initials(name).Should().Be(expected);

    [Fact]
    public async Task Approved_user_votes_with_weight_10_only_from_then_on()
    {
        var voter = await factory.NewPersonAsync();
        var admin = await factory.NewPersonAsync(Role.Administrator);
        var author = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.UniquePoint();
        var before = await author.CreateIssueAsync(lat, lng);
        var after = await author.CreateIssueAsync(Scenarios.North(lat, 30), lng, categoryId: 2);

        await voter.VoteAsync(before.Id, "yes");
        await ApproveAsync(admin, await RequestAsync(voter));
        await voter.VoteAsync(after.Id, "yes");

        var votes = await factory.InDatabaseAsync(db => db.Votes.AsNoTracking()
            .Where(v => v.UserId == voter.Id)
            .ToDictionaryAsync(v => v.IssueId, v => v.RoleAtVote));
        votes[before.Id].Should().Be(Role.Citizen);
        votes[after.Id].Should().Be(Role.VerifiedCitizen);

        var trustScores = await factory.InDatabaseAsync(db => db.Issues.AsNoTracking()
            .Where(p => p.Id == before.Id || p.Id == after.Id)
            .ToDictionaryAsync(p => p.Id, p => p.TrustScore));
        trustScores[before.Id].Should().Be(25.00m);
        trustScores[after.Id].Should().Be(76.92m);
    }
}
