using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using UrbanReports.Api.Infrastructure;

namespace UrbanReports.Api.Tests.Common;

public sealed class StartupConfigurationTests
{
    [Fact]
    public void Production_without_cors_origins_fails_to_start()
    {
        var register = () => new ServiceCollection().AddConfiguredCors(Configuration(), Environment(Environments.Production));

        register.Should().Throw<InvalidOperationException>().WithMessage("*Cors:Origins*");
    }

    [Fact]
    public void Production_with_cors_origins_starts()
    {
        var register = () => new ServiceCollection().AddConfiguredCors(
            Configuration(("Cors:Origins:0", "https://front.example")), Environment(Environments.Production));

        register.Should().NotThrow();
    }

    [Fact]
    public void Development_without_cors_origins_starts()
    {
        var register = () => new ServiceCollection().AddConfiguredCors(Configuration(), Environment(Environments.Development));

        register.Should().NotThrow();
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => KeyValuePair.Create(v.Key, (string?)v.Value))).Build();

    private static IHostEnvironment Environment(string name) => new HostingEnvironment { EnvironmentName = name };
}
