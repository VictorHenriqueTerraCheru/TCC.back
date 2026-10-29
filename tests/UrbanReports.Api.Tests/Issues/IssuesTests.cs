using UrbanReports.Api.Categories;
using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Common;
using UrbanReports.Api.Issues;
using UrbanReports.Api.Tests.Infra;

namespace UrbanReports.Api.Tests.Issues;

[Collection(ApiCollection.Name)]
public sealed class IssuesTests(ApiFactory factory)
{
    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    [Fact]
    public async Task Create_makes_new_issue_with_server_address()
    {
        var author = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.UniquePoint();

        var response = await author.PostIssueAsync(lat, lng, description: "  Buraco grande  ");
        var detail = await response.ReadAsync<GetIssueDetailResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().Be($"/api/v1/issues/{detail.Id}");
        detail.Status.Should().Be(IssueStatus.New);
        detail.Category.Should().Be(new CategorySummary(1, "Buraco na via", "viaPublica", "buraco"));
        detail.Description.Should().Be("Buraco grande");
        detail.Address.Should().Be("Rua Voluntários da Pátria, 170 — Botafogo");
        detail.Lat.Should().BeApproximately(lat, 1e-9);
        detail.Lng.Should().BeApproximately(lng, 1e-9);
        detail.CreatedAt.Should().Be(factory.Clock.GetUtcNow());
        detail.TrustScore.Should().BeNull();
        detail.Band.Should().BeNull();
        detail.Evaluation.Should().Be(new GetIssueDetailEvaluation(0, 5, detail.CreatedAt.AddHours(72)));
        detail.IsAuthor.Should().BeTrue();
        detail.MyVote.Should().BeNull();
        detail.Votes.Should().Be(new GetIssueDetailVotes(0, 0, 0));
    }

    [Fact]
    public async Task Create_with_nominatim_offline_still_creates()
    {
        var author = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.UniquePoint();
        factory.Nominatim.SetOffline(lat, lng);

        var response = await author.PostIssueAsync(lat, lng);
        var detail = await response.ReadAsync<GetIssueDetailResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        detail.Address.Should().StartWith("Próximo a ");
    }

