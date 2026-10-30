using UrbanReports.Api.Common;

namespace UrbanReports.Api.Trust.Engine;

public static class TrustEngine
{
    public static double RoleWeight(Role role, TrustConfiguration cfg) => role switch
    {
        Role.Citizen => cfg.CitizenWeight,
        Role.VerifiedCitizen => cfg.VerifiedWeight,
        Role.Administrator => cfg.AdministratorWeight,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Perfil desconhecido."),
    };

    public static double ProximityWeight(double? distanceMeters, TrustConfiguration cfg)
    {
        if (!cfg.UseProximity || distanceMeters is not { } d)
        {
            return 1;
        }

        if (d <= cfg.FullBonusRadiusMeters)
        {
            return cfg.BonusMax;
        }

        if (d <= cfg.ZeroBonusRadiusMeters)
        {
            var fraction = (d - cfg.FullBonusRadiusMeters) / (cfg.ZeroBonusRadiusMeters - cfg.FullBonusRadiusMeters);
            return cfg.BonusMax - fraction * (cfg.BonusMax - 1);
        }

        return 1;
    }

    public static double TimeFactor(TimeSpan age, TimeSpan halfLife, TrustConfiguration cfg)
    {
        if (!cfg.UseDecay)
        {
            return 1;
        }

        var effectiveAge = age < TimeSpan.Zero ? TimeSpan.Zero : age;
        return Math.Pow(2, -(effectiveAge / halfLife));
    }

    public static double VoteWeight(VoteFact vote, DateTimeOffset now, TimeSpan halfLife, TrustConfiguration cfg) =>
        RoleWeight(vote.RoleAtVote, cfg)
        * ProximityWeight(vote.DistanceMeters, cfg)
        * TimeFactor(now - vote.RegisteredAt, halfLife, cfg);

    public static TrustBand ClassifyBand(double score, TrustConfiguration cfg) =>
        score >= cfg.HighThreshold ? TrustBand.High
        : score >= cfg.MediumThreshold ? TrustBand.Medium
        : TrustBand.Low;

    public static TrustResult CalculateTrust(
        IReadOnlyCollection<VoteFact> votes, DateTimeOffset now, TrustConfiguration cfg)
    {
        var (positive, negative) = SumWeights(votes, cfg.TrustHalfLife, now, cfg);

        var denominator = positive + negative + cfg.K;
        var score = denominator > 0 ? 100 * positive / denominator : 0;

        return new TrustResult(score, ClassifyBand(score, cfg), positive, negative);
    }

    public static IssueState EvaluateLifecycle(
        IssueState state, IReadOnlyCollection<VoteFact> votes, DateTimeOffset now, TrustConfiguration cfg)
    {
        switch (state.Status)
        {
            case IssueStatus.New:
            {
                var voters = votes.Select(v => v.UserId).Distinct().Count();
                var leavesNew = voters >= cfg.VotersToLeaveNew
                    || now - state.CreatedAt >= cfg.MaxTimeInNew;

                return leavesNew
                    ? state with { Status = IssueStatus.Active, StatusChangedAt = now }
                    : state;
            }

            case IssueStatus.Active:
            {
                var (stillThere, resolved) = SumWeights(votes, cfg.LifecycleHalfLife, now, cfg);
                var enters = resolved >= cfg.MinResolvedWeight
                    && ResolvedRatio(stillThere, resolved) >= cfg.ResolvedRatio;

                return enters
                    ? state with
                    {
                        Status = IssueStatus.PossiblyResolved,
                        StatusChangedAt = now,
                        ResolutionConditionSince = now,
                    }
                    : state;
            }

            case IssueStatus.PossiblyResolved:
            {
                var (stillThere, resolved) = SumWeights(votes, cfg.LifecycleHalfLife, now, cfg);

                if (ResolvedRatio(stillThere, resolved) < cfg.ResolvedRatio)
                {
                    return state with
                    {
                        Status = IssueStatus.Active,
                        StatusChangedAt = now,
                        ResolutionConditionSince = null,
                    };
                }

                var since = state.ResolutionConditionSince ?? state.StatusChangedAt;
                return now - since >= cfg.ResolvedConfirmationTime
                    ? state with { Status = IssueStatus.Resolved, StatusChangedAt = now }
                    : state;
            }

            case IssueStatus.Resolved:
                return state;

            default:
                throw new ArgumentOutOfRangeException(nameof(state), state.Status, "Status desconhecido.");
        }
    }

    private static (double Positive, double Negative) SumWeights(
        IReadOnlyCollection<VoteFact> votes, TimeSpan halfLife, DateTimeOffset now, TrustConfiguration cfg)
    {
        double positive = 0, negative = 0;

        foreach (var vote in votes)
        {
            var weight = VoteWeight(vote, now, halfLife, cfg);
            if (vote.Value == VoteValue.Positive)
            {
                positive += weight;
            }
            else
            {
                negative += weight;
            }
        }

        return (positive, negative);
    }

    private static double ResolvedRatio(double stillThere, double resolved)
    {
        var total = stillThere + resolved;
        return total > 0 ? resolved / total : 0;
    }
}
