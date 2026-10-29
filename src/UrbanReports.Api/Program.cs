using Microsoft.Extensions.Options;
using UrbanReports.Api.Common;
using UrbanReports.Api.Infrastructure;
using UrbanReports.Api.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication(builder.Configuration)
    .AddConfiguredCors(builder.Configuration, builder.Environment)
    .AddConfiguredOpenApi()
    .AddAuthentication(builder.Configuration)
    .AddRateLimits(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(CorsPolicies.Front);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapEndpoints();

app.Services.GetRequiredService<IStartupValidator>().Validate();

if (app.Environment.IsDevelopment())
{
    await app.InitializeDevelopmentDatabaseAsync();
}
else if (app.Environment.IsProduction())
{
    await app.InitializeProductionDatabaseAsync();
}

await app.RunAsync();
