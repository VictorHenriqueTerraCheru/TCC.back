using UrbanReports.Api.Common;
using UrbanReports.Api.Trust.Engine;
using static UrbanReports.Api.Tests.Trust.Engine.Scenario;

namespace UrbanReports.Api.Tests.Trust.Engine;

public sealed class TrustEngineTests
{
    private const double Tolerance = 1e-6;
    private const double ScoreTolerance = 1e-3;

    private static readonly TrustConfiguration A = TrustConfiguration.A;
    private static readonly TrustConfiguration B = TrustConfiguration.B;

    [Fact]
    public void Case01_proximity_up_to_200m_is_2() =>
        TrustEngine.ProximityWeight(150, B).Should().BeApproximately(2, Tolerance);

    [Fact]
    public void Case02_proximity_1100m_is_1_5() =>
        TrustEngine.ProximityWeight(1100, B).Should().BeApproximately(1.5, Tolerance);

    [Fact]
    public void Case03_proximity_far_or_without_gps_is_1()
    {
        TrustEngine.ProximityWeight(5000, B).Should().BeApproximately(1, Tolerance);
        TrustEngine.ProximityWeight(null, B).Should().BeApproximately(1, Tolerance);
    }

    [Fact]
    public void Case04_config_A_ignores_proximity() =>
        TrustEngine.ProximityWeight(50, A).Should().BeApproximately(1, Tolerance);

    [Fact]
    public void Case05_trust_half_life_14_days() =>
        TrustEngine.TimeFactor(TimeSpan.FromDays(14), B.TrustHalfLife, B).Should().BeApproximately(0.5, Tolerance);

    [Fact]
    public void Case06_lifecycle_half_life_3_days() =>
        TrustEngine.TimeFactor(TimeSpan.FromDays(3), B.LifecycleHalfLife, B).Should().BeApproximately(0.5, Tolerance);

    private static List<VoteFact> FifteenYesAt100m() => Repeat(15, () => Yes());

    [Fact]
    public void Case07_fifteen_yes_now()
    {
        var r = TrustEngine.CalculateTrust(FifteenYesAt100m(), T0, B);

        r.Score.Should().BeApproximately(90.909, ScoreTolerance);
        r.Band.Should().Be(TrustBand.High);
    }

    [Fact]
    public void Case08_fifteen_yes_after_28_days()
    {
        var r = TrustEngine.CalculateTrust(FifteenYesAt100m(), T0.AddDays(28), B);

        r.Score.Should().BeApproximately(71.429, ScoreTolerance);
        r.Band.Should().Be(TrustBand.High);
    }

    [Fact]
    public void Case09_fifteen_yes_after_56_days()
    {
        var r = TrustEngine.CalculateTrust(FifteenYesAt100m(), T0.AddDays(56), B);

        r.Score.Should().BeApproximately(38.462, ScoreTolerance);
        r.Band.Should().Be(TrustBand.Low);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(28)]
    [InlineData(365)]
    public void Case10_config_A_ignores_time(int days)
    {
        var r = TrustEngine.CalculateTrust(FifteenYesAt100m(), T0.AddDays(days), A);

        r.Score.Should().BeApproximately(83.333, ScoreTolerance);
    }

    [Fact]
    public void Case11_one_verified_at_100m()
    {
        var r = TrustEngine.CalculateTrust([Yes(Role.VerifiedCitizen)], T0, B);

        r.Score.Should().BeApproximately(86.957, ScoreTolerance);
        r.Band.Should().Be(TrustBand.High);
    }

    [Fact]
    public void Case12_one_verified_with_weight_3()
    {
        var cfg = B.WithVerifiedWeight(3);

        var r = TrustEngine.CalculateTrust([Yes(Role.VerifiedCitizen)], T0, cfg);

        cfg.Name.Should().Be("B-v3");
        r.PositiveEvidence.Should().BeApproximately(6, Tolerance);
        r.Score.Should().BeApproximately(66.667, ScoreTolerance);
        r.Band.Should().Be(TrustBand.Medium);
    }

    [Fact]
    public void Case13_no_votes()
    {
        var r = TrustEngine.CalculateTrust([], T0, B);

        r.Score.Should().Be(0);
        r.Band.Should().Be(TrustBand.Low);
    }

