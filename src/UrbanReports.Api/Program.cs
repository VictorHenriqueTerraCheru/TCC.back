using UrbanReports.Api.Common;
using UrbanReports.Api.Infrastructure;
using UrbanReports.Api.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAplicacao(builder.Configuration)
    .AddCorsConfigurado(builder.Configuration)
    .AddOpenApiConfigurado()
    .AddAutenticacao(builder.Configuration)
    .AddLimitesDeRequisicao(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(PoliticasCors.Front);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapEndpoints();

if (app.Environment.IsDevelopment())
{
    await app.InicializarBancoDesenvolvimentoAsync();
}
else if (app.Environment.IsProduction())
{
    await app.InicializarBancoProducaoAsync();
}

await app.RunAsync();
