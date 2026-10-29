using System.Globalization;
using UrbanReports.Api.Issues;
using UrbanReports.Api.Common;
using UrbanReports.Api.Trust.Engine;

namespace UrbanReports.Api.Tests.Issues;

public sealed class TrustDisplayTests
{
    [Theory]
    [InlineData("0", 0.0, TrustBand.Low)]
    [InlineData("39.9", 39.9, TrustBand.Low)]
    [InlineData("40", 40.0, TrustBand.Medium)]
    [InlineData("69.9", 69.9, TrustBand.Medium)]
    [InlineData("70", 70.0, TrustBand.High)]
    [InlineData("100", 100.0, TrustBand.High)]
    public void Band_changes_exactly_at_40_and_70(string written, double shown, TrustBand band)
    {
        var (trust, calculatedBand) = TrustDisplay.Calculate(
            IssueStatus.Active, decimal.Parse(written, CultureInfo.InvariantCulture), TrustConfiguration.B);

        trust.Should().Be(shown);
        calculatedBand.Should().Be(band);
    }

    [Theory]
    [InlineData("45.46", 45.5)]
    [InlineData("45.44", 45.4)]
    [InlineData("62.50", 62.5)]
    public void Shows_one_decimal_place(string written, double expected)
    {
        var (trust, _) = TrustDisplay.Calculate(
            IssueStatus.Active, decimal.Parse(written, CultureInfo.InvariantCulture), TrustConfiguration.B);

        trust.Should().Be(expected);
    }

    [Fact]
    public void New_does_not_show_trust_even_with_stored_value()
    {
        var (trust, band) = TrustDisplay.Calculate(IssueStatus.New, 62.5m, TrustConfiguration.B);

        trust.Should().BeNull();
        band.Should().BeNull();
    }

    [Theory]
    [InlineData(IssueStatus.Active)]
    [InlineData(IssueStatus.PossiblyResolved)]
    public void No_stored_trust_shows_nothing(IssueStatus status)
    {
        var (trust, band) = TrustDisplay.Calculate(status, null, TrustConfiguration.B);

        trust.Should().BeNull();
        band.Should().BeNull();
    }
}
