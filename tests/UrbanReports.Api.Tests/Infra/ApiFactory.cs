using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using UrbanReports.Api.Common;
using UrbanReports.Api.Persistence;
using UrbanReports.Api.Geocoding;
using Testcontainers.PostgreSql;

namespace UrbanReports.Api.Tests.Infra;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string FrontOrigin = "http://localhost:5173";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgis/postgis:16-3.4").Build();

    public FakeTimeProvider Clock { get; } = new(PostgresClock.Truncate(DateTimeOffset.UtcNow));

    public FakeNominatim Nominatim { get; } = new();

    public static (double Lat, double Lng) UniquePoint()
    {
        var n = Interlocked.Increment(ref _sequence);
        return (-22.95 - n * 0.001, -43.18 - n * 0.001);
    }

    private static int _sequence;

    public static (double Lat, double Lng) IsolatedArea()
    {
        var n = Interlocked.Increment(ref _isolatedSequence);
        return (-23.5 - n * 0.05, -46.6);
    }

    private static int _isolatedSequence;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _database.GetConnectionString());
        builder.UseSetting("Jwt:Key", "test-key-with-at-least-32-characters-long");
        builder.UseSetting("Cors:Origins:0", FrontOrigin);
        builder.UseSetting("Lifecycle:Enabled", "false");
        builder.ConfigureTestServices(s =>
        {
            s.AddSingleton<TimeProvider>(Clock);
            s.AddSingleton<INominatimClient>(Nominatim);
        });
    }

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public async Task<T> InDatabaseAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public Task InDatabaseAsync(Func<AppDbContext, Task> action) =>
        InDatabaseAsync(async db => { await action(db); return true; });

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await _database.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
