using UrbanReports.Api.Common;
using UrbanReports.Api.Trust.Engine;
using static UrbanReports.Api.Tests.Trust.Engine.Scenario;

namespace UrbanReports.Api.Tests.Trust.Engine;

public sealed class ComplementaryRulesTests
{
    private static readonly TrustConfiguration B = TrustConfiguration.B;

    [Theory]
    [InlineData(Role.Citizen, 1)]
    [InlineData(Role.VerifiedCitizen, 10)]
    [InlineData(Role.Administrator, 1)]
    public void Role_weight_in_config_B(Role role, double expected) =>
        TrustEngine.RoleWeight(role, B).Should().Be(expected);

    [Fact]
    public void Config_A_weighs_verified_as_citizen() =>
        TrustEngine.RoleWeight(Role.VerifiedCitizen, TrustConfiguration.A).Should().Be(1);

    [Theory]
    [InlineData(0, 2)]
    [InlineData(200, 2)]
    [InlineData(2000, 1)]
    [InlineData(2000.001, 1)]
    public void Proximity_at_band_limits(double distance, double expected) =>
        TrustEngine.ProximityWeight(distance, B).Should().BeApproximately(expected, 1e-6);

    [Fact]
    public void Proximity_never_goes_below_1()
    {
        for (var d = 0.0; d <= 10_000; d += 7.3)
        {
            TrustEngine.ProximityWeight(d, B).Should().BeGreaterThanOrEqualTo(1);
        }
    }

    [Fact]
    public void Vote_registered_in_the_future_gains_no_extra_weight() =>
        TrustEngine.TimeFactor(TimeSpan.FromHours(-5), B.TrustHalfLife, B).Should().Be(1);

    [Fact]
    public void Config_A_does_not_decay() =>
        TrustEngine.TimeFactor(TimeSpan.FromDays(365), TrustConfiguration.A.LifecycleHalfLife, TrustConfiguration.A).Should().Be(1);

    [Fact]
    public void Vote_weight_multiplies_role_proximity_and_time()
    {
        var vote = Yes(Role.VerifiedCitizen, distance: 1100, at: T0);

        TrustEngine.VoteWeight(vote, T0.AddDays(14), B.TrustHalfLife, B).Should().BeApproximately(10 * 1.5 * 0.5, 1e-9);
    }

    [Theory]
    [InlineData(39.999, TrustBand.Low)]
    [InlineData(40, TrustBand.Medium)]
    [InlineData(69.999, TrustBand.Medium)]
    [InlineData(70, TrustBand.High)]
    public void Bands_by_thresholds(double score, TrustBand expected) =>
        TrustEngine.ClassifyBand(score, B).Should().Be(expected);

    [Fact]
    public void New_does_not_jump_straight_to_possibly_resolved()
    {
        List<VoteFact> votes = [.. Repeat(5, () => Yes()), .. Repeat(10, () => No())];

        var e = TrustEngine.EvaluateLifecycle(State(IssueStatus.New), votes, T0, B);

        e.Status.Should().Be(IssueStatus.Active);
        e.ResolutionConditionSince.Should().BeNull();
    }

    [Fact]
    public void No_votes_also_count_as_voters()
    {
        List<VoteFact> votes = [.. Repeat(3, () => Yes()), .. Repeat(2, () => No())];

        TrustEngine.EvaluateLifecycle(State(IssueStatus.New), votes, T0, B)
            .Status.Should().Be(IssueStatus.Active);
    }

    [Fact]
    public void Same_vote_ages_slowly_in_score_and_fast_in_lifecycle()
    {
        var vote = Yes(at: T0);
        var now = T0.AddDays(3);

        TrustEngine.CalculateTrust([vote], now, B).PositiveEvidence
            .Should().BeApproximately(2 * Math.Pow(2, -3.0 / 14), 1e-9);
        TrustEngine.VoteWeight(vote, now, B.LifecycleHalfLife, B).Should().BeApproximately(1, 1e-9);
    }

    [Fact]
    public void Active_requires_min_resolved_weight_to_move_on()
    {
        var votes = Repeat(2, () => No());

        TrustEngine.EvaluateLifecycle(State(IssueStatus.Active), votes, T0, B)
            .Status.Should().Be(IssueStatus.Active);
    }

    [Fact]
    public void Active_with_only_yes_stays_active() =>
        TrustEngine.EvaluateLifecycle(State(IssueStatus.Active), [Yes()], T0.AddDays(30), B)
            .Status.Should().Be(IssueStatus.Active);

    [Fact]
    public void Ratio_exactly_at_threshold_becomes_possibly_resolved()
    {
        List<VoteFact> votes = [.. Repeat(3, () => No()), .. Repeat(2, () => Yes())];

        TrustEngine.EvaluateLifecycle(State(IssueStatus.Active), votes, T0, B)
            .Status.Should().Be(IssueStatus.PossiblyResolved);
    }

    [Fact]
    public void Resolved_is_final()
    {
        var state = State(IssueStatus.Resolved);
        var votes = Repeat(10, () => Yes());

        TrustEngine.EvaluateLifecycle(state, votes, T0.AddDays(10), B).Should().Be(state);
    }

    [Fact]
    public void No_transition_returns_state_unchanged()
    {
        var state = State(IssueStatus.Active, statusChangedAt: T0);

        TrustEngine.EvaluateLifecycle(state, [Yes()], T0.AddHours(1), B).Should().Be(state);
    }
}
