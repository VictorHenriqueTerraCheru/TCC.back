using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Incidents;
using UrbanReports.Api.Tests.Infra;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Tests.Incidents;

[Collection(ApiCollection.Name)]
public sealed class IncidentsTests(ApiFactory factory)
{
    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    private Task<HttpResponseMessage> CreateAsync(Person person, double lat, double lng, short categoryId = 7, DateTimeOffset? occurredAt = null) =>
        person.Client.PostAsJsonAsync("/api/v1/incidents",
            new { categoryId, lat, lng, occurredAt = occurredAt ?? factory.Clock.GetUtcNow().AddMinutes(-30) },
            ApiClient.Json);

    private static async Task<string?> ErrorTypeAsync(HttpResponseMessage response) =>
        (await response.ReadJsonAsync()).GetProperty("type").GetString();

    [Fact]
    public async Task Create_rounds_to_3_places_keeps_the_label_and_returns_the_area()
    {
        var person = await factory.NewPersonAsync();
        var (baseLat, baseLng) = ApiFactory.IsolatedArea();
        var (lat, lng) = (baseLat + 0.00041, baseLng - 0.00067);

        var response = await CreateAsync(person, lat, lng);
        var body = await response.Content.ReadAsStringAsync();
        var area = await response.ReadAsync<GetIncidentAreaResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        body.Should().NotContainEquivalentOf("author");
        area.RadiusMeters.Should().Be(400);
        area.UpdatedAt.Should().Be(factory.Clock.GetUtcNow());
        area.Level.Should().Be(ConcentrationLevel.Low);
        area.Counts.Should().Equal(
            new GetIncidentAreaCount(7, "Roubo", 1, 1),
            new GetIncidentAreaCount(8, "Acidente de trânsito", 0, 0));

        var written = await factory.InDatabaseAsync(db => db.Incidents.AsNoTracking()
            .Where(i => i.AuthorId == person.Id).SingleAsync());
        written.ApproximateArea.Y.Should().Be(Math.Round(lat, 3, MidpointRounding.AwayFromZero));
        written.ApproximateArea.X.Should().Be(Math.Round(lng, 3, MidpointRounding.AwayFromZero));
        written.AreaLabel.Should().Be("Rua Voluntários da Pátria, Botafogo");
        written.RadiusMeters.Should().Be(100);
    }

    [Fact]
    public async Task Create_with_nominatim_offline_saves_without_label()
    {
        var person = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.IsolatedArea();
        factory.Nominatim.SetOffline(lat, lng);

        var response = await CreateAsync(person, lat, lng);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var written = await factory.InDatabaseAsync(db => db.Incidents.AsNoTracking().SingleAsync(i => i.AuthorId == person.Id));
        written.AreaLabel.Should().BeNull();
    }

