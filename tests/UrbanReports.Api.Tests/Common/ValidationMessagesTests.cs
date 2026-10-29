using System.Net;
using UrbanReports.Api.Common;
using UrbanReports.Api.Tests.Infra;

namespace UrbanReports.Api.Tests.Common;

[Collection(ApiCollection.Name)]
public sealed class ValidationMessagesTests(ApiFactory factory)
{
    [Fact]
    public async Task Unknown_issue_status_lists_the_contract_values()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/issues?minLat=-23&minLng=-44&maxLat=-22&maxLng=-43&status=nova");

        (await ErrorAsync(response, "status")).Should().Contain("new, active, possiblyResolved, resolved");
    }

    [Fact]
    public async Task Unknown_verification_status_lists_the_contract_values()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);

        var response = await admin.Client.GetAsync("/api/v1/admin/verifications?status=pendente");

        (await ErrorAsync(response, "status")).Should().Contain("pending, approved, rejected");
    }

    [Fact]
    public async Task Unknown_role_lists_the_contract_values()
    {
        var admin = await factory.NewPersonAsync(Role.Administrator);

        var response = await admin.Client.GetAsync("/api/v1/admin/users?role=cidadao");

        (await ErrorAsync(response, "role")).Should().Contain("citizen, verifiedCitizen, administrator");
    }

    [Fact]
    public async Task Unknown_history_filter_lists_the_contract_values()
    {
        var me = await factory.NewPersonAsync();

        var response = await me.Client.GetAsync("/api/v1/me/history?filter=tudo");

        (await ErrorAsync(response, "filter")).Should().Contain("all, reports, votes");
    }

    private static async Task<string> ErrorAsync(HttpResponseMessage response, string field)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = (await response.ReadJsonAsync()).GetProperty("errors");
        return string.Join(" ", errors.GetProperty(field).EnumerateArray().Select(e => e.GetString()));
    }
}
