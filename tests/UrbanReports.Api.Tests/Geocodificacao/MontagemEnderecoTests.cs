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
    public void No_number_omits_the_part()
    {
        var e = GeocodedAddress.Build(FakeNominatim.Address(number: null), Lat, Lng);

        e.Address.Should().Be("Rua Voluntários da Pátria — Botafogo");
    }

    [Fact]
    public void No_street_uses_neighborhood_and_city_in_label_and_ignores_number()
    {
        var e = GeocodedAddress.Build(FakeNominatim.Address(street: null), Lat, Lng);

        e.Address.Should().Be("Botafogo");
        e.AreaLabel.Should().Be("Botafogo, Rio de Janeiro");
    }

    [Fact]
    public void No_neighborhood()
    {
        var e = GeocodedAddress.Build(FakeNominatim.Address(neighborhood: null), Lat, Lng);

        e.Address.Should().Be("Rua Voluntários da Pátria, 170");
        e.AreaLabel.Should().Be("Rua Voluntários da Pátria");
    }

    [Fact]
    public void City_only()
    {
        var e = GeocodedAddress.Build(FakeNominatim.Address(street: null, neighborhood: null), Lat, Lng);

        e.Available.Should().BeTrue();
        e.Address.Should().Be("Rio de Janeiro");
        e.AreaLabel.Should().Be("Rio de Janeiro");
    }

    [Fact]
    public void Uses_osm_alternatives_for_neighborhood_and_city()
    {
        var response = new NominatimResponse(
            new NominatimResponse.AddressParts { Pedestrian = "Calçadão", Neighbourhood = "Leme", Town = "Niterói", Iso3166Level4 = "BR-RJ" },
            null);

        var e = GeocodedAddress.Build(response, Lat, Lng);

        e.Street.Should().Be("Calçadão");
        e.Neighborhood.Should().Be("Leme");
        e.City.Should().Be("Niterói");
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

    [Fact]
    public void State_only_for_brazilian_code()
    {
        GeocodedAddress.Build(FakeNominatim.Address(iso: "AR-B"), Lat, Lng).State.Should().BeNull();
        GeocodedAddress.Build(FakeNominatim.Address(iso: "BR-SP"), Lat, Lng).State.Should().Be("SP");
    }

    [Fact]
    public void Truncates_to_column_sizes()
    {
        var e = GeocodedAddress.Build(FakeNominatim.Address(street: new string('R', 300)), Lat, Lng);

        e.Address.Should().HaveLength(GeocodedAddress.MaxAddressLength);
        e.AreaLabel.Should().HaveLength(GeocodedAddress.MaxLabelLength);
    }

    [Fact]
    public void Real_nominatim_response_in_botafogo()
    {
        const string json = """
            {"place_id":10515445,"name":"Santander","address":{"amenity":"Santander","road":"Rua Voluntários da Pátria",
             "quarter":"Santa Marta","suburb":"Botafogo","city":"Rio de Janeiro","state":"Rio de Janeiro",
             "ISO3166-2-lvl4":"BR-RJ","region":"Região Sudeste","postcode":"22270-000","country":"Brasil","country_code":"br"}}
            """;

        var e = GeocodedAddress.Build(JsonSerializer.Deserialize<NominatimResponse>(json), -22.9519, -43.1866);

        e.Should().Be(new GeocodedAddress(
            true,
            "Rua Voluntários da Pátria — Botafogo",
            "Rua Voluntários da Pátria",
            null,
            "Botafogo",
            "Rio de Janeiro",
            "RJ",
            "Rua Voluntários da Pátria, Botafogo"));
    }
}
