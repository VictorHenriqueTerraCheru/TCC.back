using System.Globalization;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Trust.Engine;

public sealed record TrustConfiguration
{
    public required string Name { get; init; }

    public double CitizenWeight { get; init; } = 1;
    public double VerifiedWeight { get; init; } = 10;
    public double AdministratorWeight { get; init; } = 1;

    public bool UseProximity { get; init; } = true;
    public double FullBonusRadiusMeters { get; init; } = 200;
    public double ZeroBonusRadiusMeters { get; init; } = 2000;
    public double BonusMax { get; init; } = 2;

    public bool UseDecay { get; init; } = true;
    public TimeSpan TrustHalfLife { get; init; } = TimeSpan.FromDays(14);
    public TimeSpan LifecycleHalfLife { get; init; } = TimeSpan.FromDays(3);

    public double K { get; init; } = 3;
    public double MediumThreshold { get; init; } = 40;
    public double HighThreshold { get; init; } = 70;

    public int VotersToLeaveNew { get; init; } = 5;
    public TimeSpan MaxTimeInNew { get; init; } = TimeSpan.FromHours(72);
    public double MinResolvedWeight { get; init; } = 5;
    public double ResolvedRatio { get; init; } = 0.6;
    public TimeSpan ResolvedConfirmationTime { get; init; } = TimeSpan.FromHours(48);

    public static TrustConfiguration B { get; } = new() { Name = "B" };

    public static TrustConfiguration A { get; } = new()
    {
        Name = "A",
        VerifiedWeight = 1,
        UseProximity = false,
        UseDecay = false,
    };

    public TrustConfiguration WithVerifiedWeight(double p) =>
        this with { Name = string.Create(CultureInfo.InvariantCulture, $"{Name}-v{p}"), VerifiedWeight = p };

    public static TrustConfiguration ByName(string name)
    {
        var parts = name.Trim().Split("-v", 2);
        var baseCfg = parts[0].ToUpperInvariant() switch
        {
            "A" => A,
            "B" => B,
            _ => throw new ArgumentException($"Configuração de confiança desconhecida: '{name}'.", nameof(name)),
        };

        if (parts.Length == 1)
        {
            return baseCfg;
        }

        return double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var weight) && double.IsFinite(weight) && weight > 0
            ? baseCfg.WithVerifiedWeight(weight)
            : throw new ArgumentException($"Peso de verificado inválido em '{name}'.", nameof(name));
    }
}