    [Fact]
    public async Task Create_with_issue_category_returns_422()
    {
        var person = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.IsolatedArea();

        var response = await CreateAsync(person, lat, lng, categoryId: 1);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrorTypeAsync(response)).Should().Be("category-not-incident");
    }

    [Theory]
    [InlineData(-25 * 60)]
    [InlineData(1)]
    public async Task Time_outside_last_24h_returns_422(int minutesFromNow)
    {
        var person = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.IsolatedArea();

        var response = await CreateAsync(person, lat, lng, occurredAt: factory.Clock.GetUtcNow().AddMinutes(minutesFromNow));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrorTypeAsync(response)).Should().Be("time-outside-window");
    }

    [Fact]
    public async Task Eleventh_incident_in_24h_returns_429_and_is_released_later()
    {
        var person = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.IsolatedArea();

        for (var i = 0; i < 10; i++)
        {
            (await CreateAsync(person, lat, lng)).StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var eleventh = await CreateAsync(person, lat, lng);
        eleventh.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await ErrorTypeAsync(eleventh)).Should().Be("daily-incident-limit");

        factory.Clock.Advance(TimeSpan.FromHours(24));
        (await CreateAsync(person, lat, lng)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Area_classifies_the_level_by_the_7_day_total()
    {
        var person = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.IsolatedArea();
        for (var i = 0; i < 4; i++)
        {
            await CreateAsync(person, Scenarios.North(lat, i * 50), lng, categoryId: (short)(i % 2 == 0 ? 7 : 8));
        }

        var low = await (await factory.CreateClient().GetAsync(Inv($"/api/v1/incidents/area?lat={lat}&lng={lng}")))
            .ReadAsync<GetIncidentAreaResponse>();
        await CreateAsync(person, lat, lng);
        var medium = await (await factory.CreateClient().GetAsync(Inv($"/api/v1/incidents/area?lat={lat}&lng={lng}")))
            .ReadAsync<GetIncidentAreaResponse>();

        low.Level.Should().Be(ConcentrationLevel.Low);
        medium.Level.Should().Be(ConcentrationLevel.Medium);
        medium.Counts.Sum(c => c.Days7).Should().Be(5);
    }

    [Fact]
    public async Task Area_ignores_incidents_beyond_400m()
    {
        var person = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.IsolatedArea();
        await CreateAsync(person, Scenarios.North(lat, 300), lng);
        await CreateAsync(person, Scenarios.North(lat, 500), lng);

        var area = await (await factory.CreateClient().GetAsync(Inv($"/api/v1/incidents/area?lat={lat}&lng={lng}")))
            .ReadAsync<GetIncidentAreaResponse>();

        area.Counts.Sum(c => c.Days30).Should().Be(1);
    }

    [Theory]
    [InlineData(0, ConcentrationLevel.Low)]
    [InlineData(4, ConcentrationLevel.Low)]
    [InlineData(5, ConcentrationLevel.Medium)]
    [InlineData(14, ConcentrationLevel.Medium)]
    [InlineData(15, ConcentrationLevel.High)]
    public void Level_thresholds(int total, ConcentrationLevel expected) =>
        IncidentService.Level(total).Should().Be(expected);

    [Fact]
    public async Task Heatmap_groups_by_rounded_point_and_respects_window_and_groups()
    {
        var person = await factory.NewPersonAsync();
        var (lat, lng) = ApiFactory.IsolatedArea();
        await CreateAsync(person, lat + 0.0001, lng);
        await CreateAsync(person, lat - 0.0002, lng + 0.0001);
        await CreateAsync(person, lat + 0.003, lng, categoryId: 8);

        var area = Inv($"minLat={lat - 0.01}&minLng={lng - 0.01}&maxLat={lat + 0.01}&maxLng={lng + 0.01}");
        var cells = await (await factory.CreateClient().GetAsync($"/api/v1/incidents/heatmap?{area}"))
            .ReadAsync<List<GetIncidentHeatmapResponse>>();
        var security = await (await factory.CreateClient().GetAsync($"/api/v1/incidents/heatmap?{area}&groups=seguranca"))
            .ReadAsync<List<GetIncidentHeatmapResponse>>();

        cells.Should().BeEquivalentTo(
        [
            new GetIncidentHeatmapResponse(Math.Round(lat, 3), lng, 2),
            new GetIncidentHeatmapResponse(Math.Round(lat + 0.003, 3), lng, 1),
        ]);
        security.Should().ContainSingle().Which.Count.Should().Be(2);

        factory.Clock.Advance(TimeSpan.FromDays(8));
        var in7Days = await (await factory.CreateClient().GetAsync($"/api/v1/incidents/heatmap?{area}&days=7"))
            .ReadAsync<List<GetIncidentHeatmapResponse>>();
        var in30Days = await (await factory.CreateClient().GetAsync($"/api/v1/incidents/heatmap?{area}&days=30"))
            .ReadAsync<List<GetIncidentHeatmapResponse>>();

        in7Days.Should().BeEmpty();
        in30Days.Sum(c => c.Count).Should().Be(3);
    }

    [Fact]
    public async Task Heatmap_respects_the_exact_rectangle_on_wide_viewport()
    {
        var person = await factory.NewPersonAsync();
        await CreateAsync(person, 70, 0);
        await CreateAsync(person, 60.5, 10);

        var cells = await (await factory.CreateClient().GetAsync("/api/v1/incidents/heatmap?minLat=60&minLng=-60&maxLat=61&maxLng=60"))
            .ReadAsync<List<GetIncidentHeatmapResponse>>();

        cells.Should().ContainSingle().Which.Should().Be(new GetIncidentHeatmapResponse(60.5, 10, 1));
    }

    [Theory]
    [InlineData("days=0")]
    [InlineData("days=31")]
    [InlineData("groups=viaPublica,xyz")]
    public async Task Heatmap_with_invalid_parameter_returns_400(string parameter)
    {
        var response = await factory.CreateClient().GetAsync($"/api/v1/incidents/heatmap?minLat=-23&minLng=-44&maxLat=-22&maxLng=-43&{parameter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
