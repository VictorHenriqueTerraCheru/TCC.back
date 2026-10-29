using System.Net;
using System.Net.Http.Json;
using UrbanReports.Api.Common;
using UrbanReports.Api.Users;
using UrbanReports.Api.Tests.Infra;

namespace UrbanReports.Api.Tests.Users;

[Collection(ApiCollection.Name)]
public sealed class HistoryTests(ApiFactory factory)
{
    private static async Task<Paged<GetHistoryItem>> HistoryAsync(Person person, string query = "") =>
        await (await person.Client.GetAsync($"/api/v1/me/history{query}")).ReadAsync<Paged<GetHistoryItem>>();

    [Fact]
    public async Task Merges_reports_and_votes_from_newest_to_oldest()
    {
        var me = await factory.NewPersonAsync();
        var other = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.UniquePoint();

        var myIssue = await me.CreateIssueAsync(lat, lng);
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await me.Client.PostAsJsonAsync("/api/v1/incidents",
            new { categoryId = 7, lat, lng, occurredAt = factory.Clock.GetUtcNow().AddMinutes(-10) }, ApiClient.Json);
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var othersIssue = await other.CreateIssueAsync(Scenarios.North(lat, 30), lng, categoryId: 3);
        await me.VoteAsync(othersIssue.Id, "no");

        var history = await HistoryAsync(me);

        history.Total.Should().Be(3);
        history.Items.Select(i => i.Type).Should().Equal(GetHistoryItemType.Vote, GetHistoryItemType.IncidentReport, GetHistoryItemType.IssueReport);

        var vote = history.Items[0];
        vote.ReportId.Should().Be(othersIssue.Id);
        vote.Value.Should().Be(GetHistoryItemValue.No);
        vote.IssueStatus.Should().Be(IssueStatus.New);
        vote.Category.Name.Should().Be("Poste apagado");

        var incident = history.Items[1];
        incident.ReportId.Should().BeNull();
        incident.Address.Should().Be("Rua Voluntários da Pátria, Botafogo");
        incident.Value.Should().BeNull();
        incident.IssueStatus.Should().BeNull();

        var report = history.Items[2];
        report.ReportId.Should().Be(myIssue.Id);
        report.Address.Should().Be(myIssue.Address);
        report.Date.Should().Be(myIssue.CreatedAt);
    }

    [Fact]
    public async Task Filters_and_pagination()
    {
        var me = await factory.NewPersonAsync();
        var other = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.UniquePoint();
        await me.CreateIssueAsync(lat, lng);
        var foreignIssue = await other.CreateIssueAsync(Scenarios.North(lat, 30), lng);
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await me.VoteAsync(foreignIssue.Id, "yes");
        await me.VoteAsync(foreignIssue.Id, "no");

        var reports = await HistoryAsync(me, "?filter=reports");
        var votes = await HistoryAsync(me, "?filter=votes");
        var page2 = await HistoryAsync(me, "?pageSize=1&page=2");

        reports.Items.Should().ContainSingle().Which.Type.Should().Be(GetHistoryItemType.IssueReport);
        votes.Items.Select(i => (i.Type, i.Value)).Should().Equal(
            [(GetHistoryItemType.Vote, (GetHistoryItemValue?)GetHistoryItemValue.No)]);
        page2.Total.Should().Be(2);
        page2.Items.Should().ContainSingle().Which.Type.Should().Be(GetHistoryItemType.IssueReport);
    }

    [Fact]
    public async Task Pagination_is_stable_with_reports_at_the_same_instant()
    {
        var me = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.IsolatedArea();
        for (var i = 0; i < 5; i++)
        {
            var latI = lat + i * 0.001;
            factory.Nominatim.Respond(latI, lng, FakeNominatim.Address(street: $"Rua {i}"));
            (await me.Client.PostAsJsonAsync("/api/v1/incidents",
                new { categoryId = 7, lat = latI, lng, occurredAt = factory.Clock.GetUtcNow().AddMinutes(-5) }, ApiClient.Json))
                .EnsureSuccessStatusCode();
        }

        var pages = new List<GetHistoryItem>();
        for (var page = 1; page <= 5; page++)
        {
            pages.AddRange((await HistoryAsync(me, $"?pageSize=1&page={page}")).Items);
        }

        var complete = (await HistoryAsync(me, "?pageSize=5")).Items;
        pages.Should().Equal(complete);
        pages.Select(i => i.Address).Should().OnlyHaveUniqueItems().And.HaveCount(5);
    }

    [Fact]
    public async Task History_enums_travel_in_camel_case()
    {
        var me = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.UniquePoint();
        await me.CreateIssueAsync(lat, lng);

        var json = await (await me.Client.GetAsync("/api/v1/me/history")).ReadJsonAsync();

        json.GetProperty("items")[0].GetProperty("type").GetString().Should().Be("issueReport");
    }

    [Fact]
    public async Task Invalid_filter_returns_400()
    {
        var me = await factory.NewPersonAsync();

        (await me.Client.GetAsync("/api/v1/me/history?filter=others")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
