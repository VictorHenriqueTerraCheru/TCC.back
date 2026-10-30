using System.Globalization;
using UrbanReports.Api.Trust.Engine;

namespace UrbanReports.Api.Tests.Trust.Engine;

public sealed class TrustConfigurationTests
{
    [Theory]
    [InlineData("A", "A", 1)]
    [InlineData("B", "B", 10)]
    [InlineData(" b ", "B", 10)]
    [InlineData("B-v3", "B-v3", 3)]
    [InlineData("B-v5", "B-v5", 5)]
    [InlineData("A-v2.5", "A-v2.5", 2.5)]
    public void ByName_resolves_the_configurations(string name, string expected, double verifiedWeight)
    {
        var cfg = TrustConfiguration.ByName(name);

        cfg.Name.Should().Be(expected);
        cfg.VerifiedWeight.Should().Be(verifiedWeight);
    }

    [Fact]
    public void ByName_B_is_the_B_instance_itself() =>
        TrustConfiguration.ByName("B").Should().BeSameAs(TrustConfiguration.B);

    [Theory]
    [InlineData("C")]
    [InlineData("B-v")]
    [InlineData("B-vx")]
    [InlineData("B-v0")]
    [InlineData("B-vInfinity")]
    [InlineData("B-v1e400")]
    [InlineData("B-vNaN")]
    public void ByName_rejects_invalid_names(string name)
    {
        var action = () => TrustConfiguration.ByName(name);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Variant_name_ignores_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
        try
        {
            TrustConfiguration.B.WithVerifiedWeight(2.5).Name.Should().Be("B-v2.5");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
