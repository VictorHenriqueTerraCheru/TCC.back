using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UrbanReports.Api.Common;
using UrbanReports.Api.Health;
using UrbanReports.Api.Admin;
using UrbanReports.Api.Verification;
using UrbanReports.Api.Incidents;
using UrbanReports.Api.Votes;
using UrbanReports.Api.Issues;
using UrbanReports.Api.Users;
using UrbanReports.Api.Auth;
using UrbanReports.Api.Categories;
using UrbanReports.Api.Groups;
using UrbanReports.Api.Trust;
using UrbanReports.Api.Geocoding;
using UrbanReports.Api.Persistence;
using UrbanReports.Api.Trust.Engine;

namespace UrbanReports.Api.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration) =>
        services
            .AddErrorHandling()
            .AddJson()
            .AddClock()
            .AddValidation()
            .AddPersistence(configuration)
            .AddGeocoding()
            .AddTrust(configuration)
            .AddServices();

    private static IServiceCollection AddErrorHandling(this IServiceCollection services) =>
        services.AddProblemDetails();

    private static IServiceCollection AddJson(this IServiceCollection services) =>
        services.ConfigureHttpJsonOptions(o => JsonConfig.Configure(o.SerializerOptions));

    private static IServiceCollection AddClock(this IServiceCollection services) =>
        services.AddSingleton<TimeProvider>(new PostgresClock());

    private static IServiceCollection AddValidation(this IServiceCollection services)
    {
        Validation.ConfigureGlobal();
        return services.AddValidatorsFromAssemblyContaining<Program>();
    }

    private static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' não configurada (ConnectionStrings__Default).");

        services.AddDbContext<AppDbContext>(o => o
            .UseNpgsql(connection, npgsql => npgsql.UseNetTopologySuite(geographyAsDefault: true))
            .UseSnakeCaseNamingConvention());

        return services;
    }

    internal static IServiceCollection AddGeocoding(this IServiceCollection services)
    {
        services.AddOptions<NominatimOptions>()
            .BindConfiguration(NominatimOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient(NominatimClient.HttpClientName, (sp, http) =>
            {
                var options = sp.GetRequiredService<IOptions<NominatimOptions>>().Value;
                http.BaseAddress = new Uri(options.BaseUrl);
                http.Timeout = options.Timeout;
                http.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
                http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pt-BR");
            })
            .RemoveAllLoggers();

        services.AddSingleton<INominatimClient, NominatimClient>();
        services.AddSingleton<IGeocodingService, GeocodingService>();

        return services;
    }

    private static IServiceCollection AddTrust(this IServiceCollection services, IConfiguration configuration)
    {
        var name = configuration.GetValue("Trust:Configuration", TrustConfiguration.B.Name)!;
        services.AddSingleton(TrustConfiguration.ByName(name));

        services.AddOptions<LifecycleOptions>().BindConfiguration(LifecycleOptions.Section);
        services.AddScoped<LifecycleProcessor>();
        services.AddHostedService<LifecycleJob>();

        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IIssueService, IssueService>();
        services.AddScoped<IVoteService, VoteService>();
        services.AddScoped<IIncidentService, IncidentService>();
        services.AddScoped<IVerificationService, VerificationService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IHealthService, HealthService>();
        return services;
    }
}
