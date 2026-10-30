using System.Net;
using UrbanReports.Api.Tests.Infra;

namespace UrbanReports.Api.Tests.Categories;

[Collection(ApiCollection.Name)]
public sealed class CategoriesTests(ApiFactory factory)
{
    [Fact]
    public async Task Visitor_lists_active_categories_with_initial_seed()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/categories");
        var categories = (await response.ReadJsonAsync()).EnumerateArray().ToList();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        categories.Select(c => c.GetProperty("id").GetInt32()).Take(8).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);

        var pothole = categories[0];
        pothole.GetProperty("name").GetString().Should().Be("Buraco na via");
        pothole.GetProperty("group").GetString().Should().Be("viaPublica");
        pothole.GetProperty("type").GetString().Should().Be("issue");
        pothole.GetProperty("icon").GetString().Should().Be("buraco");

        categories[6].GetProperty("type").GetString().Should().Be("incident");
    }
}
