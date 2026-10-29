using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UrbanReports.Api.Persistence;
using UrbanReports.Api.Issues;
using UrbanReports.Api.Trust;
using UrbanReports.Api.Tests.Infra;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Tests.Trust;

[Collection(ApiCollection.Name)]
public sealed class LifecycleTests(ApiFactory factory)
{
    private async Task ProcessAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<LifecycleProcessor>().ProcessAsync(CancellationToken.None);
    }

    private Task<GetIssueDetailResponse> DetailAsync(Guid id) =>
        factory.CreateClient().GetAsync($"/api/v1/issues/{id}").ContinueWith(t => t.Result.ReadAsync<GetIssueDetailResponse>()).Unwrap();

    [Fact]
    public async Task Job_moves_possibly_resolved_to_resolved_after_48h()
    {
        var (lat, lng) = ApiFactory.UniquePoint();
        var issue = await factory.ActiveIssueAsync(lat, lng);
        for (var i = 0; i < 4; i++)
        {
            var voter = await factory.NewPersonAsync();
            await voter.VoteAsync(issue.Id, "no", Scenarios.North(lat, 100), lng);
        }

        (await DetailAsync(issue.Id)).Status.Should().Be(IssueStatus.PossiblyResolved);

        factory.Clock.Advance(TimeSpan.FromHours(47));
        await ProcessAsync();
        (await DetailAsync(issue.Id)).Status.Should().Be(IssueStatus.PossiblyResolved);

        factory.Clock.Advance(TimeSpan.FromHours(1));
        await ProcessAsync();
        var written = await factory.InDatabaseAsync(db => db.Issues.AsNoTracking().SingleAsync(p => p.Id == issue.Id));
        written.Status.Should().Be(IssueStatus.Resolved);
        written.StatusChangedAt.Should().Be(factory.Clock.GetUtcNow());
    }

    [Fact]
    public async Task Job_moves_out_of_new_after_72h_without_votes()
    {
        var author = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.UniquePoint();
        var issue = await author.CreateIssueAsync(lat, lng);

        factory.Clock.Advance(TimeSpan.FromHours(72));
        await ProcessAsync();

        var detail = await DetailAsync(issue.Id);
        detail.Status.Should().Be(IssueStatus.Active);
        detail.TrustScore.Should().Be(0);
        detail.Band.Should().Be(TrustBand.Low);
    }

    [Fact]
    public async Task Job_applies_time_decay_without_new_votes()
    {
        var (lat, lng) = ApiFactory.UniquePoint();
        var issue = await factory.ActiveIssueAsync(lat, lng);

        factory.Clock.Advance(TimeSpan.FromDays(14));
        await ProcessAsync();

        (await DetailAsync(issue.Id)).TrustScore.Should().Be(45.5);
    }

    [Fact]
    public async Task Job_leaves_resolved_alone()
    {
        var author = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.UniquePoint();
        var issue = await author.CreateIssueAsync(lat, lng);
        await factory.InDatabaseAsync(db => db.Issues.Where(p => p.Id == issue.Id).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.Status, IssueStatus.Resolved)
            .SetProperty(p => p.TrustScore, (decimal?)12.34m)));
        var before = await factory.InDatabaseAsync(db => db.Issues.AsNoTracking().SingleAsync(p => p.Id == issue.Id));

        factory.Clock.Advance(TimeSpan.FromDays(30));
        await ProcessAsync();

        var after = await factory.InDatabaseAsync(db => db.Issues.AsNoTracking().SingleAsync(p => p.Id == issue.Id));
        after.Status.Should().Be(IssueStatus.Resolved);
        after.TrustScore.Should().Be(12.34m);
        after.StatusChangedAt.Should().Be(before.StatusChangedAt);
    }

    [Fact]
    public async Task Job_counts_the_transition_and_finds_nothing_on_the_next_run()
    {
        await RemoveIssuesAsync();

        var author = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.UniquePoint();
        var issue = await author.CreateIssueAsync(lat, lng);
        factory.Clock.Advance(TimeSpan.FromHours(72));

        (await ProcessCountingAsync()).Should().Be(1);
        (await DetailAsync(issue.Id)).Status.Should().Be(IssueStatus.Active);

        (await ProcessCountingAsync()).Should().Be(0);
    }

    private async Task RemoveIssuesAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Issues.ExecuteDeleteAsync();
    }

    private async Task<int> ProcessCountingAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<LifecycleProcessor>().ProcessAsync(CancellationToken.None);
    }
}
