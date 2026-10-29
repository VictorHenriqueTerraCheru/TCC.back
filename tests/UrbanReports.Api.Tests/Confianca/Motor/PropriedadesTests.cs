using UrbanReports.Api.Common;
using UrbanReports.Api.Trust.Engine;
using static UrbanReports.Api.Tests.Trust.Engine.Scenario;

namespace UrbanReports.Api.Tests.Trust.Engine;

public sealed class PropertiesTests
{
    private const int Samples = 500;

    public static TheoryData<string> Configurations => ["A", "B", "B-v3", "B-v5"];

    private static TrustConfiguration Config(string name) => name switch
    {
        "A" => TrustConfiguration.A,
        "B" => TrustConfiguration.B,
        "B-v3" => TrustConfiguration.B.WithVerifiedWeight(3),
        "B-v5" => TrustConfiguration.B.WithVerifiedWeight(5),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static VoteFact RandomVote(Random rnd, VoteValue? value = null) => Vote(
        value ?? (rnd.Next(2) == 0 ? VoteValue.Positive : VoteValue.Negative),
        (Role)rnd.Next(1, 4),
        rnd.Next(4) == 0 ? null : rnd.NextDouble() * 5000,
        T0.AddHours(-rnd.Next(0, 24 * 90)));

    private static List<VoteFact> RandomVotes(Random rnd) =>
        Enumerable.Range(0, rnd.Next(0, 60)).Select(_ => RandomVote(rnd)).ToList();

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Score_stays_from_0_inclusive_to_100_exclusive(string name)
    {
        var cfg = Config(name);
        var rnd = new Random(42);

        for (var i = 0; i < Samples; i++)
        {
            var r = TrustEngine.CalculateTrust(RandomVotes(rnd), T0, cfg);

            r.Score.Should().BeGreaterThanOrEqualTo(0).And.BeLessThan(100);
        }
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Score_grows_when_a_yes_arrives(string name)
    {
        var cfg = Config(name);
        var rnd = new Random(7);

        for (var i = 0; i < Samples; i++)
        {
            var votes = RandomVotes(rnd);
            var before = TrustEngine.CalculateTrust(votes, T0, cfg);

            votes.Add(RandomVote(rnd, VoteValue.Positive));
            var after = TrustEngine.CalculateTrust(votes, T0, cfg);

            after.PositiveEvidence.Should().BeGreaterThan(before.PositiveEvidence);
            after.Score.Should().BeGreaterThan(before.Score);
        }
    }

    [Fact]
    public void Config_A_ignores_now()
    {
        var rnd = new Random(2026);

        for (var i = 0; i < Samples; i++)
        {
            var votes = RandomVotes(rnd);
            var now = TrustEngine.CalculateTrust(votes, T0, TrustConfiguration.A);
            var after = TrustEngine.CalculateTrust(votes, T0.AddDays(rnd.Next(1, 1000)), TrustConfiguration.A);

            after.Should().Be(now);
        }
    }
}