    [Fact]
    public void Case14_new_with_4_voters_and_71h_stays_new()
    {
        var votes = Repeat(4, () => Yes());

        var e = TrustEngine.EvaluateLifecycle(State(IssueStatus.New), votes, T0.AddHours(71), B);

        e.Status.Should().Be(IssueStatus.New);
    }

    [Fact]
    public void Case15_new_with_5_distinct_voters_becomes_active()
    {
        var votes = Repeat(5, () => Yes());
        var now = T0.AddHours(1);

        var e = TrustEngine.EvaluateLifecycle(State(IssueStatus.New), votes, now, B);

        e.Status.Should().Be(IssueStatus.Active);
        e.StatusChangedAt.Should().Be(now);
    }

    [Fact]
    public void Case16_new_with_1_voter_and_72h_becomes_active()
    {
        var e = TrustEngine.EvaluateLifecycle(State(IssueStatus.New), [Yes()], T0.AddHours(72), B);

        e.Status.Should().Be(IssueStatus.Active);
    }

    private static List<VoteFact> VotesCase17() => [.. Repeat(3, () => No()), Yes()];

    private static IssueState StateAfterCase17() =>
        TrustEngine.EvaluateLifecycle(State(IssueStatus.Active), VotesCase17(), T0, B);

    [Fact]
    public void Case17_active_with_majority_no_becomes_possibly_resolved()
    {
        var e = StateAfterCase17();

        e.Status.Should().Be(IssueStatus.PossiblyResolved);
        e.ResolutionConditionSince.Should().Be(T0);
        e.StatusChangedAt.Should().Be(T0);
    }

    [Fact]
    public void Case18_after_47h_without_votes_stays_possibly_resolved()
    {
        var e = TrustEngine.EvaluateLifecycle(StateAfterCase17(), VotesCase17(), T0.AddHours(47), B);

        e.Status.Should().Be(IssueStatus.PossiblyResolved);
        e.ResolutionConditionSince.Should().Be(T0);
    }

    [Fact]
    public void Case19_after_48h_without_votes_becomes_resolved()
    {
        var now = T0.AddHours(48);

        var e = TrustEngine.EvaluateLifecycle(StateAfterCase17(), VotesCase17(), now, B);

        e.Status.Should().Be(IssueStatus.Resolved);
        e.StatusChangedAt.Should().Be(now);
    }

    [Fact]
    public void Case20_three_yes_drop_the_ratio_and_return_to_active()
    {
        var now = T0.AddHours(1);
        List<VoteFact> votes = [.. VotesCase17(), .. Repeat(3, () => Yes(at: now))];

        var e = TrustEngine.EvaluateLifecycle(StateAfterCase17(), votes, now, B);

        e.Status.Should().Be(IssueStatus.Active);
        e.ResolutionConditionSince.Should().BeNull();
        e.StatusChangedAt.Should().Be(now);
    }

    [Fact]
    public void Case21_vote_uniqueness_is_guaranteed_by_the_api_not_the_engine()
    {
        var user = Guid.NewGuid();
        List<VoteFact> duplicates = [Yes(user: user), Yes(user: user)];

        var r = TrustEngine.CalculateTrust(duplicates, T0, B);
        r.PositiveEvidence.Should().BeApproximately(4, Tolerance);

        List<VoteFact> fiveFromSameUser = Repeat(5, () => Yes(user: user));
        TrustEngine.EvaluateLifecycle(State(IssueStatus.New), fiveFromSameUser, T0, B)
            .Status.Should().Be(IssueStatus.New, "votantes conta UsuarioId distintos");
    }

    [Fact]
    public void Case22_new_with_only_no_leaves_the_map_in_48h()
    {
        var votes = Repeat(5, () => No());

        TrustEngine.CalculateTrust(votes, T0, B).Band.Should().Be(TrustBand.Low);

        var active = TrustEngine.EvaluateLifecycle(State(IssueStatus.New), votes, T0, B);
        active.Status.Should().Be(IssueStatus.Active);

        var possibly = TrustEngine.EvaluateLifecycle(active, votes, T0.AddMinutes(15), B);
        possibly.Status.Should().Be(IssueStatus.PossiblyResolved);

        TrustEngine.EvaluateLifecycle(possibly, votes, T0.AddMinutes(15).AddHours(48), B)
            .Status.Should().Be(IssueStatus.Resolved);
    }
}