    [Fact]
    public async Task Create_with_incident_category_returns_422()
    {
        var author = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.UniquePoint();

        var response = await author.PostIssueAsync(lat, lng, categoryId: 7);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.ReadJsonAsync()).GetProperty("type").GetString().Should().Be("category-not-issue");
    }

    [Theory]
    [InlineData(99, "categoryId")]
    [InlineData(1, "description")]
    public async Task Invalid_create_returns_400(short categoryId, string field)
    {
        var author = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.UniquePoint();

        var description = field == "description" ? new string('x', 281) : null;

        var response = await author.PostIssueAsync(lat, lng, categoryId, description);
        var errors = (await response.ReadJsonAsync()).GetProperty("errors");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        errors.TryGetProperty(field, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Create_requires_authentication()
    {
        var response = await factory.CreateClient().PostAsync("/api/v1/issues", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Detail_for_visitor_has_no_my_vote()
    {
        var author = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.UniquePoint();
        var issue = await author.CreateIssueAsync(lat, lng);

        var detail = await (await factory.CreateClient().GetAsync($"/api/v1/issues/{issue.Id}"))
            .ReadAsync<GetIssueDetailResponse>();

        detail.MyVote.Should().BeNull();
        detail.IsAuthor.Should().BeFalse();
    }

    [Fact]
    public async Task Missing_detail_returns_404()
    {
        var response = await factory.CreateClient().GetAsync($"/api/v1/issues/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Duplicate_at_30m_is_found_and_at_60m_is_not()
    {
        var person = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.UniquePoint();
        var near = await person.CreateIssueAsync(Scenarios.North(lat, 30), lng);
        await person.CreateIssueAsync(Scenarios.North(lat, 60), lng);
        await person.CreateIssueAsync(Scenarios.North(lat, 10), lng, categoryId: 2);

        var response = await person.Client.GetAsync(Inv($"/api/v1/issues/duplicates?categoryId=1&lat={lat}&lng={lng}"));
        var duplicates = await response.ReadAsync<List<SearchDuplicatesResponse>>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        duplicates.Should().ContainSingle();
        duplicates[0].Id.Should().Be(near.Id);
        duplicates[0].DistanceMeters.Should().BeCloseTo(30, 1);
        duplicates[0].Band.Should().BeNull("o problema ainda está em nova");
        duplicates[0].Confirmations.Should().Be(0);
    }

    [Fact]
    public async Task Duplicates_come_sorted_by_distance()
    {
        var person = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.UniquePoint();
        var forty = await person.CreateIssueAsync(Scenarios.North(lat, 40), lng);
        var ten = await person.CreateIssueAsync(Scenarios.North(lat, 10), lng);

        var duplicates = await (await person.Client.GetAsync(Inv($"/api/v1/issues/duplicates?categoryId=1&lat={lat}&lng={lng}")))
            .ReadAsync<List<SearchDuplicatesResponse>>();

        duplicates.Select(d => d.Id).Should().Equal(ten.Id, forty.Id);
    }

    [Fact]
    public async Task List_in_area_filters_by_rectangle_and_group_and_hides_trust_in_new()
    {
        var person = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.UniquePoint();
        var pothole = await person.CreateIssueAsync(lat, lng, categoryId: 1);
        var pole = await person.CreateIssueAsync(Scenarios.North(lat, 20), lng, categoryId: 3);
        await person.CreateIssueAsync(Scenarios.North(lat, 2000), lng);

        var area = Inv($"minLat={lat - 0.0005}&minLng={lng - 0.0005}&maxLat={lat + 0.0005}&maxLng={lng + 0.0005}");
        var all = await (await factory.CreateClient().GetAsync($"/api/v1/issues?{area}"))
            .ReadAsync<List<ListIssuesInAreaResponse>>();
        var electrical = await (await factory.CreateClient().GetAsync($"/api/v1/issues?{area}&groups=eletrica"))
            .ReadAsync<List<ListIssuesInAreaResponse>>();

        all.Select(p => p.Id).Should().BeEquivalentTo([pothole.Id, pole.Id]);
        all.Should().AllSatisfy(p =>
        {
            p.Status.Should().Be(IssueStatus.New);
            p.TrustScore.Should().BeNull();
            p.Band.Should().BeNull();
        });
        electrical.Select(p => p.Id).Should().Equal(pole.Id);
    }

    [Fact]
    public async Task List_in_area_never_returns_resolved()
    {
        var person = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.UniquePoint();
        var issue = await person.CreateIssueAsync(lat, lng);
        await factory.InDatabaseAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdateAsync(
            db.Issues.Where(p => p.Id == issue.Id), s => s.SetProperty(p => p.Status, IssueStatus.Resolved)));

        var area = Inv($"minLat={lat - 0.001}&minLng={lng - 0.001}&maxLat={lat + 0.001}&maxLng={lng + 0.001}");
        var list = await (await factory.CreateClient().GetAsync($"/api/v1/issues?{area}&status=new,active,possiblyResolved,resolved"))
            .ReadAsync<List<ListIssuesInAreaResponse>>();

        list.Should().NotContain(p => p.Id == issue.Id);
    }

    [Fact]
    public async Task List_in_area_respects_the_exact_rectangle_on_wide_viewport()
    {
        var person = await factory.NewPersonAsync();
        var offline = await person.CreateIssueAsync(70, 0);
        var within = await person.CreateIssueAsync(60.5, 10);

        var list = await (await factory.CreateClient().GetAsync("/api/v1/issues?minLat=60&minLng=-60&maxLat=61&maxLng=60"))
            .ReadAsync<List<ListIssuesInAreaResponse>>();

        list.Select(p => p.Id).Should().Contain(within.Id).And.NotContain(offline.Id);
    }

    [Fact]
    public async Task List_in_area_asking_only_resolved_returns_empty()
    {
        var person = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.UniquePoint();
        await person.CreateIssueAsync(lat, lng);

        var area = Inv($"minLat={lat - 0.001}&minLng={lng - 0.001}&maxLat={lat + 0.001}&maxLng={lng + 0.001}");
        var list = await (await factory.CreateClient().GetAsync($"/api/v1/issues?{area}&status=resolved"))
            .ReadAsync<List<ListIssuesInAreaResponse>>();

        list.Should().BeEmpty();
    }

    [Theory]
    [InlineData("groups=inexistente")]
    [InlineData("status=quebrado")]
    [InlineData("status=1")]
    [InlineData("status=999")]
    public async Task List_in_area_with_invalid_filter_returns_400(string filter)
    {
        var response = await factory.CreateClient().GetAsync($"/api/v1/issues?minLat=-23&minLng=-44&maxLat=-22&maxLng=-43&{filter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=2147483647&pageSize=100")]
    [InlineData("pageSize=101")]
    public async Task Recent_with_invalid_pagination_returns_400(string pagination)
    {
        var response = await factory.CreateClient().GetAsync($"/api/v1/issues/recent?lat=-22.95&lng=-43.18&{pagination}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Recent_pages_from_newest_to_oldest()
    {
        var person = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.UniquePoint();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            ids.Add((await person.CreateIssueAsync(Scenarios.North(lat, i * 5), lng)).Id);
            factory.Clock.Advance(TimeSpan.FromMinutes(1));
        }

        var url = Inv($"/api/v1/issues/recent?lat={lat}&lng={lng}&radiusKm=0.05&pageSize=2");
        var page1 = await (await factory.CreateClient().GetAsync(url)).ReadAsync<Paged<ListRecentIssuesItem>>();
        var page2 = await (await factory.CreateClient().GetAsync(url + "&page=2")).ReadAsync<Paged<ListRecentIssuesItem>>();

        page1.Total.Should().Be(3);
        page1.Page.Should().Be(1);
        page1.PageSize.Should().Be(2);
        page1.Items.Select(i => i.Id).Should().Equal(ids[2], ids[1]);
        page2.Items.Select(i => i.Id).Should().Equal(ids[0]);
    }

    [Fact]
    public async Task Duplicates_ignore_resolved_and_bring_confirmations_and_band()
    {
        var person = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.UniquePoint();
        var resolved = await person.CreateIssueAsync(Scenarios.North(lat, 10), lng);
        await factory.InDatabaseAsync(db => db.Issues.Where(p => p.Id == resolved.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, IssueStatus.Resolved)));
        var active = await factory.ActiveIssueAsync(Scenarios.North(lat, 20), lng);

        var duplicates = await (await person.Client.GetAsync(Inv($"/api/v1/issues/duplicates?categoryId=1&lat={lat}&lng={lng}")))
            .ReadAsync<List<SearchDuplicatesResponse>>();

        duplicates.Should().ContainSingle();
        duplicates[0].Id.Should().Be(active.Id);
        duplicates[0].Band.Should().Be(TrustBand.Medium);
        duplicates[0].Confirmations.Should().Be(5);
    }

    [Fact]
    public async Task Recent_ignore_resolved_and_outside_radius_and_filter_by_group()
    {
        var person = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.UniquePoint();
        var pothole = await person.CreateIssueAsync(Scenarios.North(lat, 10), lng, categoryId: 1);
        var pole = await person.CreateIssueAsync(Scenarios.North(lat, 20), lng, categoryId: 3);
        var resolved = await person.CreateIssueAsync(Scenarios.North(lat, 5), lng);
        await person.CreateIssueAsync(Scenarios.North(lat, 200), lng);
        await factory.InDatabaseAsync(db => db.Issues.Where(p => p.Id == resolved.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, IssueStatus.Resolved)));

        var url = Inv($"/api/v1/issues/recent?lat={lat}&lng={lng}&radiusKm=0.05");
        var all = await (await factory.CreateClient().GetAsync(url)).ReadAsync<Paged<ListRecentIssuesItem>>();
        var electrical = await (await factory.CreateClient().GetAsync(url + "&groups=eletrica")).ReadAsync<Paged<ListRecentIssuesItem>>();

        all.Total.Should().Be(2);
        all.Items.Select(i => i.Id).Should().BeEquivalentTo([pothole.Id, pole.Id]);
        electrical.Total.Should().Be(1);
        electrical.Items.Select(i => i.Id).Should().Equal(pole.Id);
    }
}
