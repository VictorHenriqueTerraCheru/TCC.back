using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace UrbanReports.Api.Tests.Persistence;

public sealed class ProductionStartupTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgis/postgis:16-3.4").Build();

    public Task InitializeAsync() => _database.StartAsync();

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    [Fact]
    public async Task Invalid_configuration_fails_before_migrating_the_database()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Default", _database.GetConnectionString());
            builder.UseSetting("Cors:Origins:0", "https://front.example");
        });

        var start = () => factory.CreateClient();

        start.Should().Throw<Exception>();
        (await TableCountAsync()).Should().Be(0);
    }

    private async Task<long> TableCountAsync()
    {
        await using var connection = new NpgsqlConnection(_database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name IN ('users', '__EFMigrationsHistory')",
            connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
