using System.Text.Json;
using UrbanReports.Api.Geocoding;
using UrbanReports.Api.Tests.Infra;

namespace UrbanReports.Api.Tests.Geocoding;

public sealed class AddressBuildingTests
{
    private const double Lat = -22.9521;
    private const double Lng = -43.1868;

    [Fact]
    public void Full_address()
    {
        var e = GeocodedAddress.Build(FakeNominatim.Address(), Lat, Lng);

        e.Should().Be(new GeocodedAddress(
            true,
            "Rua Voluntários da Pátria, 170 — Botafogo",
            "Rua Voluntários da Pátria",
            "170",
            "Botafogo",
            "Rio de Janeiro",
            "RJ",
            "Rua Voluntários da Pátria, Botafogo"));
    }

    [Fact]
    public void No_street_uses_neighborhood_and_city_in_label_and_ignores_number()
    {
        var e = GeocodedAddress.Build(FakeNominatim.Address(street: null), Lat, Lng);

        e.Address.Should().Be("Botafogo");
        e.AreaLabel.Should().Be("Botafogo, Rio de Janeiro");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void No_address_falls_back_to_coordinates_text(bool nullResponse)
    {
        var response = nullResponse ? null : new NominatimResponse(null, "Unable to geocode");

        var e = GeocodedAddress.Build(response, Lat, Lng);

        e.Should().Be(new GeocodedAddress(false, "Próximo a -22.95210, -43.18680", null, null, null, null, null, null));
    }
}
