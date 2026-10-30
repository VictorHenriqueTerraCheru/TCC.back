using System.Net;
using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Issues;
using UrbanReports.Api.Votes;
using UrbanReports.Api.Tests.Infra;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Tests.Votes;

[Collection(ApiCollection.Name)]
public sealed class VotesTests(ApiFactory factory)
{
    private async Task<(Person Author, GetIssueDetailResponse Issue, double Lat, double Lng)> NewIssueAsync()
    {
        var author = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.UniquePoint();
        return (author, await author.CreateIssueAsync(lat, lng), lat, lng);
    }

    private static async Task<string?> ErrorTypeAsync(HttpResponseMessage response) =>
        (await response.ReadJsonAsync()).GetProperty("type").GetString();

    [Theory]
    [InlineData("yes")]
    [InlineData("no")]
    public async Task Author_cannot_vote_on_own_issue(string value)
    {
        var (author, issue, _, _) = await NewIssueAsync();

        var response = await author.VoteAsync(issue.Id, value);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrorTypeAsync(response)).Should().Be("author-cannot-vote");
    }

    [Fact]
    public async Task No_on_new_is_accepted_and_counts_as_voter()
    {
        var (_, issue, _, _) = await NewIssueAsync();
        var voter = await factory.NewPersonAsync();

        var response = await voter.VoteAsync(issue.Id, "no");
        var detail = await response.ReadAsync<GetIssueDetailResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        detail.Status.Should().Be(IssueStatus.New);
        detail.Evaluation!.Voters.Should().Be(1);
        detail.Votes.Should().Be(new GetIssueDetailVotes(0, 1, 1));
        detail.MyVote.Should().Be(VoteAnswer.No);
    }

    [Fact]
    public async Task Five_voters_move_the_issue_out_of_new()
    {
        var (_, issue, _, _) = await NewIssueAsync();
        GetIssueDetailResponse? detail = null;

        for (var i = 1; i <= 5; i++)
        {
            var voter = await factory.NewPersonAsync();
            detail = await (await voter.VoteAsync(issue.Id, "yes")).ReadAsync<GetIssueDetailResponse>();

            if (i < 5)
            {
                detail.Status.Should().Be(IssueStatus.New);
                detail.Evaluation!.Voters.Should().Be(i);
                detail.TrustScore.Should().BeNull();
            }
        }

        detail!.Status.Should().Be(IssueStatus.Active);
        detail.Evaluation.Should().BeNull();
        detail.TrustScore.Should().Be(62.5);
        detail.Band.Should().Be(TrustBand.Medium);
        detail.Votes.Should().Be(new GetIssueDetailVotes(5, 0, 0));
        detail.MyVote.Should().Be(VoteAnswer.Yes);
    }

    [Fact]
    public async Task Vote_stores_only_the_distance_computed_in_postgis()
    {
        var (_, issue, lat, lng) = await NewIssueAsync();
        var voter = await factory.NewPersonAsync();

        await voter.VoteAsync(issue.Id, "yes", Scenarios.North(lat, 100), lng);

        var vote = await factory.InDatabaseAsync(db => db.Votes.SingleAsync(v => v.IssueId == issue.Id));
        vote.DistanceMeters!.Value.Should().BeCloseTo(100, 2);
        vote.RoleAtVote.Should().Be(Role.Citizen);
        vote.RegisteredAt.Should().Be(factory.Clock.GetUtcNow());
    }

    [Fact]
    public async Task Vote_without_location_stores_null_distance()
    {
        var (_, issue, _, _) = await NewIssueAsync();
        var voter = await factory.NewPersonAsync();

        await voter.VoteAsync(issue.Id, "no");

        var vote = await factory.InDatabaseAsync(db => db.Votes.SingleAsync(v => v.IssueId == issue.Id));
        vote.DistanceMeters.Should().BeNull();
        vote.Value.Should().Be(VoteValue.Negative);
    }

    [Fact]
    public async Task Voting_again_replaces_the_vote()
    {
        var (_, issue, lat, lng) = await NewIssueAsync();
        var voter = await factory.NewPersonAsync();
        await voter.VoteAsync(issue.Id, "yes", Scenarios.North(lat, 100), lng);
        factory.Clock.Advance(TimeSpan.FromMinutes(5));

        var response = await voter.VoteAsync(issue.Id, "no");
        var detail = await response.ReadAsync<GetIssueDetailResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        detail.Votes.Should().Be(new GetIssueDetailVotes(0, 1, 1));
        detail.MyVote.Should().Be(VoteAnswer.No);

        var votes = await factory.InDatabaseAsync(db => db.Votes.Where(v => v.IssueId == issue.Id).ToListAsync());
        votes.Should().ContainSingle();
        votes[0].DistanceMeters.Should().BeNull();
        votes[0].RegisteredAt.Should().Be(factory.Clock.GetUtcNow());
    }

    [Fact]
    public async Task Vote_role_comes_from_database_not_token()
    {
        var (_, issue, _, _) = await NewIssueAsync();
        var voter = await factory.NewPersonAsync();
        await factory.InDatabaseAsync(db => db.Users.Where(u => u.Id == voter.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, Role.VerifiedCitizen)));

        await voter.VoteAsync(issue.Id, "yes");

        var vote = await factory.InDatabaseAsync(db => db.Votes.SingleAsync(v => v.IssueId == issue.Id));
        vote.RoleAtVote.Should().Be(Role.VerifiedCitizen);
    }

    [Fact]
    public async Task Resolved_issue_does_not_accept_votes()
    {
        var (_, issue, _, _) = await NewIssueAsync();
        await factory.InDatabaseAsync(db => db.Issues.Where(p => p.Id == issue.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, IssueStatus.Resolved)));
        var voter = await factory.NewPersonAsync();

        var response = await voter.VoteAsync(issue.Id, "yes");

        (await ErrorTypeAsync(response)).Should().Be("issue-resolved");
    }

    [Fact]
    public async Task Four_nearby_no_lead_to_possibly_resolved()
    {
        var (lat, lng) = ApiFactory.UniquePoint();
        var issue = await factory.ActiveIssueAsync(lat, lng);
        GetIssueDetailResponse? detail = null;

        for (var i = 0; i < 4; i++)
        {
            var voter = await factory.NewPersonAsync();
            detail = await (await voter.VoteAsync(issue.Id, "no", Scenarios.North(lat, 100), lng))
                .ReadAsync<GetIssueDetailResponse>();
        }

        detail!.Status.Should().Be(IssueStatus.PossiblyResolved);
        detail.Votes.Should().Be(new GetIssueDetailVotes(5, 4, 4));
    }

    [Fact]
    public async Task Missing_issue_returns_404()
    {
        var voter = await factory.NewPersonAsync();

        var response = await voter.VoteAsync(Guid.NewGuid(), "yes");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("talvez", -22.9, -43.1)]
    [InlineData("confirma", -22.9, -43.1)]
    [InlineData("yes", -22.9, null)]
    [InlineData("yes", -95.0, -43.1)]
    public async Task Invalid_vote_returns_400(string value, double? lat, double? lng)
    {
        var (_, issue, _, _) = await NewIssueAsync();
        var voter = await factory.NewPersonAsync();

        var response = await voter.VoteAsync(issue.Id, value, lat, lng);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Concurrent_votes_on_the_same_issue_keep_recalculation()
    {
        var (_, issue, _, _) = await NewIssueAsync();
        var voters = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => factory.NewPersonAsync()));

        var responses = await Task.WhenAll(voters.Select(v => v.VoteAsync(issue.Id, "yes")));

        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
        var detail = await (await factory.CreateClient().GetAsync($"/api/v1/issues/{issue.Id}"))
            .ReadAsync<GetIssueDetailResponse>();
        detail.Status.Should().Be(IssueStatus.Active);
        detail.TrustScore.Should().Be(62.5);
    }

    [Fact]
    public async Task No_last_24h_includes_exactly_24h_and_drops_older()
    {
        var (_, issue, _, _) = await NewIssueAsync();
        var first = await factory.NewPersonAsync();
        var second = await factory.NewPersonAsync();
        await first.VoteAsync(issue.Id, "no");
        factory.Clock.Advance(TimeSpan.FromHours(24));
        await second.VoteAsync(issue.Id, "no");

        var atLimit = await (await factory.CreateClient().GetAsync($"/api/v1/issues/{issue.Id}"))
            .ReadAsync<GetIssueDetailResponse>();
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var after = await (await factory.CreateClient().GetAsync($"/api/v1/issues/{issue.Id}"))
            .ReadAsync<GetIssueDetailResponse>();

        atLimit.Votes.Should().Be(new GetIssueDetailVotes(0, 2, 2));
        after.Votes.Should().Be(new GetIssueDetailVotes(0, 2, 1));
    }
}
